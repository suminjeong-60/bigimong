"""Deterministic armatures, skin binding, and action clips for pilot models."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any

from free3d import REQUIRED_ACTIONS


MAX_DEFORM_BONES = 64
MAX_CONTROL_BONES = 16

ACTION_SPECS = {
    "Idle": {"frames": (1, 90), "loop": True},
    "Summon": {"frames": (1, 45), "loop": False},
    "Attack": {"frames": (1, 24), "loop": False},
    "DodgeLeft": {"frames": (1, 20), "loop": False},
    "DodgeRight": {"frames": (1, 20), "loop": False},
    "Hit": {"frames": (1, 16), "loop": False},
    "Knockout": {"frames": (1, 35), "loop": False},
    "Victory": {"frames": (1, 60), "loop": False},
}

COMMON_REQUIRED_BONES = (
    "Root",
    "Hips",
    "Head",
    "Jaw",
    "Hand.L",
    "Hand.R",
    "Foot.L",
    "Foot.R",
)

ATTACHMENT_BONES = (
    "MedallionSocket",
    "HatSocket",
    "BackpackSocket",
    "RollSocket",
)


@dataclass
class RigBuild:
    armature: Any
    deform_bones: list[str]
    control_bones: list[str]
    actions: list[str]
    attachment_bones: list[str]
    action_blocks: dict[str, Any] = field(default_factory=dict)


def rig_contract_report() -> dict[str, Any]:
    return {
        "status": "RIG_CONTRACT_OK",
        "maxDeformBones": MAX_DEFORM_BONES,
        "maxControlBones": MAX_CONTROL_BONES,
        "actions": {
            name: {"frames": list(spec["frames"]), "loop": spec["loop"]}
            for name, spec in ACTION_SPECS.items()
        },
        "requiredBones": list(COMMON_REQUIRED_BONES),
        "attachmentBones": list(ATTACHMENT_BONES),
    }


def _avatar_bones() -> list[dict[str, Any]]:
    return [
        {"name": "Root", "head": (0, 0, 0), "tail": (0, 0, 0.12), "parent": None},
        {"name": "Hips", "head": (0, 0, 0.68), "tail": (0, 0, 0.85), "parent": "Root"},
        {"name": "Spine", "head": (0, 0, 0.82), "tail": (0, 0, 1.12), "parent": "Hips"},
        {"name": "Chest", "head": (0, 0, 1.08), "tail": (0, 0, 1.32), "parent": "Spine"},
        {"name": "Neck", "head": (0, 0, 1.28), "tail": (0, 0, 1.48), "parent": "Chest"},
        {"name": "Head", "head": (0, 0, 1.43), "tail": (0, 0, 1.78), "parent": "Neck"},
        {"name": "Jaw", "head": (0, -0.03, 1.53), "tail": (0, -0.2, 1.51), "parent": "Head"},
        {"name": "UpperArm.L", "head": (-0.2, 0, 1.26), "tail": (-0.33, 0, 1.02), "parent": "Chest"},
        {"name": "LowerArm.L", "head": (-0.33, 0, 1.02), "tail": (-0.35, 0, 0.75), "parent": "UpperArm.L"},
        {"name": "Hand.L", "head": (-0.35, 0, 0.75), "tail": (-0.35, -0.01, 0.63), "parent": "LowerArm.L"},
        {"name": "UpperArm.R", "head": (0.2, 0, 1.26), "tail": (0.33, 0, 1.02), "parent": "Chest"},
        {"name": "LowerArm.R", "head": (0.33, 0, 1.02), "tail": (0.35, 0, 0.75), "parent": "UpperArm.R"},
        {"name": "Hand.R", "head": (0.35, 0, 0.75), "tail": (0.35, -0.01, 0.63), "parent": "LowerArm.R"},
        {"name": "UpperLeg.L", "head": (-0.1, 0, 0.72), "tail": (-0.1, 0, 0.38), "parent": "Hips"},
        {"name": "LowerLeg.L", "head": (-0.1, 0, 0.38), "tail": (-0.1, 0, 0.11), "parent": "UpperLeg.L"},
        {"name": "Foot.L", "head": (-0.1, 0, 0.11), "tail": (-0.1, -0.18, 0.08), "parent": "LowerLeg.L"},
        {"name": "UpperLeg.R", "head": (0.1, 0, 0.72), "tail": (0.1, 0, 0.38), "parent": "Hips"},
        {"name": "LowerLeg.R", "head": (0.1, 0, 0.38), "tail": (0.1, 0, 0.11), "parent": "UpperLeg.R"},
        {"name": "Foot.R", "head": (0.1, 0, 0.11), "tail": (0.1, -0.18, 0.08), "parent": "LowerLeg.R"},
    ]


def _tyrannosaur_bones() -> list[dict[str, Any]]:
    return [
        {"name": "Root", "head": (0, 0, 0), "tail": (0, 0, 0.12), "parent": None},
        {"name": "Hips", "head": (0, 0.16, 0.64), "tail": (0, 0.04, 0.83), "parent": "Root"},
        {"name": "Spine", "head": (0, 0.03, 0.8), "tail": (0, -0.18, 1.02), "parent": "Hips"},
        {"name": "Chest", "head": (0, -0.16, 0.98), "tail": (0, -0.3, 1.18), "parent": "Spine"},
        {"name": "Neck", "head": (0, -0.28, 1.12), "tail": (0, -0.39, 1.3), "parent": "Chest"},
        {"name": "Head", "head": (0, -0.39, 1.28), "tail": (0, -0.63, 1.34), "parent": "Neck"},
        {"name": "Jaw", "head": (0, -0.45, 1.23), "tail": (0, -0.78, 1.2), "parent": "Head"},
        {"name": "Tail.1", "head": (0, 0.25, 0.76), "tail": (0, 0.57, 0.68), "parent": "Hips"},
        {"name": "Tail.2", "head": (0, 0.57, 0.68), "tail": (0, 0.95, 0.52), "parent": "Tail.1"},
        {"name": "Tail.3", "head": (0, 0.95, 0.52), "tail": (0, 1.3, 0.4), "parent": "Tail.2"},
        {"name": "UpperLeg.L", "head": (-0.22, 0.05, 0.7), "tail": (-0.24, -0.02, 0.38), "parent": "Hips"},
        {"name": "LowerLeg.L", "head": (-0.24, -0.02, 0.38), "tail": (-0.24, -0.12, 0.12), "parent": "UpperLeg.L"},
        {"name": "Foot.L", "head": (-0.24, -0.12, 0.12), "tail": (-0.24, -0.42, 0.08), "parent": "LowerLeg.L"},
        {"name": "UpperLeg.R", "head": (0.22, 0.05, 0.7), "tail": (0.24, -0.02, 0.38), "parent": "Hips"},
        {"name": "LowerLeg.R", "head": (0.24, -0.02, 0.38), "tail": (0.24, -0.12, 0.12), "parent": "UpperLeg.R"},
        {"name": "Foot.R", "head": (0.24, -0.12, 0.12), "tail": (0.24, -0.42, 0.08), "parent": "LowerLeg.R"},
        {"name": "UpperArm.L", "head": (-0.2, -0.25, 1.0), "tail": (-0.28, -0.43, 0.91), "parent": "Chest"},
        {"name": "Hand.L", "head": (-0.28, -0.43, 0.91), "tail": (-0.2, -0.56, 0.86), "parent": "UpperArm.L"},
        {"name": "UpperArm.R", "head": (0.2, -0.25, 1.0), "tail": (0.28, -0.43, 0.91), "parent": "Chest"},
        {"name": "Hand.R", "head": (0.28, -0.43, 0.91), "tail": (0.2, -0.56, 0.86), "parent": "UpperArm.R"},
    ]


def _create_armature(character: Any, bone_specs: list[dict[str, Any]]) -> Any:
    import bpy  # type: ignore

    name = f"{character.root.name}_Rig"
    data = bpy.data.armatures.new(f"{name}_Data")
    armature = bpy.data.objects.new(name, data)
    character.root.users_collection[0].objects.link(armature)
    armature.parent = character.root
    armature.show_in_front = True
    bpy.context.view_layer.objects.active = armature
    armature.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    created = {}
    for spec in bone_specs:
        bone = data.edit_bones.new(spec["name"])
        bone.head = spec["head"]
        bone.tail = spec["tail"]
        bone.use_deform = True
        created[spec["name"]] = bone
    for spec in bone_specs:
        if spec["parent"]:
            created[spec["name"]].parent = created[spec["parent"]]
    for index, name_value in enumerate(ATTACHMENT_BONES):
        bone = data.edit_bones.new(name_value)
        bone.head = (0.0, -0.12, 1.05 + index * 0.02)
        bone.tail = (0.0, -0.12, 1.12 + index * 0.02)
        bone.use_deform = False
        bone.parent = created["Chest"]
    bpy.ops.object.mode_set(mode="OBJECT")
    armature.select_set(False)
    return armature


def _bone_for_object(name: str, kind: str) -> str:
    if "Head" in name or any(token in name for token in ("Eye", "Iris", "Pupil", "Catchlight", "Brow", "Hair", "Ear", "Horn", "Nose", "Cheek", "Smile", "Nostril", "Cap", "Muzzle", "Snout")):
        return "Head"
    if "Jaw" in name or "Tooth" in name or "Mouth" in name:
        return "Jaw"
    if any(token in name for token in ("SkinMark", "Dorsal", "Backpack", "ExplorerPack", "Vest", "Jacket", "PackRoll")):
        return "Spine"
    if "Finger" in name or "Palm" in name or "Thumb" in name:
        return "Hand.L" if "Left" in name else "Hand.R"
    if "Left" in name:
        if "Foot" in name or "Toe" in name or "Claw" in name or "Leg" in name or "Shin" in name:
            return "Foot.L" if "Foot" in name or "Claw" in name else "UpperLeg.L"
        return "Hand.L"
    if "Right" in name:
        if "Foot" in name or "Toe" in name or "Claw" in name or "Leg" in name or "Shin" in name:
            return "Foot.R" if "Foot" in name or "Claw" in name else "UpperLeg.R"
        return "Hand.R"
    if "Tail" in name:
        return "Tail.1" if kind == "tyrannosaur" else "Hips"
    return "Spine"


def _bind_mesh(obj: Any, armature: Any, bone_name: str) -> None:
    if getattr(obj, "type", None) != "MESH":
        return
    group = obj.vertex_groups.get(bone_name) or obj.vertex_groups.new(name=bone_name)
    if obj.data.vertices:
        group.add(list(range(len(obj.data.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("BigimongArmature", "ARMATURE")
    modifier.object = armature


def _bone_parent(obj: Any, armature: Any, bone_name: str) -> None:
    world = obj.matrix_world.copy()
    obj.parent = armature
    obj.parent_type = "BONE"
    obj.parent_bone = bone_name
    obj.matrix_world = world


def _key_curve(action: Any, data_path: str, index: int, points: list[tuple[int, float]]) -> None:
    curve = action.fcurves.new(data_path=data_path, index=index)
    for frame, value in points:
        point = curve.keyframe_points.insert(frame, value)
        point.interpolation = "BEZIER"


def create_required_actions(armature: Any, kind: str) -> tuple[list[str], dict[str, Any]]:
    import bpy  # type: ignore

    armature.animation_data_create()
    blocks = {}
    resource = armature.name.removesuffix("_Rig")
    for action_name in REQUIRED_ACTIONS:
        spec = ACTION_SPECS[action_name]
        start, end = spec["frames"]
        middle = int((start + end) / 2)
        action = bpy.data.actions.new(f"{resource}|{action_name}")
        action.use_fake_user = True
        action["bigimong_clip"] = action_name
        action["bigimong_loop"] = bool(spec["loop"])
        root_path = 'pose.bones["Root"].location'
        head_path = 'pose.bones["Head"].rotation_euler'
        hand_path = 'pose.bones["Hand.R"].location'
        if action_name == "Idle":
            _key_curve(action, root_path, 2, [(start, 0.0), (middle, 0.025), (end, 0.0)])
            _key_curve(action, head_path, 2, [(start, -0.03), (middle, 0.03), (end, -0.03)])
            for curve in action.fcurves:
                curve.modifiers.new("CYCLES")
        elif action_name == "Summon":
            start_z = -0.65 if kind == "tyrannosaur" else 0.0
            _key_curve(action, root_path, 2, [(start, start_z), (middle, 0.08), (end, 0.0)])
            _key_curve(action, hand_path, 1, [(start, 0.0), (middle, -0.25), (end, -0.5 if kind == "avatar" else 0.0)])
        elif action_name == "Attack":
            _key_curve(action, root_path, 1, [(start, 0.0), (middle, -0.28), (end, 0.0)])
            _key_curve(action, head_path, 0, [(start, 0.0), (middle, -0.18), (end, 0.0)])
        elif action_name == "DodgeLeft":
            _key_curve(action, root_path, 0, [(start, 0.0), (middle, -0.35), (end, 0.0)])
        elif action_name == "DodgeRight":
            _key_curve(action, root_path, 0, [(start, 0.0), (middle, 0.35), (end, 0.0)])
        elif action_name == "Hit":
            _key_curve(action, root_path, 1, [(start, 0.0), (middle, 0.18), (end, 0.0)])
            _key_curve(action, head_path, 2, [(start, 0.0), (middle, 0.2), (end, 0.0)])
        elif action_name == "Knockout":
            _key_curve(action, root_path, 2, [(start, 0.0), (middle, 0.03), (end, -0.06)])
            _key_curve(action, head_path, 1, [(start, 0.0), (middle, 0.4), (end, 1.15)])
        elif action_name == "Victory":
            _key_curve(action, root_path, 2, [(start, 0.0), (middle, 0.16), (end, 0.0)])
            _key_curve(action, head_path, 2, [(start, -0.08), (middle, 0.08), (end, -0.08)])
        blocks[action_name] = action
    armature.animation_data.action = blocks["Idle"]
    return list(REQUIRED_ACTIONS), blocks


def _rig(character: Any, bone_specs: list[dict[str, Any]]) -> RigBuild:
    armature = _create_armature(character, bone_specs)
    deform_bones = [spec["name"] for spec in bone_specs]
    if len(deform_bones) > MAX_DEFORM_BONES or len(ATTACHMENT_BONES) > MAX_CONTROL_BONES:
        raise ValueError("pilot rig exceeds the mobile bone budget")
    for obj in character.skin_meshes:
        _bind_mesh(obj, armature, _bone_for_object(obj.name, character.kind))
    for obj in character.rigid_parts:
        if getattr(obj, "name", "") == "MedallionSocket":
            _bone_parent(obj, armature, "MedallionSocket")
        elif getattr(obj, "type", None) in ("MESH", "EMPTY"):
            _bone_parent(obj, armature, _bone_for_object(obj.name, character.kind))
    actions, blocks = create_required_actions(armature, character.kind)
    return RigBuild(
        armature=armature,
        deform_bones=deform_bones,
        control_bones=list(ATTACHMENT_BONES),
        actions=actions,
        attachment_bones=list(ATTACHMENT_BONES),
        action_blocks=blocks,
    )


def rig_avatar(character: Any) -> RigBuild:
    return _rig(character, _avatar_bones())


def rig_tyrannosaur(character: Any) -> RigBuild:
    return _rig(character, _tyrannosaur_bones())
