import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { resolve } from "node:path";

const expectedKeys = [
  "avatar_male",
  "avatar_female",
  "bigimong_01_baby",
  "bigimong_01_teen",
  "bigimong_01_adult",
  "bigimong_egg",
];

const expectedAssetContracts = Object.freeze({
  avatar_male: Object.freeze({
    providerTitle: "Stylized Male T-pose",
    status: "existing",
    role: "player-avatar",
    unitySourceModelPath: "Assets/BigimongAR/Art/Varco/Source/Avatar_Masculine.fbx",
    unityPrefabPath: "Assets/BigimongAR/Resources/GeneratedCharacters/Avatar_Masculine.prefab",
    targetHeightM: 1.65,
    rig: "humanoid",
  }),
  avatar_female: Object.freeze({
    providerTitle: "Chibi Female Model",
    status: "generated",
    role: "player-avatar",
    unitySourceModelPath: "Assets/BigimongAR/Art/Varco/Source/Avatar_Feminine.fbx",
    unityPrefabPath: "Assets/BigimongAR/Resources/GeneratedCharacters/Avatar_Feminine.prefab",
    targetHeightM: 1.62,
    rig: "humanoid",
  }),
  bigimong_01_baby: Object.freeze({
    providerTitle: "Cute Red Dragon",
    status: "generated",
    role: "battle-companion",
    unitySourceModelPath: "Assets/BigimongAR/Art/Varco/Source/Bigimong_01_Baby.fbx",
    unityPrefabPath: "Assets/BigimongAR/Resources/GeneratedCharacters/Bigimong_01_Baby.prefab",
    targetHeightM: 0.3,
    rig: "generic",
  }),
  bigimong_01_teen: Object.freeze({
    providerTitle: "Red Cartoon Dinosaur",
    status: "generated",
    role: "battle-companion",
    unitySourceModelPath: "Assets/BigimongAR/Art/Varco/Source/Bigimong_01_Teen.fbx",
    unityPrefabPath: "Assets/BigimongAR/Resources/GeneratedCharacters/Bigimong_01_Teen.prefab",
    targetHeightM: 0.9,
    rig: "generic",
  }),
  bigimong_01_adult: Object.freeze({
    providerTitle: "Red Fire Dragon",
    status: "generated",
    role: "battle-companion",
    unitySourceModelPath: "Assets/BigimongAR/Art/Varco/Source/Bigimong_01_Adult.fbx",
    unityPrefabPath: "Assets/BigimongAR/Resources/GeneratedCharacters/Bigimong_01_Adult.prefab",
    targetHeightM: 2.1,
    rig: "generic",
  }),
  bigimong_egg: Object.freeze({
    providerTitle: "Colorful Abstract Egg",
    status: "generated",
    role: "hatch-prop",
    unitySourceModelPath: "Assets/BigimongAR/Art/Varco/Source/Bigimong_Egg.fbx",
    unityPrefabPath: "Assets/BigimongAR/Resources/GeneratedCharacters/Bigimong_Egg.prefab",
    targetHeightM: 0.42,
    rig: "none",
  }),
});

const forbiddenKeys = new Set([
  "apiKey",
  "accessToken",
  "refreshToken",
  "oauthToken",
  "providerAssetId",
  "downloadUrl",
]);

function collectForbiddenKeys(value, found = new Set()) {
  if (!value || typeof value !== "object") return found;
  for (const [key, child] of Object.entries(value)) {
    if (forbiddenKeys.has(key)) found.add(key);
    collectForbiddenKeys(child, found);
  }
  return found;
}

function sha256(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

function validate(manifest) {
  const errors = [];
  const assets = Array.isArray(manifest?.assets) ? manifest.assets : [];
  const keys = assets.map((asset) => asset?.key);
  const defaults = manifest?.defaults ?? {};
  const runtime = manifest?.runtime ?? {};

  if (manifest?.schemaVersion !== 1) errors.push("schemaVersion must equal 1");
  if (manifest?.provider !== "VARCO 3D") errors.push("provider must equal VARCO 3D");
  if (JSON.stringify(keys) !== JSON.stringify(expectedKeys)) {
    errors.push("assets must be the ordered six-model AR pilot");
  }
  if (runtime.credentialsInClientBuild !== false) errors.push("credentialsInClientBuild must be false");
  if (runtime.providerAssetIdsInRepository !== false) errors.push("providerAssetIdsInRepository must be false");
  if (defaults.topology !== "triangles") errors.push("topology must be triangles");
  if (defaults.textureSize !== 1024) errors.push("textureSize must equal 1024");
  if (defaults.pbrTexture !== true) errors.push("pbrTexture must be true");
  if (defaults.alpha !== false) errors.push("alpha must be false");
  if (defaults.characterTriangles !== 25000) errors.push("characterTriangles must equal 25000");
  if (defaults.staticPropTriangles !== 12000) errors.push("staticPropTriangles must equal 12000");

  for (const field of ["unitySourceModelPath", "unityPrefabPath"]) {
    const values = assets.map((asset) => asset?.[field]);
    if (new Set(values).size !== values.length) errors.push(`${field} values must be unique`);
  }

  for (const key of collectForbiddenKeys(manifest)) errors.push(`${key} must not be stored in the repository`);

  const credits = manifest?.credits ?? {};
  if (credits.before - credits.after !== credits.perGeneration * credits.generatedCount) {
    errors.push("credit delta must equal perGeneration multiplied by generatedCount");
  }

  for (const asset of assets) {
    const label = asset?.key ?? "asset";
    const expected = expectedAssetContracts[asset?.key];
    if (expected) {
      for (const field of [
        "providerTitle", "status", "role", "unitySourceModelPath", "unityPrefabPath", "targetHeightM", "rig",
      ]) {
        if (asset?.[field] !== expected[field]) {
          errors.push(`${label}: ${field} must equal ${expected[field]}`);
        }
      }
    }
    if (typeof asset?.providerTitle !== "string" || asset.providerTitle.trim() === "") {
      errors.push(`${label}: providerTitle is required`);
    }
    if (!(Number(asset?.targetHeightM) > 0)) errors.push(`${label}: targetHeightM must be positive`);
    if (!/^Assets\/BigimongAR\/Art\/Varco\/Source\/[A-Za-z0-9_]+\.fbx$/.test(asset?.unitySourceModelPath ?? "")) {
      errors.push(`${label}: unitySourceModelPath must target the VARCO Source folder`);
    }
    if (!/^Assets\/BigimongAR\/Resources\/GeneratedCharacters\/[A-Za-z0-9_]+\.prefab$/.test(asset?.unityPrefabPath ?? "")) {
      errors.push(`${label}: unityPrefabPath must target GeneratedCharacters Resources`);
    }
    const expectedTriangleLimit = asset.role === "hatch-prop" ? 15000 : 30000;
    if (asset.triangleLimit !== expectedTriangleLimit) {
      errors.push(`${label}: triangleLimit must equal ${expectedTriangleLimit}`);
    }
    if (asset.materialLimit !== 4) errors.push(`${label}: materialLimit must equal 4`);

    const sourcePath = resolve(asset?.sourceAsset ?? "");
    if (!asset?.sourceAsset || !existsSync(sourcePath)) {
      errors.push(`${label}: sourceAsset does not exist`);
    } else if (asset.sourceSha256 && sha256(sourcePath) !== asset.sourceSha256) {
      errors.push(`${label}: sourceSha256 does not match sourceAsset`);
    }

    if (asset.role === "player-avatar") {
      if (asset.rig !== "humanoid") errors.push(`${label}: avatar rig must be humanoid`);
      if (asset.tPose !== true) errors.push(`${label}: avatar tPose must be true`);
    } else if (asset.role === "battle-companion") {
      if (asset.rig !== "generic") errors.push(`${label}: companion rig must be generic`);
      if (asset.tPose !== false) errors.push(`${label}: companion tPose must be false`);
    } else if (asset.role === "hatch-prop") {
      if (asset.rig !== "none") errors.push(`${label}: rig must be none`);
      if (asset.tPose !== false) errors.push(`${label}: egg tPose must be false`);
    } else {
      errors.push(`${label}: unsupported role`);
    }

    if (asset.status === "generated") {
      const expectedTriangles = asset.role === "hatch-prop" ? 12000 : 25000;
      if (asset.triangles !== expectedTriangles) {
        errors.push(`${label}: triangles must equal ${expectedTriangles}`);
      }
    }
  }

  return { errors, keys };
}

const path = process.argv[2];
if (!path) {
  console.error("usage: node scripts/validate-varco3d-manifest.mjs <manifest.json>");
  process.exit(2);
}

try {
  const manifest = JSON.parse(readFileSync(resolve(path), "utf8"));
  const { errors, keys } = validate(manifest);
  if (errors.length) throw new Error(errors.join("\n"));
  console.log(JSON.stringify({
    valid: true,
    keys,
    characterTriangles: manifest.defaults.characterTriangles,
    staticPropTriangles: manifest.defaults.staticPropTriangles,
    credentialsInClientBuild: manifest.runtime.credentialsInClientBuild,
  }));
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
