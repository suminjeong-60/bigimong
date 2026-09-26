#!/usr/bin/env python3
"""Verify and privately extract the modular male avatar GLBs from the user's APK."""

import argparse
import hashlib
import json
import os
import struct
import zipfile
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
DEFAULT_OUTPUT = ROOT / "unreal/Bigimong/PrivateAssets/Varco/MaleParts"
EXPECTED = json.loads((ROOT / "scripts/male-parts-sha256.json").read_text())
PARTS = ["male-base.glb"] + [
    f"male-{kind}-{number:02d}.glb"
    for kind in ("eye", "hair") for number in range(15)
]
MAX_PART_BYTES = 90 * 1024 * 1024


def describe_glb(data, *, base=False):
    if len(data) < 20 or struct.unpack_from("<III", data) != (0x46546C67, 2, len(data)):
        raise ValueError("Expected a complete GLB v2 file")
    size, kind = struct.unpack_from("<II", data, 12)
    if kind != 0x4E4F534A or size > len(data) - 20:
        raise ValueError("Missing GLB JSON")
    try:
        document = json.loads(data[20:20 + size])
        meshes = document["meshes"]
        accessors = document["accessors"]
        triangles = 0
        morphs = 0
        morph_names = []
        for mesh in meshes:
            for primitive in mesh["primitives"]:
                if primitive.get("mode", 4) != 4:
                    raise ValueError("Only triangles are supported")
                position = primitive["attributes"]["POSITION"]
                count = accessors[primitive.get("indices", position)]["count"]
                if not isinstance(count, int) or count <= 0 or count % 3:
                    raise ValueError("Invalid GLB triangles")
                triangles += count // 3
                morphs = max(morphs, len(primitive.get("targets", [])))
            if morphs:
                morph_names = mesh.get("extras", {}).get("targetNames", [])
        if base and (morphs != 15 or morph_names != [f"face_{i:02d}" for i in range(15)]):
            raise ValueError("Male base does not contain the 15 face shapes")
        if not base and morphs:
            raise ValueError("Eye and hair parts must be independently selectable meshes")
    except (KeyError, IndexError, TypeError, json.JSONDecodeError) as error:
        raise ValueError("Invalid GLB mesh description") from error
    return {"triangles": triangles, "morphs": morphs, "sha256": hashlib.sha256(data).hexdigest()}


def prepare(apk: Path, output: Path = DEFAULT_OUTPUT, *, expected_hashes=None):
    expected = EXPECTED if expected_hashes is None else expected_hashes
    if set(expected) != set(PARTS):
        raise ValueError("Expected exactly the 31 male avatar parts")
    apk, output = Path(apk), Path(output)
    if output.is_symlink():
        raise ValueError("Output cannot be a symlink")

    parts = {}
    report = {}
    with zipfile.ZipFile(apk) as archive:
        for name in PARTS:
            member_name = f"assets/parts/{name}"
            matches = [entry for entry in archive.infolist() if entry.filename == member_name]
            if len(matches) != 1 or matches[0].file_size > MAX_PART_BYTES:
                raise ValueError(f"Missing, duplicated or oversize APK member: {member_name}")
            data = archive.read(matches[0])
            detail = describe_glb(data, base=name == "male-base.glb")
            if detail["sha256"] != expected[name]:
                raise ValueError(f"Unexpected male avatar part: {name}")
            destination = output / name
            if destination.is_symlink() or (destination.exists() and destination.read_bytes() != data):
                raise ValueError(f"Refusing to overwrite another asset: {destination}")
            if destination.with_suffix(".glb.tmp").exists():
                raise ValueError(f"Unfinished previous extraction: {destination}")
            parts[name] = data
            report[name] = detail

    output.mkdir(parents=True, exist_ok=True)
    for name, data in parts.items():
        destination = output / name
        if destination.exists():
            continue
        temporary = destination.with_suffix(".glb.tmp")
        try:
            with temporary.open("xb") as handle:
                handle.write(data)
            os.replace(temporary, destination)
        finally:
            temporary.unlink(missing_ok=True)
    return report


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apk", type=Path, required=True)
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    print(json.dumps(prepare(args.apk, args.output), ensure_ascii=False, indent=2))
