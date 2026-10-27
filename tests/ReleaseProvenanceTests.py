#!/usr/bin/env python3
"""Ensure a package cannot be substituted after its CI fingerprint is recorded."""
import json
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from rebuild_historical_release import sha256, verify


class ReleaseProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.version = "0.2.7"
        self.tag = "v0.2.7"
        self.commit = "a" * 40
        self.package = self.root / "Helium-Navigator-v0.2.7.pext"
        self.dll = b"MZtest-binary"
        manifest = (
            "Id: HeliumCharlo_b59e3c9f-9c52-4582-a332-0716b56cf023\n"
            "Version: 0.2.7\n"
            "Module: HeliumCharlo.dll\n"
        )
        with zipfile.ZipFile(self.package, "w") as zf:
            zf.writestr("HeliumCharlo.dll", self.dll)
            zf.writestr("extension.yaml", manifest)
        self.evidence = self.root / "release-evidence.json"
        self.data = {
            "schema": "helium.release-evidence.v1",
            "sourceTag": self.tag,
            "sourceCommit": self.commit,
            "version": self.version,
            "assemblyVersion": "0.2.7.0",
            "artifacts": [{
                "name": self.package.name,
                "sha256": sha256(self.package.read_bytes()),
                "bytes": self.package.stat().st_size,
                "dllSha256": sha256(self.dll),
            }],
        }
        self.save()

    def save(self):
        self.evidence.write_text(json.dumps(self.data), encoding="utf-8")

    def check(self):
        verify(self.evidence, self.package, self.commit, self.tag)

    def test_original_asset_is_accepted(self):
        self.check()

    def test_changed_archive_rejected(self):
        with self.package.open("ab") as out:
            out.write(b"one extra byte")
        with self.assertRaisesRegex(ValueError, "size mismatch"):
            self.check()

    def test_same_size_modified_bytes_rejected(self):
        content = self.package.read_bytes()
        self.package.write_bytes(content[:-1] + bytes([content[-1] ^ 1]))
        with self.assertRaisesRegex(ValueError, "SHA-256"):
            self.check()

    def test_source_commit_mismatch(self):
        with self.assertRaisesRegex(ValueError, "source commit"):
            verify(self.evidence, self.package, "b" * 40, self.tag)

    def test_wrong_tag_rejected(self):
        with self.assertRaisesRegex(ValueError, "release tag"):
            verify(self.evidence, self.package, self.commit, "v.0.2.6")

    def test_unexpected_manifest_version_rejected(self):
        self.data["version"] = "0.2.6"
        self.save()
        with self.assertRaisesRegex(ValueError, "version"):
            self.check()

    def test_bad_dll_digest_rejected(self):
        self.data["artifacts"][0]["dllSha256"] = "0" * 64
        self.save()
        with self.assertRaisesRegex(ValueError, "DLL hash"):
            self.check()

    def test_bad_asset_name_rejected(self):
        self.data["artifacts"][0]["name"] = "wrong.pext"
        self.save()
        with self.assertRaisesRegex(ValueError, "filename mismatch"):
            self.check()


if __name__ == "__main__":
    unittest.main()
