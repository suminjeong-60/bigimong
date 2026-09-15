"""Reference-led masculine and feminine Bigimong avatar mesh builders."""

from __future__ import annotations

from pathlib import Path
from typing import Any

from free3d.geometry import CharacterBuild, create_curve_tube, create_ellipsoid_mesh, create_profile_mesh
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


def avatar_contract_report() -> dict[str, Any]:
    return {
        "status": "AVATAR_CONTRACT_OK",
        "ids": list(AVATAR_PROFILES),
        "profiles": {key: value["profile"] for key, value in AVATAR_PROFILES.items()},
        "namedParts": list(AVATAR_NAMED_PARTS),
        "detachableMedallion": True,
        "stockPrimitiveOperators": False,
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
        rigid.extend((white, iris, pupil, glint))

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
    socket = bpy.data.objects.new("MedallionSocket", None)
    collection.objects.link(socket)
    socket.location = (0.0, -0.205, 1.075)
    socket.parent = root
    rigid.extend((chain, medallion, socket))

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
    starts = [-0.23, -0.13, -0.03, 0.08, 0.18]
    for index, start in enumerate(starts):
        lock = create_curve_tube(
            f"HairLock{index + 1}", collection,
            [(start, -0.09, 1.9 + 0.03 * (index % 2)), (start + 0.08, -0.16, 2.02), (0.25 - index * 0.025, -0.12, 1.91)],
            radius=0.055 - index * 0.003,
            resolution=3,
            bevel_resolution=4,
        )
        lock.parent = hair_root
        _color(lock, material, keys, "hair")
        character.rigid_parts.append(lock)
    side = _ellipsoid("HairBack", collection, root, material, keys, "hair", (0.0, 0.13, 1.85), (0.29, 0.15, 0.25), rings=18, segments=32)
    character.rigid_parts.append(side)


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
        segments=48, radial_scale=(0.33, 0.285),
    )
    bob.location = (0.0, 0.055, 1.73)
    bob.parent = hair_root
    _color(bob, material, keys, "hair")
    character.rigid_parts.append(bob)
    fringe = create_curve_tube(
        "HairSideFringe", collection,
        [(-0.22, -0.16, 1.93), (-0.1, -0.25, 2.0), (0.13, -0.25, 1.93), (0.24, -0.19, 1.82)],
        radius=0.06,
        resolution=4,
        bevel_resolution=4,
    )
    fringe.parent = hair_root
    _color(fringe, material, keys, "hair")
    character.rigid_parts.append(fringe)
    for side, x_value in (("Left", -0.27), ("Right", 0.27)):
        curl = create_curve_tube(
            f"HairCurl{side}", collection,
            [(x_value, 0.0, 1.72), (x_value * 1.03, -0.02, 1.5), (x_value * 0.88, -0.08, 1.45)],
            radius=0.065,
            resolution=3,
            bevel_resolution=4,
        )
        curl.parent = hair_root
        _color(curl, material, keys, "hair")
        character.rigid_parts.append(curl)


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
