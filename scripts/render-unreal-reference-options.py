#!/usr/bin/env python3
"""Use Unreal to export 15 individual preview PNGs from each supplied sheet.

Run with UnrealEditor-Cmd -RenderOffscreen -ExecutePythonScript. Set
BIGIMONG_OPTION_OUTPUT to the desired output directory. The user-supplied
montages stay in PrivateAssets/ReferenceSheets, outside Git.
"""

import os
from pathlib import Path

import unreal


ROOT = Path(__file__).resolve().parents[1]
SHEETS = ROOT / "unreal/Bigimong/PrivateAssets/ReferenceSheets"
OUTPUT = Path(os.environ.get("BIGIMONG_OPTION_OUTPUT", str(
    ROOT / "unreal/Bigimong/PrivateAssets/RenderedOptions")))
SOURCE = {
    "eye": ("eyes-15.png", "T_EyeSheet"),
    "face": ("face-15.png", "T_FaceSheet"),
    "hair": ("hair-15.png", "T_HairSheet"),
}
SKIN = (
    (1.00, .84, .72), (.99, .82, .68), (.97, .79, .64),
    (.96, .75, .61), (.92, .70, .55), (.88, .64, .48),
    (.82, .57, .41), (.76, .51, .36), (.70, .45, .31),
    (.64, .40, .27), (.57, .34, .23), (.51, .30, .20),
    (.45, .26, .17), (.39, .22, .15), (.28, .15, .11),
)


def import_sheet(filename, asset_name):
    source = SHEETS / filename
    if not source.is_file():
        raise RuntimeError(f"Missing user reference sheet: {source}")
    path = f"/Game/ReferenceSheets/{asset_name}"
    if not unreal.EditorAssetLibrary.does_asset_exist(path):
        task = unreal.AssetImportTask()
        for field, value in (
            ("filename", str(source)), ("destination_path", "/Game/ReferenceSheets"),
            ("destination_name", asset_name), ("automated", True),
            ("replace_existing", False), ("save", True),
        ):
            task.set_editor_property(field, value)
        unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([task])
    texture = unreal.EditorAssetLibrary.load_asset(path)
    if not isinstance(texture, unreal.Texture2D):
        raise RuntimeError(f"Unreal did not import {source.name} as a texture")
    return texture


def crop_material():
    path = "/Game/ReferenceSheets/M_ReferenceCrop"
    material = unreal.EditorAssetLibrary.load_asset(path)
    if not material:
        material = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
            "M_ReferenceCrop", "/Game/ReferenceSheets", unreal.Material,
            unreal.MaterialFactoryNew(),
        )
        material.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT)
        make = unreal.MaterialEditingLibrary.create_material_expression
        tex = make(material, unreal.MaterialExpressionTextureSample, 200, 0)
        uv = make(material, unreal.MaterialExpressionTextureCoordinate, -600, 0)
        scale = make(material, unreal.MaterialExpressionConstant2Vector, -600, 200)
        offset = make(material, unreal.MaterialExpressionConstant2Vector, -400, 300)
        mul = make(material, unreal.MaterialExpressionMultiply, -200, 0)
        add = make(material, unreal.MaterialExpressionAdd, 0, 0)
        links = ((uv, mul, "A"), (scale, mul, "B"), (mul, add, "A"),
                 (offset, add, "B"), (add, tex, "UVs"))
        for source, target, pin in links:
            if not unreal.MaterialEditingLibrary.connect_material_expressions(
                    source, "", target, pin):
                raise RuntimeError(f"Unreal crop material connection failed: {pin}")
        if not unreal.MaterialEditingLibrary.connect_material_property(
                tex, "RGB", unreal.MaterialProperty.MP_EMISSIVE_COLOR):
            raise RuntimeError("Unreal crop material has no emissive output")
        unreal.EditorAssetLibrary.save_loaded_asset(material)
    nodes = unreal.MaterialEditingLibrary.get_material_expressions(material)
    tex = next(x for x in nodes if isinstance(x, unreal.MaterialExpressionTextureSample))
    vectors = [x for x in nodes if isinstance(x, unreal.MaterialExpressionConstant2Vector)]
    if len(vectors) != 2:
        raise RuntimeError("Unexpected Unreal crop material graph")
    return material, tex, vectors[0], vectors[1]


def skin_material():
    path = "/Game/ReferenceSheets/M_ReferenceSkinSwatch"
    material = unreal.EditorAssetLibrary.load_asset(path)
    if not material:
        material = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
            "M_ReferenceSkinSwatch", "/Game/ReferenceSheets", unreal.Material,
            unreal.MaterialFactoryNew(),
        )
        material.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT)
        node = unreal.MaterialEditingLibrary.create_material_expression(
            material, unreal.MaterialExpressionVectorParameter, -300, 0)
        node.set_editor_property("parameter_name", "SkinSwatch")
        if not unreal.MaterialEditingLibrary.connect_material_property(
                node, "RGB", unreal.MaterialProperty.MP_EMISSIVE_COLOR):
            raise RuntimeError("Unreal skin swatch has no emissive output")
        unreal.EditorAssetLibrary.save_loaded_asset(material)
    node = next(x for x in unreal.MaterialEditingLibrary.get_material_expressions(material)
                if isinstance(x, unreal.MaterialExpressionVectorParameter))
    return material, node


def crop_box(category, index):
    column = index % 5
    row = index // 5
    x = .01 + .20 * column
    if category == "eye":
        return x, (.06, .39, .72)[row], .19, .27
    return x, (.09, .39, .69)[row], .19, .19


def export_render(world, target, material, category, number):
    directory = OUTPUT / category
    directory.mkdir(parents=True, exist_ok=True)
    unreal.RenderingLibrary.draw_material_to_render_target(world, target, material)
    unreal.RenderingLibrary.export_render_target(
        world, target, str(directory), f"{category}-{number:02d}.png")
    output = directory / f"{category}-{number:02d}.png"
    if not output.is_file() or output.stat().st_size < 1000:
        raise RuntimeError(f"Unreal did not export an option preview: {output}")
    return output


def import_previews(files):
    tasks = []
    for category, number, path in files:
        name = f"T_{category.capitalize()}_{number:02d}"
        target = f"/Game/Customization/Thumbnails/{name}"
        if unreal.EditorAssetLibrary.does_asset_exist(target):
            continue
        task = unreal.AssetImportTask()
        for field, value in (
            ("filename", str(path)),
            ("destination_path", "/Game/Customization/Thumbnails"),
            ("destination_name", name), ("automated", True),
            ("replace_existing", False), ("save", True),
        ):
            task.set_editor_property(field, value)
        tasks.append(task)
    if tasks:
        unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks(tasks)
    for category, number, _ in files:
        name = f"T_{category.capitalize()}_{number:02d}"
        asset = unreal.EditorAssetLibrary.load_asset(
            f"/Game/Customization/Thumbnails/{name}")
        if not isinstance(asset, unreal.Texture2D):
            raise RuntimeError(f"Missing Unreal option texture: {name}")


def main():
    textures = {category: import_sheet(*names) for category, names in SOURCE.items()}
    material, tex, scale, offset = crop_material()
    world = unreal.EditorLevelLibrary.get_editor_world()
    target = unreal.RenderingLibrary.create_render_target2d(
        world, 256, 256, unreal.TextureRenderTargetFormat.RTF_RGBA8)
    files = []
    for category in ("eye", "face", "hair"):
        tex.set_editor_property("texture", textures[category])
        for index in range(15):
            u, v, width, height = crop_box(category, index)
            scale.set_editor_property("r", width)
            scale.set_editor_property("g", height)
            offset.set_editor_property("r", u)
            offset.set_editor_property("g", v)
            unreal.MaterialEditingLibrary.recompile_material(material)
            files.append((category, index + 1, export_render(
                world, target, material, category, index + 1)))
            print("OPTION_RENDERED", category, index + 1)
    swatch, color = skin_material()
    for index, (red, green, blue) in enumerate(SKIN, 1):
        color.set_editor_property("default_value", unreal.LinearColor(red, green, blue, 1.0))
        unreal.MaterialEditingLibrary.recompile_material(swatch)
        files.append(("skin", index, export_render(
            world, target, swatch, "skin", index)))
        print("OPTION_RENDERED", "skin", index)
    import_previews(files)
    print("OPTION_RENDER_COMPLETE", len(files), str(OUTPUT))


if __name__ == "__main__":
    main()
