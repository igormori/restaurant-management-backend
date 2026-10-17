#!/usr/bin/env python3
"""
Summarises code coverage (Cobertura files from `dotnet test --collect:"XPlat Code Coverage"`): which share of each
module's code the tests run, and which files have the most lines no test runs, with those line numbers.

Every test project writes its own file, and Web.Tests runs code from every module, so the files are merged: a line
counts as covered when any test project ran it. Generated migrations are left out.

- On GitHub Actions: the report goes to the run's Summary page.
- Locally: it is printed.
Reporting only; always exits 0.

Usage: python3 .claude/scripts/report-coverage.py <results directory>
"""
import os
import sys
import xml.etree.ElementTree as ET
from collections import defaultdict
from pathlib import Path

WORST_FILES_PER_MODULE = 8
MAX_RANGES = 12


def merge(directory: Path):
    """Maps module -> file -> {line: covered}, merged across every Cobertura file."""
    modules = defaultdict(lambda: defaultdict(dict))
    for report in sorted(directory.rglob("*.cobertura.xml")):
        for package in ET.parse(report).getroot().iter("package"):
            module = package.get("name", "?")
            for cls in package.iter("class"):
                filename = cls.get("filename", "?").replace("\\", "/")
                if "/Migrations/" in filename:
                    continue
                lines = modules[module][filename]
                for line in cls.iter("line"):
                    number = int(line.get("number"))
                    lines[number] = lines.get(number, False) or int(line.get("hits", 0)) > 0
    return modules


def ranges(numbers) -> str:
    """12, 13, 14, 20 -> "12–14, 20"."""
    spans, start, previous = [], None, None
    for number in sorted(numbers):
        if start is None:
            start = previous = number
        elif number == previous + 1:
            previous = number
        else:
            spans.append((start, previous))
            start = previous = number
    if start is not None:
        spans.append((start, previous))
    text = [f"{a}" if a == b else f"{a}–{b}" for a, b in spans[:MAX_RANGES]]
    return ", ".join(text) + (" …" if len(spans) > MAX_RANGES else "")


def percent(covered: int, total: int) -> str:
    return f"{100 * covered / total:.1f}%" if total else "—"


def main() -> int:
    if len(sys.argv) != 2:
        print(__doc__.strip().splitlines()[-1])
        return 0
    modules = merge(Path(sys.argv[1]))
    if not modules:
        print(f"No coverage files (*.cobertura.xml) found under {sys.argv[1]}.")
        return 0

    report = ["## Code coverage", "", "Share of each module's lines that at least one test runs (migrations left out).", "",
              "| Module | Lines covered | Coverage |", "|---|---|---|"]
    details = []
    all_covered = all_total = 0
    for module in sorted(modules):
        files = modules[module]
        covered = sum(sum(lines.values()) for lines in files.values())
        total = sum(len(lines) for lines in files.values())
        all_covered, all_total = all_covered + covered, all_total + total
        report.append(f"| {module} | {covered} / {total} | {percent(covered, total)} |")

        worst = sorted(((f, [n for n, hit in lines.items() if not hit], len(lines)) for f, lines in files.items()),
                       key=lambda item: -len(item[1]))
        worst = [item for item in worst if item[1]][:WORST_FILES_PER_MODULE]
        if worst:
            details += ["", f"### {module}: files with the most lines no test runs", "",
                        "| File | Coverage | Lines no test runs |", "|---|---|---|"]
            details += [f"| `{f}` | {percent(total_lines - len(missed), total_lines)} | {ranges(missed)} |"
                        for f, missed, total_lines in worst]
    report.append(f"| **All** | **{all_covered} / {all_total}** | **{percent(all_covered, all_total)}** |")
    text = "\n".join(report + details) + "\n"

    summary_file = os.environ.get("GITHUB_STEP_SUMMARY")
    if summary_file:
        with open(summary_file, "a", encoding="utf-8") as handle:
            handle.write(text)
    else:
        print(text)
    return 0


if __name__ == "__main__":
    sys.exit(main())
