#!/usr/bin/env python3
"""Verify and privately extract the original VARCO display meshes from an APK."""

import argparse
import hashlib
import json
import os
import struct
import zipfile
from pathlib import Path


ORIGINAL_SHA256 = {
    "male": "da89aab88f30417ba4040e1fa48e74c20e5fd3d739e727e65fe527271e9b1b35",
    "female": "7af702802ee9da3ac3dfe0d46ca2dc28eef93aa677eaa68f4a5e4313fe9f24bf",
}
DEFAULT_OUTPUT = Path(__file__).resolve().parents[1] / "unreal/Bigimong/PrivateAssets/Varco"
MAX_MODEL_BYTES = 90 * 1024 * 1024


def inspect_glb(data: bytes) -> dict:
    if len(data) < 20:
        raise ValueError("Truncated GLB header")
    magic, version, length = struct.unpack_from("<III", data)
    if magic != 0x46546C67 or version != 2 or length != len(data):
        raise ValueError("Expected a complete GLB v2 file")
    json_length, chunk_type = struct.unpack_from("<II", data, 12)
    if chunk_type != 0x4E4F534A or json_length > len(data) - 20:
        raise ValueError("Invalid GLB JSON chunk")
    try:
        model = json.loads(data[20:20 + json_length])
        accessors = model["accessors"]
        meshes = model["meshes"]
        triangles = 0
        morph_targets = 0
        for mesh in meshes:
            for primitive in mesh["primitives"]:
                if primitive.get("mode", 4) != 4:
                    raise ValueError("Only triangulated GLB meshes are supported")
                indices = primitive.get("indices")
                position = primitive.get("attributes", {}).get("POSITION")
                count = accessors[indices if indices is not None else position]["count"]
                if not isinstance(count, int) or count < 0 or count % 3:
                    raise ValueError("Invalid triangle index count")
                triangles += count // 3
                morph_targets += len(primitive.get("targets", []))
    except (KeyError, IndexError, TypeError, json.JSONDecodeError) as error:
        raise ValueError("Invalid GLB mesh description") from error
    if triangles < 1:
        raise ValueError("No triangles found in the model")
    return {
        "triangles": triangles,
        "skins": len(model.get("skins", [])),
        "animations": len(model.get("animations", [])),
        "morphTargets": morph_targets,
        "materials": len(model.get("materials", [])),
        "sha256": hashlib.sha256(data).hexdigest(),
    }


def prepare(apk: Path, output: Path = DEFAULT_OUTPUT, *, expected_hashes=None) -> dict:
    """Fail before writing if either model or an existing destination fails validation."""
    hashes = ORIGINAL_SHA256 if expected_hashes is None else expected_hashes
    apk, output = Path(apk), Path(output)
    if output.is_symlink():
        raise ValueError("Output cannot be a symlink")
    models = {}
    report = {}
    with zipfile.ZipFile(apk) as archive:
        required = ["assets/models/provenance.json"] + [
            f"assets/models/varco-{sex}.glb" for sex in ("male", "female")
        ]
        members = {name: [member for member in archive.infolist() if member.filename == name]
                   for name in required}
        for name, matches in members.items():
            if len(matches) != 1 or matches[0].file_size > MAX_MODEL_BYTES:
                raise ValueError(f"Missing, duplicated or oversize APK member: {name}")
        if members[required[0]][0].file_size > 4096:
            raise ValueError("Oversize provenance record")
        try:
            provenance = json.loads(archive.read(required[0]))
        except (UnicodeDecodeError, json.JSONDecodeError) as error:
            raise ValueError("Invalid provenance record") from error
        if not isinstance(provenance, dict) or any(
                not isinstance(provenance.get(sex), dict) for sex in ("male", "female")):
            raise ValueError("Invalid provenance record: expected male and female objects")
        for sex in ("male", "female"):
            name = f"varco-{sex}.glb"
            data = archive.read(f"assets/models/{name}")
            info = inspect_glb(data)
            if (info["sha256"] != hashes[sex] or
                    provenance.get(sex, {}).get("sha256") != hashes[sex] or
                    provenance[sex].get("file") != name):
                raise ValueError(f"Original VARCO {sex} SHA-256/provenance mismatch")
            destination = output / name
            if destination.is_symlink() or (destination.exists() and destination.read_bytes() != data):
                raise ValueError(f"Refusing to overwrite another model: {destination}")
            temporary = destination.with_suffix(".glb.tmp")
            if temporary.exists() or temporary.is_symlink():
                raise ValueError(f"Temporary file already exists: {temporary}")
            models[sex] = data
            report[sex] = info

    output.mkdir(parents=True, exist_ok=True)
    for sex, data in models.items():
        destination = output / f"varco-{sex}.glb"
        if destination.exists():
            continue
        temporary = destination.with_suffix(".glb.tmp")
        if temporary.exists() or temporary.is_symlink():
            raise ValueError(f"Temporary file already exists: {temporary}")
        try:
            with temporary.open("xb") as handle:
                handle.write(data)
            os.replace(temporary, destination)
        finally:
            temporary.unlink(missing_ok=True)
    return report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--apk", type=Path, required=True, help="An APK with original VARCO GLBs")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT)
    args = parser.parse_args()
    print(json.dumps(prepare(args.apk, args.output), ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
