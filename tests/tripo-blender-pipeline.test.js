import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";

const root = new URL("../", import.meta.url);

test("Tripo manifest defines only the approved five-model pilot with reproducible inputs", () => {
  const result = spawnSync(process.execPath, ["scripts/validate-tripo-manifest.mjs", "art/tripo/v0.16-pilot-jobs.json"], {
    cwd: root,
    encoding: "utf8",
  });
  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.valid, true);
  assert.deepEqual(report.ids, ["avatar_male", "avatar_female", "tyrannosaurus_baby", "tyrannosaurus_teen", "tyrannosaurus_adult"]);
  assert.equal(report.billableExecutionEnabled, false);
});

test("Tripo manifest validator rejects a job without a reference source", () => {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-tripo-"));
  const manifest = join(directory, "bad.json");
  writeFileSync(manifest, JSON.stringify({ version: 1, billableExecutionEnabled: false, jobs: [{ id: "avatar_male" }] }));
  try {
    const result = spawnSync(process.execPath, ["scripts/validate-tripo-manifest.mjs", manifest], { cwd: root, encoding: "utf8" });
    assert.notEqual(result.status, 0);
    assert.match(result.stderr, /sourceAsset/);
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
});

test("Blender batch script can self-test bottom-origin and layout math without bpy", () => {
  const result = spawnSync("python3", ["scripts/blender_prepare_bigimong.py", "--self-test"], {
    cwd: root,
    encoding: "utf8",
  });
  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.equal(report.status, "SELF_TEST_OK");
  assert.deepEqual(report.bottomOrigin, [2, 3, -4]);
  assert.deepEqual(report.layoutSlots.slice(0, 3), [[-2.4, 0, 0], [0, 0, 0], [2.4, 0, 0]]);
  const source = readFileSync(new URL("../scripts/blender_prepare_bigimong.py", import.meta.url), "utf8");
  assert.match(source, /export_scene\.gltf/);
  assert.match(source, /export_scene\.fbx/);
});
