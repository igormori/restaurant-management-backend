#!/usr/bin/env python3
"""
Keeps docs/security/identity-matrix.md and the Identity tests in step. Fails (exit 1) when:
  1. the matrix names a test that does not exist (renamed or deleted), or
  2. an Identity test is named nowhere in the matrix (every test maps to a row or to section 11).
  3. a row marked ✅ names no test.

Usage: python3 .claude/scripts/check-identity-matrix.py   (from the repository root; run by CI)
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
# A test attribute, then anything up to the method declaration (other attributes, comments).
TEST_METHOD = re.compile(r"\[(?:Fact|Theory)\b.*?public\s+(?:async\s+Task|void)\s+(\w+)\s*\(", re.S)


def tests_in(path: Path) -> set:
    source = path.read_text(encoding="utf-8")
    return {(path.stem, name) for name in TEST_METHOD.findall(source)}


def collect(paths) -> set:
    found = set()
    for path in paths:
        if path.is_dir():
            for file in path.rglob("*.cs"):
                if not {"bin", "obj"} & set(file.parts):
                    found |= tests_in(file)
        elif path.exists():
            found |= tests_in(path)
    return found


def main() -> int:
    matrix = MATRIX.read_text(encoding="utf-8")
    named = set(NAMED_TEST.findall(matrix))
    identity_tests = collect(IDENTITY_TEST_DIRS)
    existing = identity_tests | collect(SHARED_TEST_FILES)

    problems = []
    for cls, method in sorted(named - existing):
        problems.append(f"Matrix names a test that does not exist: {cls}.{method}")
    for cls, method in sorted(identity_tests - named):
        problems.append(f"Identity test not mapped in the matrix: {cls}.{method}")
    for line in matrix.splitlines():
        cells = [c.strip() for c in line.split("|")]
        if "✅" in cells and not NAMED_TEST.search(line):
            problems.append(f"Row marked ✅ without a named test: {cells[1]}")

    if problems:
        print(f"{MATRIX}: {len(problems)} problem(s)")
        print("\n".join(problems))
        return 1
    print(f"{MATRIX}: {len(named)} named tests, every Identity test mapped")
    return 0


if __name__ == "__main__":
    os.chdir(Path(__file__).resolve().parents[2])
    sys.exit(main())
