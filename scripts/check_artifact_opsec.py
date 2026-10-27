#!/usr/bin/env python3
"""Reject release artifacts containing absolute local build paths.

Never prints the discovered path or username, only the affected package/member.
"""
import re
import sys
import zipfile
from pathlib import Path

USER_PATH = re.compile(
    r"(?i)(?:[a-z]:[\\/](?:users|documents and settings)[\\/]"
    r"|/(?:home|users)/[^\\/\s]+[\\/])"
)
ABSOLUTE_DRIVE = re.compile(r"(?i)^[a-z]:[\\/]")
MAX_MEMBER_BYTES = 64 * 1024 * 1024


def findings(data, filename):
    problems = []
    # .NET can embed paths as ASCII/UTF-8 or UTF-16LE, at either alignment.
    for text in (
        data.decode("latin-1"),
        data.decode("utf-16-le", errors="ignore"),
        data[1:].decode("utf-16-le", errors="ignore"),
    ):
        if USER_PATH.search(text):
            problems.append("embedded user-profile path")
            break
    if filename.lower().endswith((".dll", ".exe")):
        # CodeView RSDS: signature (4), GUID (16), age (4), PDB path (NUL-terminated).
        start = 0
        while True:
            index = data.find(b"RSDS", start)
            if index < 0:
                break
            start = index + 4
            end = data.find(b"\0", index + 24, min(len(data), index + 1024))
            if end < 0:
                continue
            pdb_path = data[index + 24:end].decode("utf-8", errors="ignore")
            if ABSOLUTE_DRIVE.match(pdb_path) or pdb_path.startswith("/"):
                problems.append("absolute CodeView PDB path")
                break
    if filename.lower().endswith(".pdb"):
        problems.append("debug symbols included in release")
    return problems


def scan(path):
    problems = []
    if zipfile.is_zipfile(path):
        with zipfile.ZipFile(path) as archive:
            for member in archive.infolist():
                if member.is_dir():
                    continue
                if member.file_size > MAX_MEMBER_BYTES:
                    problems.append("{}: member exceeds scan limit".format(member.filename))
                    continue
                for issue in findings(archive.read(member), member.filename):
                    problems.append("{}: {}".format(member.filename, issue))
    else:
        if path.stat().st_size > MAX_MEMBER_BYTES:
            return ["file exceeds scan limit"]
        problems.extend(findings(path.read_bytes(), path.name))
    return problems


def main():
    if len(sys.argv) < 2:
        print("Usage: python scripts/check_artifact_opsec.py <package-or-binary> [...]")
        return 2
    failed = False
    for argument in sys.argv[1:]:
        path = Path(argument)
        try:
            problems = scan(path)
        except (OSError, zipfile.BadZipFile, RuntimeError, ValueError) as exc:
            print("FAIL {}: unable to scan ({})".format(path.name, type(exc).__name__))
            failed = True
            continue
        if problems:
            failed = True
            for issue in problems:
                print("FAIL {}: {}".format(path.name, issue))
        else:
            print("PASS {}: no embedded local build paths".format(path.name))
    return int(failed)


if __name__ == "__main__":
    sys.exit(main())
