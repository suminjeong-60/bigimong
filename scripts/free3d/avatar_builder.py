"""Reference-led masculine and feminine Bigimong avatar mesh builders."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from free3d.geometry import (
    CharacterBuild,
    create_curve_tube,
    create_ellipsoid_mesh,
    create_profile_mesh,
    create_tapered_tube_mesh,
)
from free3d.materials import assign_palette_region, create_atlas_material, palette_index


AVATAR_NAMED_PARTS = (
    "Head",
    "EyeWhiteLeft",
    "EyeWhiteRight",
    "IrisLeft",
    "IrisRight",
    "Hair",
    "Body",
    "HandLeft",
    "HandRight",
    "FootLeft",
    "FootRight",
    "NecklaceChain",
    "SummoningMedallion",
    "MedallionSocket",
)

AVATAR_PROFILES = {
    "avatar_male": {
        "profile": "masculine_swept_five_lock",
        "shoulder": 0.245,
        "waist": 0.165,
        "head_width": 0.315,
        "brow_radius": 0.022,
        "hair": "swept",
    },
    "avatar_female": {
        "profile": "feminine_bob_curled_tip",
        "shoulder": 0.218,
        "waist": 0.148,
        "head_width": 0.305,
        "brow_radius": 0.015,
        "hair": "bob",
    },
}

AVATAR_POLISH_PARTS = {
    "avatar_male": (
        "EarLeft", "EarRight", "EyelidLeft", "EyelidRight",
        "PalmLeft", "PalmRight", "ThumbLeft", "ThumbRight",
        "TopCollar", "TopHem", "ShortsWaistband", "MedallionInset",
        "HairCrownShell", "HairSweptClump01", "HairSweptClump02", "HairSweptClump03",
    ),
    "avatar_female": (
        "EarLeft", "EarRight", "EyelidLeft", "EyelidRight",
        "PalmLeft", "PalmRight", "ThumbLeft", "ThumbRight",
        "TopCollar", "TopHem", "ShortsWaistband", "MedallionInset",
        "HairBobShell", "HairFringeClump", "HairCurlLeft", "HairCurlRight",
    ),
}


def avatar_contract_report() -> dict[str, Any]:
    return {
        "status": "AVATAR_CONTRACT_OK",
        "ids": list(AVATAR_PROFILES),
        "profiles": {key: value["profile"] for key, value in AVATAR_PROFILES.items()},
        "namedParts": list(AVATAR_NAMED_PARTS),
        "detachableMedallion": True,
        "stockPrimitiveOperators": False,
        "hairConstruction": {
            "avatar_male": "layered_swept_clumps",
            "avatar_female": "layered_bob_clumps",
        },
        "polishParts": {key: list(value) for key, value in AVATAR_POLISH_PARTS.items()},
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
    obj = create_ellipsoid_mesh(
        name,
        collection,
        location=location,
        radii=radii,
        rings=rings,
        segments=segments,
    )
    _parent(obj, root)
    return _color(obj, material, keys, color_key)


def _curve(
    name: str,
    collection: Any,
    root: Any,
    material: Any,
    keys: list[str],
    color_key: str,
    points: list[tuple[float, float, float]],
    radius: float,
) -> Any:
    obj = create_curve_tube(name, collection, points, radius=radius, resolution=3, bevel_resolution=3)
    _parent(obj, root)
    return _color(obj, material, keys, color_key)


def _build_shared(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path, profile: dict[str, Any]) -> CharacterBuild:
    import bpy  # type: ignore

    palette = dict(job["palette"])
    palette.update({
        "eyeWhite": "#FFF8EB",
        "pupil": "#241713",
        "catchlight": "#FFFFFF",
        "blush": "#EC806F",
        "mouth": "#71362F",
        "hairHighlight": "#6A3C30",
        "hairShadow": "#2B1715",
        "clothingEdge": "#22242A",
        "medallionInset": "#8D531D",
    })
    atlas_path = texture_dir / f"{job['outputStem']}_Atlas.png"
    material, atlas, keys = create_atlas_material(job["outputStem"], palette, atlas_path)

    skin: list[Any] = []
    rigid: list[Any] = []
    head = _ellipsoid(
        "Head", collection, root, material, keys, "skin", (0.0, 0.0, 1.65),
        (profile["head_width"], 0.255, 0.335), rings=24, segments=40,
    )
    skin.append(head)
    neck = _ellipsoid("Neck", collection, root, material, keys, "skin", (0.0, 0.0, 1.31), (0.09, 0.075, 0.14))
    skin.append(neck)
    for side, x_sign in (("Left", -1.0), ("Right", 1.0)):
        ear = _ellipsoid(
            f"Ear{side}", collection, root, material, keys, "skin",
            (x_sign * profile["head_width"] * 0.97, -0.005, 1.65),
            (0.055, 0.035, 0.082), rings=12, segments=20,
        )
        ear_inner = _ellipsoid(
            f"EarInner{side}", collection, root, material, keys, "blush",
            (x_sign * profile["head_width"] * 1.005, -0.032, 1.65),
            (0.027, 0.012, 0.048), rings=8, segments=14,
        )
        rigid.extend((ear, ear_inner))

    body = create_profile_mesh(
        "Body",
        [(-0.34, 0.78), (-0.28, 0.92), (0.02, 1.0), (0.25, 1.12), (0.34, 0.9)],
        collection,
        segments=40,
        radial_scale=(profile["shoulder"], 0.145),
    )
    body.location.z = 1.06
    _parent(body, root)
    _color(body, material, keys, "clothing")
    rigid.append(body)

    shorts = create_profile_mesh(
        "Shorts", [(-0.17, 0.92), (0.04, 1.0), (0.16, 0.98)], collection,
        segments=36, radial_scale=(profile["waist"], 0.14),
    )
    shorts.location.z = 0.76
    _parent(shorts, root)
    _color(shorts, material, keys, "clothing")
    rigid.append(shorts)

    top_collar = create_profile_mesh(
        "TopCollar", [(-0.014, 1.0), (0.014, 1.0)], collection,
        segments=28, radial_scale=(0.132, 0.112),
    )
    top_collar.location.z = 1.285
    _parent(top_collar, root)
    _color(top_collar, material, keys, "clothingEdge")
    top_hem = create_profile_mesh(
        "TopHem", [(-0.012, 1.0), (0.012, 1.0)], collection,
        segments=28, radial_scale=(profile["waist"] * 1.07, 0.145),
    )
    top_hem.location.z = 0.88
    _parent(top_hem, root)
    _color(top_hem, material, keys, "clothingEdge")
    waistband = create_profile_mesh(
        "ShortsWaistband", [(-0.014, 1.0), (0.014, 1.0)], collection,
        segments=28, radial_scale=(profile["waist"] * 1.08, 0.145),
    )
    waistband.location.z = 0.91
    _parent(waistband, root)
    _color(waistband, material, keys, "clothingEdge")
    rigid.extend((top_collar, top_hem, waistband))

    for side, x_value in (("Left", -1.0), ("Right", 1.0)):
        arm = _ellipsoid(
            f"Arm{side}", collection, root, material, keys, "skin",
            (x_value * (profile["shoulder"] + 0.095), 0.0, 1.02), (0.075, 0.075, 0.34),
        )
        hand = _ellipsoid(
            f"Hand{side}", collection, root, material, keys, "skin",
            (x_value * (profile["shoulder"] + 0.105), -0.005, 0.65), (0.082, 0.065, 0.11),
            rings=16, segments=28,
        )
        leg = _ellipsoid(
            f"Leg{side}", collection, root, material, keys, "skin",
            (x_value * 0.105, 0.0, 0.39), (0.088, 0.09, 0.38),
        )
        foot = _ellipsoid(
            f"Foot{side}", collection, root, material, keys, "skin",
            (x_value * 0.11, -0.055, 0.075), (0.105, 0.17, 0.075), rings=16, segments=28,
        )
        skin.extend((arm, hand, leg, foot))
        palm = _ellipsoid(
            f"Palm{side}", collection, root, material, keys, "skinHighlight",
            (x_value * (profile["shoulder"] + 0.105), -0.062, 0.65),
            (0.064, 0.018, 0.078), rings=10, segments=18,
        )
        thumb = _ellipsoid(
            f"Thumb{side}", collection, root, material, keys, "skin",
            (x_value * (profile["shoulder"] + 0.055), -0.07, 0.645),
            (0.022, 0.026, 0.05), rings=8, segments=14,
        )
        rigid.extend((palm, thumb))
        for finger_index, x_offset in enumerate((-0.035, 0.0, 0.035), start=1):
            finger = _ellipsoid(
                f"Finger{side}{finger_index:02d}", collection, root, material, keys, "skin",
                (x_value * (profile["shoulder"] + 0.105) + x_offset, -0.069, 0.585),
                (0.017, 0.022, 0.043), rings=7, segments=12,
            )
            rigid.append(finger)
        for toe_index, x_offset in enumerate((-0.045, 0.0, 0.045), start=1):
            toe = _ellipsoid(
                f"Toe{side}{toe_index:02d}", collection, root, material, keys, "skinHighlight",
                (x_value * 0.11 + x_offset, -0.205, 0.055),
                (0.025, 0.04, 0.025), rings=7, segments=12,
            )
            rigid.append(toe)

    eye_x = 0.118
    for side, x_value in (("Left", -eye_x), ("Right", eye_x)):
        white = _ellipsoid(
            f"EyeWhite{side}", collection, root, material, keys, "eyeWhite",
            (x_value, -0.235, 1.7), (0.093, 0.035, 0.112), rings=18, segments=28,
        )
        iris = _ellipsoid(
            f"Iris{side}", collection, root, material, keys, "iris",
            (x_value, -0.267, 1.695), (0.052, 0.014, 0.066), rings=14, segments=24,
        )
        pupil = _ellipsoid(
            f"Pupil{side}", collection, root, material, keys, "pupil",
            (x_value, -0.278, 1.695), (0.026, 0.009, 0.036), rings=10, segments=20,
        )
        glint = _ellipsoid(
            f"Catchlight{side}", collection, root, material, keys, "catchlight",
            (x_value - 0.014, -0.286, 1.723), (0.012, 0.006, 0.015), rings=8, segments=16,
        )
        eyelid = _curve(
            f"Eyelid{side}", collection, root, material, keys, "hairShadow",
            [
                (x_value - 0.07, -0.284, 1.755),
                (x_value, -0.294, 1.785),
                (x_value + 0.07, -0.284, 1.755),
            ],
            0.008,
        )
        rigid.extend((white, iris, pupil, glint, eyelid))

    nose = _ellipsoid("Nose", collection, root, material, keys, "skinHighlight", (0.0, -0.265, 1.58), (0.045, 0.045, 0.05), rings=14, segments=24)
    left_cheek = _ellipsoid("CheekLeft", collection, root, material, keys, "blush", (-0.205, -0.222, 1.56), (0.055, 0.016, 0.035), rings=10, segments=20)
    right_cheek = _ellipsoid("CheekRight", collection, root, material, keys, "blush", (0.205, -0.222, 1.56), (0.055, 0.016, 0.035), rings=10, segments=20)
    smile = _curve("Smile", collection, root, material, keys, "mouth", [(-0.07, -0.268, 1.505), (0.0, -0.282, 1.485), (0.07, -0.268, 1.505)], 0.009)
    rigid.extend((nose, left_cheek, right_cheek, smile))

    for side, x_value in (("Left", -1.0), ("Right", 1.0)):
        brow = _curve(
            f"Brow{side}", collection, root, material, keys, "hair",
            [(x_value * 0.195, -0.263, 1.82), (x_value * 0.12, -0.284, 1.842), (x_value * 0.055, -0.27, 1.825)],
            profile["brow_radius"],
        )
        rigid.append(brow)

    chain = _curve(
        "NecklaceChain", collection, root, material, keys, "medallion",
        [(-0.09, -0.142, 1.29), (-0.065, -0.175, 1.16), (0.0, -0.19, 1.1), (0.065, -0.175, 1.16), (0.09, -0.142, 1.29)],
        0.008,
    )
    medallion = _ellipsoid(
        "SummoningMedallion", collection, root, material, keys, "medallion",
        (0.0, -0.205, 1.075), (0.065, 0.018, 0.065), rings=16, segments=28,
    )
    medallion["detachable"] = True
    medallion_inset = _ellipsoid(
        "MedallionInset", collection, root, material, keys, "medallionInset",
        (0.0, -0.225, 1.075), (0.037, 0.008, 0.037), rings=10, segments=18,
    )
    medallion_inset["detachable"] = True
    rune = _curve(
        "MedallionRune", collection, root, material, keys, "medallion",
        [(0.0, -0.234, 1.101), (-0.019, -0.236, 1.067), (0.019, -0.236, 1.067), (0.0, -0.234, 1.101)],
        0.004,
    )
    rune["detachable"] = True
    socket = bpy.data.objects.new("MedallionSocket", None)
    collection.objects.link(socket)
    socket.location = (0.0, -0.205, 1.075)
    socket.parent = root
    rigid.extend((chain, medallion, medallion_inset, rune, socket))

    return CharacterBuild(
        root=root,
        skin_meshes=skin,
        rigid_parts=rigid,
        required_parts=list(job["requiredParts"]),
        materials=[material],
        kind="avatar",
        metadata={
            "atlas": atlas,
            "atlas_path": str(atlas_path),
            "palette_keys": keys,
            "named_parts": list(dict.fromkeys([*AVATAR_NAMED_PARTS, *job["requiredParts"]])),
            "profile": profile["profile"],
            "inferred_rear": True,
        },
    )


def _add_masculine_hair(character: CharacterBuild, collection: Any, profile: dict[str, Any]) -> None:
    import bpy  # type: ignore

    root = character.root
    material = character.materials[0]
    keys = character.metadata["palette_keys"]
    hair_root = bpy.data.objects.new("Hair", None)
    collection.objects.link(hair_root)
    hair_root.parent = root
    crown = _ellipsoid(
        "HairCrownShell", collection, root, material, keys, "hairShadow",
        (0.0, 0.09, 1.85), (0.30, 0.19, 0.255), rings=16, segments=28,
    )
    character.rigid_parts.append(crown)
    starts = (-0.235, -0.135, -0.035, 0.075, 0.175)
    sweep = (0.105, 0.125, 0.14, 0.14, 0.12)
    for index, (start, shift) in enumerate(zip(starts, sweep), start=1):
        clump = create_tapered_tube_mesh(
            f"HairSweptClump{index:02d}",
            collection,
            [
                (start, 0.085, 1.91 + 0.018 * (index % 2)),
                (start + shift * 0.38, -0.04, 2.015 + 0.012 * (index % 2)),
                (start + shift * 0.78, -0.17, 2.005 - 0.018 * index),
                (start + shift, -0.245, 1.87 - 0.015 * index),
            ],
            [(0.062, 0.055), (0.068, 0.058), (0.055, 0.045), (0.018, 0.022)],
            segments=18,
        )
        clump.parent = hair_root
        shade = "hairHighlight" if index in (2, 3) else "hair"
        _color(clump, material, keys, shade)
        character.rigid_parts.append(clump)


def _add_feminine_hair(character: CharacterBuild, collection: Any, profile: dict[str, Any]) -> None:
    import bpy  # type: ignore

    root = character.root
    material = character.materials[0]
    keys = character.metadata["palette_keys"]
    hair_root = bpy.data.objects.new("Hair", None)
    collection.objects.link(hair_root)
    hair_root.parent = root
    bob = create_profile_mesh(
        "HairBobShell", [(-0.24, 0.9), (0.0, 1.02), (0.2, 1.0), (0.32, 0.72)], collection,
        segments=36, radial_scale=(0.33, 0.285),
    )
    bob.location = (0.0, 0.055, 1.73)
    bob.parent = hair_root
    _color(bob, material, keys, "hair")
    character.rigid_parts.append(bob)
    fringe = create_tapered_tube_mesh(
        "HairFringeClump", collection,
        [
            (-0.23, -0.08, 1.94),
            (-0.12, -0.18, 2.015),
            (0.05, -0.255, 1.965),
            (0.235, -0.245, 1.81),
        ],
        [(0.07, 0.055), (0.075, 0.06), (0.06, 0.05), (0.022, 0.026)],
        segments=20,
    )
    fringe.parent = hair_root
    _color(fringe, material, keys, "hairHighlight")
    character.rigid_parts.append(fringe)
    for side, x_value in (("Left", -0.27), ("Right", 0.27)):
        curl = _ellipsoid(
            f"HairCurl{side}", collection,
            root, material, keys, "hair",
            (x_value, -0.015, 1.57), (0.088, 0.112, 0.165), rings=14, segments=22,
        )
        curl.parent = hair_root
        character.rigid_parts.append(curl)
        tip = _ellipsoid(
            f"HairTip{side}", collection, root, material, keys, "hairHighlight",
            (x_value * 0.92, -0.075, 1.46), (0.072, 0.085, 0.075), rings=10, segments=18,
        )
        tip.parent = hair_root
        character.rigid_parts.append(tip)


def build_masculine_avatar(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path) -> CharacterBuild:
    profile = AVATAR_PROFILES["avatar_male"]
    character = _build_shared(job, collection, root, texture_dir, profile)
    _add_masculine_hair(character, collection, profile)
    return character


def build_feminine_avatar(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path) -> CharacterBuild:
    profile = AVATAR_PROFILES["avatar_female"]
    character = _build_shared(job, collection, root, texture_dir, profile)
    _add_feminine_hair(character, collection, profile)
    return character


def build_avatar(job: dict[str, Any], collection: Any, root: Any, texture_dir: Path) -> CharacterBuild:
    if job["id"] == "avatar_male":
        return build_masculine_avatar(job, collection, root, texture_dir)
    if job["id"] == "avatar_female":
        return build_feminine_avatar(job, collection, root, texture_dir)
    raise ValueError(f"unsupported avatar job: {job['id']}")
