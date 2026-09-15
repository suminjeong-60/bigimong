import { createHash } from "node:crypto";
import { existsSync, readFileSync } from "node:fs";
import { dirname, isAbsolute, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const APPROVED_IDS = [
  "avatar_male",
  "avatar_female",
  "tyrannosaurus_baby",
  "tyrannosaurus_teen",
  "tyrannosaurus_adult",
];

const SURFACE_CONTRACT = {
  sourceTexture: "single_1k_rgba_atlas",
  roughnessSource: "atlas_alpha",
  glbRoughness: "metallicRoughnessTexture.green",
  fbxUnityImport: "invert_atlas_alpha_into_mask_smoothness",
};

function sha256(path) {
  return createHash("sha256").update(readFileSync(path)).digest("hex");
}

function manifestDetailParts(root) {
  const manifest = JSON.parse(readFileSync(resolve(root, "art/free3d/v0.17-pilot.json"), "utf8"));
  return Object.fromEntries((manifest.jobs ?? []).map((job) => [job.id, job.detailParts ?? []]));
}

export function validateFree3dReport(report, { root = process.cwd(), expectedDetailParts } = {}) {
  const errors = [];
  const approvedDetails = expectedDetailParts ?? manifestDetailParts(root);
  if (report?.valid !== true) errors.push("report valid must be true");
  if (report?.generator !== "blender-python") errors.push("generator must be blender-python");
  if (!Array.isArray(report?.models) || report.models.length !== 5) errors.push("report must contain five models");
  const ids = (report.models ?? []).map((model) => model.id);
  if (JSON.stringify(ids) !== JSON.stringify(APPROVED_IDS)) errors.push("report model sequence mismatch");
  for (const model of report.models ?? []) {
    if (model.valid !== true) errors.push(`${model.id}: validation failed`);
    const detailParts = model.detail_parts && typeof model.detail_parts === "object"
      ? Object.entries(model.detail_parts)
      : [];
    const detailNames = detailParts.map(([name]) => name);
    const expectedNames = approvedDetails[model.id] ?? [];
    if (detailNames.length !== expectedNames.length || !expectedNames.every((name) => detailNames.includes(name))) {
      errors.push(`${model.id}: polish detail keys do not match manifest`);
    }
    const missingDetails = detailParts.filter(([, present]) => present !== true).map(([name]) => name);
    if (detailParts.length === 0) errors.push(`${model.id}: polish detail report is missing`);
    if (missingDetails.length) errors.push(`${model.id}: missing polish details ${missingDetails.join(", ")}`);
    const roughnessRange = model.surface_export?.roughnessGreenRange;
    const hasSemanticRoughness = Array.isArray(roughnessRange)
      && roughnessRange.length === 2
      && roughnessRange.every((value) => Number.isInteger(value) && value >= 0 && value <= 255)
      && roughnessRange[0] < roughnessRange[1];
    if (model.surface_export?.valid !== true
        || model.surface_export?.materialCount !== 1
        || model.surface_export?.textureCount !== 2
        || model.surface_export?.imageCount !== 2
        || model.surface_export?.baseColorTexture !== true
        || model.surface_export?.roughnessTexture !== true
        || model.surface_export?.repackedRoughness !== true
        || !hasSemanticRoughness) {
      errors.push(`${model.id}: portable GLB surface validation failed`);
    }
    if (Object.entries(SURFACE_CONTRACT).some(([key, value]) => model.surface_contract?.[key] !== value)) {
      errors.push(`${model.id}: surface import contract mismatch`);
    }
    for (const [kind, pathValue] of Object.entries({ fbx: model.fbx_path, glb: model.glb_path, atlas: model.atlas_path })) {
      const path = isAbsolute(pathValue) ? pathValue : resolve(root, pathValue);
      if (!existsSync(path)) {
        errors.push(`${model.id}: missing ${kind}`);
      } else if (model.output_sha256?.[kind] !== sha256(path)) {
        errors.push(`${model.id}: ${kind} SHA-256 mismatch`);
      }
    }
    const renders = Array.isArray(model.render_paths) ? model.render_paths : [];
    if (renders.length !== 9) errors.push(`${model.id}: expected nine review renders`);
    for (const render of renders) {
      const path = isAbsolute(render) ? render : resolve(root, render);
      if (!existsSync(path)) errors.push(`${model.id}: missing render ${render}`);
    }
  }
  if (!report?.pilotIndex || !existsSync(isAbsolute(report.pilotIndex) ? report.pilotIndex : resolve(root, report.pilotIndex))) {
    errors.push("pilot index is missing");
  }
  if (errors.length) throw new Error(errors.join("\n"));
  return { valid: true, ids, verifiedHashes: ids.length * 3, reviewFiles: ids.length * 9 + 1 };
}

function runCli() {
  const argument = process.argv[2];
  if (!argument) throw new Error("usage: node scripts/validate-free3d-report.mjs <report.json>");
  const reportPath = isAbsolute(argument) ? argument : resolve(process.cwd(), argument);
  const report = JSON.parse(readFileSync(reportPath, "utf8"));
  const result = validateFree3dReport(report, { root: process.cwd() || dirname(reportPath) });
  process.stdout.write(`${JSON.stringify(result)}\n`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    runCli();
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  }
}
