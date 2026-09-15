import { readFileSync } from "node:fs";
import { isAbsolute, resolve } from "node:path";
import { pathToFileURL } from "node:url";

const FORBIDDEN = [
  /TRIPO_API_KEY/i,
  /FAL_KEY/i,
  /FAL_API_KEY/i,
  /MESHY_API_KEY/i,
  /REPLICATE_API_TOKEN/i,
  /api\.tripo3d\.ai/i,
  /fal-ai\//i,
  /replicate\.com\/v1/i,
];

export function validateFree3dWorkflow(source) {
  const errors = [];
  const paidMatches = FORBIDDEN.flatMap((pattern) => source.match(pattern) ?? []);
  if (paidMatches.length) errors.push("paid generation service or credential reference is forbidden");
  if (/\$\{\{\s*secrets\./i.test(source)) errors.push("workflow must not read repository credentials");
  if (!/^permissions:\s*\n\s+contents:\s*read\s*$/m.test(source)) errors.push("permissions must be contents: read");
  if (!/validate-free3d-manifest\.mjs/.test(source)) errors.push("manifest validation step is missing");
  if (!/apt-get install[^\n]*blender/.test(source)) errors.push("Blender installation step is missing");
  if (!/apt-get install[^\n]*python3-numpy/.test(source)) {
    errors.push("Blender glTF export requires python3-numpy");
  }
  for (const dependency of ["libegl1", "libgl1-mesa-dri", "xvfb", "xauth"]) {
    const packagePattern = new RegExp(`apt-get install[^\\n]*\\b${dependency}\\b`);
    if (!packagePattern.test(source)) errors.push(`headless Blender dependency is missing: ${dependency}`);
  }
  if (!/blender --version \| grep -F ["']Blender 4\.0\.2["']/.test(source)) {
    errors.push("pinned headless Blender version check is missing");
  }
  const headlessCommand = /xvfb-run --auto-servernum blender --background --python scripts\/blender_generate_bigimong\.py/g;
  if ((source.match(headlessCommand) ?? []).length < 2) {
    errors.push("headless Blender must wrap both smoke and production renders");
  }
  const smokeIndex = source.indexOf("--smoke-render");
  const productionIndex = source.indexOf("--manifest art/free3d/v0.17-pilot.json");
  if (smokeIndex < 0 || !/test -s build\/free3d-v017\/headless-smoke\.png/.test(source)) {
    errors.push("headless smoke render gate is missing");
  } else if (productionIndex < 0 || smokeIndex > productionIndex) {
    errors.push("headless smoke render must run before production generation");
  }
  if (!/blender --background --python scripts\/blender_generate_bigimong\.py/.test(source)) {
    errors.push("Blender generation command is missing");
  }
  if ((source.match(/if:\s*always\(\)/g) ?? []).length < 2) {
    errors.push("diagnostic listing and artifact upload must run after model validation failure");
  }
  if (!/name:\s*Bigimong-Free3D-v0\.17-pilot/.test(source)) errors.push("approved artifact name is missing");
  if (!/retention-days:\s*14/.test(source)) errors.push("artifact retention must be 14 days");
  if (!/timeout-minutes:\s*45/.test(source)) errors.push("workflow timeout must be 45 minutes");
  if (errors.length) throw new Error(errors.join("\n"));
  return {
    valid: true,
    permissions: "contents: read",
    paidServiceReferences: 0,
    artifact: "Bigimong-Free3D-v0.17-pilot",
    blenderGeneration: true,
  };
}

function runCli() {
  const argument = process.argv[2];
  if (!argument) throw new Error("usage: node scripts/validate-free3d-workflow.mjs <workflow.yml>");
  const path = isAbsolute(argument) ? argument : resolve(process.cwd(), argument);
  process.stdout.write(`${JSON.stringify(validateFree3dWorkflow(readFileSync(path, "utf8")))}\n`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  try {
    runCli();
  } catch (error) {
    process.stderr.write(`${error instanceof Error ? error.message : String(error)}\n`);
    process.exitCode = 1;
  }
}
