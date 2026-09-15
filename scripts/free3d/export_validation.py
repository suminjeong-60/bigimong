"""Portable surface-contract inspection for exported binary glTF assets."""

from __future__ import annotations

import json
import struct
import zlib
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


def _glb_payload(path: Path) -> tuple[dict[str, Any], bytes, int]:
    payload = path.read_bytes()
    if len(payload) < 20:
        raise ValueError("GLB header is truncated")
    magic, version, total_length = struct.unpack("<4sII", payload[:12])
    if magic != b"glTF" or version != 2 or total_length != len(payload):
        raise ValueError("GLB header is invalid")
    offset = 12
    document = None
    binary = b""
    while offset + 8 <= len(payload):
        chunk_length, chunk_type = struct.unpack("<I4s", payload[offset:offset + 8])
        start = offset + 8
        end = start + chunk_length
        if end > len(payload):
            raise ValueError("GLB chunk is truncated")
        chunk = payload[start:end]
        if chunk_type == b"JSON":
            document = json.loads(chunk.rstrip(b" \t\r\n\x00").decode("utf-8"))
        elif chunk_type == b"BIN\x00":
            binary = chunk
        offset = end
    if document is None:
        raise ValueError("GLB JSON chunk is missing")
    buffers = document.get("buffers") if isinstance(document.get("buffers"), list) else []
    buffer = buffers[0] if len(buffers) == 1 and isinstance(buffers[0], dict) else {}
    declared_length = buffer.get("byteLength")
    if (len(buffers) != 1 or buffer.get("uri") is not None
            or not isinstance(declared_length, int) or declared_length <= 0
            or declared_length > len(binary) or len(binary) - declared_length > 3):
        raise ValueError("GLB buffer byteLength is invalid")
    return document, binary, declared_length


def _texture_image(
    document: dict[str, Any],
    binary: bytes,
    declared_length: int,
    reference: Any,
) -> tuple[bytes, int] | None:
    if not isinstance(reference, dict) or not isinstance(reference.get("index"), int):
        return None
    textures = document.get("textures") if isinstance(document.get("textures"), list) else []
    images = document.get("images") if isinstance(document.get("images"), list) else []
    views = document.get("bufferViews") if isinstance(document.get("bufferViews"), list) else []
    texture_index = reference["index"]
    if texture_index < 0 or texture_index >= len(textures):
        return None
    texture = textures[texture_index]
    if not isinstance(texture, dict):
        return None
    source = texture.get("source")
    if not isinstance(source, int) or source < 0 or source >= len(images):
        return None
    image = images[source]
    if not isinstance(image, dict):
        return None
    view_index = image.get("bufferView")
    if image.get("mimeType") != "image/png" or not isinstance(view_index, int) or view_index < 0 or view_index >= len(views):
        return None
    view = views[view_index]
    if not isinstance(view, dict):
        return None
    if view.get("buffer", 0) != 0:
        return None
    start = int(view.get("byteOffset", 0))
    length = view.get("byteLength")
    if not isinstance(length, int) or start < 0 or length <= 0 or start + length > declared_length:
        return None
    return binary[start:start + length], source


def _paeth(left: int, above: int, upper_left: int) -> int:
    prediction = left + above - upper_left
    left_distance = abs(prediction - left)
    above_distance = abs(prediction - above)
    corner_distance = abs(prediction - upper_left)
    if left_distance <= above_distance and left_distance <= corner_distance:
        return left
    return above if above_distance <= corner_distance else upper_left


def _png_green_range(payload: bytes) -> tuple[int, int]:
    if not payload.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValueError("roughness image is not PNG")
    offset = 8
    width = height = channels = None
    compressed = bytearray()
    while offset + 12 <= len(payload):
        length = struct.unpack(">I", payload[offset:offset + 4])[0]
        kind = payload[offset + 4:offset + 8]
        chunk = payload[offset + 8:offset + 8 + length]
        if offset + 12 + length > len(payload):
            raise ValueError("roughness PNG chunk is truncated")
        if kind == b"IHDR":
            width, height, bit_depth, color_type, compression, filtering, interlace = struct.unpack(">IIBBBBB", chunk)
            if bit_depth != 8 or color_type not in (2, 6) or compression != 0 or filtering != 0 or interlace != 0:
                raise ValueError("roughness PNG format is unsupported")
            channels = 3 if color_type == 2 else 4
        elif kind == b"IDAT":
            compressed.extend(chunk)
        offset += 12 + length
        if kind == b"IEND":
            break
    if not width or not height or channels is None or not compressed:
        raise ValueError("roughness PNG payload is incomplete")

    raw = zlib.decompress(bytes(compressed))
    stride = width * channels
    expected = height * (stride + 1)
    if len(raw) != expected:
        raise ValueError("roughness PNG scanline size mismatch")
    previous = bytearray(stride)
    green_min, green_max = 255, 0
    cursor = 0
    for _row in range(height):
        filter_type = raw[cursor]
        cursor += 1
        filtered = raw[cursor:cursor + stride]
        cursor += stride
        scanline = bytearray(stride)
        for index, value in enumerate(filtered):
            left = scanline[index - channels] if index >= channels else 0
            above = previous[index]
            upper_left = previous[index - channels] if index >= channels else 0
            if filter_type == 0:
                predictor = 0
            elif filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = above
            elif filter_type == 3:
                predictor = (left + above) // 2
            elif filter_type == 4:
                predictor = _paeth(left, above, upper_left)
            else:
                raise ValueError("roughness PNG uses an invalid filter")
            scanline[index] = (value + predictor) & 0xFF
        for index in range(1, stride, channels):
            green_min = min(green_min, scanline[index])
            green_max = max(green_max, scanline[index])
        previous = scanline
    return green_min, green_max


def inspect_glb_surface(path: Path) -> dict[str, Any]:
    """Require one material plus portable glTF base-color and roughness textures."""
    errors: list[str] = []
    try:
        document, binary, declared_length = _glb_payload(path)
    except (OSError, ValueError, UnicodeDecodeError, json.JSONDecodeError) as error:
        return {
            "valid": False,
            "materialCount": 0,
            "textureCount": 0,
            "imageCount": 0,
            "baseColorTexture": False,
            "roughnessTexture": False,
            "repackedRoughness": False,
            "roughnessGreenRange": [],
            "errors": [str(error)],
        }

    materials = document.get("materials") if isinstance(document.get("materials"), list) else []
    textures = document.get("textures") if isinstance(document.get("textures"), list) else []
    images = document.get("images") if isinstance(document.get("images"), list) else []
    pbr = materials[0].get("pbrMetallicRoughness", {}) if len(materials) == 1 else {}
    base_reference = pbr.get("baseColorTexture")
    roughness_reference = pbr.get("metallicRoughnessTexture")
    base_image = _texture_image(document, binary, declared_length, base_reference)
    roughness_image = _texture_image(document, binary, declared_length, roughness_reference)
    base_color_texture = base_image is not None
    roughness_texture = roughness_image is not None
    repacked_roughness = bool(
        base_image and roughness_image
        and base_image[1] != roughness_image[1]
        and base_image[0] != roughness_image[0]
    )
    roughness_range: list[int] = []

    if len(materials) != 1:
        errors.append("GLB must contain exactly one material")
    if not isinstance(base_reference, dict):
        errors.append("base-color texture is missing")
    elif not base_color_texture:
        errors.append("base-color texture reference is invalid")
    if not isinstance(roughness_reference, dict):
        errors.append("standard roughness texture is missing")
    elif not roughness_texture:
        errors.append("roughness texture reference is invalid")
    if len(textures) != 2:
        errors.append("GLB texture count must equal two")
    if len(images) != 2:
        errors.append("GLB image count must equal two")
    if base_image and roughness_image and not repacked_roughness:
        if base_image[0] == roughness_image[0]:
            errors.append("roughness texture payload duplicates base color")
        else:
            errors.append("roughness texture must be repacked separately from base color")
    if roughness_image is not None:
        try:
            roughness_range = list(_png_green_range(roughness_image[0]))
            if roughness_range[0] == roughness_range[1]:
                errors.append("roughness green channel has no semantic variation")
        except (ValueError, zlib.error) as error:
            errors.append(str(error))

    return {
        "valid": not errors,
        "materialCount": len(materials),
        "textureCount": len(textures),
        "imageCount": len(images),
        "baseColorTexture": base_color_texture,
        "roughnessTexture": roughness_texture,
        "repackedRoughness": repacked_roughness,
        "roughnessGreenRange": roughness_range,
        "errors": errors,
    }
