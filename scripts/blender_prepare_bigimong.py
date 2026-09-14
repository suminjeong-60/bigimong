#!/usr/bin/env python3
"""Normalize generated Bigimong models in Blender and create review exports."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path
from typing import Iterable, Sequence


def bottom_origin(bounds_min: Sequence[float], bounds_max: Sequence[float]) -> tuple[float, float, float]:
    return (
        (float(bounds_min[0]) + float(bounds_max[0])) / 2.0,
        (float(bounds_min[1]) + float(bounds_max[1])) / 2.0,
        float(bounds_min[2]),
    )


def layout_slots(count: int, spacing: float = 2.4) -> list[tuple[float, float, float]]:
    slots: list[tuple[float, float, float]] = []
    for index in range(max(0, count)):
        column = index % 3
        row = index // 3
        row_offset = 0.0 if row == 0 else -row * spacing
        slots.append(((column - 1) * spacing, row_offset, 0.0))
    return slots


def self_test() -> int:
    origin = bottom_origin((1, 2, -4), (3, 4, 6))
    slots = layout_slots(5)
    assert origin == (2.0, 3.0, -4.0)
    assert slots[:3] == [(-2.4, 0.0, 0.0), (0.0, 0.0, 0.0), (2.4, 0.0, 0.0)]
    print(json.dumps({"status": "SELF_TEST_OK", "bottomOrigin": origin, "layoutSlots": slots}))
    return 0


def blender_args(argv: Sequence[str]) -> argparse.Namespace:
    forwarded = list(argv)
    if "--" in forwarded:
        forwarded = forwarded[forwarded.index("--") + 1 :]
    parser = argparse.ArgumentParser()
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--input-dir", type=Path, required=True)
    parser.add_argument("--output-dir", type=Path, required=True)
    parser.add_argument("--blend", type=Path, required=True)
    return parser.parse_args(forwarded)


def import_bpy():
    try:
        import bpy  # type: ignore
        from mathutils import Vector  # type: ignore
    except ImportError as error:
        raise RuntimeError("Run this mode through Blender: blender --background --python scripts/blender_prepare_bigimong.py -- ...") from error
    return bpy, Vector


def find_model(input_dir: Path, stem: str) -> Path:
    for suffix in (".glb", ".gltf", ".fbx", ".obj"):
        candidate = input_dir / f"{stem}{suffix}"
        if candidate.is_file():
            return candidate
    raise FileNotFoundError(f"No GLB/GLTF/FBX/OBJ found for {stem} in {input_dir}")


def import_model(bpy, path: Path) -> list:
    before = set(bpy.data.objects)
    suffix = path.suffix.lower()
    if suffix in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=str(path))
    elif suffix == ".fbx":
        bpy.ops.import_scene.fbx(filepath=str(path))
    elif hasattr(bpy.ops.wm, "obj_import"):
        bpy.ops.wm.obj_import(filepath=str(path))
    else:
        bpy.ops.import_scene.obj(filepath=str(path))
    return [obj for obj in bpy.data.objects if obj not in before]


def mesh_bounds(bpy, Vector, objects: Iterable) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    points = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in objects:
        if obj.type != "MESH":
            continue
        evaluated = obj.evaluated_get(depsgraph)
        points.extend(evaluated.matrix_world @ Vector(corner) for corner in evaluated.bound_box)
    if not points:
        raise ValueError("Imported model contains no mesh bounds")
    return (
        tuple(min(point[axis] for point in points) for axis in range(3)),
        tuple(max(point[axis] for point in points) for axis in range(3)),
    )


def apply_mesh_transforms(bpy, objects: Iterable) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objects:
        if obj.type == "MESH":
            obj.select_set(True)
            bpy.context.view_layer.objects.active = obj
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
            obj.select_set(False)


def normalize_model(bpy, Vector, job: dict, imported: list):
    apply_mesh_transforms(bpy, imported)
    bounds_min, bounds_max = mesh_bounds(bpy, Vector, imported)
    origin = bottom_origin(bounds_min, bounds_max)
    root = bpy.data.objects.new(job["id"], None)
    bpy.context.scene.collection.objects.link(root)
    root.location = origin
    for obj in imported:
        if obj.parent is None:
            world = obj.matrix_world.copy()
            obj.parent = root
            obj.matrix_world = world
    root.location = (0.0, 0.0, 0.0)
    height = float(bounds_max[2] - bounds_min[2])
    if height <= 0:
        raise ValueError(f"{job['id']} has zero height")
    scale = float(job["targetHeightM"]) / height
    root.scale = (scale, scale, scale)
    return root, bounds_min, bounds_max, height, scale


def descendants(root) -> list:
    result = [root]
    stack = list(root.children)
    while stack:
        value = stack.pop()
        result.append(value)
        stack.extend(value.children)
    return result


def select_hierarchy(bpy, root) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in descendants(root):
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_glb(bpy, root, output: Path) -> None:
    select_hierarchy(bpy, root)
    bpy.ops.export_scene.gltf(filepath=str(output), export_format="GLB", use_selection=True, export_yup=True)


def export_fbx(bpy, root, output: Path) -> None:
    select_hierarchy(bpy, root)
    bpy.ops.export_scene.fbx(
        filepath=str(output),
        use_selection=True,
        apply_unit_scale=True,
        add_leaf_bones=False,
        bake_anim=True,
    )


def run_blender(argv: Sequence[str]) -> int:
    args = blender_args(argv)
    bpy, Vector = import_bpy()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    args.output_dir.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    reports = []
    roots = []
    jobs = manifest["jobs"]
    for job, slot in zip(jobs, layout_slots(len(jobs))):
        source = find_model(args.input_dir, job["outputStem"])
        imported = import_model(bpy, source)
        root, bounds_min, bounds_max, source_height, scale = normalize_model(bpy, Vector, job, imported)
        export_glb(bpy, root, args.output_dir / f"{job['outputStem']}_unity.glb")
        export_fbx(bpy, root, args.output_dir / f"{job['outputStem']}_unity.fbx")
        root.location = slot
        roots.append(root)
        mesh_objects = [obj for obj in descendants(root) if obj.type == "MESH"]
        for obj in mesh_objects:
            obj.data.calc_loop_triangles()
        triangles = sum(len(obj.data.loop_triangles) for obj in mesh_objects)
        reports.append({
            "id": job["id"],
            "source": str(source),
            "sourceBoundsMin": bounds_min,
            "sourceBoundsMax": bounds_max,
            "sourceHeight": source_height,
            "scale": scale,
            "targetHeightM": job["targetHeightM"],
            "originAtBottom": True,
            "triangles": triangles,
            "reviewSlot": slot,
        })
    args.blend.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(args.blend))
    report_path = args.output_dir / "bigimong-v016-model-report.json"
    report_path.write_text(json.dumps({"models": reports}, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"status": "OK", "blend": str(args.blend), "report": str(report_path)}))
    return 0


if __name__ == "__main__":
    if "--self-test" in sys.argv:
        raise SystemExit(self_test())
    try:
        raise SystemExit(run_blender(sys.argv[1:]))
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
