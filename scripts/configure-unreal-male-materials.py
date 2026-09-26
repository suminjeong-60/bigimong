import unreal

path = "/Game/Varco/Male/M_FaceSkinTint"
material = unreal.EditorAssetLibrary.load_asset(path)
if not material:
    material = unreal.AssetToolsHelpers.get_asset_tools().create_asset(
        "M_FaceSkinTint", "/Game/Varco/Male", unreal.Material,
        unreal.MaterialFactoryNew(),
    )
    color = unreal.MaterialEditingLibrary.create_material_expression(
        material, unreal.MaterialExpressionVectorParameter, -400, 0,
    )
    color.set_editor_property("parameter_name", "BaseColorFactor")
    color.set_editor_property("default_value", unreal.LinearColor(1.0, 0.84, 0.72, 1.0))
    unreal.MaterialEditingLibrary.connect_material_property(
        color, "RGB", unreal.MaterialProperty.MP_BASE_COLOR,
    )
    roughness = unreal.MaterialEditingLibrary.create_material_expression(
        material, unreal.MaterialExpressionConstant, -400, 200,
    )
    roughness.set_editor_property("r", 0.62)
    unreal.MaterialEditingLibrary.connect_material_property(
        roughness, "", unreal.MaterialProperty.MP_ROUGHNESS,
    )
    unreal.MaterialEditingLibrary.recompile_material(material)
    unreal.EditorAssetLibrary.save_loaded_asset(material)
print("FACE_TINT_MATERIAL", material)
mesh = unreal.EditorAssetLibrary.load_asset("/Game/Varco/Male/SK_MaleFace")
print("FACE_MESH_METHODS", [x for x in dir(mesh) if "material" in x.lower()])
slots = mesh.get_editor_property("materials")
for index, slot in enumerate(slots):
    if str(slot.get_editor_property("material_slot_name")) in ("Skin", "SkinInner"):
        slot.set_editor_property("material_interface", material)
        slots[index] = slot
mesh.set_editor_property("materials", slots)
unreal.EditorAssetLibrary.save_loaded_asset(mesh)
print("FACE_TINT_SLOTS", [(str(x.material_slot_name), str(x.material_interface)) for x in mesh.get_editor_property("materials")])
