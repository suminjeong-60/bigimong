"""Import-ready GLBs must keep the original shapes and prevent partial staging."""

import hashlib
import importlib.util
import json
import math
import struct
import tempfile
import unittest
from pathlib import Path


SCRIPT = Path(__file__).resolve().parents[1] / "scripts/stage-unreal-male.py"
spec = importlib.util.spec_from_file_location("stage_unreal_male", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def pack_glb(document, binary):
    document["buffers"] = [{"byteLength": len(binary)}]
    data = json.dumps(document, separators=(",", ":")).encode()
    data += b" " * (-len(data) % 4)
    binary += b"\0" * (-len(binary) % 4)
    return (struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(data) + 8 + len(binary))
            + struct.pack("<II", len(data), 0x4E4F534A) + data
            + struct.pack("<II", len(binary), 0x004E4942) + binary)


def unpack_glb(path):
    data = path.read_bytes()
    length = struct.unpack_from("<I", data, 12)[0]
    doc = json.loads(data[20:20 + length])
    offset = 20 + length
    binary_length = struct.unpack_from("<I", data, offset)[0]
    return doc, data[offset + 8:offset + 8 + binary_length]


def source_glb(base=False):
    binary = bytearray()
    views = []
    accessors = []
    meshes = []
    nodes = []
    for index in range(2):
        vertices = [(1.0, 2.0, 3.0), (2.0, 2.0, 3.0), (1.0, 3.0, 3.0)]
        attrs = {}
        for attribute, values in (
            ("POSITION", vertices),
            ("NORMAL", [(1 / math.sqrt(2), 0.0, 1 / math.sqrt(2))] * 3),
        ):
            views.append({"buffer": 0, "byteOffset": len(binary), "byteLength": 36,
                          "target": 34962})
            for row in values:
                binary.extend(struct.pack("<3f", *row))
            attrs[attribute] = len(accessors)
            accessor = {"bufferView": len(views) - 1, "componentType": 5126,
                        "count": 3, "type": "VEC3"}
            if attribute == "POSITION":
                accessor.update({"min": [1, 2, 3], "max": [2, 3, 3]})
            accessors.append(accessor)
        primitive = {"attributes": attrs, "material": index}
        if base and index == 1:
            primitive["targets"] = [{"POSITION": attrs["POSITION"]} for _ in range(15)]
        mesh = {"name": "Body" if base and index == 0 else "Face" if base else "Eye piece",
                "primitives": [primitive]}
        if base and index == 1:
            mesh["extras"] = {"targetNames": [f"face_{i:02d}" for i in range(15)]}
        meshes.append(mesh)
        node = {"name": mesh["name"], "mesh": index}
        if not base and index == 1:
            node.update({"translation": [10, 20, 30], "scale": [2, 1, .5]})
        nodes.append(node)
    document = {"asset": {"version": "2.0"}, "scene": 0,
                "scenes": [{"nodes": [0, 1]}], "nodes": nodes, "meshes": meshes,
                "accessors": accessors, "bufferViews": views,
                "materials": [{"name": "Skin"}, {"name": "Iris"}]}
    return pack_glb(document, bytes(binary))


class StageMaleAssetsTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.source = Path(self.temp.name) / "parts"
        self.output = Path(self.temp.name) / "UnrealImport"
        self.source.mkdir()
        self.hashes = {}
        for name in ["male-base.glb"] + [
            f"male-{kind}-{index:02d}.glb"
            for kind in ("eye", "hair") for index in range(15)
        ]:
            data = source_glb(base=name == "male-base.glb")
            (self.source / name).write_bytes(data)
            self.hashes[name] = hashlib.sha256(data).hexdigest()

    def stage(self):
        return module.stage(self.source, self.output, expected_hashes=self.hashes)

    def test_splits_body_and_morph_face_without_changing_originals(self):
        original = (self.source / "male-base.glb").read_bytes()
        report = self.stage()
        self.assertEqual(len(report), 32)
        body, _ = unpack_glb(self.output / "SM_MaleBody.glb")
        face, _ = unpack_glb(self.output / "SK_MaleFace.glb")
        self.assertEqual(len(body["meshes"]), 1)
        self.assertEqual(len(face["meshes"]), 1)
        self.assertEqual(body["nodes"][0]["name"], "SM_MaleBody")
        self.assertEqual(face["meshes"][0]["extras"]["targetNames"],
                         [f"face_{i:02d}" for i in range(15)])
        self.assertEqual(len(face["meshes"][0]["primitives"][0]["targets"]), 15)
        self.assertEqual(len(face["materials"]), 2)
        self.assertEqual((self.source / "male-base.glb").read_bytes(), original)

    def test_combines_eye_pieces_with_baked_position_and_normal(self):
        self.stage()
        doc, binary = unpack_glb(self.output / "SM_MaleEye_00.glb")
        self.assertEqual(len(doc["nodes"]), 1)
        self.assertEqual(len(doc["meshes"]), 1)
        self.assertEqual(len(doc["meshes"][0]["primitives"]), 2)
        self.assertEqual([p["material"] for p in doc["meshes"][0]["primitives"]], [0, 1])
        self.assertNotIn("scale", doc["nodes"][0])
        transformed = doc["meshes"][0]["primitives"][1]["attributes"]

        def first_vector(attribute):
            accessor = doc["accessors"][transformed[attribute]]
            view = doc["bufferViews"][accessor["bufferView"]]
            return struct.unpack_from("<3f", binary, view["byteOffset"] + accessor.get("byteOffset", 0))

        self.assertEqual(first_vector("POSITION"), (12, 22, 31.5))
        x, y, z = first_vector("NORMAL")
        self.assertAlmostEqual(x, 1 / math.sqrt(17), places=5)
        self.assertAlmostEqual(y, 0, places=5)
        self.assertAlmostEqual(z, 4 / math.sqrt(17), places=5)

    def test_wrong_hash_or_foreign_output_refuses_to_write_any_assets(self):
        (self.source / "male-hair-14.glb").write_bytes(b"tampered")
        with self.assertRaises(ValueError):
            self.stage()
        self.assertFalse(self.output.exists())

        (self.source / "male-hair-14.glb").write_bytes(source_glb())
        self.output.mkdir()
        foreign = self.output / "SM_MaleBody.glb"
        foreign.write_bytes(b"somebody else's model")
        with self.assertRaises(ValueError):
            self.stage()
        self.assertEqual(foreign.read_bytes(), b"somebody else's model")

    def test_repeated_staging_is_idempotent(self):
        first = self.stage()
        self.assertEqual(self.stage(), first)


if __name__ == "__main__":
    unittest.main()
