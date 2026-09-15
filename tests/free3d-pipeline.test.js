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
  const digest = "2d711642b726b04401627ca9fbac32f5c8530fb1903cc4db02258717921a4881";
  const model = (id) => ({
    id,
    valid: true,
    fbx_path: artifact,
    glb_path: artifact,
    atlas_path: artifact,
    output_sha256: { fbx: digest, glb: digest, atlas: digest },
    render_paths: Array(9).fill(artifact),
    detail_parts: { UpperMuzzle: true },
  });
  const report = {
    valid: true,
    generator: "blender-python",
    pilotIndex: artifact,
    models: pilotIds.map(model),
  };
  report.models[0].detail_parts.UpperMuzzle = false;

  try {
    const { validateFree3dReport } = await import("../scripts/validate-free3d-report.mjs");
    assert.throws(
      () => validateFree3dReport(report, { root: directory }),
      /avatar_male: missing polish details UpperMuzzle/,
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
    faceCount: 5,
    boundaryEdges: 0,
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
