"""Test the private VARCO handoff without publishing its model bytes."""

import hashlib
import importlib.util
import json
import os
import struct
import tempfile
import unittest
import zipfile
from pathlib import Path


SOURCE = Path(__file__).resolve().parents[1] / "scripts" / "prepare-unreal-varco.py"
module = None
if SOURCE.is_file():
    spec = importlib.util.spec_from_file_location("prepare_unreal_varco", SOURCE)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)


def minimal_glb(index_count=6, *, skins=0):
    gltf = {
        "asset": {"version": "2.0"},
        "accessors": [{"count": index_count}],
        "meshes": [{"primitives": [{"indices": 0}]}],
        "skins": [{}] * skins,
    }
    chunk = json.dumps(gltf, separators=(",", ":")).encode()
    chunk += b" " * (-len(chunk) % 4)
    return struct.pack("<III", 0x46546C67, 2, 20 + len(chunk)) + struct.pack("<II", len(chunk), 0x4E4F534A) + chunk


def sample_archive(apk):
    male = minimal_glb()
    female = minimal_glb(12)
    data = {"male": male, "female": female}
    hashes = {sex: hashlib.sha256(contents).hexdigest() for sex, contents in data.items()}
    provenance = {sex: {"file": f"varco-{sex}.glb", "sha256": hashes[sex]} for sex in data}
    with zipfile.ZipFile(apk, "w") as archive:
        archive.writestr("assets/models/provenance.json", json.dumps(provenance))
        for sex, contents in data.items():
            archive.writestr(f"assets/models/varco-{sex}.glb", contents)
    return data, hashes


class VarcoHandoffTest(unittest.TestCase):
    def setUp(self):
        self.assertIsNotNone(module, "VARCO Unreal handoff script must exist")

    def test_inspection_counts_triangles_and_discloses_unrigged_mesh(self):
        info = module.inspect_glb(minimal_glb())
        self.assertEqual(info["triangles"], 2)
        self.assertEqual(info["skins"], 0)
        self.assertEqual(info["animations"], 0)

    def test_corrupt_glb_is_rejected_before_export(self):
        with self.assertRaises(ValueError):
            module.inspect_glb(b"not a glb")

    def test_extract_only_expected_bytes_and_never_overwrite_a_different_model(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            apk = root / "source.apk"
            data, hashes = sample_archive(apk)
            output = root / "private"
            report = module.prepare(apk, output, expected_hashes=hashes)
            self.assertEqual(report["female"]["triangles"], 4)
            self.assertEqual((output / "varco-male.glb").read_bytes(), data["male"])
            (output / "varco-male.glb").write_bytes(b"someone else's model")
            with self.assertRaises(ValueError):
                module.prepare(apk, output, expected_hashes=hashes)
            self.assertEqual((output / "varco-male.glb").read_bytes(), b"someone else's model")

    def test_rejects_mismatched_provenance_without_creating_output(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            apk = root / "source.apk"
            with zipfile.ZipFile(apk, "w") as archive:
                archive.writestr("assets/models/provenance.json", '{"male":{"file":"varco-male.glb","sha256":"wrong"}}')
                archive.writestr("assets/models/varco-male.glb", minimal_glb())
            out = root / "private"
            with self.assertRaises(ValueError):
                module.prepare(apk, out)
            self.assertFalse(out.exists())

    def test_non_object_provenance_is_reported_as_invalid(self):
        with tempfile.TemporaryDirectory() as temp:
            apk = Path(temp) / "source.apk"
            data, hashes = sample_archive(apk)
            malformed = Path(temp) / "malformed.apk"
            with zipfile.ZipFile(malformed, "w") as archive:
                archive.writestr("assets/models/provenance.json", "[]")
                for sex, contents in data.items():
                    archive.writestr(f"assets/models/varco-{sex}.glb", contents)
            with self.assertRaises(ValueError):
                module.prepare(malformed, Path(temp) / "out", expected_hashes=hashes)

    def test_stale_second_temp_does_not_partially_publish_first_model(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            apk = root / "source.apk"
            _, hashes = sample_archive(apk)
            out = root / "private"
            out.mkdir()
            (out / "varco-female.glb.tmp").write_text("unfinished")
            with self.assertRaises(ValueError):
                module.prepare(apk, out, expected_hashes=hashes)
            self.assertFalse((out / "varco-male.glb").exists())

    @unittest.skipUnless(os.environ.get("BIGIMONG_TEST_APK"), "Provide a private APK to check real models")
    def test_original_models_from_uploaded_apk(self):
        with tempfile.TemporaryDirectory() as temp:
            report = module.prepare(Path(os.environ["BIGIMONG_TEST_APK"]), Path(temp) / "varco")
            self.assertEqual(report["male"]["triangles"], 500000)
            self.assertEqual(report["female"]["triangles"], 25000)
            self.assertEqual(report["male"]["skins"], 0)


if __name__ == "__main__":
    unittest.main()
