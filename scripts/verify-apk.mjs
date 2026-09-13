import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { existsSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { delimiter, join } from "node:path";
import { pathToFileURL } from "node:url";

export const EXPECTED_APK = Object.freeze({
  applicationId: "com.bigimong.app",
  versionName: "0.12.0",
  versionCode: "12",
  minSdk: "28",
  requiredUnityLibrary: "lib/arm64-v8a/libunity.so",
  forbiddenAbis: ["armeabi-v7a", "x86", "x86_64"],
});

const normalized = (value) => String(value).trim();

export function validateMetadata(metadata) {
  const errors = [];
  for (const key of ["applicationId", "versionName", "versionCode", "minSdk"]) {
    const actual = normalized(metadata[key]);
    if (actual !== EXPECTED_APK[key]) {
      errors.push(`${key} expected ${EXPECTED_APK[key]} but received ${actual}`);
    }
  }

  const files = metadata.files.map(normalized);
  if (!files.includes(EXPECTED_APK.requiredUnityLibrary)) {
    errors.push(`missing ${EXPECTED_APK.requiredUnityLibrary}`);
  }
  for (const abi of EXPECTED_APK.forbiddenAbis) {
    if (files.some((file) => file.startsWith(`lib/${abi}/`))) {
      errors.push(`forbidden native ABI ${abi}`);
    }
  }
  return errors;
}

function analyzerCandidates(env) {
  const candidates = [];
  if (env.APK_ANALYZER) candidates.push(env.APK_ANALYZER);
  for (const root of [env.ANDROID_SDK_ROOT, env.ANDROID_HOME]) {
    if (!root) continue;
    candidates.push(
      join(root, "cmdline-tools", "latest", "bin", "apkanalyzer"),
      join(root, "tools", "bin", "apkanalyzer"),
    );
  }
  for (const directory of (env.PATH ?? "").split(delimiter)) {
    if (directory) candidates.push(join(directory, "apkanalyzer"));
  }
  return [...new Set(candidates)];
}

function resolveAnalyzer(env, exists) {
  const analyzer = analyzerCandidates(env).find(exists);
  if (!analyzer) {
    throw new Error("apkanalyzer was not found; install Android SDK Command-line Tools or set APK_ANALYZER");
  }
  return analyzer;
}

export async function inspectWithApkAnalyzer(apkPath, dependencies = {}) {
  const env = dependencies.env ?? process.env;
  const exists = dependencies.exists ?? existsSync;
  const execute = dependencies.execute ?? ((file, args) => execFileSync(file, args, { encoding: "utf8" }));
  const analyzer = resolveAnalyzer(env, exists);
  const query = (...args) => normalized(execute(analyzer, [...args, apkPath]));
  return {
    applicationId: query("manifest", "application-id"),
    versionName: query("manifest", "version-name"),
    versionCode: query("manifest", "version-code"),
    minSdk: query("manifest", "min-sdk"),
    files: query("files", "list").split(/\r?\n/).filter(Boolean),
  };
}

export async function verifyApk({ apkPath, reportPath, inspect = inspectWithApkAnalyzer }) {
  if (!apkPath || !reportPath) throw new Error("Usage: --apk <path> --report <path>");
  if (!existsSync(apkPath)) throw new Error(`APK not found: ${apkPath}`);
  const metadata = await inspect(apkPath);
  const errors = validateMetadata(metadata);
  const bytes = readFileSync(apkPath);
  const report = {
    valid: errors.length === 0,
    apkPath,
    sizeBytes: statSync(apkPath).size,
    sha256: createHash("sha256").update(bytes).digest("hex"),
    expected: EXPECTED_APK,
    actual: metadata,
    errors,
  };
  writeFileSync(reportPath, `${JSON.stringify(report, null, 2)}\n`, "utf8");
  if (!report.valid) throw new Error(`APK verification failed: ${errors.join("; ")}`);
  return report;
}

function parseArguments(argv) {
  const values = {};
  for (let index = 0; index < argv.length; index += 2) values[argv[index]] = argv[index + 1];
  return { apkPath: values["--apk"], reportPath: values["--report"] };
}

export async function main(argv = process.argv.slice(2)) {
  const options = parseArguments(argv);
  const report = await verifyApk(options);
  console.log(`Verified ${options.apkPath}: sha256=${report.sha256}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((error) => {
    console.error(error.message);
    process.exitCode = 1;
  });
}
