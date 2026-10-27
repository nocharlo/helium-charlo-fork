#!/usr/bin/env python3
"""Fail closed when a release binary or archive exposes absolute local paths.

Inspection covers archive members, printable ASCII/UTF-8 and UTF-16LE in both
alignments, and .NET CodeView records. Findings never echo potentially private
paths into CI logs. This is a focused path check, not a general secrets scanner.
"""
import re
import sys
import zipfile
from pathlib import Path

MAX_MEMBER_BYTES = 64 * 1024 * 1024
ARCHIVE_EXTENSIONS = {".pext", ".pthm", ".zip"}
BINARY_EXTENSIONS = {".dll", ".exe", ".pdb"}
TEXT_EXTENSIONS = {".md", ".txt", ".xaml", ".xml", ".json", ".yaml", ".yml"}

# A drive-rooted path anywhere in an embedded string (not just in CodeView).
WINDOWS_PATH = re.compile(
    r"(?i)(?<![A-Za-z0-9])"
    # At least two realistic, printable path components. Raw PNG/JPEG pixel
    # bytes frequently contain accidental two-letter patterns like "u:/ÿ";
    # these are not evidence of developer-machine paths.
    r"[A-Z]:[\\/][A-Za-z0-9_.$~ -]+"
    r"(?:[\\/][A-Za-z0-9_.$~ -]+)+"
)
# UNC server/share, with optional descendants. Require backslashes to avoid
# falsely identifying URLs (https://github.com/...) as network paths.
UNC_PATH = re.compile(
    r"(?i)(?<!\\)\\\\[A-Za-z0-9_.-]+\\[A-Za-z0-9_.$-]+"
    r"(?:\\[^\x00-\x20\\/:*?\"<>|]+)*"
)
POSIX_PROFILE = re.compile(
    r"(?i)(?<![A-Za-z0-9:/])/(?:home|Users)/"
    r"[^/\\\x00-\x20]+(?:/[^/\\\x00-\x20]+)?"
)
# Avoid failing a *text-only* sample that explicitly uses a placeholder user.
# Executables, CodeView records and other non-text assets receive no exception.
TEXT_EXAMPLE = re.compile(
    r"(?i)^[A-Z]:[\\/]Users[\\/]"
    r"(?:Example|YourName|USERNAME|Placeholder)(?:[\\/]|$)"
)


def _printable_representations(data):
    yield data.decode("latin-1")
    # UTF-16LE may start on either byte offset in a binary payload.
    yield data.decode("utf-16-le", errors="ignore")
    yield data[1:].decode("utf-16-le", errors="ignore")


def _absolute_path_in_text(text, filename):
    extension = Path(filename).suffix.lower()
    for pattern in (WINDOWS_PATH, UNC_PATH, POSIX_PROFILE):
        for match in pattern.finditer(text):
            if extension in TEXT_EXTENSIONS and TEXT_EXAMPLE.match(match.group()):
                continue
            return True
    return False


def _codeview_path(data):
    # RSDS + 16-byte GUID + 4-byte age + zero-terminated PDB path.
    index = 0
    while True:
        index = data.find(b"RSDS", index)
        if index < 0:
            return False
        index += 4
        start = index + 20
        end = data.find(b"\0", start, min(len(data), start + 4096))
        if end < 0:
            continue
        pdb_path = data[start:end].decode("utf-8", errors="ignore")
        if (
            re.match(r"(?i)^[A-Z]:[\\/]", pdb_path)
            or pdb_path.startswith("\\\\")
            or pdb_path.startswith("/")
        ):
            return True


def findings(data, filename):
    problems = []
    if Path(filename).suffix.lower() == ".pdb":
        problems.append("debug symbol file in release")
    if any(_absolute_path_in_text(text, filename) for text in _printable_representations(data)):
        problems.append("embedded absolute filesystem path")
    if Path(filename).suffix.lower() in {".dll", ".exe"} and _codeview_path(data):
        problems.append("absolute CodeView PDB path")
    return problems


def _bad_member_name(name):
    normalized = name.replace("\\", "/")
    if (
        name.startswith(("/", "\\"))
        or re.match(r"(?i)^[A-Z]:[/\\]", name)
        or ".." in normalized.split("/")
        or "\0" in name
    ):
        return True
    return _absolute_path_in_text(name, "archive-member.dll")


def scan(path):
    path = Path(path)
    if path.suffix.lower() in ARCHIVE_EXTENSIONS:
        if not zipfile.is_zipfile(path):
            return ["invalid or unreadable archive"]
        with zipfile.ZipFile(path) as archive:
            members = archive.infolist()
            if not members:
                return ["empty archive"]
            problems = []
            for number, member in enumerate(members, 1):
                if _bad_member_name(member.filename):
                    problems.append("entry #{}: unsafe or private member name".format(number))
                if member.is_dir():
                    continue
                if member.file_size > MAX_MEMBER_BYTES:
                    problems.append("entry #{}: exceeds inspection limit".format(number))
                    continue
                try:
                    payload = archive.read(member)
                except (OSError, RuntimeError, zipfile.BadZipFile, ValueError):
                    problems.append("entry #{}: unreadable data".format(number))
                    continue
                for issue in findings(payload, member.filename):
                    problems.append("entry #{}: {}".format(number, issue))
            return problems
    if path.stat().st_size > MAX_MEMBER_BYTES:
        return ["file exceeds inspection limit"]
    return findings(path.read_bytes(), path.name)


def main():
    if len(sys.argv) < 2:
        print("Usage: python scripts/check_artifact_opsec.py <package-or-binary> [...]")
        return 2
    failed = False
    for i, argument in enumerate(sys.argv[1:], 1):
        try:
            problems = scan(Path(argument))
        except (OSError, RuntimeError, ValueError, zipfile.BadZipFile) as exc:
            problems = ["unable to scan ({})".format(type(exc).__name__)]
        if problems:
            failed = True
            for issue in problems:
                print("FAIL artifact #{}: {}".format(i, issue))
        else:
            print("PASS artifact #{}: no private build paths detected".format(i))
    return int(failed)


if __name__ == "__main__":
    sys.exit(main())
