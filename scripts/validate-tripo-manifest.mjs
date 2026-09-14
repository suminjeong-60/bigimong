import { readFileSync } from "node:fs";
import { resolve } from "node:path";

const expectedIds = [
  "avatar_male",
  "avatar_female",
  "tyrannosaurus_baby",
  "tyrannosaurus_teen",
  "tyrannosaurus_adult",
];

function validate(manifest) {
  const errors = [];
  const jobs = Array.isArray(manifest?.jobs) ? manifest.jobs : [];
  jobs.forEach((job, index) => {
    const label = `jobs[${index}]`;
    if (!job?.sourceAsset || typeof job.sourceAsset !== "string") errors.push(`${label}.sourceAsset is required`);
    if (!/^[a-f0-9]{40}$/.test(job?.sourceGitBlobSha ?? "")) errors.push(`${label}.sourceGitBlobSha must be a 40-character Git blob SHA`);
    if (!/^tripo3d\/h3\.1\/(image|multiview)-to-3d$/.test(job?.endpoint ?? "")) errors.push(`${label}.endpoint is not an approved Tripo H3.1 endpoint`);
    if (typeof job?.detailedAppearance !== "string" || job.detailedAppearance.length < 120) errors.push(`${label}.detailedAppearance must be detailed`);
    if (!(Number(job?.targetHeightM) > 0)) errors.push(`${label}.targetHeightM must be positive`);
    if (job?.enabled !== false) errors.push(`${label}.enabled must remain false before the billable gate`);
  });
  if (manifest?.schemaVersion !== 1) errors.push("schemaVersion must equal 1");
  if (manifest?.billableExecutionEnabled !== false) errors.push("billableExecutionEnabled must remain false");
  if ("apiKey" in (manifest ?? {})) errors.push("API keys must never be stored in the manifest");
  const ids = jobs.map((job) => job?.id);
  if (JSON.stringify(ids) !== JSON.stringify(expectedIds)) errors.push("jobs must be the ordered five-model pilot");
  return { errors, ids };
}

const path = process.argv[2];
if (!path) {
  console.error("usage: node scripts/validate-tripo-manifest.mjs <manifest.json>");
  process.exit(2);
}

try {
  const manifest = JSON.parse(readFileSync(resolve(path), "utf8"));
  const { errors, ids } = validate(manifest);
  if (errors.length) {
    console.error(errors.join("\n"));
    process.exit(1);
  }
  console.log(JSON.stringify({ valid: true, ids, billableExecutionEnabled: manifest.billableExecutionEnabled }));
} catch (error) {
  console.error(error instanceof Error ? error.message : String(error));
  process.exit(1);
}
