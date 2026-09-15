#!/usr/bin/env python3
"""Generate Bigimong v0.17 pilot characters entirely inside Blender."""

from __future__ import annotations

import argparse
import hashlib
import json
import random
import sys
from dataclasses import asdict, dataclass
from pathlib import Path
from typing import Any, Sequence

from free3d import REQUIRED_ACTIONS
from free3d.geometry import bottom_offset, transformed_bottom, uniform_scale


@dataclass
class AssetBuildResult:
    id: str
    resource_name: str
    fbx_path: str
    glb_path: str
    atlas_path: str
    bounds: dict[str, list[float]]
    triangle_count: int
    materials: int
    bones: int
    actions: list[str]
    warnings: list[str]
    valid: bool
    render_paths: list[str]
    errors: list[str]
    source_git_blob_sha: str
    output_sha256: dict[str, str]
    origin_error_m: float
    target_height_error_pct: float
    texture_size: int
    deform_bones: int
    control_bones: int
    required_parts: dict[str, bool]


def self_test() -> int:
    scale = uniform_scale(4.0, 2.1)
    offset = bottom_offset(-1.0 * scale)
    report = {
        "status": "SELF_TEST_OK",
        "generator": "blender-python",
        "requiredActions": list(REQUIRED_ACTIONS),
        "scaledHeightM": round(4.0 * scale, 6),
        "bottomAfterTransformM": round(transformed_bottom(-1.0, scale, offset), 6),
    }
    print(json.dumps(report, separators=(",", ":")))
    return 0


def forwarded_args(argv: Sequence[str]) -> list[str]:
    values = list(argv)
    return values[values.index("--") + 1 :] if "--" in values else values


def parse_args(argv: Sequence[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=Path("art/free3d/v0.17-pilot.json"))
    parser.add_argument("--output-dir", type=Path, default=Path("build/free3d-v017"))
    parser.add_argument("--only")
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--self-test", action="store_true")
    parser.add_argument("--self-test-section", choices=("avatars", "tyrannosaurs", "rigging", "delivery"))
    return parser.parse_args(forwarded_args(argv))


def require_blender() -> Any:
    try:
        import bpy  # type: ignore
    except ImportError as error:
        raise RuntimeError(
            "Production generation must run through Blender: blender --background --python "
            "scripts/blender_generate_bigimong.py -- --manifest art/free3d/v0.17-pilot.json "
            "--output-dir build/free3d-v017 --render"
        ) from error
    return bpy


def reset_scene(bpy: Any) -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        if collection.users == 0:
            bpy.data.collections.remove(collection)


def select_character(bpy: Any, root: Any) -> None:
    from free3d.geometry import descendants

    bpy.ops.object.select_all(action="DESELECT")
    for obj in [root, *descendants(root)]:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root


def export_character(bpy: Any, root: Any, fbx_path: Path, glb_path: Path) -> None:
    select_character(bpy, root)
    bpy.ops.export_scene.gltf(
        filepath=str(glb_path),
        export_format="GLB",
        use_selection=True,
        export_yup=True,
        export_animations=True,
    )
    select_character(bpy, root)
    bpy.ops.export_scene.fbx(
        filepath=str(fbx_path),
        use_selection=True,
        apply_unit_scale=True,
        add_leaf_bones=False,
        bake_anim=True,
    )


def build_asset(bpy: Any, job: dict[str, Any], defaults: dict[str, Any], output_dir: Path, render: bool) -> AssetBuildResult:
    from free3d.geometry import descendants, place_origin_at_bottom, scale_to_height, world_bounds
    from free3d.rigging import rig_avatar, rig_tyrannosaur
    from free3d.validation import validate_character

    collection = bpy.data.collections.new(job["resourceName"])
    bpy.context.scene.collection.children.link(collection)
    root = bpy.data.objects.new(job["resourceName"], None)
    collection.objects.link(root)

    if job["id"].startswith("avatar_"):
        from free3d.avatar_builder import build_avatar

        character = build_avatar(job, collection, root, output_dir / "textures")
        rig = rig_avatar(character)
    else:
        from free3d.tyrannosaur_builder import build_tyrannosaur

        character = build_tyrannosaur(job, collection, root, output_dir / "textures")
        rig = rig_tyrannosaur(character)

    scale_to_height(root, float(job["targetHeightM"]))
    place_origin_at_bottom(root)
    validation = validate_character(character, rig, job, defaults)
    model_dir = output_dir / "models"
    model_dir.mkdir(parents=True, exist_ok=True)
    fbx_path = model_dir / f"{job['outputStem']}.fbx"
    glb_path = model_dir / f"{job['outputStem']}.glb"
    export_character(bpy, root, fbx_path, glb_path)

    render_paths: list[str] = []
    if render:
        from free3d.rendering import render_character_review

        render_paths = [str(path) for path in render_character_review(character, rig, output_dir / "renders" / job["id"])]

    bounds_min, bounds_max = world_bounds(descendants(root))
    return AssetBuildResult(
        id=job["id"],
        resource_name=job["resourceName"],
        fbx_path=str(fbx_path),
        glb_path=str(glb_path),
        atlas_path=str(output_dir / "textures" / f"{job['outputStem']}_Atlas.png"),
        bounds={"min": list(bounds_min), "max": list(bounds_max)},
        triangle_count=validation.triangle_count,
        materials=validation.material_count,
        bones=len(rig.deform_bones) + len(rig.control_bones),
        actions=list(rig.actions),
        warnings=list(validation.warnings),
        valid=validation.valid,
        render_paths=render_paths,
        errors=list(validation.errors),
        source_git_blob_sha=job["sourceGitBlobSha"],
        output_sha256={
            "fbx": sha256(fbx_path),
            "glb": sha256(glb_path),
            "atlas": sha256(output_dir / "textures" / f"{job['outputStem']}_Atlas.png"),
        },
        origin_error_m=validation.origin_error_m,
        target_height_error_pct=validation.target_height_error_pct,
        texture_size=validation.texture_size,
        deform_bones=validation.deform_bones,
        control_bones=validation.control_bones,
        required_parts=validation.required_parts,
    )


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def run_blender(args: argparse.Namespace) -> int:
    bpy = require_blender()
    manifest = json.loads(args.manifest.read_text(encoding="utf-8"))
    random.seed(int(manifest["deterministicSeed"]))
    try:
        import numpy  # type: ignore

        numpy.random.seed(int(manifest["deterministicSeed"]))
    except ImportError:
        pass
    reset_scene(bpy)
    args.output_dir.mkdir(parents=True, exist_ok=True)
    jobs = [job for job in manifest["jobs"] if not args.only or job["id"] == args.only]
    if args.only and not jobs:
        raise ValueError(f"unknown pilot id: {args.only}")

    results = [build_asset(bpy, job, manifest["defaults"], args.output_dir, args.render) for job in jobs]
    pilot_index = None
    if args.render and len(results) > 1:
        from free3d.rendering import write_contact_sheet

        three_quarter_paths = [
            Path(path)
            for result in results
            for path in result.render_paths
            if path.endswith("/three_quarter.png")
        ]
        pilot_index = write_contact_sheet(three_quarter_paths, args.output_dir / "renders" / "pilot_index.png")
    blend_path = args.output_dir / "bigimong-v017-review.blend"
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path))
    report_path = args.output_dir / "bigimong-v017-model-report.json"
    payload = {
        "schemaVersion": 1,
        "release": manifest["release"],
        "generator": manifest["generator"],
        "deterministicSeed": manifest["deterministicSeed"],
        "manifestSha256": sha256(args.manifest),
        "blenderVersion": bpy.app.version_string,
        "valid": all(result.valid for result in results) and (len(results) == 5 or bool(args.only)),
        "pilotIndex": str(pilot_index) if pilot_index else None,
        "models": [asdict(result) for result in results],
    }
    report_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"status": "OK" if payload["valid"] else "INVALID", "report": str(report_path)}))
    return 0 if payload["valid"] else 1


def main(argv: Sequence[str]) -> int:
    args = parse_args(argv)
    if args.self_test:
        return self_test()
    if args.self_test_section == "avatars":
        from free3d.avatar_builder import avatar_contract_report

        print(json.dumps(avatar_contract_report(), separators=(",", ":")))
        return 0
    if args.self_test_section == "tyrannosaurs":
        from free3d.tyrannosaur_builder import tyrannosaur_contract_report

        print(json.dumps(tyrannosaur_contract_report(), separators=(",", ":")))
        return 0
    if args.self_test_section == "rigging":
        from free3d.rigging import rig_contract_report

        print(json.dumps(rig_contract_report(), separators=(",", ":")))
        return 0
    if args.self_test_section == "delivery":
        from free3d.rendering import rendering_contract
        from free3d.validation import delivery_validation_contract

        report = {"status": "DELIVERY_CONTRACT_OK", **rendering_contract(), **delivery_validation_contract()}
        print(json.dumps(report, separators=(",", ":")))
        return 0
    return run_blender(args)


if __name__ == "__main__":
    try:
        raise SystemExit(main(sys.argv[1:]))
    except Exception as error:
        print(str(error), file=sys.stderr)
        raise SystemExit(1)
