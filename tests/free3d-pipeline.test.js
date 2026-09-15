import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";

const root = new URL("../", import.meta.url);
const pilotIds = [
  "avatar_male",
  "avatar_female",
  "tyrannosaurus_baby",
  "tyrannosaurus_teen",
  "tyrannosaurus_adult",
];
const pilotManifest = JSON.parse(readFileSync(new URL("../art/free3d/v0.17-pilot.json", import.meta.url), "utf8"));
const approvedDetailParts = Object.fromEntries(pilotManifest.jobs.map((job) => [job.id, job.detailParts]));

function completePilotReport(artifact) {
  const digest = "2d711642b726b04401627ca9fbac32f5c8530fb1903cc4db02258717921a4881";
  const model = (id) => ({
    id,
    valid: true,
    fbx_path: artifact,
    glb_path: artifact,
    atlas_path: artifact,
    output_sha256: { fbx: digest, glb: digest, atlas: digest },
    render_paths: Array(9).fill(artifact),
    detail_parts: Object.fromEntries(approvedDetailParts[id].map((name) => [name, true])),
    surface_export: {
      valid: true,
      materialCount: 1,
      textureCount: 2,
      imageCount: 2,
      baseColorTexture: true,
      roughnessTexture: true,
      repackedRoughness: true,
      roughnessGreenRange: [71, 184],
      errors: [],
    },
    surface_contract: {
      sourceTexture: "single_1k_rgba_atlas",
      roughnessSource: "atlas_alpha",
      glbRoughness: "metallicRoughnessTexture.green",
      fbxUnityImport: "invert_atlas_alpha_into_mask_smoothness",
    },
  });
  return {
    valid: true,
    generator: "blender-python",
    pilotIndex: artifact,
    models: pilotIds.map(model),
  };
}

test("free 3D manifest defines the approved five-model sequence", () => {
  const result = spawnSync(process.execPath, [
    "scripts/validate-free3d-manifest.mjs",
    "art/free3d/v0.17-pilot.json",
  ], { cwd: root, encoding: "utf8" });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.deepEqual(report.ids, pilotIds);
  assert.equal(report.paidApisEnabled, false);
  assert.equal(report.generator, "blender-python");
});

test("free 3D validator rejects any paid provider or enabled billing", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-"));
  const file = join(directory, "paid.json");
  writeFileSync(file, JSON.stringify({
    schemaVersion: 1,
    release: "0.17",
    generator: "fal-tripo",
    paidApisEnabled: true,
    jobs: [],
  }));

  try {
    const result = spawnSync(process.execPath, ["scripts/validate-free3d-manifest.mjs", file], {
      cwd: root,
      encoding: "utf8",
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /paidApisEnabled|generator/);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free 3D validator rejects a pilot job without required polish details", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-polish-"));
  const file = join(directory, "missing-detail-parts.json");
  const manifest = JSON.parse(readFileSync(new URL("../art/free3d/v0.17-pilot.json", import.meta.url), "utf8"));
  delete manifest.jobs[0].detailParts;
  writeFileSync(file, JSON.stringify(manifest));

  try {
    const result = spawnSync(process.execPath, ["scripts/validate-free3d-manifest.mjs", file], {
      cwd: root,
      encoding: "utf8",
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /avatar_male: detailParts/);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free 3D report rejects a model with an unmodeled polish detail", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-report-polish-"));
  const artifact = join(directory, "artifact.bin");
  writeFileSync(artifact, "x");
  const report = completePilotReport(artifact);
  report.models[0].detail_parts.EarLeft = false;

  try {
    const { validateFree3dReport } = await import("../scripts/validate-free3d-report.mjs");
    assert.throws(
      () => validateFree3dReport(report, { root: directory, expectedDetailParts: approvedDetailParts }),
      /avatar_male: missing polish details EarLeft/,
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free 3D report rejects an omitted required polish detail", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-report-omitted-polish-"));
  const artifact = join(directory, "artifact.bin");
  writeFileSync(artifact, "x");
  const report = completePilotReport(artifact);
  delete report.models[4].detail_parts.JacketBelt;

  try {
    const { validateFree3dReport } = await import("../scripts/validate-free3d-report.mjs");
    assert.throws(
      () => validateFree3dReport(report, { root: directory, expectedDetailParts: approvedDetailParts }),
      /tyrannosaurus_adult: polish detail keys do not match manifest/,
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free 3D report rejects a non-portable exported surface", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-report-surface-"));
  const artifact = join(directory, "artifact.bin");
  writeFileSync(artifact, "x");
  const report = completePilotReport(artifact);
  report.models[2].surface_export.valid = false;
  report.models[2].surface_export.roughnessTexture = false;
  report.models[2].surface_export.errors = ["standard roughness texture is missing"];

  try {
    const { validateFree3dReport } = await import("../scripts/validate-free3d-report.mjs");
    assert.throws(
      () => validateFree3dReport(report, { root: directory, expectedDetailParts: approvedDetailParts }),
      /tyrannosaurus_baby: portable GLB surface validation failed/,
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free 3D report rejects a roughness payload without semantic variation", async () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-report-flat-roughness-"));
  const artifact = join(directory, "artifact.bin");
  writeFileSync(artifact, "x");
  const report = completePilotReport(artifact);
  report.models[3].surface_export.roughnessGreenRange = [107, 107];

  try {
    const { validateFree3dReport } = await import("../scripts/validate-free3d-report.mjs");
    assert.throws(
      () => validateFree3dReport(report, { root: directory, expectedDetailParts: approvedDetailParts }),
      /tyrannosaurus_teen: portable GLB surface validation failed/,
    );
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("Blender generator exposes deterministic offline geometry math", () => {
  const result = spawnSync("python3", ["scripts/blender_generate_bigimong.py", "--self-test"], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.status, "SELF_TEST_OK");
  assert.equal(report.generator, "blender-python");
  assert.deepEqual(report.requiredActions, [
    "Idle", "Summon", "Attack", "DodgeLeft", "DodgeRight", "Hit", "Knockout", "Victory",
  ]);
  assert.equal(report.scaledHeightM, 2.1);
  assert.equal(report.bottomAfterTransformM, 0);
});

test("character normalization synchronizes scale and bottom transforms", () => {
  const python = `
import json, sys
sys.path.insert(0, "scripts")
from free3d.geometry import normalize_character_transform

class Vector:
    def __init__(self, x=0.0, y=0.0, z=0.0):
        self.x, self.y, self.z = x, y, z
    def __imul__(self, value):
        self.x *= value
        self.y *= value
        self.z *= value
        return self

class Node:
    def __init__(self):
        self.children = []
        self.location = Vector()
        self.scale = Vector(1.0, 1.0, 1.0)

root = Node()
child = Node()
root.children.append(child)
cached = {"min": -1.0, "max": 3.0, "updates": 0}

def bounds(_objects):
    return ((0.0, 0.0, cached["min"]), (0.0, 0.0, cached["max"]))

def update_scene():
    cached["min"] = child.location.z - child.scale.z
    cached["max"] = child.location.z + 3.0 * child.scale.z
    cached["updates"] += 1

factor = normalize_character_transform(
    root,
    2.0,
    bounds_fn=bounds,
    update_scene=update_scene,
)
print(json.dumps({
    "factor": factor,
    "bottom": cached["min"],
    "height": cached["max"] - cached["min"],
    "updates": cached["updates"],
}))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), {
    factor: 0.5,
    bottom: 0,
    height: 2,
    updates: 2,
  });
});

test("closed tapered tube topology leaves no boundary edges", () => {
  const python = `
import json, sys
from collections import Counter
sys.path.insert(0, "scripts")
from free3d.geometry import closed_tube_faces

faces = closed_tube_faces(3, 28)
edges = Counter()
for face in faces:
    for index, first in enumerate(face):
        second = face[(index + 1) % len(face)]
        edges[tuple(sorted((first, second)))] += 1
print(json.dumps({
    "faceCount": len(faces),
    "boundaryEdges": sum(count != 2 for count in edges.values()),
}))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), {
    faceCount: 58,
    boundaryEdges: 0,
  });
});

test("closed dorsal-fin topology leaves no boundary edges", () => {
  const python = `
import json, sys
from collections import Counter
sys.path.insert(0, "scripts")
import free3d.geometry as geometry
faces = getattr(geometry, "closed_fin_faces", lambda: [])()
vertices = [
    (-1, -1, 0), (1, -1, 0), (0, -1, 1),
    (-1, 1, 0), (1, 1, 0), (0, 1, 1),
]
edges = Counter()
signed_volume = 0.0
for face in faces:
    for index, first in enumerate(face):
        second = face[(index + 1) % len(face)]
        edges[tuple(sorted((first, second)))] += 1
    for index in range(1, len(face) - 1):
        a, b, c = (vertices[face[position]] for position in (0, index, index + 1))
        cross = (
            b[1] * c[2] - b[2] * c[1],
            b[2] * c[0] - b[0] * c[2],
            b[0] * c[1] - b[1] * c[0],
        )
        signed_volume += (a[0] * cross[0] + a[1] * cross[1] + a[2] * cross[2]) / 6.0
print(json.dumps({
    "faceCount": len(faces),
    "boundaryEdges": sum(count != 2 for count in edges.values()),
    "signedVolume": round(signed_volume, 6),
}))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), {
    faceCount: 5,
    boundaryEdges: 0,
    signedVolume: 2,
  });
});

test("single texture atlas carries material-specific roughness in alpha", () => {
  const python = `
import json, struct, sys, tempfile, zlib
from pathlib import Path
sys.path.insert(0, "scripts")
from free3d.materials import write_palette_atlas

with tempfile.TemporaryDirectory() as directory:
    path = Path(directory) / "atlas.png"
    write_palette_atlas(path, {
        "skin": "#F4B08E",
        "hair": "#45251E",
        "clothing": "#34363D",
        "medallion": "#F4B72C",
    })
    payload = path.read_bytes()
    offset = 8
    idat = bytearray()
    while offset < len(payload):
        length = struct.unpack(">I", payload[offset:offset + 4])[0]
        kind = payload[offset + 4:offset + 8]
        chunk = payload[offset + 8:offset + 8 + length]
        if kind == b"IDAT":
            idat.extend(chunk)
        offset += 12 + length
    row = zlib.decompress(bytes(idat))[:1 + 1024 * 4]
    samples = [row[1 + x * 4 + 3] for x in (128, 384, 640, 896)]
    print(json.dumps(samples))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), [148, 107, 184, 71]);
});

test("GLB export contract requires standard base-color and roughness textures", () => {
  const python = `
import binascii, json, struct, sys, tempfile, zlib
from pathlib import Path
sys.path.insert(0, "scripts")
try:
    from free3d.export_validation import inspect_glb_surface
except ImportError:
    inspect_glb_surface = lambda _path: {"valid": False, "errors": ["validator missing"]}

def png(pixels):
    def chunk(kind, payload):
        return struct.pack(">I", len(payload)) + kind + payload + struct.pack(">I", binascii.crc32(kind + payload) & 0xFFFFFFFF)
    raw = b"\\x00" + b"".join(bytes(pixel) for pixel in pixels)
    ihdr = struct.pack(">IIBBBBB", len(pixels), 1, 8, 2, 0, 0, 0)
    return b"\\x89PNG\\r\\n\\x1a\\n" + chunk(b"IHDR", ihdr) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")

def aligned(payload):
    return payload + b"\\x00" * ((4 - len(payload) % 4) % 4)

def write_glb(path, pbr, *, duplicate_roughness=False, declared_length=None):
    base = png([(220, 60, 50), (245, 190, 130)])
    roughness = base if duplicate_roughness else png([(0, 71, 0), (0, 184, 0)])
    first = aligned(base)
    binary = first + aligned(roughness)
    document = {
        "asset": {"version": "2.0"},
        "materials": [{"pbrMetallicRoughness": pbr}],
        "textures": [{"source": 0}, {"source": 1}],
        "images": [
            {"mimeType": "image/png", "bufferView": 0},
            {"mimeType": "image/png", "bufferView": 1},
        ],
        "buffers": [{"byteLength": len(binary) if declared_length is None else declared_length}],
        "bufferViews": [
            {"buffer": 0, "byteOffset": 0, "byteLength": len(base)},
            {"buffer": 0, "byteOffset": len(first), "byteLength": len(roughness)},
        ],
    }
    encoded = json.dumps(document, separators=(",", ":")).encode("utf-8")
    encoded += b" " * ((4 - len(encoded) % 4) % 4)
    total = 12 + 8 + len(encoded) + 8 + len(binary)
    path.write_bytes(
        struct.pack("<4sII", b"glTF", 2, total)
        + struct.pack("<I4s", len(encoded), b"JSON") + encoded
        + struct.pack("<I4s", len(binary), b"BIN\\x00") + binary
    )

with tempfile.TemporaryDirectory() as directory:
    good = Path(directory) / "good.glb"
    bad = Path(directory) / "bad.glb"
    duplicate = Path(directory) / "duplicate.glb"
    bad_buffer = Path(directory) / "bad-buffer.glb"
    write_glb(good, {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {"index": 1}})
    write_glb(bad, {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {}})
    write_glb(duplicate, {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {"index": 1}}, duplicate_roughness=True)
    write_glb(bad_buffer, {"baseColorTexture": {"index": 0}, "metallicRoughnessTexture": {"index": 1}}, declared_length=1)
    print(json.dumps({
        "good": inspect_glb_surface(good),
        "bad": inspect_glb_surface(bad),
        "duplicate": inspect_glb_surface(duplicate),
        "badBuffer": inspect_glb_surface(bad_buffer),
    }, sort_keys=True))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.deepEqual(report.good, {
    baseColorTexture: true,
    errors: [],
    imageCount: 2,
    materialCount: 1,
    repackedRoughness: true,
    roughnessGreenRange: [71, 184],
    roughnessTexture: true,
    textureCount: 2,
    valid: true,
  });
  assert.equal(report.bad.valid, false);
  assert.ok(report.bad.errors.includes("roughness texture reference is invalid"));
  assert.equal(report.duplicate.valid, false);
  assert.ok(report.duplicate.errors.includes("roughness texture payload duplicates base color"));
  assert.equal(report.badBuffer.valid, false);
  assert.ok(report.badBuffer.errors.includes("GLB buffer byteLength is invalid"));
});

test("Blender-style script execution resolves the bundled free3d package", () => {
  const python = [
    "import runpy, sys",
    "sys.argv = ['scripts/blender_generate_bigimong.py', '--self-test']",
    "runpy.run_path('scripts/blender_generate_bigimong.py', run_name='__main__')",
  ].join("; ");
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.equal(JSON.parse(result.stdout).status, "SELF_TEST_OK");
});

test("Blender generator accepts the headless smoke-render gate", () => {
  const result = spawnSync("python3", [
    "scripts/blender_generate_bigimong.py",
    "--smoke-render",
  ], { cwd: root, encoding: "utf8" });

  assert.equal(result.status, 1);
  assert.match(result.stderr, /Production generation must run through Blender/);
  assert.doesNotMatch(result.stderr, /unrecognized arguments/);
});

test("Blender generator exposes invalid model reasons without Blender", () => {
  const python = `
import json, sys
sys.path.insert(0, "scripts")
from blender_generate_bigimong import AssetBuildResult, validation_summary
result = AssetBuildResult(
    id="avatar_male",
    resource_name="Avatar_Masculine",
    fbx_path="model.fbx",
    glb_path="model.glb",
    atlas_path="atlas.png",
    bounds={"min": [0, 0, 0], "max": [1, 1, 2]},
    materials=1,
    bones=23,
    actions=[],
    valid=False,
    errors=["triangle_count exceeds 30000"],
    warnings=["triangle_count 31000 is outside target range 18000..25000"],
    triangle_count=31000,
    render_paths=[],
    source_git_blob_sha="source",
    output_sha256={},
    texture_size=1024,
    deform_bones=19,
    control_bones=4,
    origin_error_m=0.0,
    target_height_error_pct=0.0,
    required_parts={"Head": True, "Hair": False},
    detail_parts={"EarLeft": True},
    surface_export={"valid": False, "errors": ["standard roughness texture is missing"]},
    surface_contract={"roughnessSource": "atlas_alpha"},
)
print(json.dumps(validation_summary([result]), sort_keys=True))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), {
    invalidModels: [{
      id: "avatar_male",
      errors: ["triangle_count exceeds 30000"],
      warnings: ["triangle_count 31000 is outside target range 18000..25000"],
      metrics: {
        triangleCount: 31000,
        materialCount: 1,
        textureSize: 1024,
        deformBones: 19,
        controlBones: 4,
        originErrorM: 0,
        targetHeightErrorPct: 0,
      },
      missingDetails: [],
      missingParts: ["Hair"],
    }],
  });
});

test("invalid model summary exposes missing modeled polish details", () => {
  const python = `
import json, sys
from types import SimpleNamespace as NS
sys.path.insert(0, "scripts")
from blender_generate_bigimong import validation_summary
result = NS(
    id="tyrannosaurus_adult",
    errors=["required polish detail is missing: JacketBelt"],
    warnings=[],
    triangle_count=28000,
    materials=1,
    texture_size=1024,
    deform_bones=20,
    control_bones=4,
    origin_error_m=0.0,
    target_height_error_pct=0.0,
    required_parts={"Head": True},
    detail_parts={"JacketCollar": True, "JacketBelt": False},
    valid=False,
)
print(json.dumps(validation_summary([result]), sort_keys=True))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout).invalidModels[0].missingDetails, ["JacketBelt"]);
});

test("Blender numeric suffixes preserve stable logical polish part names", () => {
  const python = `
import json, sys
sys.path.insert(0, "scripts")
import free3d.validation as validation
normalize = getattr(validation, "logical_object_name", lambda value: value)
print(json.dumps([
    normalize("EarLeft"),
    normalize("EarLeft.001"),
    normalize("DorsalSpine01.004"),
    normalize("Pack.Roll"),
]))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), [
    "EarLeft",
    "EarLeft",
    "DorsalSpine01",
    "Pack.Roll",
  ]);
});

test("polish validation accepts only non-empty mesh objects", () => {
  const python = `
import json, sys
from types import SimpleNamespace as NS
sys.path.insert(0, "scripts")
import free3d.validation as validation
modeled = getattr(validation, "modeled_part_names", lambda _objects: set())
objects = [
    NS(name="EarLeft.001", type="MESH", data=NS(vertices=[1, 2, 3], polygons=[1])),
    NS(name="HairCrownShell", type="MESH", data=NS(vertices=[1, 2, 3], polygons=[1])),
    NS(name="VertexOnlyBadge", type="MESH", data=NS(vertices=[1], polygons=[])),
    NS(name="JacketBelt", type="MESH", data=NS(vertices=[], polygons=[])),
    NS(name="DorsalSpine01", type="EMPTY", data=None),
]
print(json.dumps(sorted(modeled(objects))))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), ["EarLeft", "HairCrownShell"]);
});

test("avatar builders preserve distinct identities and a detachable summoning medallion", () => {
  const result = spawnSync("python3", [
    "scripts/blender_generate_bigimong.py",
    "--self-test-section",
    "avatars",
  ], { cwd: root, encoding: "utf8" });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.status, "AVATAR_CONTRACT_OK");
  assert.deepEqual(report.ids, ["avatar_male", "avatar_female"]);
  assert.notEqual(report.profiles.avatar_male, report.profiles.avatar_female);
  assert.equal(report.detachableMedallion, true);
  assert.equal(report.stockPrimitiveOperators, false);
  assert.deepEqual(report.hairConstruction, {
    avatar_male: "layered_swept_clumps",
    avatar_female: "layered_bob_clumps",
  });
  for (const id of report.ids) {
    for (const detail of [
      "EarLeft", "EarRight", "EyelidLeft", "EyelidRight",
      "PalmLeft", "PalmRight", "ThumbLeft", "ThumbRight",
      "TopCollar", "TopHem", "ShortsWaistband", "MedallionInset",
    ]) {
      assert.ok(report.polishParts[id].includes(detail), `${id} missing polish contract ${detail}`);
    }
  }
  for (const part of [
    "Head", "EyeWhiteLeft", "EyeWhiteRight", "IrisLeft", "IrisRight",
    "Hair", "Body", "HandLeft", "HandRight", "FootLeft", "FootRight",
    "NecklaceChain", "SummoningMedallion", "MedallionSocket",
  ]) {
    assert.ok(report.namedParts.includes(part), `missing avatar part ${part}`);
  }
});

test("Tyrannosaurus stages use independent proportions and wardrobe", () => {
  const result = spawnSync("python3", [
    "scripts/blender_generate_bigimong.py",
    "--self-test-section",
    "tyrannosaurs",
  ], { cwd: root, encoding: "utf8" });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.status, "TYRANNOSAUR_CONTRACT_OK");
  assert.deepEqual(report.ids, ["tyrannosaurus_baby", "tyrannosaurus_teen", "tyrannosaurus_adult"]);
  assert.equal(new Set(Object.values(report.profiles)).size, 3);
  assert.deepEqual(report.targetHeightsM, [0.3, 0.9, 2.1]);
  assert.deepEqual(report.wardrobe.tyrannosaurus_baby, ["ExplorerCap"]);
  assert.ok(report.wardrobe.tyrannosaurus_teen.includes("ExplorerVest"));
  assert.ok(report.wardrobe.tyrannosaurus_adult.includes("PackRoll"));
  assert.equal(report.uniformStageScaling, false);
  assert.equal(report.facialConstruction, "articulated_upper_muzzle_and_lower_jaw");
  assert.equal(report.surfaceDetail, "staged_dorsal_spines_and_markings");
  assert.equal(new Set(Object.values(report.expressions)).size, 3);
  for (const id of report.ids) {
    for (const detail of [
      "UpperMuzzle", "BrowRidgeLeft", "BrowRidgeRight",
      "CheekPlateLeft", "CheekPlateRight",
      "DorsalSpine01", "DorsalSpine02", "DorsalSpine03",
      "FingerClawLeft01", "FingerClawRight01",
    ]) {
      assert.ok(report.polishParts[id].includes(detail), `${id} missing polish contract ${detail}`);
    }
  }
  for (const part of ["Head", "Jaw", "Tail", "EyeWhiteLeft", "EyeWhiteRight", "Teeth"]) {
    assert.ok(report.namedParts.includes(part), `missing Tyrannosaurus part ${part}`);
  }
});

test("free Blender rigs expose runtime actions, limits, and attachment bones", () => {
  const result = spawnSync("python3", [
    "scripts/blender_generate_bigimong.py",
    "--self-test-section",
    "rigging",
  ], { cwd: root, encoding: "utf8" });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.status, "RIG_CONTRACT_OK");
  assert.equal(report.maxDeformBones, 64);
  assert.equal(report.maxControlBones, 16);
  assert.deepEqual(Object.keys(report.actions), [
    "Idle", "Summon", "Attack", "DodgeLeft", "DodgeRight", "Hit", "Knockout", "Victory",
  ]);
  assert.deepEqual(report.actions.Idle, { frames: [1, 90], loop: true });
  for (const bone of ["Root", "Hips", "Head", "Jaw", "Hand.L", "Hand.R", "Foot.L", "Foot.R"]) {
    assert.ok(report.requiredBones.includes(bone), `missing rig bone ${bone}`);
  }
  assert.ok(report.attachmentBones.includes("MedallionSocket"));
});

test("polish parts follow the anatomical rig segment they visually belong to", () => {
  const python = `
import json, sys
sys.path.insert(0, "scripts")
from free3d.rigging import _bone_for_object
names = [
    "ToeLeft01", "ToeRight03",
    "ToothUpper1", "ToothLower1",
    "DorsalSpine01", "DorsalSpine02", "DorsalSpine03", "DorsalSpine05",
    "FingerClawLeft01", "FingerClawRight02",
]
print(json.dumps({name: _bone_for_object(name, "tyrannosaur") for name in names}, sort_keys=True))
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.deepEqual(JSON.parse(result.stdout), {
    DorsalSpine01: "Head",
    DorsalSpine02: "Neck",
    DorsalSpine03: "Spine",
    DorsalSpine05: "Tail.1",
    FingerClawLeft01: "Hand.L",
    FingerClawRight02: "Hand.R",
    ToeLeft01: "Foot.L",
    ToeRight03: "Foot.R",
    ToothLower1: "Jaw",
    ToothUpper1: "Head",
  });
});

test("asset delivery enforces hard budgets and five review angles", () => {
  const result = spawnSync("python3", [
    "scripts/blender_generate_bigimong.py",
    "--self-test-section",
    "delivery",
  ], { cwd: root, encoding: "utf8" });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.status, "DELIVERY_CONTRACT_OK");
  assert.deepEqual(report.reviewAngles, ["front", "left", "rear", "right", "three_quarter"]);
  assert.deepEqual(report.animationPreviews, ["Idle", "Summon", "Attack"]);
  assert.deepEqual(report.renderResolution, [1024, 1024]);
  assert.equal(report.transparentFilm, false);
  assert.equal(report.reviewStyle, "neutral_studio_v2");
  assert.equal(report.contactShadows, true);
  assert.equal(report.heroScaleGuide, false);
  assert.deepEqual(report.hardLimits, {
    triangles: 30000,
    materials: 4,
    textureSize: 1024,
    deformBones: 64,
    controlBones: 16,
    originErrorM: 0.001,
  });
  assert.equal(report.invalidFixture.valid, false);
  assert.ok(report.invalidFixture.errors.includes("triangle_count exceeds 30000"));
});

test("review renderer reads available engines from Blender render settings", () => {
  const python = `
import sys
sys.path.insert(0, "scripts")
from types import SimpleNamespace as NS
from free3d.rendering import _configure_scene
render = NS(
    bl_rna=NS(properties={"engine": NS(enum_items=[NS(identifier="BLENDER_EEVEE")])}),
    engine=None,
    film_transparent=False,
    image_settings=NS(file_format=None, color_mode=None, color_depth=None),
    resolution_percentage=0,
    resolution_x=0,
    resolution_y=0,
)
scene = NS(
    render=render,
    world=NS(color=None),
    view_settings=NS(view_transform=None, look=None),
)
_configure_scene(NS(context=NS(scene=scene)))
print(render.engine)
`;
  const result = spawnSync("python3", ["-I", "-c", python], {
    cwd: root,
    encoding: "utf8",
  });

  assert.equal(result.status, 0, result.stderr);
  assert.equal(result.stdout.trim(), "BLENDER_EEVEE");
});

test("free pilot workflow passes the offline-only policy validator", () => {
  const result = spawnSync(process.execPath, [
    "scripts/validate-free3d-workflow.mjs",
    ".github/workflows/build-free-3d-pilot.yml",
  ], { cwd: root, encoding: "utf8" });

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.valid, true);
  assert.equal(report.permissions, "contents: read");
  assert.equal(report.paidServiceReferences, 0);
  assert.equal(report.artifact, "Bigimong-Free3D-v0.17-pilot");
  assert.equal(report.blenderGeneration, true);
});

test("free pilot workflow requires the glTF exporter's NumPy dependency", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-dependency-"));
  const file = join(directory, "missing-numpy.yml");
  const workflow = readFileSync(new URL("../.github/workflows/build-free-3d-pilot.yml", import.meta.url), "utf8");
  writeFileSync(file, workflow.replace(/\s+python3-numpy/g, ""));

  try {
    const result = spawnSync(process.execPath, ["scripts/validate-free3d-workflow.mjs", file], {
      cwd: root,
      encoding: "utf8",
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /python3-numpy/);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free pilot workflow gates production on a headless smoke render", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-headless-"));
  const workflow = readFileSync(new URL("../.github/workflows/build-free-3d-pilot.yml", import.meta.url), "utf8");
  const required = [
    "libegl1",
    "libgl1-mesa-dri",
    "xvfb",
    "xauth",
    "Blender 4.0.2",
    "--smoke-render",
    "xvfb-run --auto-servernum",
  ];

  try {
    for (const token of required) {
      const file = join(directory, `${token.replaceAll(/[^a-z0-9]/gi, "-")}.yml`);
      writeFileSync(file, workflow.replaceAll(token, ""));
      const result = spawnSync(process.execPath, ["scripts/validate-free3d-workflow.mjs", file], {
        cwd: root,
        encoding: "utf8",
      });
      assert.notEqual(result.status, 0, `${token} must be required`);
      assert.match(result.stderr, /headless|dependency|smoke/i);
    }
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free pilot workflow preserves invalid model diagnostics", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-diagnostics-"));
  const file = join(directory, "missing-always.yml");
  const workflow = readFileSync(new URL("../.github/workflows/build-free-3d-pilot.yml", import.meta.url), "utf8");
  writeFileSync(file, workflow.replaceAll("if: always()", ""));

  try {
    const result = spawnSync(process.execPath, ["scripts/validate-free3d-workflow.mjs", file], {
      cwd: root,
      encoding: "utf8",
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /diagnostic/i);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("free pilot workflow policy rejects credentials for a paid generator", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-free3d-workflow-"));
  const file = join(directory, "paid.yml");
  writeFileSync(file, "permissions: contents: read\nenv:\n  TRIPO_API_KEY: paid\n");

  try {
    const result = spawnSync(process.execPath, ["scripts/validate-free3d-workflow.mjs", file], {
      cwd: root,
      encoding: "utf8",
    });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /paid generation|credential/i);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});
