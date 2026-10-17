#!/usr/bin/env python3
"""
Keeps each module's security matrix (docs/security/<module>-matrix.md) and the tests in step. Fails (exit 1) when:
  1. a matrix names a test that does not exist (renamed or deleted),
  2. a test in a module's test folders is named nowhere in that module's matrix,
  3. a row marked ✅ names no test,
  4. a test's [Trait("Rule", "<ID>")] tags differ from the IDs of the rows naming it, across all matrices,
  5. a row ID is used twice, in one matrix or across matrices, or
  6. a file has a [Fact] or [Theory] this script cannot pair with a method (so nothing is skipped silently).
Modules whose matrix does not exist yet are skipped.

Usage: python3 .claude/scripts/check-security-matrices.py              (from anywhere; run by CI)
       python3 .claude/scripts/check-security-matrices.py --fix-tags   rewrites each test's Rule tags from the
                                                                        matrices, then checks
"""
import os
import re
import sys
from pathlib import Path

# Module: (matrix, test folders whose every test the matrix must name)
MODULES = {
    "Identity": ("docs/security/identity-matrix.md", [
        "tests/RestaurantManagement.Modules.Identity.Tests", "tests/RestaurantManagement.Web.Tests/Identity"]),
    "Web": ("docs/security/web-matrix.md", [
        "tests/RestaurantManagement.Web.Tests/Web"]),
    "Menu": ("docs/security/menu-matrix.md", [
        "tests/RestaurantManagement.Modules.Menu.Tests", "tests/RestaurantManagement.Web.Tests/Menu"]),
    "Organization": ("docs/security/organization-matrix.md", [
        "tests/RestaurantManagement.Modules.Organization.Tests", "tests/RestaurantManagement.Web.Tests/Organization"]),
    "Shared": ("docs/security/shared-matrix.md", [
        "tests/RestaurantManagement.Shared.Tests", "tests/RestaurantManagement.Web.Tests/Shared"]),
}
ALL_TESTS = Path("tests")

NAMED_TEST = re.compile(r"`([A-Z]\w*Tests)\.(\w+)`")
ROW_ID = re.compile(r"^[A-Z]{1,3}\d+$")
METHOD = re.compile(r"^\s*public\s+(?:async\s+Task|void)\s+(\w+)\s*\(")
ATTRIBUTE_OR_COMMENT = re.compile(r"^\s*(\[.*|//.*|)$")
TEST_ATTRIBUTE = re.compile(r"\[(?:Fact|Theory)\b")
RULE_TRAIT = re.compile(r'Trait\(\s*"Rule"\s*,\s*"([^"]+)"\s*\)')
END_OF_MEMBER = re.compile(r"^\s*([{}]|.*;\s*(//.*)?)$")

problems = []


def tests_in(path: Path) -> dict:
    """Maps (class, method) to the set of Rule trait IDs, for every [Fact] or [Theory] method."""
    lines = path.read_text(encoding="utf-8").splitlines()
    found = {}
    attributes = 0
    index = 0
    while index < len(lines):
        if not TEST_ATTRIBUTE.match(lines[index].strip()):
            index += 1
            continue
        attributes += 1
        # Attributes and comments directly above the test attribute belong to the test too.
        start = index
        while start > 0 and ATTRIBUTE_OR_COMMENT.match(lines[start - 1]):
            start -= 1
        # The method is the next declaration; attributes may span several lines before it.
        end = index + 1
        while end < len(lines) and not METHOD.match(lines[end]) and not TEST_ATTRIBUTE.match(lines[end].strip()):
            end += 1
        if end < len(lines) and METHOD.match(lines[end]):
            block = "\n".join(lines[start:end])
            found[(path.stem, METHOD.match(lines[end]).group(1))] = set(RULE_TRAIT.findall(block))
        index = end if end > index else index + 1
    if attributes != len(found):
        problems.append(f"{path}: {attributes} [Fact]/[Theory] attributes but {len(found)} test methods read; "
                        "declare tests as `public async Task` or `public void`")
    return found


def fix_tags(expected_tags: dict) -> int:
    """Replaces the Rule tags above every test method with the IDs of the rows naming it."""
    changed = 0
    for file in ALL_TESTS.rglob("*.cs"):
        if {"bin", "obj"} & set(file.parts):
            continue
        lines = file.read_text(encoding="utf-8").split("\n")
        output, touched = [], False
        for line in lines:
            match = METHOD.match(line)
            key = (file.stem, match.group(1)) if match else None
            if key and (key in expected_tags or any(RULE_TRAIT.search(previous) for previous in output[-12:])):
                # Drop the Rule tags directly above this method, then write the expected ones. The block
                # ends at the previous member, so attributes spanning several lines stay inside it.
                cursor = len(output)
                while cursor > 0 and not END_OF_MEMBER.match(output[cursor - 1]):
                    cursor -= 1
                block = output[cursor:]
                kept = [item for item in block if not RULE_TRAIT.search(item)]
                if TEST_ATTRIBUTE.search("\n".join(block)):
                    indent = line[: len(line) - len(line.lstrip())]
                    wanted = [f'{indent}[Trait("Rule", "{rule}")]' for rule in sorted(expected_tags.get(key, set()))]
                    if kept + wanted != block:
                        output[cursor:] = kept + wanted
                        touched = True
            output.append(line)
        if touched:
            file.write_text("\n".join(output), encoding="utf-8")
            changed += 1
    return changed


def collect(folder: Path) -> dict:
    found = {}
    if folder.exists():
        for file in folder.rglob("*.cs"):
            if not {"bin", "obj"} & set(file.parts):
                found.update(tests_in(file))
    return found


def read_matrix(path: Path, seen_ids: dict) -> dict:
    """Maps each named test to the IDs of the rows naming it."""
    rows = {}
    for line in path.read_text(encoding="utf-8").splitlines():
        cells = [c.strip() for c in line.split("|")]
        if len(cells) < 3 or not ROW_ID.match(cells[1]):
            continue
        row = cells[1]
        if row in seen_ids:
            problems.append(f"Row ID {row} is used in {seen_ids[row]} and again in {path}")
        seen_ids[row] = path
        named = NAMED_TEST.findall(line)
        if "✅" in cells and not named:
            problems.append(f"{path}: row {row} is marked ✅ without a named test")
        for test in named:
            rows.setdefault(test, set()).add(row)
    return rows


def main() -> int:
    if "--fix-tags" in sys.argv:
        tags = {}
        for matrix, _ in MODULES.values():
            if Path(matrix).exists():
                for test, ids in read_matrix(Path(matrix), {}).items():
                    tags.setdefault(test, set()).update(ids)
        problems.clear()
        print(f"Rule tags rewritten in {fix_tags(tags)} file(s)")
    all_tests = collect(ALL_TESTS)
    expected_tags = {}
    seen_ids = {}
    checked = []
    for module, (matrix, folders) in MODULES.items():
        path = Path(matrix)
        if not path.exists():
            continue
        rows = read_matrix(path, seen_ids)
        for test, ids in rows.items():
            expected_tags.setdefault(test, set()).update(ids)
        for cls, method in sorted(set(rows) - set(all_tests)):
            problems.append(f"{path}: names a test that does not exist: {cls}.{method}")
        module_tests = {}
        for folder in folders:
            module_tests.update(collect(Path(folder)))
        for cls, method in sorted(set(module_tests) - set(rows)):
            problems.append(f"{path}: {module} test not mapped in the matrix: {cls}.{method}")
        checked.append(f"{module} {len(rows)}")

    for test, tagged in sorted(all_tests.items()):
        expected = expected_tags.get(test, set())
        if tagged != expected:
            wanted = " ".join(f'[Trait("Rule", "{r}")]' for r in sorted(expected)) or "no Rule tag (no row names it)"
            problems.append(f"Rule tags of {test[0]}.{test[1]} are {sorted(tagged) or 'missing'}; the matrices need {wanted}")

    if problems:
        print(f"Security matrices: {len(problems)} problem(s)")
        print("\n".join(problems))
        return 1
    print(f"Security matrices in step with the tests (named tests per module: {', '.join(checked)})")
    return 0


if __name__ == "__main__":
    os.chdir(Path(__file__).resolve().parents[2])
    sys.exit(main())
