#!/usr/bin/env python3
"""
Claude Code PreToolUse hook for Write and Edit.

Blocks (exit code 2, reason sent back to the agent):
  1. New files outside the allowed folder and naming patterns
  2. Production C# files over the line limit
  3. Content that looks like a secret

Edit the CONFIG section to match your repository.
"""
import json
import os
import re
import sys

# ---------------- CONFIG ----------------
MAX_LINES = 200  # production .cs files (generated migrations excluded)
PROGRAM_MAX_LINES = 300  # Program.cs (composition root)

MODULE_FOLDERS = "Controllers|Services|Data|Entities|Models"
PASCAL = r"[A-Z][A-Za-z0-9]*"
DATE_SLUG = r"\d{4}-\d{2}-\d{2}-[a-z0-9-]+"

# Paths where a NEW file may be created (relative to the repository root)
ALLOWED_NEW_FILES = [
    rf"^docs/specs/[a-z-]+/{DATE_SLUG}\.md$",
    rf"^docs/plans/[a-z-]+/{DATE_SLUG}/(plan|scenarios|review)\.md$",
    rf"^docs/plans/[a-z-]+/{DATE_SLUG}/batches/B\d+\.md$",
    rf"^backlog/(features|bugs|improvements|tech-debt)/{DATE_SLUG}\.md$",
    rf"^src/RestaurantManagement\.Modules\.{PASCAL}/({MODULE_FOLDERS})/{PASCAL}\.cs$",
    rf"^src/RestaurantManagement\.Modules\.{PASCAL}/Migrations/\d{{14}}_{PASCAL}(\.Designer)?\.cs$",
    rf"^src/RestaurantManagement\.Modules\.{PASCAL}/Migrations/{PASCAL}ModelSnapshot\.cs$",
    rf"^src/RestaurantManagement\.(Shared|Web)/({PASCAL}/)*{PASCAL}\.cs$",
    rf"^tests/RestaurantManagement\.[A-Za-z.]+\.Tests/({PASCAL}/)*{PASCAL}\.cs$",
    r"^\.claude/",
]

# Generated or non-production files that skip the line limit
LINE_LIMIT_EXEMPT = [r"/Migrations/", r"^tests/", r"^docs/", r"^backlog/", r"^\.claude/"]

# Secret patterns (checked outside tests/)
SECRET_PATTERNS = [
    (r"-----BEGIN [A-Z ]*PRIVATE KEY-----", "a private key"),
    (r"https://[0-9a-f]{32}@[\w.-]*sentry\.io", "a Sentry DSN (Data Source Name)"),
    (r"(?i)(Host|Server)=[^;\"']+;[^\"']*Password=(?!\s*(CHANGE_ME|<|\{|\$|\"|;))[^;\"'\s]+",
     "a database connection string with a password"),
    (r"(?i)\"(jwt|secret|signing|api)[_-]?key\"\s*:\s*\"(?!CHANGE_ME|<)[^\"]{16,}\"",
     "a key value in configuration"),
    (r"AKIA[0-9A-Z]{16}", "an AWS (Amazon Web Services) access key"),
]
# ----------------------------------------


def block(reason: str) -> None:
    print(f"BLOCKED by enforce-structure hook: {reason}", file=sys.stderr)
    sys.exit(2)


def main() -> None:
    data = json.load(sys.stdin)
    tool = data.get("tool_name", "")
    if tool not in ("Write", "Edit"):
        sys.exit(0)

    tool_input = data.get("tool_input", {})
    root = os.environ.get("CLAUDE_PROJECT_DIR") or data.get("cwd") or os.getcwd()
    abs_path = os.path.abspath(tool_input.get("file_path", ""))
    rel = os.path.relpath(abs_path, root).replace(os.sep, "/")

    if rel.startswith(".."):
        block(f"{abs_path} is outside the repository.")
    if re.search(r"(^|/)(bin|obj)/", rel):
        block(f"{rel} is a build output folder.")

    exists = os.path.exists(abs_path)

    # 1. Folder and naming patterns for new files
    if not exists and not any(re.search(p, rel) for p in ALLOWED_NEW_FILES):
        block(
            f"{rel} is not an allowed location or name for a new file. "
            "Module code goes in Controllers/, Services/, Data/, Entities/ or Models/ with a "
            "PascalCase file name; tests in tests/<Project>.Tests/. Check the architecture skill. "
            "If this file is really needed, stop and report it as [Blocking]."
        )

    # Resulting content after the write or edit
    if tool == "Write":
        new_content = tool_input.get("content", "")
        added = new_content
    else:
        old = tool_input.get("old_string", "")
        new = tool_input.get("new_string", "")
        current = open(abs_path, encoding="utf-8").read() if exists else ""
        if tool_input.get("replace_all"):
            new_content = current.replace(old, new)
        else:
            new_content = current.replace(old, new, 1)
        added = new

    # 2. Line limit for production C# files
    if rel.endswith(".cs") and not any(re.search(p, rel) for p in LINE_LIMIT_EXEMPT):
        lines = new_content.count("\n") + 1
        limit = PROGRAM_MAX_LINES if os.path.basename(rel) == "Program.cs" else MAX_LINES
        if lines > limit:
            block(
                f"{rel} would have {lines} lines (limit {limit}). The class likely does more "
                "than one job: split it by responsibility (clean-code skill)."
            )

    # 3. Secrets
    if not rel.startswith("tests/"):
        for pattern, label in SECRET_PATTERNS:
            if re.search(pattern, added):
                block(
                    f"{rel} would contain {label}. Use user-secrets locally and environment "
                    "variables in deployment; appsettings.json holds placeholders only."
                )

    sys.exit(0)


if __name__ == "__main__":
    main()
