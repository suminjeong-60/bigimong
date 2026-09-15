"""Portable surface-contract inspection for exported binary glTF assets."""

from __future__ import annotations

import json
import struct
from pathlib import Path
from typing import Any


SURFACE_IMPORT_CONTRACT = {
    "sourceTexture": "single_1k_rgba_atlas",
    "roughnessSource": "atlas_alpha",
    "glbRoughness": "metallicRoughnessTexture.green",
    "fbxUnityImport": "invert_atlas_alpha_into_mask_smoothness",
}


def surface_import_contract() -> dict[str, str]:
    return dict(SURFACE_IMPORT_CONTRACT)


def _glb_json(path: Path) -> dict[str, Any]:
    payload = path.read_bytes()
    if len(payload) < 20:
        raise ValueError("GLB header is truncated")
    magic, version, total_length = struct.unpack("<4sII", payload[:12])
    if magic != b"glTF" or version != 2 or total_length != len(payload):
        raise ValueError("GLB header is invalid")
    chunk_length, chunk_type = struct.unpack("<I4s", payload[12:20])
    if chunk_type != b"JSON" or 20 + chunk_length > len(payload):
        raise ValueError("GLB JSON chunk is invalid")
    encoded = payload[20:20 + chunk_length].rstrip(b" \t\r\n\x00")
    return json.loads(encoded.decode("utf-8"))


def inspect_glb_surface(path: Path) -> dict[str, Any]:
    """Require one material plus portable glTF base-color and roughness textures."""
    errors: list[str] = []
    try:
        document = _glb_json(path)
    except (OSError, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
        return {
            "valid": False,
            "materialCount": 0,
            "textureCount": 0,
            "imageCount": 0,
            "baseColorTexture": False,
            "roughnessTexture": False,
            "errors": [str(error)],
        }

    materials = document.get("materials") if isinstance(document.get("materials"), list) else []
    textures = document.get("textures") if isinstance(document.get("textures"), list) else []
    images = document.get("images") if isinstance(document.get("images"), list) else []
    pbr = materials[0].get("pbrMetallicRoughness", {}) if len(materials) == 1 else {}
    base_color_texture = isinstance(pbr.get("baseColorTexture"), dict)
    roughness_texture = isinstance(pbr.get("metallicRoughnessTexture"), dict)

    if len(materials) != 1:
        errors.append("GLB must contain exactly one material")
    if not base_color_texture:
        errors.append("base-color texture is missing")
    if not roughness_texture:
        errors.append("standard roughness texture is missing")
    if len(textures) == 0 or len(textures) > 2:
        errors.append("GLB texture count must be one or two")
    if len(images) == 0 or len(images) > 2:
        errors.append("GLB image count must be one or two")

    return {
        "valid": not errors,
        "materialCount": len(materials),
        "textureCount": len(textures),
        "imageCount": len(images),
        "baseColorTexture": base_color_texture,
        "roughnessTexture": roughness_texture,
        "errors": errors,
    }
