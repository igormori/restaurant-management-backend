#!/usr/bin/env python3
"""
Turns the test results (TRX files from `dotnet test --logger trx`) into a report a non-developer can read:
each failed test is shown with the matrix rule it protects, the rule's expected result and the failure message.

- On GitHub Actions: one error annotation per failed test (shown on the run page and the pull request, on the
  test's line), and a summary table in the run's Summary page.
- Locally: the same report printed to the terminal.

Rules come from docs/security/*-matrix.md. A failed test no matrix names is reported with rule "—".
Always exits 0: the test step itself fails the build; this step only explains the failure.

Usage: python3 .claude/scripts/report-test-results.py <results directory>
"""
import os
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

TRX = "{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}"
NAMED_TEST = re.compile(r"`([A-Z]\w*Tests)\.(\w+)`")
ROW_ID = re.compile(r"^[A-Z]{1,2}\d+$")
MAX_MESSAGE = 600


def load_rules() -> dict:
    """Maps (class, method) to a list of (row ID, case, expected, matrix file)."""
    rules = {}
    for matrix in sorted(Path("docs/security").glob("*-matrix.md")):
        for line in matrix.read_text(encoding="utf-8").splitlines():
            cells = [c.strip() for c in line.split("|")]
            if len(cells) < 4 or not ROW_ID.match(cells[1]):
                continue
            # Behaviour rows: ID | Case | Expected | Status | Test. Section 11 rows: ID | Test | Protects.
            case, expected = (cells[2], cells[3]) if len(cells) >= 7 else (cells[3], "")
            for test in NAMED_TEST.findall(line):
                rules.setdefault(test, []).append((cells[1], case, expected, matrix.name))
    return rules


def find_test_line(cls: str, method: str):
    """The file and line of a test method, for an annotation on that line."""
    pattern = re.compile(rf"public\s+(?:async\s+Task|void)\s+{re.escape(method)}\s*\(")
    for file in Path("tests").rglob(f"{cls}.cs"):
        for number, text in enumerate(file.read_text(encoding="utf-8").splitlines(), start=1):
            if pattern.search(text):
                return str(file), number
    return None, None


def read_results(directory: Path):
    """Yields (project, totals) per TRX file and the failed tests across all of them."""
    projects, failures = [], []
    for trx in sorted(directory.rglob("*.trx")):
        root = ET.parse(trx).getroot()
        definitions = {}
        project = trx.stem
        for unit_test in root.iter(f"{TRX}UnitTest"):
            method = unit_test.find(f"{TRX}TestMethod")
            definitions[unit_test.get("id")] = (method.get("className").split(".")[-1], method.get("name").split("(")[0])
            project = Path(unit_test.get("storage", project)).stem
        counters = root.find(f"{TRX}ResultSummary/{TRX}Counters")
        projects.append((project, int(counters.get("total", 0)), int(counters.get("passed", 0)), int(counters.get("failed", 0))))
        for result in root.iter(f"{TRX}UnitTestResult"):
            if result.get("outcome") != "Failed":
                continue
            cls, method = definitions.get(result.get("testId"), ("?", result.get("testName", "?")))
            message = result.findtext(f"{TRX}Output/{TRX}ErrorInfo/{TRX}Message", default="").strip()
            failures.append((cls, method, result.get("testName", method), message))
    return projects, failures


def escape_annotation(text: str) -> str:
    return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")


# Property values (title, file) also end at ":" or ",", so GitHub needs those escaped too.
def escape_property(text: str) -> str:
    return escape_annotation(text).replace(":", "%3A").replace(",", "%2C")


def cell(text: str) -> str:
    return text.replace("|", "\\|").replace("\r", "").replace("\n", "<br>")


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__.strip().splitlines()[-1])
        return 0
    os.chdir(Path(__file__).resolve().parents[2])
    projects, failures = read_results(Path(sys.argv[1]))
    if not projects:
        print(f"No test results (.trx) found under {sys.argv[1]}.")
        return 0
    rules = load_rules()
    on_github = os.environ.get("GITHUB_ACTIONS") == "true"

    summary = ["## Test results", "", "| Project | Total | Passed | Failed |", "|---|---|---|---|"]
    summary += [f"| {name} | {total} | {passed} | {failed} |" for name, total, passed, failed in projects]
    if failures:
        summary += ["", f"## {len(failures)} failed test(s), by business rule", "",
                    "| Rule | Case | Expected | Test | Failure |", "|---|---|---|---|---|"]
    else:
        summary += ["", "Every test passed."]

    for cls, method, display_name, message in failures:
        short = message if len(message) <= MAX_MESSAGE else message[:MAX_MESSAGE] + " …"
        matched = rules.get((cls, method)) or [("—", "Not in a matrix", "", "")]
        rule_ids = ", ".join(rule for rule, *_ in matched)
        case = "; ".join(case for _, case, _, _ in matched)
        expected = "; ".join(expected for _, _, expected, _ in matched if expected)
        summary.append(f"| {rule_ids} | {cell(case)} | {cell(expected)} | `{cls}.{method}` | {cell(short)} |")
        title = f"Rule {rule_ids}: {case}"
        body = f"Expected: {expected}\nTest: {display_name}\n\n{short}" if expected else f"Test: {display_name}\n\n{short}"
        if on_github:
            file, line = find_test_line(cls, method)
            location = f"file={escape_property(file)},line={line}," if file else ""
            print(f"::error {location}title={escape_property(title)}::{escape_annotation(body)}")
        else:
            print(f"FAILED  {title}\n  {body.replace(chr(10), chr(10) + '  ')}\n")

    report = "\n".join(summary) + "\n"
    summary_file = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_file:
        with open(summary_file, "a", encoding="utf-8") as handle:
            handle.write(report)
    if not on_github:
        print(report)
    return 0


if __name__ == "__main__":
    sys.exit(main())
