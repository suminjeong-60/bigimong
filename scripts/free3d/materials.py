"""Single-atlas material helpers for the free Blender character pipeline."""

from __future__ import annotations

import binascii
import struct
import zlib
from pathlib import Path
from typing import Any


def _rgba(hex_color: str) -> tuple[int, int, int, int]:
    value = hex_color.lstrip("#")
    if len(value) != 6:
        raise ValueError(f"expected #RRGGBB color, received {hex_color}")
    return int(value[0:2], 16), int(value[2:4], 16), int(value[4:6], 16), 255


def _png_chunk(kind: bytes, payload: bytes) -> bytes:
    return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", binascii.crc32(kind + payload) & 0xFFFFFFFF)


def write_palette_atlas(path: Path, palette: dict[str, str], size: int = 1024) -> list[str]:
    """Write a deterministic RGBA PNG with one vertical region per palette key."""
    if size != 1024:
        raise ValueError("Bigimong character atlases must be 1024x1024")
    keys = list(palette)
    if not keys:
        raise ValueError("palette cannot be empty")
    rows = bytearray()
    colors = [_rgba(palette[key]) for key in keys]
    for _y in range(size):
        rows.append(0)
        for x_value in range(size):
            region = min(len(colors) - 1, x_value * len(colors) // size)
            rows.extend(colors[region])
    signature = b"\x89PNG\r\n\x1a\n"
    header = struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(signature + _png_chunk(b"IHDR", header) + _png_chunk(b"IDAT", zlib.compress(bytes(rows), 9)) + _png_chunk(b"IEND", b""))
    return keys


def create_atlas_material(name: str, palette: dict[str, str], atlas_path: Path) -> tuple[Any, Any, list[str]]:
    import bpy  # type: ignore

    keys = write_palette_atlas(atlas_path, palette)
    image = bpy.data.images.load(str(atlas_path), check_existing=False)
    image.name = f"{name}_Atlas"
    image.pack()
    material = bpy.data.materials.new(f"{name}_ToyPBR")
    material.use_nodes = True
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    texture = nodes.new("ShaderNodeTexImage")
    texture.image = image
    texture.interpolation = "Closest"
    links.new(texture.outputs["Color"], shader.inputs["Base Color"])
    if "Roughness" in shader.inputs:
        shader.inputs["Roughness"].default_value = 0.48
    if "Specular IOR Level" in shader.inputs:
        shader.inputs["Specular IOR Level"].default_value = 0.3
    elif "Specular" in shader.inputs:
        shader.inputs["Specular"].default_value = 0.3
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    return material, image, keys


def assign_palette_region(obj: Any, material: Any, region_index: int, region_count: int) -> None:
    if getattr(obj, "type", None) != "MESH":
        return
    if material.name not in obj.data.materials:
        obj.data.materials.append(material)
    uv_layer = obj.data.uv_layers.get("BigimongAtlas") or obj.data.uv_layers.new(name="BigimongAtlas")
    u_value = (float(region_index) + 0.5) / float(region_count)
    for loop in uv_layer.data:
        loop.uv = (u_value, 0.5)


def palette_index(keys: list[str], preferred: str, fallback: int = 0) -> int:
    return keys.index(preferred) if preferred in keys else min(fallback, len(keys) - 1)
