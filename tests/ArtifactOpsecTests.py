#!/usr/bin/env python3
"""Regression coverage for distributable DLL and archive privacy checks."""
import sys
import tempfile
import unittest
import zipfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from check_artifact_opsec import findings, scan


class ArtifactOpsecTests(unittest.TestCase):
    def test_windows_user_path_ascii(self):
        result = findings(b"C:\\Users\\ActualUser\\build\\Helium.pdb", "HeliumCharlo.dll")
        self.assertIn("embedded absolute filesystem path", result)

    def test_absolute_drive_outside_codeview(self):
        result = findings(b"other\x00D:\\build\\ci\\bin\\file.pdb\x00", "HeliumCharlo.dll")
        self.assertIn("embedded absolute filesystem path", result)

    def test_drive_with_forward_slashes(self):
        self.assertIn("embedded absolute filesystem path",
                      findings(b"E:/workspace/Helium/src", "HeliumCharlo.dll"))

    def test_utf16_two_alignments(self):
        payload = "D:\\workspace\\source".encode("utf-16-le")
        for content in (payload, b"\x00" + payload):
            self.assertIn("embedded absolute filesystem path",
                          findings(content, "HeliumCharlo.dll"))

    def test_unc_path_in_binary(self):
        self.assertIn("embedded absolute filesystem path",
                      findings(b"\\\\server\\private-share\\build\\release", "HeliumCharlo.dll"))

    def test_codeview_drive(self):
        payload = b"RSDS" + bytes(20) + b"C:\\Users\\Example\\project\\file.pdb\0"
        self.assertIn("absolute CodeView PDB path", findings(payload, "HeliumCharlo.dll"))

    def test_codeview_unc(self):
        payload = b"RSDS" + bytes(20) + b"\\\\server\\share\\project\\file.pdb\0"
        self.assertIn("absolute CodeView PDB path", findings(payload, "HeliumCharlo.dll"))

    def test_codeview_relative_pdb_name(self):
        payload = b"RSDS" + bytes(20) + b"HeliumCharlo.pdb\0"
        self.assertNotIn("absolute CodeView PDB path", findings(payload, "HeliumCharlo.dll"))

    def test_posix_profiles(self):
        for path in (b"/home/developer/project/", b"/Users/developer/project/"):
            self.assertIn("embedded absolute filesystem path",
                          findings(path, "HeliumCharlo.dll"))

    def test_clean_binary(self):
        self.assertEqual([], findings(b"Helium release compiled", "HeliumCharlo.dll"))

    def test_web_urls_are_not_unc(self):
        self.assertEqual([], findings(
            b"Source: https://github.com/nocharlo/helium-charlo-fork", "README.md"
        ))

    def test_png_noise_does_not_look_like_a_local_path(self):
        # Non-printable binary payloads previously produced false positives.
        data = b"\x89PNG\r\n" + b"u:/\xe7\xff\x80" + b"C:/\xd6\xf9\x00"
        self.assertEqual([], findings(data, "cover.png"))

    def test_windows_profile_with_spaces_still_detected(self):
        self.assertIn("embedded absolute filesystem path",
                      findings(b"C:\\Users\\Actual User\\build\\file.pdb",
                               "HeliumCharlo.dll"))

    def test_text_placeholder_does_not_block(self):
        # Documentation or template strings with obviously fake example names
        # are not private paths. The same content in a DLL still fails.
        text = b"Example: C:\\Users\\Example\\project\\file.pdb"
        self.assertEqual([], findings(text, "README.txt"))
        self.assertIn("embedded absolute filesystem path", findings(text, "HeliumCharlo.dll"))

    def test_symbols_file_is_rejected(self):
        self.assertIn("debug symbol file in release", findings(b"", "HeliumCharlo.pdb"))

    def test_archive_member_name_absolute(self):
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "test.pext"
            with zipfile.ZipFile(package, "w") as zf:
                zf.writestr("C:/Users/developer/private.txt", "nothing")
            self.assertTrue(any("member name" in issue for issue in scan(package)))

    def test_archive_member_name_embedded(self):
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "test.pext"
            with zipfile.ZipFile(package, "w") as zf:
                zf.writestr("documentation/D:\\build\\private.txt", "nothing")
            self.assertTrue(any("member name" in issue for issue in scan(package)))

    def test_archive_member_dotdot(self):
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "test.pext"
            with zipfile.ZipFile(package, "w") as zf:
                zf.writestr("../secrets.txt", "nothing")
            self.assertTrue(any("member name" in issue for issue in scan(package)))

    def test_archive_payload_and_no_echo(self):
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "test.pext"
            with zipfile.ZipFile(package, "w") as zf:
                zf.writestr("extension.yaml", "Version: 0.2.7")
                zf.writestr("HeliumCharlo.dll", b"D:\\personal\\dir\\app.pdb")
            errors = scan(package)
            self.assertTrue(errors)
            self.assertTrue(all("personal" not in e for e in errors))

    def test_corrupt_archive_fails_closed(self):
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "broken.pext"
            package.write_bytes(b"not a zip")
            self.assertTrue(scan(package))

    def test_clean_package_passes(self):
        with tempfile.TemporaryDirectory() as temp:
            package = Path(temp) / "test.pext"
            with zipfile.ZipFile(package, "w") as zf:
                zf.writestr("extension.yaml", "Version: 0.2.7")
                zf.writestr("HeliumCharlo.dll", b"MZcleanbinary")
            self.assertEqual([], scan(package))


if __name__ == "__main__":
    unittest.main()
