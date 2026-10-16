#!/usr/bin/env python3
"""
Keeps docs/security/identity-matrix.md and the Identity tests in step. Fails (exit 1) when:
  1. the matrix names a test that does not exist (renamed or deleted),
  2. an Identity test is named nowhere in the matrix (every test maps to a row),
  3. a row marked ✅ names no test, or
  4. a test's [Trait("Rule", "<ID>")] tags differ from the IDs of the rows that name it, or
  5. a file has a [Fact] or [Theory] this script cannot pair with a method (so nothing is skipped silently).

Usage: python3 .claude/scripts/check-identity-matrix.py   (from anywhere; run by CI)
"""
import os
import re
import sys
from pathlib import Path

MATRIX = Path("docs/security/identity-matrix.md")
IDENTITY_TEST_DIRS = [
    Path("tests/RestaurantManagement.Modules.Identity.Tests"),
    Path("tests/RestaurantManagement.Web.Tests/Identity"),
]
# Cover several modules: their tests may be named in the matrix but need not all be.
SHARED_TEST_FILES = [
    Path("tests/RestaurantManagement.Web.Tests/PrivateByDefaultTests.cs"),
    Path("tests/RestaurantManagement.Web.Tests/StartupConfigurationTests.cs"),
    Path("tests/RestaurantManagement.Web.Tests/RequiredIdFieldEndpointTests.cs"),
]

NAMED_TEST = re.compile(r"`([A-Z]\w*Tests)\.(\w+)`")
ROW_ID = re.compile(r"^[A-Z]{1,2}\d+$")
METHOD = re.compile(r"^\s*public\s+(?:async\s+Task|void)\s+(\w+)\s*\(")
ATTRIBUTE_OR_COMMENT = re.compile(r"^\s*(\[.*|//.*|)$")
TEST_ATTRIBUTE = re.compile(r"\[(?:Fact|Theory)\b")
RULE_TRAIT = re.compile(r'Trait\(\s*"Rule"\s*,\s*"([^"]+)"\s*\)')


UNREADABLE = []


def tests_in(path: Path) -> dict:
    """Maps (class, method) to the set of Rule trait IDs, for every [Fact] or [Theory] method."""
    source = path.read_text(encoding="utf-8")
    lines = source.splitlines()
    found = {}
    for index, line in enumerate(lines):
        match = METHOD.match(line)
        if not match:
            continue
        # The attribute block: attribute, comment and blank lines directly above the method.
        start = index
        while start > 0 and ATTRIBUTE_OR_COMMENT.match(lines[start - 1]):
            start -= 1
        block = "\n".join(lines[start:index])
        if TEST_ATTRIBUTE.search(block):
            found[(path.stem, match.group(1))] = set(RULE_TRAIT.findall(block))
    attributes = len(TEST_ATTRIBUTE.findall(source))
    if attributes != len(found):
        UNREADABLE.append(f"{path}: {attributes} [Fact]/[Theory] attributes but {len(found)} test methods read; "
                          "declare tests as `public async Task` or `public void` with single-line attributes")
    return found


def collect(paths) -> dict:
    found = {}
    for path in paths:
        files = path.rglob("*.cs") if path.is_dir() else [path] if path.exists() else []
        for file in files:
            if not {"bin", "obj"} & set(file.parts):
                found.update(tests_in(file))
    return found


def rows_by_test(matrix: str) -> tuple:
    """Maps each named test to the IDs of the rows naming it; also returns ✅ rows without a test."""
    rows = {}
    untested = []
    for line in matrix.splitlines():
        cells = [c.strip() for c in line.split("|")]
        if len(cells) < 3 or not ROW_ID.match(cells[1]):
            continue
        named = NAMED_TEST.findall(line)
        if "✅" in cells and not named:
            untested.append(cells[1])
        for test in named:
            rows.setdefault(test, set()).add(cells[1])
    return rows, untested


def main() -> int:
    matrix = MATRIX.read_text(encoding="utf-8")
    rows, untested = rows_by_test(matrix)
    identity_tests = collect(IDENTITY_TEST_DIRS)
    existing = {**collect(SHARED_TEST_FILES), **identity_tests}

    problems = []
    for cls, method in sorted(set(rows) - set(existing)):
        problems.append(f"Matrix names a test that does not exist: {cls}.{method}")
    for cls, method in sorted(set(identity_tests) - set(rows)):
        problems.append(f"Identity test not mapped in the matrix: {cls}.{method}")
    problems += UNREADABLE
    for row in untested:
        problems.append(f"Row marked ✅ without a named test: {row}")
    for test in sorted(set(rows) & set(existing)):
        expected, tagged = rows[test], existing[test]
        if expected != tagged:
            wanted = " ".join(f'[Trait("Rule", "{r}")]' for r in sorted(expected))
            problems.append(f"Rule tags of {test[0]}.{test[1]} are {sorted(tagged) or 'missing'}; the matrix needs {wanted}")

    if problems:
        print(f"{MATRIX}: {len(problems)} problem(s)")
        print("\n".join(problems))
        return 1
    print(f"{MATRIX}: {len(rows)} named tests, every Identity test mapped and tagged with its rule")
    return 0


if __name__ == "__main__":
    os.chdir(Path(__file__).resolve().parents[2])
    sys.exit(main())
