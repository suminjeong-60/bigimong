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
