import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { spawnSync } from "node:child_process";
import test from "node:test";

const root = new URL("../", import.meta.url);
const manifestPath = "art/varco3d/v0.18-ar-pilot.json";
const expectedKeys = [
  "avatar_male",
  "avatar_female",
  "bigimong_01_baby",
  "bigimong_01_teen",
  "bigimong_01_adult",
  "bigimong_egg",
];

function runValidator(path) {
  return spawnSync(process.execPath, ["scripts/validate-varco3d-manifest.mjs", path], {
    cwd: root,
    encoding: "utf8",
  });
}

function withMutatedManifest(mutator, assertion) {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-varco3d-"));
  const file = join(directory, "manifest.json");
  const manifest = JSON.parse(readFileSync(new URL(`../${manifestPath}`, import.meta.url), "utf8"));
  mutator(manifest);
  writeFileSync(file, JSON.stringify(manifest));
  try {
    assertion(runValidator(file));
  } finally {
    rmSync(directory, { recursive: true, force: true });
  }
}

test("VARCO registry validates the six approved AR assets", () => {
  const result = runValidator(manifestPath);

  assert.equal(result.status, 0, result.stderr);
  const report = JSON.parse(result.stdout);
  assert.deepEqual(report.keys, expectedKeys);
  assert.equal(report.characterTriangles, 25000);
  assert.equal(report.staticPropTriangles, 12000);
  assert.equal(report.credentialsInClientBuild, false);
});

test("VARCO registry rejects credentials, provider IDs, and direct download URLs", () => {
  withMutatedManifest(
    (manifest) => {
      manifest.apiKey = "must-not-ship";
      manifest.assets[0].providerAssetId = "private-account-id";
      manifest.assets[1].downloadUrl = "https://example.test/private-model.glb";
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /apiKey|providerAssetId|downloadUrl/);
    },
  );
});

test("VARCO registry rejects a character outside the mobile triangle budget", () => {
  withMutatedManifest(
    (manifest) => {
      manifest.assets.find((asset) => asset.key === "bigimong_01_adult").triangles = 50000;
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /bigimong_01_adult: triangles must equal 25000/);
    },
  );
});

test("VARCO registry rejects an egg that is rigged or exceeds its prop budget", () => {
  withMutatedManifest(
    (manifest) => {
      const egg = manifest.assets.find((asset) => asset.key === "bigimong_egg");
      egg.rig = "generic";
      egg.triangles = 25000;
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /bigimong_egg: rig must be none/);
      assert.match(result.stderr, /bigimong_egg: triangles must equal 12000/);
    },
  );
});

test("VARCO registry rejects a missing project reference image", () => {
  withMutatedManifest(
    (manifest) => {
      manifest.assets.find((asset) => asset.key === "avatar_female").sourceAsset = "art/varco3d/references/missing.png";
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /avatar_female: sourceAsset does not exist/);
    },
  );
});

test("VARCO registry rejects Unity model and prefab paths outside approved roots", () => {
  withMutatedManifest(
    (manifest) => {
      const female = manifest.assets.find((asset) => asset.key === "avatar_female");
      female.unitySourceModelPath = "Assets/Untrusted/Avatar_Feminine.fbx";
      female.unityPrefabPath = "Assets/BigimongAR/Art/Varco/Avatar_Feminine.prefab";
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /avatar_female: unitySourceModelPath must target the VARCO Source folder/);
      assert.match(result.stderr, /avatar_female: unityPrefabPath must target GeneratedCharacters Resources/);
    },
  );
});

test("VARCO registry locks runtime triangle and material acceptance limits", () => {
  withMutatedManifest(
    (manifest) => {
      const adult = manifest.assets.find((asset) => asset.key === "bigimong_01_adult");
      adult.triangleLimit = 50000;
      adult.materialLimit = 8;
      const egg = manifest.assets.find((asset) => asset.key === "bigimong_egg");
      egg.triangleLimit = 30000;
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /bigimong_01_adult: triangleLimit must equal 30000/);
      assert.match(result.stderr, /bigimong_01_adult: materialLimit must equal 4/);
      assert.match(result.stderr, /bigimong_egg: triangleLimit must equal 15000/);
    },
  );
});

test("VARCO registry locks each approved title, status, transform, and Unity destination", () => {
  withMutatedManifest(
    (manifest) => {
      const male = manifest.assets.find((asset) => asset.key === "avatar_male");
      const female = manifest.assets.find((asset) => asset.key === "avatar_female");
      male.status = "generated";
      female.providerTitle = "Replacement Female";
      female.targetHeightM = 1.8;
      female.unitySourceModelPath = male.unitySourceModelPath;
      female.unityPrefabPath = male.unityPrefabPath;
    },
    (result) => {
      assert.notEqual(result.status, 0);
      assert.match(result.stderr, /avatar_male: status must equal existing/);
      assert.match(result.stderr, /avatar_female: providerTitle must equal Chibi Female Model/);
      assert.match(result.stderr, /avatar_female: targetHeightM must equal 1.62/);
      assert.match(result.stderr, /unitySourceModelPath values must be unique/);
      assert.match(result.stderr, /unityPrefabPath values must be unique/);
    },
  );
});
