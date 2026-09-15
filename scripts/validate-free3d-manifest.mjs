import { existsSync, readFileSync } from "node:fs";
import { spawnSync } from "node:child_process";
import { dirname, isAbsolute, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const APPROVED_IDS = [
  "avatar_male",
  "avatar_female",
  "tyrannosaurus_baby",
  "tyrannosaurus_teen",
  "tyrannosaurus_adult",
];

const APPROVED_RESOURCES = [
  "Avatar_Masculine",
  "Avatar_Feminine",
  "Bigimong_01_Baby",
  "Bigimong_01_Teen",
  "Bigimong_01_Adult",
];

const REQUIRED_ACTIONS = [
  "Idle",
  "Summon",
  "Attack",
  "DodgeLeft",
  "DodgeRight",
  "Hit",
  "Knockout",
  "Victory",
];

const FORBIDDEN_SERVICE_PATTERN = /\b(tripo|fal(?:\.ai)?|meshy|replicate)\b/i;

function sameSequence(actual, expected) {
  return actual.length === expected.length && actual.every((value, index) => value === expected[index]);
}

function containsForbiddenService(value, key = "") {
  if (FORBIDDEN_SERVICE_PATTERN.test(key)) return true;
  if (typeof value === "string") return FORBIDDEN_SERVICE_PATTERN.test(value);
  if (Array.isArray(value)) return value.some((item) => containsForbiddenService(item));
  if (value && typeof value === "object") {
    return Object.entries(value).some(([childKey, childValue]) => containsForbiddenService(childValue, childKey));
  }
  return false;
}

export function validateFree3dManifest(manifest, { sourceExists, hashObject } = {}) {
  const errors = [];

  if (manifest?.schemaVersion !== 1) errors.push("schemaVersion must be 1");
  if (manifest?.release !== "0.17") errors.push("release must be 0.17");
  if (manifest?.generator !== "blender-python") errors.push("generator must be blender-python");
  if (manifest?.paidApisEnabled !== false) errors.push("paidApisEnabled must be false");
  if (manifest?.deterministicSeed !== 17015) errors.push("deterministicSeed must be 17015");
  if (containsForbiddenService(manifest)) errors.push("manifest must not reference a paid generation service");

  const defaults = manifest?.defaults;
  if (!defaults || typeof defaults !== "object") {
    errors.push("defaults are required");
  } else {
    if (defaults.textureSize !== 1024) errors.push("textureSize must be 1024");
    if (defaults.targetTrianglesMin !== 18000 || defaults.targetTrianglesMax !== 25000) {
      errors.push("target triangle range must be 18000..25000");
    }
    if (defaults.hardTriangleLimit !== 30000) errors.push("hardTriangleLimit must be 30000");
    if (defaults.materialLimit !== 4) errors.push("materialLimit must be 4");
    if (defaults.deformBoneLimit !== 64 || defaults.controlBoneLimit !== 16) {
      errors.push("bone limits must be 64 deform and 16 control");
    }
    if (defaults.originToleranceM !== 0.001) errors.push("originToleranceM must be 0.001");
    if (!sameSequence(defaults.requiredActions ?? [], REQUIRED_ACTIONS)) {
      errors.push("requiredActions must match the approved eight-action sequence");
    }
  }

  if (!Array.isArray(manifest?.jobs) || manifest.jobs.length !== 5) {
    errors.push("jobs must contain five entries");
  }

  const jobs = Array.isArray(manifest?.jobs) ? manifest.jobs : [];
  const ids = jobs.map((job) => job.id);
  const resourceNames = jobs.map((job) => job.resourceName);
  if (new Set(ids).size !== ids.length) errors.push("job ids must be unique");
  if (new Set(resourceNames).size !== resourceNames.length) errors.push("resource names must be unique");
  if (!sameSequence(ids, APPROVED_IDS)) errors.push("job ids must match the approved five-model sequence");
  if (!sameSequence(resourceNames, APPROVED_RESOURCES)) {
    errors.push("resource names must match the approved stable sequence");
  }

  for (const job of jobs) {
    const label = job?.id || "unknown job";
    if (!job?.sourceAsset || !job?.sourceGitBlobSha) errors.push(`${label}: source reference is required`);
    if (!/^[0-9a-f]{40}$/.test(job?.sourceGitBlobSha ?? "")) errors.push(`${label}: sourceGitBlobSha is invalid`);
    if (!Number.isFinite(job?.targetHeightM) || job.targetHeightM <= 0) errors.push(`${label}: targetHeightM is invalid`);
    if (!job?.bodyRatios || Object.keys(job.bodyRatios).length < 5) errors.push(`${label}: bodyRatios are incomplete`);
    if (!job?.palette || Object.keys(job.palette).length < 5) errors.push(`${label}: palette is incomplete`);
    if (!Array.isArray(job?.requiredParts) || job.requiredParts.length === 0) errors.push(`${label}: requiredParts is empty`);
    if (job?.inferredRear !== true) errors.push(`${label}: inferredRear must be true for the single-view source`);
    if (job?.outputStem !== job?.resourceName) errors.push(`${label}: outputStem must equal resourceName`);
    if (job?.id?.startsWith("avatar_") && (!Array.isArray(job.cropTopLeft) || job.cropTopLeft.length !== 4)) {
      errors.push(`${label}: cropTopLeft is required for avatar references`);
    }
    if (sourceExists && job?.sourceAsset && !sourceExists(job.sourceAsset)) {
      errors.push(`${label}: sourceAsset does not exist`);
    }
    if (hashObject && job?.sourceAsset && job?.sourceGitBlobSha) {
      const actualHash = hashObject(job.sourceAsset);
      if (actualHash && actualHash !== job.sourceGitBlobSha) errors.push(`${label}: source hash mismatch`);
    }
  }

  if (errors.length) throw new Error(errors.join("\n"));
  return {
    valid: true,
    ids,
    resourceNames,
    paidApisEnabled: false,
    generator: "blender-python",
  };
}

function createRepositoryChecks(repoRoot) {
  const gitCheck = spawnSync("git", ["rev-parse", "--is-inside-work-tree"], {
    cwd: repoRoot,
    encoding: "utf8",
  });
  const isGitWorktree = gitCheck.status === 0 && gitCheck.stdout.trim() === "true";

  return {
    sourceExists(sourceAsset) {
      return existsSync(isAbsolute(sourceAsset) ? sourceAsset : resolve(repoRoot, sourceAsset));
    },
    hashObject: isGitWorktree
      ? (sourceAsset) => {
        const absolutePath = isAbsolute(sourceAsset) ? sourceAsset : resolve(repoRoot, sourceAsset);
        const result = spawnSync("git", ["hash-object", "--", absolutePath], {
          cwd: repoRoot,
          encoding: "utf8",
        });
        if (result.status !== 0) throw new Error(`could not hash ${sourceAsset}: ${result.stderr.trim()}`);
        return result.stdout.trim();
      }
      : undefined,
  };
}

function runCli() {
  const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), "..");
  const manifestArgument = process.argv[2];
  if (!manifestArgument) throw new Error("usage: node scripts/validate-free3d-manifest.mjs <manifest.json>");
  const manifestPath = isAbsolute(manifestArgument) ? manifestArgument : resolve(repoRoot, manifestArgument);
  const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
  const report = validateFree3dManifest(manifest, createRepositoryChecks(repoRoot));
  process.stdout.write(`${JSON.stringify(report)}\n`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    runCli();
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  }
}
