import assert from "node:assert/strict";
import { mkdtempSync, rmSync, writeFileSync } from "node:fs";
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
  for (const part of [
    "Head", "EyeWhiteLeft", "EyeWhiteRight", "IrisLeft", "IrisRight",
    "Hair", "Body", "HandLeft", "HandRight", "FootLeft", "FootRight",
    "NecklaceChain", "SummoningMedallion", "MedallionSocket",
  ]) {
    assert.ok(report.namedParts.includes(part), `missing avatar part ${part}`);
  }
});
