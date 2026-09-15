"""Three independently proportioned red Tyrannosaurus character builders."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from free3d.geometry import (
    CharacterBuild,
    create_curve_tube,
    create_ellipsoid_mesh,
    create_fin_mesh,
    create_profile_mesh,
    create_tapered_tube_mesh,
)
from free3d.materials import assign_palette_region, create_atlas_material, palette_index


TYRANNOSAUR_NAMED_PARTS = (
    "Body",
    "Head",
    "Jaw",
    "Tail",
    "EyeWhiteLeft",
    "EyeWhiteRight",
    "IrisLeft",
    "IrisRight",
    "Teeth",
    "ArmLeft",
    "ArmRight",
    "LegLeft",
    "LegRight",
    "FootLeft",
    "FootRight",
    "ExplorerCap",
    "ExplorerVest",
    "ExplorerPack",
    "PackRoll",
)

STAGE_PROFILES = {
    "tyrannosaurus_baby": {
        "profile": "baby_round_four_tooth",
        "target_height": 0.3,
        "head": (0.35, 0.34, 0.31),
        "head_z": 1.23,
        "muzzle_y": -0.54,
        "muzzle": (0.27, 0.25, 0.15),
        "body": (0.34, 0.42, 0.42),
        "body_z": 0.68,
        "leg_height": 0.25,
        "tail_length": 0.75,
        "eye": 0.092,
        "teeth": 4,
        "wardrobe": ["ExplorerCap"],
        "expression": "wide_eyed_curious",
        "brow_tilt": 0.08,
        "spine_size": 0.085,
    },
    "tyrannosaurus_teen": {
        "profile": "teen_long_jaw_runner",
        "target_height": 0.9,
        "head": (0.34, 0.39, 0.28),
        "head_z": 1.34,
        "muzzle_y": -0.64,
        "muzzle": (0.29, 0.34, 0.14),
        "body": (0.38, 0.5, 0.45),
        "body_z": 0.78,
        "leg_height": 0.38,
        "tail_length": 1.05,
        "eye": 0.075,
        "teeth": 6,
        "wardrobe": ["ExplorerCap", "ExplorerVest", "ExplorerPack"],
        "expression": "bright_confident_runner",
        "brow_tilt": -0.08,
        "spine_size": 0.12,
    },
    "tyrannosaurus_adult": {
        "profile": "adult_broad_expedition",
        "target_height": 2.1,
        "head": (0.4, 0.47, 0.32),
        "head_z": 1.48,
        "muzzle_y": -0.75,
        "muzzle": (0.35, 0.42, 0.16),
        "body": (0.47, 0.62, 0.55),
        "body_z": 0.91,
        "leg_height": 0.49,
        "tail_length": 1.35,
        "eye": 0.068,
        "teeth": 8,
        "wardrobe": ["ExplorerCap", "ExpeditionJacket", "ExplorerPack", "PackRoll"],
        "expression": "focused_expedition_guardian",
        "brow_tilt": -0.15,
        "spine_size": 0.155,
    },
}

TYRANNOSAUR_POLISH_PARTS = {
    "tyrannosaurus_baby": (
        "UpperMuzzle", "BrowRidgeLeft", "BrowRidgeRight", "CheekPlateLeft", "CheekPlateRight",
        "DorsalSpine01", "DorsalSpine02", "DorsalSpine03",
        "FingerClawLeft01", "FingerClawRight01", "CapBand", "CapBadge",
    ),
    "tyrannosaurus_teen": (
        "UpperMuzzle", "BrowRidgeLeft", "BrowRidgeRight", "CheekPlateLeft", "CheekPlateRight",
        "DorsalSpine01", "DorsalSpine02", "DorsalSpine03",
        "FingerClawLeft01", "FingerClawRight01", "VestCollar", "BackpackFlap",
        "BackpackStrapLeft", "BackpackStrapRight",
    ),
    "tyrannosaurus_adult": (
        "UpperMuzzle", "BrowRidgeLeft", "BrowRidgeRight", "CheekPlateLeft", "CheekPlateRight",
        "DorsalSpine01", "DorsalSpine02", "DorsalSpine03",
        "FingerClawLeft01", "FingerClawRight01", "JacketCollar", "JacketBelt",
        "BackpackFlap", "PackRollStrap",
    ),
}


def tyrannosaur_contract_report() -> dict[str, Any]:
    return {
        "status": "TYRANNOSAUR_CONTRACT_OK",
        "ids": list(STAGE_PROFILES),
        "profiles": {key: value["profile"] for key, value in STAGE_PROFILES.items()},
        "targetHeightsM": [value["target_height"] for value in STAGE_PROFILES.values()],
        "wardrobe": {key: list(value["wardrobe"]) for key, value in STAGE_PROFILES.items()},
        "uniformStageScaling": False,
        "namedParts": list(TYRANNOSAUR_NAMED_PARTS),
        "stockPrimitiveOperators": False,
        "facialConstruction": "articulated_upper_muzzle_and_lower_jaw",
        "surfaceDetail": "staged_dorsal_spines_and_markings",
        "expressions": {key: value["expression"] for key, value in STAGE_PROFILES.items()},
        "polishParts": {key: list(value) for key, value in TYRANNOSAUR_POLISH_PARTS.items()},
    }


def _parent(obj: Any, root: Any) -> Any:
    obj.parent = root
    return obj


def _color(obj: Any, material: Any, keys: list[str], key: str, fallback: int = 0) -> Any:
    assign_palette_region(obj, material, palette_index(keys, key, fallback), len(keys))
    obj["bigimong_palette_region"] = key
    return obj


def _ellipsoid(
    name: str,
    collection: Any,
    root: Any,
    material: Any,
    keys: list[str],
    color_key: str,
    location: tuple[float, float, float],
    radii: tuple[float, float, float],
    *,
    rings: int = 20,
    segments: int = 32,
) -> Any:
    obj = create_ellipsoid_mesh(name, collection, location=location, radii=radii, rings=rings, segments=segments)
    obj.parent = root
    return _color(obj, material, keys, color_key)


def _build_teeth(
    count: int,
    collection: Any,
    root: Any,
    material: Any,
    keys: list[str],
    muzzle_y: float,
    jaw_z: float,
) -> list[Any]:
    teeth = []
    upper_count = max(2, count // 2)
    positions = [(-0.17 + index * 0.34 / max(1, upper_count - 1)) for index in range(upper_count)]
    for index, x_value in enumerate(positions):
        upper = _ellipsoid(
            f"ToothUpper{index + 1}", collection, root, material, keys, "teeth",
            (x_value, muzzle_y - 0.2, jaw_z + 0.05), (0.028, 0.025, 0.055), rings=8, segments=12,
        )
        lower = _ellipsoid(
            f"ToothLower{index + 1}", collection, root, material, keys, "teeth",
            (x_value * 0.88, muzzle_y - 0.21, jaw_z - 0.035), (0.025, 0.022, 0.047), rings=8, segments=12,
        )
        teeth.extend((upper, lower))
    return teeth[:count]


def _build_base(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path, profile: dict[str, Any]) -> CharacterBuild:
    palette = dict(job["palette"])
    palette.update({
        "eyeWhite": "#FFF8E9",
        "pupil": "#241510",
        "catchlight": "#FFFFFF",
        "mouth": "#8C3C3A",
        "nostril": "#752620",
        "claw": "#F6E1BD",
        "bodyShadow": "#A92424",
        "dorsal": "#B72A28",
        "mouthDark": "#5B2026",
        "accessoryEdge": "#3E3028",
    })
    atlas_path = texture_dir / f"{job['outputStem']}_Atlas.png"
    material, atlas, keys = create_atlas_material(job["outputStem"], palette, atlas_path)
    skin: list[Any] = []
    rigid: list[Any] = []

    body = _ellipsoid("Body", collection, root, material, keys, "body", (0.0, 0.08, profile["body_z"]), profile["body"], rings=20, segments=32)
    chest = _ellipsoid("Chest", collection, root, material, keys, "bodyHighlight", (0.0, -0.22, profile["body_z"] + 0.18), (profile["body"][0] * 0.83, 0.35, profile["body"][2] * 0.72), rings=18, segments=28)
    neck = _ellipsoid("Neck", collection, root, material, keys, "body", (0.0, -0.27, profile["head_z"] - 0.27), (profile["head"][0] * 0.7, 0.24, 0.32), rings=16, segments=24)
    head = _ellipsoid("Head", collection, root, material, keys, "bodyHighlight", (0.0, -0.42, profile["head_z"]), profile["head"], rings=20, segments=32)
    muzzle = _ellipsoid("UpperMuzzle", collection, root, material, keys, "bodyHighlight", (0.0, profile["muzzle_y"], profile["head_z"] - 0.005), profile["muzzle"], rings=18, segments=28)
    jaw_z = profile["head_z"] - profile["muzzle"][2] * 0.92
    jaw = _ellipsoid("Jaw", collection, root, material, keys, "underside", (0.0, profile["muzzle_y"] - 0.005, jaw_z - 0.018), (profile["muzzle"][0] * 0.9, profile["muzzle"][1] * 0.84, profile["muzzle"][2] * 0.48), rings=14, segments=24)
    belly = _ellipsoid("CreamBelly", collection, root, material, keys, "underside", (0.0, -profile["body"][1] * 0.77, profile["body_z"] - 0.02), (profile["body"][0] * 0.63, 0.035, profile["body"][2] * 0.73), rings=14, segments=24)
    skin.extend((body, chest, neck, head, muzzle, jaw))
    rigid.append(belly)

    mouth_cavity = _ellipsoid(
        "MouthCavity", collection, root, material, keys, "mouthDark",
        (0.0, profile["muzzle_y"] - profile["muzzle"][1] * 0.94, jaw_z + 0.045),
        (profile["muzzle"][0] * 0.76, 0.016, 0.038), rings=10, segments=20,
    )
    snout_bridge = _ellipsoid(
        "SnoutBridge", collection, root, material, keys, "body",
        (0.0, profile["muzzle_y"] + 0.035, profile["head_z"] + profile["muzzle"][2] * 0.72),
        (profile["muzzle"][0] * 0.52, profile["muzzle"][1] * 0.62, profile["muzzle"][2] * 0.38),
        rings=12, segments=22,
    )
    rigid.extend((mouth_cavity, snout_bridge))

    for side, x_sign in (("Left", -1.0), ("Right", 1.0)):
        cheek = _ellipsoid(
            f"CheekPlate{side}", collection, root, material, keys, "body",
            (x_sign * profile["head"][0] * 0.72, -0.58, profile["head_z"] - 0.035),
            (profile["head"][0] * 0.18, 0.06, profile["head"][2] * 0.28), rings=10, segments=18,
        )
        rigid.append(cheek)

    spine_locations = (
        (-0.27, profile["head_z"] + profile["head"][2] * 0.76),
        (-0.08, profile["head_z"] - 0.02),
        (0.16, profile["body_z"] + profile["body"][2] * 0.86),
        (0.40, profile["body_z"] + profile["body"][2] * 0.68),
        (0.66, profile["body_z"] + profile["body"][2] * 0.43),
    )
    for index, (y_value, z_value) in enumerate(spine_locations, start=1):
        taper = 1.0 - (index - 1) * 0.09
        spine = create_fin_mesh(
            f"DorsalSpine{index:02d}", collection,
            location=(0.0, y_value, z_value),
            width=profile["spine_size"] * 0.78 * taper,
            depth=profile["spine_size"] * 1.08,
            height=profile["spine_size"] * taper,
        )
        _parent(spine, root)
        _color(spine, material, keys, "dorsal")
        rigid.append(spine)

    for side, x_sign in (("Left", -1.0), ("Right", 1.0)):
        for index, (y_value, z_offset, size) in enumerate(((0.0, 0.18, 0.07), (0.18, 0.04, 0.055), (0.34, -0.08, 0.045)), start=1):
            marking = _ellipsoid(
                f"SkinMark{side}{index:02d}", collection, root, material, keys, "bodyShadow",
                (x_sign * profile["body"][0] * 0.94, y_value, profile["body_z"] + z_offset),
                (0.016, size * 1.18, size), rings=7, segments=12,
            )
            rigid.append(marking)

    if profile["spine_size"] >= 0.12:
        for side, x_sign in (("Left", -1.0), ("Right", 1.0)):
            horn = create_fin_mesh(
                f"Horn{side}", collection,
                location=(x_sign * profile["head"][0] * 0.48, -0.40, profile["head_z"] + profile["head"][2] * 0.82),
                width=profile["spine_size"] * 0.68,
                depth=profile["spine_size"] * 0.74,
                height=profile["spine_size"] * 0.9,
            )
            horn.rotation_euler[1] = x_sign * 0.24
            _parent(horn, root)
            _color(horn, material, keys, "claw")
            rigid.append(horn)

    tail = create_tapered_tube_mesh(
        "Tail", collection,
        [(0.0, 0.35, profile["body_z"]), (0.0, 0.65, profile["body_z"] - 0.04), (0.0, 0.35 + profile["tail_length"] * 0.72, profile["body_z"] - 0.2), (0.0, 0.35 + profile["tail_length"], profile["body_z"] - 0.32)],
        [(profile["body"][0] * 0.58, profile["body"][2] * 0.55), (0.25, 0.25), (0.12, 0.13), (0.025, 0.035)],
        segments=32,
    )
    _parent(tail, root)
    _color(tail, material, keys, "body")
    skin.append(tail)

    leg_x = profile["body"][0] * 0.58
    for side, x_sign in (("Left", -1.0), ("Right", 1.0)):
        thigh = _ellipsoid(
            f"Leg{side}", collection, root, material, keys, "body",
            (x_sign * leg_x, 0.05, profile["leg_height"] + 0.2),
            (profile["body"][0] * 0.34, 0.23, profile["leg_height"] * 0.72), rings=18, segments=28,
        )
        shin = _ellipsoid(
            f"Shin{side}", collection, root, material, keys, "bodyHighlight",
            (x_sign * leg_x, -0.055, profile["leg_height"] * 0.52),
            (profile["body"][0] * 0.22, 0.15, profile["leg_height"] * 0.58), rings=14, segments=22,
        )
        foot = _ellipsoid(
            f"Foot{side}", collection, root, material, keys, "bodyHighlight",
            (x_sign * leg_x, -0.19, 0.09), (profile["body"][0] * 0.28, 0.3, 0.09), rings=14, segments=24,
        )
        skin.extend((thigh, shin, foot))
        for toe in (-1, 0, 1):
            claw = _ellipsoid(
                f"Claw{side}{toe + 2}", collection, root, material, keys, "claw",
                (x_sign * leg_x + toe * 0.065, -0.44, 0.075), (0.025, 0.07, 0.025), rings=6, segments=10,
            )
            rigid.append(claw)

        arm_z = profile["body_z"] + 0.23
        arm_radius = profile["body"][0] * 0.075
        arm = create_tapered_tube_mesh(
            f"Arm{side}",
            collection,
            [
                (x_sign * profile["body"][0] * 0.72, -0.3, arm_z),
                (x_sign * profile["body"][0] * 0.86, -0.46, arm_z - 0.08),
                (x_sign * profile["body"][0] * 0.74, -0.55, arm_z - 0.13),
            ],
            [
                (arm_radius, arm_radius),
                (arm_radius * 0.86, arm_radius * 0.86),
                (arm_radius * 0.68, arm_radius * 0.68),
            ],
            segments=28,
        )
        _parent(arm, root)
        _color(arm, material, keys, "bodyHighlight")
        skin.append(arm)
        hand_x = x_sign * profile["body"][0] * 0.74
        hand_y = -0.555
        hand_z = arm_z - 0.13
        palm = _ellipsoid(
            f"Palm{side}", collection, root, material, keys, "bodyHighlight",
            (hand_x, hand_y, hand_z), (arm_radius * 1.08, arm_radius * 0.72, arm_radius * 0.88),
            rings=8, segments=14,
        )
        rigid.append(palm)
        for claw_index, x_offset in enumerate((-arm_radius * 0.58, arm_radius * 0.58), start=1):
            finger_claw = _ellipsoid(
                f"FingerClaw{side}{claw_index:02d}", collection, root, material, keys, "claw",
                (hand_x + x_offset, hand_y - arm_radius * 0.84, hand_z - arm_radius * 0.08),
                (arm_radius * 0.24, arm_radius * 0.55, arm_radius * 0.19), rings=6, segments=10,
            )
            rigid.append(finger_claw)

    eye_x = profile["head"][0] * 0.61
    for side, x_value in (("Left", -eye_x), ("Right", eye_x)):
        white = _ellipsoid(
            f"EyeWhite{side}", collection, root, material, keys, "eyeWhite",
            (x_value, -0.665, profile["head_z"] + 0.08), (profile["eye"], 0.035, profile["eye"] * 1.2), rings=14, segments=22,
        )
        iris = _ellipsoid(
            f"Iris{side}", collection, root, material, keys, "iris",
            (x_value, -0.695, profile["head_z"] + 0.075), (profile["eye"] * 0.57, 0.015, profile["eye"] * 0.72), rings=10, segments=18,
        )
        pupil = _ellipsoid(
            f"Pupil{side}", collection, root, material, keys, "pupil",
            (x_value, -0.707, profile["head_z"] + 0.075), (profile["eye"] * 0.28, 0.009, profile["eye"] * 0.4), rings=8, segments=14,
        )
        glint = _ellipsoid(
            f"Catchlight{side}", collection, root, material, keys, "catchlight",
            (x_value - 0.012, -0.714, profile["head_z"] + 0.105), (0.012, 0.006, 0.015), rings=6, segments=10,
        )
        brow = _ellipsoid(
            f"BrowRidge{side}", collection, root, material, keys, "bodyShadow",
            (x_value, -0.665, profile["head_z"] + profile["eye"] * 1.92),
            (profile["eye"] * 1.18, 0.032, profile["eye"] * 0.28), rings=8, segments=16,
        )
        brow.rotation_euler[1] = profile["brow_tilt"] * (-1.0 if x_value < 0 else 1.0)
        rigid.extend((white, iris, pupil, glint, brow))

    left_nostril = _ellipsoid("NostrilLeft", collection, root, material, keys, "nostril", (-0.095, profile["muzzle_y"] - profile["muzzle"][1] * 0.88, profile["head_z"] + 0.015), (0.025, 0.012, 0.015), rings=8, segments=14)
    right_nostril = _ellipsoid("NostrilRight", collection, root, material, keys, "nostril", (0.095, profile["muzzle_y"] - profile["muzzle"][1] * 0.88, profile["head_z"] + 0.015), (0.025, 0.012, 0.015), rings=8, segments=14)
    teeth = _build_teeth(profile["teeth"], collection, root, material, keys, profile["muzzle_y"], jaw_z)
    rigid.extend((left_nostril, right_nostril, *teeth))

    cap_brim = create_profile_mesh("ExplorerCap", [(-0.018, 1.0), (0.018, 1.0)], collection, segments=40, radial_scale=(profile["head"][0] * 0.92, profile["head"][1] * 0.77))
    cap_brim.location = (0.0, -0.39, profile["head_z"] + profile["head"][2] * 0.82)
    _parent(cap_brim, root)
    _color(cap_brim, material, keys, "cap")
    cap_crown = _ellipsoid("ExplorerCapCrown", collection, root, material, keys, "cap", (0.0, -0.37, profile["head_z"] + profile["head"][2] * 0.93), (profile["head"][0] * 0.62, profile["head"][1] * 0.56, profile["head"][2] * 0.27), rings=12, segments=22)
    cap_band = create_profile_mesh(
        "CapBand", [(-0.014, 1.0), (0.014, 1.0)], collection,
        segments=24, radial_scale=(profile["head"][0] * 0.65, profile["head"][1] * 0.59),
    )
    cap_band.location = (0.0, -0.37, profile["head_z"] + profile["head"][2] * 0.885)
    _parent(cap_band, root)
    _color(cap_band, material, keys, "accessoryEdge")
    cap_badge = _ellipsoid(
        "CapBadge", collection, root, material, keys, "claw",
        (0.0, -0.37 - profile["head"][1] * 0.58, profile["head_z"] + profile["head"][2] * 0.98),
        (0.035, 0.012, 0.035), rings=8, segments=14,
    )
    rigid.extend((cap_brim, cap_crown, cap_band, cap_badge))

    return CharacterBuild(
        root=root,
        skin_meshes=skin,
        rigid_parts=rigid,
        required_parts=list(job["requiredParts"]),
        materials=[material],
        kind="tyrannosaur",
        metadata={
            "atlas": atlas,
            "atlas_path": str(atlas_path),
            "palette_keys": keys,
            "named_parts": list(dict.fromkeys([*TYRANNOSAUR_NAMED_PARTS, *job["requiredParts"]])),
            "profile": profile["profile"],
            "stage": job["id"].removeprefix("tyrannosaurus_"),
            "inferred_rear": True,
        },
    )


def _add_teen_wardrobe(character: CharacterBuild, collection: Any) -> None:
    root, material, keys = character.root, character.materials[0], character.metadata["palette_keys"]
    vest = create_profile_mesh("ExplorerVest", [(-0.3, 0.92), (0.08, 1.03), (0.31, 0.86)], collection, segments=28, radial_scale=(0.36, 0.39))
    vest.location = (0.0, 0.01, 0.81)
    _parent(vest, root)
    _color(vest, material, keys, "outerwear")
    pack = _ellipsoid("ExplorerPack", collection, root, material, keys, "pack", (0.0, 0.49, 0.9), (0.25, 0.15, 0.27), rings=14, segments=24)
    collar = create_profile_mesh(
        "VestCollar", [(-0.016, 1.0), (0.016, 1.0)], collection,
        segments=24, radial_scale=(0.285, 0.265),
    )
    collar.location = (0.0, -0.03, 1.105)
    _parent(collar, root)
    _color(collar, material, keys, "accessoryEdge")
    flap = _ellipsoid(
        "BackpackFlap", collection, root, material, keys, "accessoryEdge",
        (0.0, 0.635, 0.99), (0.19, 0.026, 0.095), rings=8, segments=16,
    )
    straps = []
    for side, x_value in (("Left", -0.205), ("Right", 0.205)):
        strap = create_curve_tube(
            f"BackpackStrap{side}", collection,
            [(x_value, 0.29, 1.08), (x_value * 1.08, 0.37, 0.91), (x_value * 0.92, 0.34, 0.72)],
            radius=0.018, resolution=2, bevel_resolution=2,
        )
        _parent(strap, root)
        _color(strap, material, keys, "accessoryEdge")
        straps.append(strap)
    character.rigid_parts.extend((vest, pack, collar, flap, *straps))


def _add_adult_wardrobe(character: CharacterBuild, collection: Any) -> None:
    root, material, keys = character.root, character.materials[0], character.metadata["palette_keys"]
    jacket = create_profile_mesh("ExpeditionJacket", [(-0.37, 0.93), (0.1, 1.04), (0.38, 0.88)], collection, segments=32, radial_scale=(0.45, 0.5))
    jacket.location = (0.0, 0.02, 0.94)
    _parent(jacket, root)
    _color(jacket, material, keys, "outerwear")
    pack = _ellipsoid("ExplorerPack", collection, root, material, keys, "pack", (0.0, 0.61, 1.02), (0.31, 0.17, 0.34), rings=16, segments=26)
    roll = create_profile_mesh("PackRoll", [(-0.24, 0.82), (0.24, 0.82)], collection, segments=24, radial_scale=(0.18, 0.18))
    roll.location = (0.0, 0.72, 1.28)
    roll.rotation_euler[1] = 1.57079632679
    _parent(roll, root)
    _color(roll, material, keys, "rollMat")
    collar = create_profile_mesh(
        "JacketCollar", [(-0.018, 1.0), (0.018, 1.0)], collection,
        segments=26, radial_scale=(0.35, 0.32),
    )
    collar.location = (0.0, -0.02, 1.255)
    _parent(collar, root)
    _color(collar, material, keys, "accessoryEdge")
    belt = create_profile_mesh(
        "JacketBelt", [(-0.018, 1.0), (0.018, 1.0)], collection,
        segments=28, radial_scale=(0.455, 0.50),
    )
    belt.location = (0.0, 0.02, 0.885)
    _parent(belt, root)
    _color(belt, material, keys, "accessoryEdge")
    flap = _ellipsoid(
        "BackpackFlap", collection, root, material, keys, "accessoryEdge",
        (0.0, 0.775, 1.13), (0.235, 0.026, 0.115), rings=8, segments=16,
    )
    roll_strap = create_profile_mesh(
        "PackRollStrap", [(-0.055, 1.0), (0.055, 1.0)], collection,
        segments=18, radial_scale=(0.19, 0.19),
    )
    roll_strap.location = (0.0, 0.72, 1.28)
    roll_strap.rotation_euler[1] = 1.57079632679
    _parent(roll_strap, root)
    _color(roll_strap, material, keys, "accessoryEdge")
    character.rigid_parts.extend((jacket, pack, roll, collar, belt, flap, roll_strap))


def build_baby(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path) -> CharacterBuild:
    return _build_base(job, collection, root, texture_dir, STAGE_PROFILES["tyrannosaurus_baby"])


def build_teen(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path) -> CharacterBuild:
    character = _build_base(job, collection, root, texture_dir, STAGE_PROFILES["tyrannosaurus_teen"])
    _add_teen_wardrobe(character, collection)
    return character


def build_adult(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path) -> CharacterBuild:
    character = _build_base(job, collection, root, texture_dir, STAGE_PROFILES["tyrannosaurus_adult"])
    _add_adult_wardrobe(character, collection)
    return character


def build_tyrannosaur(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path) -> CharacterBuild:
    builders = {
        "tyrannosaurus_baby": build_baby,
        "tyrannosaurus_teen": build_teen,
        "tyrannosaurus_adult": build_adult,
    }
    try:
        return builders[job["id"]](job, collection, root, texture_dir)
    except KeyError as error:
        raise ValueError(f"unsupported Tyrannosaurus job: {job['id']}") from error
