#!/usr/bin/env python3
"""Import the 31 male static meshes and morphable face in Unreal Editor.

Run inside Unreal Editor's Python plugin after scripts/stage-unreal-male.py.
The unrigged face is converted to a skeletal mesh by an Interchange pipeline.
"""

import argparse
import runpy
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unreal/Bigimong/PrivateAssets/Varco/UnrealImport"
DESTINATION = "/Game/Varco/Male"
STAGE = runpy.run_path(str(ROOT / "scripts/stage-unreal-male.py"))
MORPHS = [f"face_{i:02d}" for i in range(15)]


def preflight(source=SOURCE):
    """Inspect every prepared GLB before importing any asset into a project."""
    source = Path(source)
    names = (["SM_MaleBody"]
             + [f"SM_MaleEye_{i:02d}" for i in range(15)]
             + [f"SM_MaleHair_{i:02d}" for i in range(15)])
    result = []
    for name in names + ["SK_MaleFace"]:
        path = source / f"{name}.glb"
        if path.is_symlink() or not path.is_file():
            raise ValueError(f"Missing or linked prepared model: {path.name}")
        document, _ = STAGE["read_glb"](path.read_bytes())
        if (len(document.get("meshes", [])) != 1 or len(document.get("nodes", [])) != 1
                or document["nodes"][0].get("mesh") != 0
                or document["meshes"][0].get("name") != name):
            raise ValueError(f"Prepared model is not a single expected mesh: {name}")
        mesh = document["meshes"][0]
        if name == "SK_MaleFace":
            if mesh.get("extras", {}).get("targetNames") != MORPHS or any(
                    len(part.get("targets", [])) != 15 for part in mesh["primitives"]):
                raise ValueError("SK_MaleFace must have face_00..face_14 morph targets")
        elif any(part.get("targets") for part in mesh["primitives"]):
            raise ValueError(f"Expected a static mesh without morphs: {name}")
        if name != "SK_MaleFace":
            result.append((path, f"{DESTINATION}/{name}"))
    return result


def import_static(plan, engine):
    """Import missing static meshes only; fail visibly if Interchange names/types differ."""
    library = engine.EditorAssetLibrary
    for _, target in plan:
        if library.does_asset_exist(target) and not isinstance(library.load_asset(target), engine.StaticMesh):
            raise RuntimeError(f"An existing asset is not a static mesh: {target}")

    tools = engine.AssetToolsHelpers.get_asset_tools()
    imported = 0
    for source, target in plan:
        if library.does_asset_exist(target):
            continue
        nested = f"{DESTINATION}/{source.stem}/StaticMeshes/{source.stem}"
        if library.does_asset_exist(nested):
            if not isinstance(library.load_asset(nested), engine.StaticMesh):
                raise RuntimeError(f"An existing nested asset is not a static mesh: {nested}")
            if not library.rename_asset(nested, target):
                raise RuntimeError(f"Could not move existing static mesh: {nested}")
            continue
        task = engine.AssetImportTask()
        for property_name, value in (
            ("filename", str(source)), ("destination_path", DESTINATION),
            ("automated", True), ("async_", False),
            ("replace_existing", False), ("save", True),
        ):
            task.set_editor_property(property_name, value)
        tools.import_asset_tasks([task])
        if not library.does_asset_exist(target):
            if (library.does_asset_exist(nested)
                    and isinstance(library.load_asset(nested), engine.StaticMesh)):
                library.rename_asset(nested, target)
        if not library.does_asset_exist(target) or not isinstance(library.load_asset(target), engine.StaticMesh):
            raise RuntimeError(f"Interchange did not create the expected static mesh: {target}")
        imported += 1
    return imported


def import_face(source, engine):
    """Import and verify the base face plus its 14 selectable morph targets."""
    source = Path(source) / "SK_MaleFace.glb"
    target = f"{DESTINATION}/SK_MaleFace"
    library = engine.EditorAssetLibrary
    was_present = library.does_asset_exist(target)
    if not was_present:
        pipeline = engine.InterchangeGenericAssetsPipeline()
        pipeline.set_editor_property("asset_type_sub_folders", False)
        mesh = pipeline.get_editor_property("mesh_pipeline")
        mesh.set_editor_property("import_morph_targets", True)
        mesh.set_editor_property("import_skeletal_meshes", True)
        mesh.set_editor_property("import_static_meshes", False)
        common = pipeline.get_editor_property("common_meshes_properties")
        common.set_editor_property("convert_statics_with_morph_targets_to_skeletals", True)
        stack = engine.InterchangePipelineStackOverride()
        stack.add_pipeline(pipeline)
        task = engine.AssetImportTask()
        for property_name, value in (
            ("filename", str(source)), ("destination_path", DESTINATION),
            ("destination_name", "SK_MaleFace"), ("automated", True),
            ("async_", False), ("replace_existing", False),
            ("save", True), ("options", stack),
        ):
            task.set_editor_property(property_name, value)
        engine.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    face = library.load_asset(target)
    if not isinstance(face, engine.SkeletalMesh):
        raise RuntimeError("Interchange did not create the expected skeletal face")
    morphs = {str(name) for name in face.get_all_morph_target_names()}
    required = set(MORPHS[1:])
    if not required.issubset(morphs):
        raise RuntimeError(f"Skeletal face is missing morph targets: {sorted(required - morphs)}")
    return not was_present


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=SOURCE)
    args = parser.parse_args()
    try:
        plan = preflight(args.source)
        import unreal
        count = import_static(plan, unreal)
        face_imported = import_face(args.source, unreal)
    except (ModuleNotFoundError, AttributeError) as error:
        parser.exit(2, f"Unreal Editor with Python Editor Script and Editor Scripting Utilities is required: {error}\n")
    except (OSError, KeyError, IndexError, TypeError, ValueError, RuntimeError) as error:
        parser.exit(1, f"Male import stopped: {error}\n")
    print(f"Verified {len(plan)} static meshes, imported {count}; face {'imported' if face_imported else 'verified'} with 14 morph targets.")
