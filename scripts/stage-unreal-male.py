#!/usr/bin/env python3
"""Stage verified private VARCO male parts as 32 single-mesh GLBs for Unreal import.

This does not create .uasset files: Interchange import and rendering need Unreal Editor.
"""

import argparse
import copy
import hashlib
import json
import math
import os
import runpy
import struct
import tempfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_SOURCE = ROOT / "unreal/Bigimong/PrivateAssets/Varco/MaleParts"
DEFAULT_OUTPUT = ROOT / "unreal/Bigimong/PrivateAssets/Varco/UnrealImport"
VERIFIER = runpy.run_path(str(ROOT / "scripts/prepare-unreal-male.py"))
EXPECTED = VERIFIER["EXPECTED"]
PARTS = VERIFIER["PARTS"]
DESCRIBE = VERIFIER["describe_glb"]
MAX_PART_BYTES = VERIFIER["MAX_PART_BYTES"]


def read_glb(data):
    if len(data) < 28 or struct.unpack_from("<III", data) != (0x46546C67, 2, len(data)):
        raise ValueError("Expected a complete GLB v2")
    json_length, json_type = struct.unpack_from("<II", data, 12)
    binary_header = 20 + json_length
    if json_type != 0x4E4F534A or binary_header + 8 > len(data):
        raise ValueError("Missing GLB JSON or BIN chunk")
    bin_length, bin_type = struct.unpack_from("<II", data, binary_header)
    if bin_type != 0x004E4942 or binary_header + 8 + bin_length != len(data):
        raise ValueError("Invalid GLB BIN chunk")
    document = json.loads(data[20:binary_header])
    if len(document.get("buffers", [])) != 1 or document["buffers"][0].get("uri"):
        raise ValueError("Expected one embedded GLB buffer")
    binary = data[binary_header + 8:]
    if document["buffers"][0]["byteLength"] > len(binary):
        raise ValueError("GLB buffer is truncated")
    if document.get("skins") or document.get("animations"):
        raise ValueError("Avatar part has an unsupported rig or animation")
    return document, binary


def write_glb(document, binary):
    document["buffers"] = [{"byteLength": len(binary)}]
    encoded = json.dumps(document, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    encoded += b" " * (-len(encoded) % 4)
    binary += b"\0" * (-len(binary) % 4)
    return (struct.pack("<III", 0x46546C67, 2, 28 + len(encoded) + len(binary))
            + struct.pack("<II", len(encoded), 0x4E4F534A) + encoded
            + struct.pack("<II", len(binary), 0x004E4942) + binary)


def one_mesh(document, binary, mesh_index, name):
    result = copy.deepcopy(document)
    nodes = [node for node in result["nodes"] if node.get("mesh") == mesh_index]
    if len(nodes) != 1 or any(k in nodes[0] for k in ("matrix", "rotation", "children")):
        raise ValueError("Expected one unambiguous mesh node")
    node = nodes[0]
    result["meshes"] = [result["meshes"][mesh_index]]
    result["meshes"][0]["name"] = name
    result["nodes"] = [{**node, "name": name, "mesh": 0}]
    result["scenes"] = [{"nodes": [0]}]
    result["scene"] = 0
    return write_glb(result, binary)


def transformed_accessor(document, binary, index, translation, scale, normal=False):
    source = document["accessors"][index]
    view = document["bufferViews"][source["bufferView"]]
    if (source.get("componentType") != 5126 or source.get("type") != "VEC3"
            or source.get("sparse") or view.get("buffer") != 0):
        raise ValueError("Only dense floating point VEC3 geometry is supported")
    offset = view.get("byteOffset", 0) + source.get("byteOffset", 0)
    stride = view.get("byteStride", 12)
    if stride < 12 or offset + (source["count"] - 1) * stride + 12 > len(binary):
        raise ValueError("Geometry accessor exceeds its buffer")
    values = []
    for i in range(source["count"]):
        vector = struct.unpack_from("<3f", binary, offset + i * stride)
        if normal:
            v = [component / component_scale for component, component_scale in zip(vector, scale)]
            length = math.sqrt(sum(component * component for component in v))
            if not length:
                raise ValueError("Cannot transform a zero-length normal")
            v = [component / length for component in v]
        else:
            v = [component * component_scale + shift for component, component_scale, shift
                 in zip(vector, scale, translation)]
        values.append(v)
    transformed = b"".join(struct.pack("<3f", *v) for v in values)
    binary += b"\0" * (-len(binary) % 4)
    document["bufferViews"].append({"buffer": 0, "byteOffset": len(binary),
                                    "byteLength": len(transformed), "target": 34962})
    accessor = {k: v for k, v in source.items() if k not in ("bufferView", "byteOffset", "min", "max")}
    accessor["bufferView"] = len(document["bufferViews"]) - 1
    if not normal:
        accessor["min"] = [min(v[axis] for v in values) for axis in range(3)]
        accessor["max"] = [max(v[axis] for v in values) for axis in range(3)]
    document["accessors"].append(accessor)
    return len(document["accessors"]) - 1, binary + transformed


def combine_pieces(document, binary, name):
    result = copy.deepcopy(document)
    primitives = []
    for node in result["nodes"]:
        if "mesh" not in node or any(k in node for k in ("rotation", "matrix", "children")):
            raise ValueError("Each eye/hair part needs a mesh and translation/scale only")
        translation = node.get("translation", [0, 0, 0])
        scale = node.get("scale", [1, 1, 1])
        if len(translation) != 3 or len(scale) != 3 or any(s <= 0 for s in scale):
            raise ValueError("Unsupported mesh transform")
        for primitive in result["meshes"][node["mesh"]]["primitives"]:
            if primitive.get("targets") or "POSITION" not in primitive["attributes"]:
                raise ValueError("Eye and hair pieces must be static triangle meshes")
            part = copy.deepcopy(primitive)
            if translation != [0, 0, 0] or scale != [1, 1, 1]:
                for key in ("POSITION", "NORMAL"):
                    if key in part["attributes"]:
                        part["attributes"][key], binary = transformed_accessor(
                            result, binary, part["attributes"][key], translation, scale,
                            normal=key == "NORMAL")
            primitives.append(part)
    result["meshes"] = [{"name": name, "primitives": primitives}]
    result["nodes"] = [{"name": name, "mesh": 0}]
    result["scenes"] = [{"nodes": [0]}]
    result["scene"] = 0
    return write_glb(result, binary)


def stage(source=DEFAULT_SOURCE, output=DEFAULT_OUTPUT, *, expected_hashes=None):
    source, output = Path(source), Path(output)
    hashes = EXPECTED if expected_hashes is None else expected_hashes
    if set(hashes) != set(PARTS):
        raise ValueError("Expected hashes for all 31 male parts")
    if output.is_symlink():
        raise ValueError("Output directory cannot be a symlink")

    # Verify all originals before creating a destination or replacing any previous result.
    models = {}
    for name in PARTS:
        path = source / name
        if path.is_symlink() or not path.is_file() or path.stat().st_size > MAX_PART_BYTES:
            raise ValueError(f"Missing, linked or oversize avatar part: {name}")
        data = path.read_bytes()
        details = DESCRIBE(data, base=name == "male-base.glb")
        if details["sha256"] != hashes[name]:
            raise ValueError(f"Unexpected avatar part: {name}")
        models[name] = read_glb(data)

    base, base_binary = models["male-base.glb"]
    if len(base["meshes"]) != 2 or len(base["nodes"]) != 2:
        raise ValueError("Male base must contain only body and face meshes")
    face = [i for i, mesh in enumerate(base["meshes"])
            if mesh.get("extras", {}).get("targetNames") == [f"face_{n:02d}" for n in range(15)]]
    if len(face) != 1:
        raise ValueError("Male base must contain 15 unambiguous face morph targets")
    assets = {"SM_MaleBody.glb": one_mesh(base, base_binary, 1 - face[0], "SM_MaleBody"),
              "SK_MaleFace.glb": one_mesh(base, base_binary, face[0], "SK_MaleFace")}
    for kind, label in (("eye", "Eye"), ("hair", "Hair")):
        for i in range(15):
            name = f"SM_Male{label}_{i:02d}"
            document, binary = models[f"male-{kind}-{i:02d}.glb"]
            assets[f"{name}.glb"] = combine_pieces(document, binary, name)

    report = {name: hashlib.sha256(data).hexdigest() for name, data in assets.items()}
    if output.exists():
        if not output.is_dir() or {p.name for p in output.iterdir()} != set(assets):
            raise ValueError(f"Refusing to replace another asset directory: {output}")
        if any((output / name).is_symlink() or (output / name).read_bytes() != data
               for name, data in assets.items()):
            raise ValueError(f"Refusing to overwrite another asset: {output}")
        return report

    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix=".unreal-import-", dir=output.parent) as temp:
        staging = Path(temp) / "UnrealImport"
        staging.mkdir()
        for name, data in assets.items():
            (staging / name).write_bytes(data)
        os.replace(staging, output)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    try:
        result = stage(args.source, args.output)
    except (OSError, ValueError, KeyError, IndexError, TypeError) as error:
        parser.exit(1, f"Staging failed: {error}\n")
    print(f"Verified and staged {len(result)} GLBs in {args.output}")
