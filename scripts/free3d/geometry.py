"""Geometry helpers shared by the free Bigimong Blender builders.

The pure numeric helpers intentionally import no Blender modules so CI can test
the scale/origin contract with the system Python before Blender is installed.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from math import cos, pi, sin
from typing import Any, Iterable, Sequence


@dataclass
class CharacterBuild:
    root: Any
    skin_meshes: list[Any] = field(default_factory=list)
    rigid_parts: list[Any] = field(default_factory=list)
    required_parts: list[str] = field(default_factory=list)
    materials: list[Any] = field(default_factory=list)
    kind: str = ""
    metadata: dict[str, Any] = field(default_factory=dict)


def bottom_offset(bounds_min_z: float) -> float:
    return -float(bounds_min_z)


def uniform_scale(current_height: float, target_height: float) -> float:
    if current_height <= 0.0 or target_height <= 0.0:
        raise ValueError("heights must be positive")
    return float(target_height) / float(current_height)


def transformed_bottom(bounds_min_z: float, scale: float, offset_after_scale: float) -> float:
    return float(bounds_min_z) * float(scale) + float(offset_after_scale)


def _link_object(collection: Any, obj: Any) -> Any:
    collection.objects.link(obj)
    return obj


def create_profile_mesh(
    name: str,
    profile: Sequence[tuple[float, float]],
    collection: Any,
    *,
    segments: int = 32,
    radial_scale: tuple[float, float] = (1.0, 1.0),
) -> Any:
    """Create a closed, continuous lathed mesh from authored radius/Z loops."""
    import bpy  # type: ignore

    if len(profile) < 2 or segments < 3:
        raise ValueError("profile needs two rings and at least three segments")
    vertices: list[tuple[float, float, float]] = []
    for z_value, radius in profile:
        for index in range(segments):
            angle = 2.0 * pi * index / segments
            vertices.append((
                radius * radial_scale[0] * cos(angle),
                radius * radial_scale[1] * sin(angle),
                z_value,
            ))
    faces: list[tuple[int, ...]] = []
    for ring in range(len(profile) - 1):
        start = ring * segments
        next_start = (ring + 1) * segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((start + index, start + nxt, next_start + nxt, next_start + index))
    faces.append(tuple(reversed(tuple(range(segments)))))
    top_start = (len(profile) - 1) * segments
    faces.append(tuple(top_start + index for index in range(segments)))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    _link_object(collection, obj)
    smooth_mesh(obj)
    return obj


def create_ellipsoid_mesh(
    name: str,
    collection: Any,
    *,
    location: tuple[float, float, float],
    radii: tuple[float, float, float],
    rings: int = 20,
    segments: int = 32,
) -> Any:
    """Create an authored parametric ellipsoid without Blender primitive ops."""
    import bpy  # type: ignore

    if rings < 3 or segments < 3 or min(radii) <= 0:
        raise ValueError("invalid ellipsoid resolution or radii")
    vertices = [(0.0, 0.0, radii[2])]
    for ring in range(1, rings):
        phi = pi * ring / rings
        for index in range(segments):
            theta = 2.0 * pi * index / segments
            vertices.append((
                radii[0] * sin(phi) * cos(theta),
                radii[1] * sin(phi) * sin(theta),
                radii[2] * cos(phi),
            ))
    bottom_index = len(vertices)
    vertices.append((0.0, 0.0, -radii[2]))
    faces: list[tuple[int, ...]] = []
    for index in range(segments):
        faces.append((0, 1 + index, 1 + (index + 1) % segments))
    for ring in range(rings - 2):
        a = 1 + ring * segments
        b = a + segments
        for index in range(segments):
            nxt = (index + 1) % segments
            faces.append((a + index, b + index, b + nxt, a + nxt))
    last = 1 + (rings - 2) * segments
    for index in range(segments):
        faces.append((last + index, bottom_index, last + (index + 1) % segments))

    mesh = bpy.data.meshes.new(f"{name}_Mesh")
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    _link_object(collection, obj)
    obj.location = location
    smooth_mesh(obj)
    return obj


def create_curve_tube(
    name: str,
    collection: Any,
    points: Sequence[tuple[float, float, float]],
    *,
    radius: float,
    resolution: int = 3,
    bevel_resolution: int = 3,
) -> Any:
    import bpy  # type: ignore

    if len(points) < 2:
        raise ValueError("curve tube needs at least two points")
    curve = bpy.data.curves.new(f"{name}_Curve", "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = resolution
    curve.bevel_depth = radius
    curve.bevel_resolution = bevel_resolution
    curve.resolution_v = bevel_resolution
    spline = curve.splines.new("BEZIER")
    spline.bezier_points.add(len(points) - 1)
    for point, coordinate in zip(spline.bezier_points, points):
        point.co = coordinate
        point.handle_left_type = "AUTO"
        point.handle_right_type = "AUTO"
    obj = bpy.data.objects.new(name, curve)
    _link_object(collection, obj)
    return curve_to_mesh(obj)


def curve_to_mesh(obj: Any) -> Any:
    import bpy  # type: ignore

    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.convert(target="MESH")
    obj.select_set(False)
    smooth_mesh(obj)
    return obj


def add_mirror_modifier(obj: Any, *, axis: int = 0) -> Any:
    modifier = obj.modifiers.new("AuthoredSymmetry", "MIRROR")
    modifier.use_axis[0] = axis == 0
    modifier.use_axis[1] = axis == 1
    modifier.use_axis[2] = axis == 2
    modifier.use_clip = True
    return modifier


def smooth_mesh(obj: Any) -> None:
    if getattr(obj, "type", None) != "MESH":
        return
    for polygon in obj.data.polygons:
        polygon.use_smooth = True


def add_subdivision(obj: Any, *, viewport_levels: int = 1, render_levels: int = 1) -> Any:
    modifier = obj.modifiers.new("MobileSmooth", "SUBSURF")
    modifier.levels = viewport_levels
    modifier.render_levels = render_levels
    return modifier


def voxel_remesh(obj: Any, voxel_size: float) -> Any:
    """Apply Blender's voxel remesh to an authored joined surface."""
    import bpy  # type: ignore

    if voxel_size <= 0:
        raise ValueError("voxel_size must be positive")
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    if hasattr(obj.data, "remesh_voxel_size") and hasattr(bpy.ops.object, "voxel_remesh"):
        obj.data.remesh_voxel_size = voxel_size
        bpy.ops.object.voxel_remesh()
    else:
        modifier = obj.modifiers.new("ContinuousVoxelRemesh", "REMESH")
        modifier.mode = "VOXEL"
        modifier.voxel_size = voxel_size
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    smooth_mesh(obj)
    return obj


def join_meshes(objects: Iterable[Any], name: str) -> Any:
    import bpy  # type: ignore

    meshes = [obj for obj in objects if getattr(obj, "type", None) == "MESH"]
    if not meshes:
        raise ValueError("join_meshes requires at least one mesh")
    bpy.ops.object.select_all(action="DESELECT")
    for obj in meshes:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    meshes[0].name = name
    meshes[0].data.name = f"{name}_Mesh"
    smooth_mesh(meshes[0])
    return meshes[0]


def descendants(root: Any) -> list[Any]:
    result: list[Any] = []
    stack = list(root.children)
    while stack:
        obj = stack.pop()
        result.append(obj)
        stack.extend(obj.children)
    return result


def world_bounds(objects: Iterable[Any]) -> tuple[tuple[float, float, float], tuple[float, float, float]]:
    from mathutils import Vector  # type: ignore

    points = []
    for obj in objects:
        if getattr(obj, "type", None) != "MESH":
            continue
        points.extend(obj.matrix_world @ Vector(corner) for corner in obj.bound_box)
    if not points:
        raise ValueError("character contains no mesh bounds")
    return (
        tuple(min(point[axis] for point in points) for axis in range(3)),
        tuple(max(point[axis] for point in points) for axis in range(3)),
    )


def place_origin_at_bottom(root: Any) -> float:
    objects = descendants(root)
    bounds_min, _ = world_bounds(objects)
    shift = bottom_offset(bounds_min[2])
    for child in root.children:
        child.location.z += shift
    root.location.z = 0.0
    return shift


def scale_to_height(root: Any, target_height_m: float) -> float:
    bounds_min, bounds_max = world_bounds(descendants(root))
    factor = uniform_scale(bounds_max[2] - bounds_min[2], target_height_m)
    for child in root.children:
        child.location *= factor
        child.scale *= factor
    place_origin_at_bottom(root)
    return factor


def parent_preserve_world(obj: Any, parent: Any) -> None:
    world = obj.matrix_world.copy()
    obj.parent = parent
    obj.matrix_world = world
