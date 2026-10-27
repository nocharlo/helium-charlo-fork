#!/usr/bin/env python3
"""Regression checks for private build-path detection."""
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from check_artifact_opsec import findings, scan


class ArtifactOpsecTests(unittest.TestCase):
    def test_rejects_embedded_windows_pdb(self):
        payload = b"RSDS" + bytes(20) + b"C:\\Users\\Example\\project\\bin\\HeliumCharlo.pdb\0"
        self.assertIn("embedded user-profile path", findings(payload, "HeliumCharlo.dll"))
        self.assertIn("absolute CodeView PDB path", findings(payload, "HeliumCharlo.dll"))

    def test_rejects_utf16_profile_path(self):
        payload = "C:\\Users\\Example\\build".encode("utf-16-le")
        self.assertIn("embedded user-profile path", findings(payload, "HeliumCharlo.dll"))

    def test_rejects_linux_and_macos_profile_paths(self):
        for path in (b"/home/example/build/", b"/Users/example/project/"):
            self.assertIn("embedded user-profile path", findings(path, "HeliumCharlo.dll"))

    def test_clean_payload_is_accepted(self):
        self.assertEqual([], findings(b"HeliumCharlo release binary", "HeliumCharlo.dll"))

    def test_pdb_file_not_allowed(self):
        self.assertIn("debug symbols included in release", findings(b"", "HeliumCharlo.pdb"))

    def test_zip_archive_scanning(self):
        with tempfile.TemporaryDirectory() as tmp:
            archive = Path(tmp) / "test.pext"
            with zipfile.ZipFile(archive, "w") as zf:
                zf.writestr("extension.yaml", "Version: 0.2.7")
                zf.writestr("HeliumCharlo.dll", b"C:\\Users\\Example\\debug")
            self.assertTrue(any("HeliumCharlo.dll" in issue for issue in scan(archive)))


if __name__ == "__main__":
    unittest.main()
