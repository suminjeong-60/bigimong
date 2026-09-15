"""Hard mobile and delivery validation for generated Bigimong characters."""

from __future__ import annotations

from dataclasses import dataclass
from math import isfinite
import re
from typing import Any

from free3d import REQUIRED_ACTIONS
from free3d.geometry import descendants, world_bounds
from free3d.rigging import COMMON_REQUIRED_BONES, MAX_CONTROL_BONES, MAX_DEFORM_BONES


BLENDER_NUMERIC_SUFFIX = re.compile(r"\.\d{3}$")


def logical_object_name(name: str) -> str:
    """Strip only Blender's globally-unique numeric suffix from a scoped object name."""
    return BLENDER_NUMERIC_SUFFIX.sub("", name)


@dataclass
class ValidationReport:
    valid: bool
    triangle_count: int
    material_count: int
    texture_size: int
    deform_bones: int
    control_bones: int
    origin_error_m: float
    target_height_error_pct: float
    required_actions: list[str]
    required_parts: dict[str, bool]
    detail_parts: dict[str, bool]
    warnings: list[str]
    errors: list[str]


def hard_limits() -> dict[str, float | int]:
    return {
        "triangles": 30000,
        "materials": 4,
        "textureSize": 1024,
        "deformBones": MAX_DEFORM_BONES,
        "controlBones": MAX_CONTROL_BONES,
        "originErrorM": 0.001,
    }


def validate_budget_facts(facts: dict[str, float | int]) -> list[str]:
    limits = hard_limits()
    errors = []
    if facts["triangle_count"] > limits["triangles"]:
        errors.append("triangle_count exceeds 30000")
    if facts["material_count"] > limits["materials"]:
        errors.append("material_count exceeds 4")
    if facts["texture_size"] != limits["textureSize"]:
        errors.append("texture_size must equal 1024")
    if facts["deform_bones"] > limits["deformBones"]:
        errors.append("deform_bones exceeds 64")
    if facts["control_bones"] > limits["controlBones"]:
        errors.append("control_bones exceeds 16")
    if facts["origin_error_m"] > limits["originErrorM"]:
        errors.append("origin_error_m exceeds 0.001")
    return errors


def delivery_validation_contract() -> dict[str, Any]:
    invalid_errors = validate_budget_facts({
        "triangle_count": 30001,
        "material_count": 1,
        "texture_size": 1024,
        "deform_bones": 20,
        "control_bones": 4,
        "origin_error_m": 0.0,
    })
    return {
        "hardLimits": hard_limits(),
        "invalidFixture": {"valid": not invalid_errors, "errors": invalid_errors},
    }


def _triangle_count(meshes: list[Any]) -> int:
    count = 0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        count += len(obj.data.loop_triangles)
    return count


def _has_open_boundary(obj: Any) -> bool:
    if getattr(obj, "type", None) != "MESH":
        return False
    edge_faces = [0] * len(obj.data.edges)
    edge_index = {tuple(sorted(edge.vertices)): index for index, edge in enumerate(obj.data.edges)}
    for polygon in obj.data.polygons:
        vertices = polygon.vertices
        for index, first in enumerate(vertices):
            second = vertices[(index + 1) % len(vertices)]
            key = tuple(sorted((first, second)))
            if key in edge_index:
                edge_faces[edge_index[key]] += 1
    return any(count != 2 for count in edge_faces)


def validate_character(character: Any, rig: Any, job: dict[str, Any], defaults: dict[str, Any]) -> ValidationReport:
    objects = descendants(character.root)
    meshes = [obj for obj in objects if getattr(obj, "type", None) == "MESH"]
    errors: list[str] = []
    warnings: list[str] = []
    if not meshes:
        errors.append("character contains no mesh")
        bounds_min = bounds_max = (0.0, 0.0, 0.0)
    else:
        bounds_min, bounds_max = world_bounds(meshes)

    triangle_count = _triangle_count(meshes)
    unique_materials = {slot.material.name for obj in meshes for slot in obj.material_slots if slot.material}
    material_count = len(unique_materials)
    atlas = character.metadata.get("atlas")
    texture_size = int(atlas.size[0]) if atlas is not None and atlas.size else 0
    origin_error_m = abs(float(bounds_min[2]))
    height = float(bounds_max[2] - bounds_min[2])
    target = float(job["targetHeightM"])
    target_height_error_pct = abs(height - target) / target * 100.0 if target > 0 else 100.0
    facts = {
        "triangle_count": triangle_count,
        "material_count": material_count,
        "texture_size": texture_size,
        "deform_bones": len(rig.deform_bones),
        "control_bones": len(rig.control_bones),
        "origin_error_m": origin_error_m,
    }
    errors.extend(validate_budget_facts(facts))
    if target_height_error_pct > 0.5:
        errors.append("target height error exceeds 0.5 percent")
    if atlas is None or atlas.packed_file is None:
        errors.append("packed atlas is missing")
    if character.root.name != job["resourceName"]:
        errors.append("stable root name mismatch")
    if any(not isfinite(value) for obj in objects for value in (*obj.location, *obj.rotation_euler, *obj.scale)):
        errors.append("non-finite object transform")

    bone_names = set(rig.deform_bones) | set(rig.control_bones)
    for name in COMMON_REQUIRED_BONES:
        if name not in bone_names:
            errors.append(f"required bone is missing: {name}")
    if list(rig.actions) != list(REQUIRED_ACTIONS):
        errors.append("required action sequence mismatch")
    for name in REQUIRED_ACTIONS:
        action = rig.action_blocks.get(name)
        if action is None or len(action.fcurves) == 0:
            errors.append(f"required action is empty: {name}")

    for obj in character.skin_meshes:
        if getattr(obj, "type", None) != "MESH":
            continue
        if len(obj.data.vertices) and not obj.vertex_groups:
            errors.append(f"unweighted deforming mesh: {obj.name}")
        if _has_open_boundary(obj):
            errors.append(f"open skin boundary: {obj.name}")

    known_names = set(character.metadata.get("named_parts", [])) | {logical_object_name(obj.name) for obj in objects}
    required_parts = {name: name in known_names for name in job["requiredParts"]}
    for name, present in required_parts.items():
        if not present:
            errors.append(f"required part is missing: {name}")

    object_names = {logical_object_name(obj.name) for obj in objects}
    detail_parts = {name: name in object_names for name in job.get("detailParts", [])}
    for name, present in detail_parts.items():
        if not present:
            errors.append(f"required polish detail is missing: {name}")

    minimum = int(defaults["targetTrianglesMin"])
    maximum = int(defaults["targetTrianglesMax"])
    if triangle_count < minimum or triangle_count > maximum:
        warnings.append(f"triangle_count {triangle_count} is outside target range {minimum}..{maximum}")
    if job.get("inferredRear"):
        warnings.append("rear appearance is inferred from the approved single-view reference")

    return ValidationReport(
        valid=not errors,
        triangle_count=triangle_count,
        material_count=material_count,
        texture_size=texture_size,
        deform_bones=len(rig.deform_bones),
        control_bones=len(rig.control_bones),
        origin_error_m=origin_error_m,
        target_height_error_pct=target_height_error_pct,
        required_actions=list(rig.actions),
        required_parts=required_parts,
        detail_parts=detail_parts,
        warnings=warnings,
        errors=errors,
    )
