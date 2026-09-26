"""The 31 male GLBs must be verified before any private files are written."""

import hashlib
import importlib.util
import json
import os
import struct
import tempfile
import unittest
import zipfile
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "scripts/prepare-unreal-male.py"
spec = importlib.util.spec_from_file_location("prepare_unreal_male", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def glb(morphs=0):
    mesh = {"primitives": [{"attributes": {"POSITION": 0},
                            "targets": [{"POSITION": 0}] * morphs}]}
    if morphs:
        mesh["extras"] = {"targetNames": [f"face_{i:02d}" for i in range(15)]}
    body = json.dumps({"meshes": [mesh], "accessors": [{"count": 3}]}).encode()
    body += b" " * (-len(body) % 4)
    return struct.pack("<IIIII", 0x46546C67, 2, 20 + len(body), len(body), 0x4E4F534A) + body


class MalePartExtractionTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.apk = self.root / "avatar.apk"
        self.output = self.root / "private"
        self.parts = {"male-base.glb": glb(15)}
        for kind in ("eye", "hair"):
            for number in range(15):
                self.parts[f"male-{kind}-{number:02d}.glb"] = glb()
        self.hashes = {name: hashlib.sha256(data).hexdigest()
                       for name, data in self.parts.items()}

    def write_apk(self, *, corrupt=None):
        with zipfile.ZipFile(self.apk, "w") as archive:
            for name, data in self.parts.items():
                archive.writestr(f"assets/parts/{name}", b"corrupt" if name == corrupt else data)

    def test_verified_parts_extract_without_overwriting_another_asset(self):
        self.write_apk()
        report = module.prepare(self.apk, self.output, expected_hashes=self.hashes)
        self.assertEqual(len(report), 31)
        self.assertEqual((self.output / "male-eye-03.glb").read_bytes(), glb())
        self.assertEqual((self.output / "male-base.glb").read_bytes(), glb(15))
        self.assertEqual(len(module.prepare(self.apk, self.output, expected_hashes=self.hashes)), 31)
        (self.output / "male-eye-03.glb").write_bytes(b"owned by someone else")
        with self.assertRaises(ValueError):
            module.prepare(self.apk, self.output, expected_hashes=self.hashes)

    def test_hash_or_missing_morph_fails_before_writing(self):
        self.write_apk(corrupt="male-hair-14.glb")
        with self.assertRaises(ValueError):
            module.prepare(self.apk, self.output, expected_hashes=self.hashes)
        self.assertFalse(self.output.exists())

        self.parts["male-base.glb"] = glb()
        self.hashes["male-base.glb"] = hashlib.sha256(glb()).hexdigest()
        self.write_apk()
        with self.assertRaises(ValueError):
            module.prepare(self.apk, self.output, expected_hashes=self.hashes)
        self.assertFalse(self.output.exists())

    def test_rejects_symlink_destination(self):
        self.write_apk()
        try:
            self.output.symlink_to(self.root, target_is_directory=True)
        except OSError as error:
            if getattr(error, "winerror", None) == 1314:
                self.skipTest("Windows symlink creation requires Developer Mode or privilege")
            raise
        with self.assertRaises(ValueError):
            module.prepare(self.apk, self.output, expected_hashes=self.hashes)

    @unittest.skipUnless(os.environ.get("BIGIMONG_TEST_APK"), "Set BIGIMONG_TEST_APK for the supplied APK")
    def test_uploaded_apk_contains_matching_male_parts(self):
        report = module.prepare(Path(os.environ["BIGIMONG_TEST_APK"]), self.output)
        self.assertEqual(len(report), 31)
        self.assertEqual(report["male-base.glb"]["morphs"], 15)
        self.assertGreater(report["male-base.glb"]["triangles"], 0)


if __name__ == "__main__":
    unittest.main()
