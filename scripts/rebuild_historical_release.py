#!/usr/bin/env python3
"""Rebuild a historical Helium .pext from an exact, pinned release-tag commit.

No git history or tag is changed. The compiled DLL has release debug symbols
disabled by command-line properties, including on historical unpatched csproj.
Only an inspected package and a path-free JSON evidence document are emitted.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import zipfile

from check_artifact_opsec import scan

EXTENSION_ID = "HeliumCharlo_b59e3c9f-9c52-4582-a332-0716b56cf023"
MODULE_NAME = "HeliumCharlo.dll"


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def git(root, *args):
    return subprocess.check_output(
        ["git", "-C", str(root), *args], text=True
    ).strip()


def manifest_value(data, name):
    match = re.search(r"(?m)^" + re.escape(name) + r":\s*(\S+)\s*$", data)
    if not match:
        raise ValueError("Missing release manifest field: " + name)
    return match.group(1)


def create_pext(package, dll_data, yaml_data):
    with zipfile.ZipFile(package, "w", compression=zipfile.ZIP_DEFLATED) as archive:
        for name, data in (
            (MODULE_NAME, dll_data),
            ("extension.yaml", yaml_data),
        ):
            entry = zipfile.ZipInfo(name, date_time=(1980, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o644 << 16
            archive.writestr(entry, data)


def build(args):
    source = args.source.resolve()
    output = args.output.resolve()
    if output.exists():
        raise ValueError("Output directory must be new")
    if git(source, "rev-parse", "HEAD").lower() != args.commit.lower():
        raise ValueError("Source HEAD does not match pinned historical commit")
    if git(source, "status", "--porcelain"):
        raise ValueError("Historical source checkout must be clean")
    if not re.fullmatch(r"[0-9a-fA-F]{40}", args.commit):
        raise ValueError("Invalid source commit SHA")
    if args.tag not in ("v0.2.5", "v.0.2.6", "v0.2.7"):
        raise ValueError("Unsupported historical release tag")
    version = args.tag.lstrip("v.")
    manifest = (source / "source/HeliumHybrid/extension.yaml").read_bytes()
    decoded_manifest = manifest.decode("utf-8")
    theme = (source / "theme/theme.yaml").read_text(encoding="utf-8")
    if manifest_value(decoded_manifest, "Version") != version:
        raise ValueError("Extension tag/manifest version mismatch")
    if manifest_value(theme, "Version") != version:
        raise ValueError("Theme tag/manifest version mismatch")
    if manifest_value(decoded_manifest, "Id") != EXTENSION_ID:
        raise ValueError("Unexpected extension identifier")
    if manifest_value(decoded_manifest, "Module") != MODULE_NAME:
        raise ValueError("Unexpected extension module")
    if 'AssemblyVersion("' + version + '.0")' not in (
        source / "source/HeliumHybrid/Plugin.cs"
    ).read_text(encoding="utf-8"):
        raise ValueError("Declared assembly version differs from manifest")

    output.mkdir(parents=True)
    project = source / "source/HeliumHybrid/HeliumHybrid.csproj"
    bin_dir = output / "bin"
    obj_dir = output / "obj"
    bin_dir.mkdir()
    obj_dir.mkdir()
    env = dict(os.environ)
    env["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1"
    command = [
        "dotnet", "build", str(project),
        "--configuration", "Release",
        "-p:DebugType=none", "-p:DebugSymbols=false",
        "-p:Deterministic=true", "-p:ContinuousIntegrationBuild=true",
        "-p:BaseOutputPath=" + str(bin_dir) + os.sep,
        "-p:BaseIntermediateOutputPath=" + str(obj_dir) + os.sep,
    ]
    subprocess.run(command, check=True, env=env)
    dlls = list(bin_dir.rglob(MODULE_NAME))
    if len(dlls) != 1:
        raise ValueError("Must have exactly one compiled Navigator DLL")
    dll_data = dlls[0].read_bytes()
    if not dll_data.startswith(b"MZ"):
        raise ValueError("Invalid Windows DLL")

    # This checks assembly metadata, not merely the source-level attribute.
    ps_script = (
        "[Reflection.AssemblyName]::GetAssemblyName('" +
        str(dlls[0]).replace("'", "''") +
        "').Version.ToString()"
    )
    compiled_version = subprocess.check_output(
        ["pwsh", "-NoProfile", "-Command", ps_script], text=True
    ).strip()
    if compiled_version != version + ".0":
        raise ValueError("Compiled DLL AssemblyVersion does not match manifest")

    package = output / ("Helium-Navigator-v" + version + ".pext")
    create_pext(package, dll_data, manifest)
    with zipfile.ZipFile(package) as archive:
        if set(archive.namelist()) != {MODULE_NAME, "extension.yaml"}:
            raise ValueError("Unexpected .pext contents")
        if archive.read(MODULE_NAME) != dll_data:
            raise ValueError("Packaged DLL differs from compiled DLL")
        if archive.read("extension.yaml") != manifest:
            raise ValueError("Packaged manifest differs from tagged source")
    problems = scan(package)
    if problems:
        raise ValueError("Artifact OPSEC failed: " + "; ".join(problems))

    evidence = {
        "schema": "helium.release-evidence.v1",
        "sourceTag": args.tag,
        "sourceCommit": args.commit.lower(),
        "version": version,
        "assemblyVersion": compiled_version,
        "projectConfiguration": "Release",
        "debugType": "none",
        "debugSymbols": False,
        "runtimeTested": False,
        "artifacts": [
            {
                "name": package.name,
                "sha256": sha256(package.read_bytes()),
                "bytes": package.stat().st_size,
                "dllSha256": sha256(dll_data),
            }
        ],
    }
    (output / "release-evidence.json").write_text(
        json.dumps(evidence, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    verify(output / "release-evidence.json", package, args.commit, args.tag)
    print("PASS: historical release candidate verified", args.tag)
    print("SHA-256:", evidence["artifacts"][0]["sha256"])


def verify(evidence_path, artifact, source_commit=None, source_tag=None):
    data = json.loads(Path(evidence_path).read_text(encoding="utf-8"))
    if data.get("schema") != "helium.release-evidence.v1":
        raise ValueError("Unrecognized evidence format")
    if source_commit and data.get("sourceCommit") != source_commit.lower():
        raise ValueError("Pinned source commit does not match evidence")
    if source_tag and data.get("sourceTag") != source_tag:
        raise ValueError("Pinned release tag does not match evidence")
    entries = data.get("artifacts")
    if not isinstance(entries, list) or len(entries) != 1:
        raise ValueError("Expected exactly one candidate asset in evidence")
    entry = entries[0]
    package = Path(artifact)
    if package.name != entry.get("name"):
        raise ValueError("Candidate asset filename mismatch")
    if not package.is_file() or package.stat().st_size != entry.get("bytes"):
        raise ValueError("Candidate asset size mismatch")
    if sha256(package.read_bytes()) != entry.get("sha256"):
        raise ValueError("Candidate asset SHA-256 mismatch")
    problems = scan(package)
    if problems:
        raise ValueError("Package OPSEC scan failed")
    with zipfile.ZipFile(package) as zf:
        if set(zf.namelist()) != {MODULE_NAME, "extension.yaml"}:
            raise ValueError("Invalid extension package file set")
        manifest = zf.read("extension.yaml").decode("utf-8")
        version = manifest_value(manifest, "Version")
        if version != data.get("version"):
            raise ValueError("Package version does not match evidence")
        if manifest_value(manifest, "Id") != EXTENSION_ID:
            raise ValueError("Unexpected extension ID")
        if sha256(zf.read(MODULE_NAME)) != entry.get("dllSha256"):
            raise ValueError("Packaged DLL hash differs from evidence")
    print("PASS: exact release asset matches recorded hash and inspection")


def main():
    parser = argparse.ArgumentParser()
    sub = parser.add_subparsers(dest="action", required=True)
    b = sub.add_parser("build")
    b.add_argument("--source", required=True, type=Path)
    b.add_argument("--output", required=True, type=Path)
    b.add_argument("--tag", required=True)
    b.add_argument("--commit", required=True)
    v = sub.add_parser("verify")
    v.add_argument("--evidence", required=True, type=Path)
    v.add_argument("--artifact", required=True, type=Path)
    v.add_argument("--tag", required=True)
    v.add_argument("--commit", required=True)
    args = parser.parse_args()
    if args.action == "build":
        build(args)
    else:
        verify(args.evidence, args.artifact, args.commit, args.tag)


if __name__ == "__main__":
    try:
        main()
    except (OSError, ValueError, subprocess.CalledProcessError, zipfile.BadZipFile) as exc:
        print("FAIL: historical release candidate cannot be certified",
              "(" + type(exc).__name__ + ")")
        sys.exit(1)
