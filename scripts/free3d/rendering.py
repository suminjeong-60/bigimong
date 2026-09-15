"""Fixed-camera review rendering and contact-sheet assembly."""

from __future__ import annotations

from array import array
from math import radians
from pathlib import Path
from typing import Any

from free3d.geometry import descendants, world_bounds


REVIEW_ANGLES = ("front", "left", "rear", "right", "three_quarter")
ANIMATION_PREVIEWS = ("Idle", "Summon", "Attack")
RENDER_RESOLUTION = (1024, 1024)


def rendering_contract() -> dict[str, Any]:
    return {
        "reviewAngles": list(REVIEW_ANGLES),
        "animationPreviews": list(ANIMATION_PREVIEWS),
        "renderResolution": list(RENDER_RESOLUTION),
        "transparentFilm": True,
        "cameraType": "ORTHO",
    }


def _material(bpy: Any, name: str, rgba: tuple[float, float, float, float]) -> Any:
    existing = bpy.data.materials.get(name)
    if existing:
        return existing
    material = bpy.data.materials.new(name)
    material.diffuse_color = rgba
    material.use_nodes = True
    shader = material.node_tree.nodes.get("Principled BSDF")
    if shader:
        shader.inputs["Base Color"].default_value = rgba
        shader.inputs["Roughness"].default_value = 0.7
    return material


def _ensure_studio(bpy: Any) -> tuple[Any, Any]:
    scene = bpy.context.scene
    camera = bpy.data.objects.get("BigimongReviewCamera")
    if camera is None:
        data = bpy.data.cameras.new("BigimongReviewCameraData")
        camera = bpy.data.objects.new("BigimongReviewCamera", data)
        scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    scene.camera = camera

    ground = bpy.data.objects.get("BigimongReviewGround")
    if ground is None:
        bpy.ops.mesh.primitive_plane_add(size=8.0, location=(0.0, 0.0, -0.006))
        ground = bpy.context.object
        ground.name = "BigimongReviewGround"
        ground.data.materials.append(_material(bpy, "ReviewGroundMaterial", (0.32, 0.37, 0.43, 1.0)))

    lights = (
        ("BigimongKey", "AREA", (-3.0, -4.0, 5.0), 950.0, 4.0),
        ("BigimongFill", "AREA", (3.0, -2.0, 3.0), 600.0, 3.0),
        ("BigimongRim", "AREA", (0.0, 3.0, 4.5), 850.0, 3.0),
    )
    for name, kind, location, energy, size in lights:
        light = bpy.data.objects.get(name)
        if light is None:
            data = bpy.data.lights.new(f"{name}Data", kind)
            light = bpy.data.objects.new(name, data)
            scene.collection.objects.link(light)
        light.location = location
        light.data.energy = energy
        light.data.shape = "DISK"
        light.data.size = size
        direction = (ground.location - light.location).to_track_quat("-Z", "Y")
        light.rotation_euler = direction.to_euler()

    marker = bpy.data.objects.get("BigimongScaleMarker")
    if marker is None:
        bpy.ops.mesh.primitive_cube_add(size=1.0)
        marker = bpy.context.object
        marker.name = "BigimongScaleMarker"
        marker.data.materials.append(_material(bpy, "ReviewMarkerMaterial", (0.15, 0.7, 0.85, 1.0)))
    return camera, marker


def _configure_scene(bpy: Any) -> None:
    scene = bpy.context.scene
    available = {item.identifier for item in scene.render.bl_rna.properties["engine"].enum_items}
    scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in available else "BLENDER_EEVEE"
    scene.render.film_transparent = True
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.resolution_percentage = 100
    scene.render.resolution_x, scene.render.resolution_y = RENDER_RESOLUTION
    scene.render.image_settings.color_depth = "8"
    scene.world.color = (0.055, 0.075, 0.11)
    if hasattr(scene, "view_settings"):
        scene.view_settings.view_transform = "Standard"
        scene.view_settings.look = "Medium High Contrast"


def _aim_camera(camera: Any, target: tuple[float, float, float], position: tuple[float, float, float], ortho_scale: float) -> None:
    from mathutils import Vector  # type: ignore

    camera.location = position
    camera.rotation_euler = (Vector(target) - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.ortho_scale = ortho_scale


def _set_character_visibility(bpy: Any, character: Any) -> dict[Any, bool]:
    visible = set(descendants(character.root)) | {character.root}
    studio_names = {
        "BigimongReviewGround", "BigimongScaleMarker", "BigimongReviewCamera",
        "BigimongKey", "BigimongFill", "BigimongRim",
    }
    states = {}
    for obj in bpy.context.scene.objects:
        states[obj] = obj.hide_render
        if obj.name not in studio_names and obj not in visible:
            obj.hide_render = True
        elif obj in visible or obj.name in studio_names:
            obj.hide_render = False
    return states


def _restore_visibility(states: dict[Any, bool]) -> None:
    for obj, value in states.items():
        obj.hide_render = value


def write_contact_sheet(render_paths: list[Path], output_path: Path, *, tile_size: int = 512) -> Path:
    import bpy  # type: ignore

    if not render_paths:
        raise ValueError("contact sheet needs at least one render")
    images = [bpy.data.images.load(str(path), check_existing=False) for path in render_paths]
    width = tile_size * len(images)
    height = tile_size
    combined = array("f", [0.0]) * (width * height * 4)
    for column, image in enumerate(images):
        image.scale(tile_size, tile_size)
        source = array("f", [0.0]) * (tile_size * tile_size * 4)
        image.pixels.foreach_get(source)
        for row in range(tile_size):
            source_start = row * tile_size * 4
            target_start = (row * width + column * tile_size) * 4
            combined[target_start:target_start + tile_size * 4] = source[source_start:source_start + tile_size * 4]
    sheet = bpy.data.images.new(output_path.stem, width=width, height=height, alpha=True)
    sheet.pixels.foreach_set(combined)
    output_path.parent.mkdir(parents=True, exist_ok=True)
    sheet.filepath_raw = str(output_path)
    sheet.file_format = "PNG"
    sheet.save()
    for image in images:
        bpy.data.images.remove(image)
    bpy.data.images.remove(sheet)
    return output_path


def render_character_review(character: Any, rig: Any, output_dir: Path) -> list[Path]:
    import bpy  # type: ignore

    output_dir.mkdir(parents=True, exist_ok=True)
    _configure_scene(bpy)
    camera, marker = _ensure_studio(bpy)
    states = _set_character_visibility(bpy, character)
    bounds_min, bounds_max = world_bounds(descendants(character.root))
    width = bounds_max[0] - bounds_min[0]
    depth = bounds_max[1] - bounds_min[1]
    height = bounds_max[2] - bounds_min[2]
    center = ((bounds_min[0] + bounds_max[0]) * 0.5, (bounds_min[1] + bounds_max[1]) * 0.5, bounds_min[2] + height * 0.51)
    distance = max(height, width, depth) * 3.2
    ortho = max(height * 1.24, width * 1.65, depth * 1.25)
    marker.dimensions = (max(height * 0.018, 0.01), max(height * 0.018, 0.01), height)
    marker.location = (bounds_max[0] + max(height * 0.11, 0.04), 0.0, height * 0.5)
    bpy.context.view_layer.update()
    positions = {
        "front": (center[0], center[1] - distance, center[2]),
        "left": (center[0] - distance, center[1], center[2]),
        "rear": (center[0], center[1] + distance, center[2]),
        "right": (center[0] + distance, center[1], center[2]),
        "three_quarter": (center[0] - distance * 0.7, center[1] - distance * 0.7, center[2] + height * 0.06),
    }
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = RENDER_RESOLUTION
    rig.armature.animation_data.action = rig.action_blocks["Idle"]
    scene.frame_set(1)
    directions = []
    for name in REVIEW_ANGLES:
        _aim_camera(camera, center, positions[name], ortho)
        path = output_dir / f"{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        directions.append(path)

    previews = []
    scene.render.resolution_x = scene.render.resolution_y = 512
    _aim_camera(camera, center, positions["three_quarter"], ortho)
    for name in ANIMATION_PREVIEWS:
        rig.armature.animation_data.action = rig.action_blocks[name]
        start, end = rig.action_blocks[name].frame_range
        scene.frame_set(int((start + end) * 0.5))
        path = output_dir / f"preview_{name}.png"
        scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        previews.append(path)
    rig.armature.animation_data.action = rig.action_blocks["Idle"]
    scene.frame_set(1)
    contact = write_contact_sheet(directions, output_dir / "contact_sheet.png")
    _restore_visibility(states)
    return [*directions, *previews, contact]
