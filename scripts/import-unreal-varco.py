#!/usr/bin/env python3
"""Import the untouched original VARCO male and female GLBs with Unreal.

Run inside UnrealEditor-Cmd after scripts/prepare-unreal-varco.py. The source
models remain private; only the locally imported Unreal assets are created.
"""

from pathlib import Path

import unreal


ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / "unreal/Bigimong/PrivateAssets/Varco"
DESTINATION = "/Game/Varco"


def import_one(filename, name):
    source = SOURCE / filename
    target = f"{DESTINATION}/{name}"
    library = unreal.EditorAssetLibrary
    if not source.is_file():
        raise RuntimeError(f"Missing original VARCO model: {source}")
    if library.does_asset_exist(target):
        if not isinstance(library.load_asset(target), unreal.StaticMesh):
            raise RuntimeError(f"Wrong Unreal asset type: {target}")
        return False
    before = set(library.list_assets(DESTINATION, recursive=True, include_folder=False))
    task = unreal.AssetImportTask()
    for field, value in (
        ("filename", str(source)), ("destination_path", DESTINATION),
        ("destination_name", name), ("automated", True),
        ("async_", False), ("replace_existing", False), ("save", True),
    ):
        task.set_editor_property(field, value)
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    after = set(library.list_assets(DESTINATION, recursive=True, include_folder=False))
    meshes = [path for path in after - before
              if isinstance(library.load_asset(path), unreal.StaticMesh)]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one VARCO static mesh, got {meshes}")
    if meshes[0].split(".")[0] != target:
        if not library.rename_asset(meshes[0], target):
            raise RuntimeError(f"Could not move {meshes[0]} to {target}")
    if not isinstance(library.load_asset(target), unreal.StaticMesh):
        raise RuntimeError(f"VARCO model not available at {target}")
    return True


if __name__ == "__main__":
    for source_name, asset_name in (("varco-male.glb", "SM_VARCO_Male"),
                                    ("varco-female.glb", "SM_VARCO_Female")):
        print("VARCO_ORIGINAL", asset_name, "imported" if import_one(
            source_name, asset_name) else "verified")
