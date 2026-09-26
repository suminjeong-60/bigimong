#!/usr/bin/env python3
"""Import the 31 male static meshes in Unreal Editor; verify face for manual morph import.

Run inside Unreal Editor's Python plugin after scripts/stage-unreal-male.py.
The unrigged face requires Interchange morph-to-skeletal options in the Editor UI.
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
        task = engine.AssetImportTask()
        for property_name, value in (
            ("filename", str(source)), ("destination_path", DESTINATION),
            ("automated", True), ("async_", False),
            ("replace_existing", False), ("save", True),
        ):
            task.set_editor_property(property_name, value)
        tools.import_asset_tasks([task])
        if not library.does_asset_exist(target) or not isinstance(library.load_asset(target), engine.StaticMesh):
            raise RuntimeError(f"Interchange did not create the expected static mesh: {target}")
        imported += 1
    return imported


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, default=SOURCE)
    args = parser.parse_args()
    try:
        plan = preflight(args.source)
        import unreal
        count = import_static(plan, unreal)
    except (ModuleNotFoundError, AttributeError) as error:
        parser.exit(2, f"Unreal Editor with Python Editor Script and Editor Scripting Utilities is required: {error}\n")
    except (OSError, KeyError, IndexError, TypeError, ValueError, RuntimeError) as error:
        parser.exit(1, f"Male import stopped: {error}\n")
    print(f"Verified {len(plan)} static meshes, imported {count}. Import SK_MaleFace with morph-to-skeletal options in Unreal Editor.")
