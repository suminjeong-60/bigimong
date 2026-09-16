import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { EXPECTED_APK, inspectWithApkAnalyzer, validateMetadata, verifyApk } from "../scripts/verify-apk.mjs";

const validMetadata = {
  applicationId: "com.bigimong.app",
  versionName: "0.19.0",
  versionCode: "19",
  minSdk: "28",
  targetSdk: "36",
  permissions: ["android.permission.CAMERA"],
  arCoreRequirement: "optional",
  signatureVerified: true,
  files: ["lib/arm64-v8a/libunity.so", "lib/arm64-v8a/libUnityARCore.so", "assets/bin/Data/globalgamemanagers"],
};

test("APK release contract is pinned to v0.19", () => {
  assert.equal(EXPECTED_APK.versionName, "0.19.0");
  assert.equal(EXPECTED_APK.versionCode, "19");
});

test("APK verifier rejects the previous v0.16 release", () => {
  assert.deepEqual(validateMetadata({ ...validMetadata, versionName: "0.16.0", versionCode: "16" }), [
    "versionName expected 0.19.0 but received 0.16.0",
    "versionCode expected 19 but received 16",
  ]);
});

test("APK analyzer root-absolute paths match the ARM64 Unity library", () => {
  assert.deepEqual(validateMetadata({
    ...validMetadata,
    files: ["/lib/arm64-v8a/libunity.so", "/lib/arm64-v8a/libUnityARCore.so", "/assets/bin/Data/globalgamemanagers"],
  }), []);
});

test("APK analyzer root-absolute paths still reject forbidden ABIs", () => {
  assert.deepEqual(validateMetadata({
    ...validMetadata,
    files: ["/lib/arm64-v8a/libunity.so", "/lib/arm64-v8a/libUnityARCore.so", "/lib/x86_64/libunity.so"],
  }), ["forbidden native ABI x86_64"]);
});

function withTempApk(run) {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-apk-"));
  const apkPath = join(directory, "Bigimong-AR-v0.19-debug.apk");
  const reportPath = join(directory, "Bigimong-AR-v0.19-verification.json");
  writeFileSync(apkPath, "deterministic fake apk bytes");
  return Promise.resolve(run({ apkPath, reportPath })).finally(() => {
    rmSync(directory, { recursive: true, force: true });
  });
}

test("verifyApk writes a passing v0.19 report with SHA-256", async () => {
  await withTempApk(async ({ apkPath, reportPath }) => {
    const report = await verifyApk({
      apkPath,
      reportPath,
      inspect: async () => validMetadata,
    });

    assert.equal(report.valid, true);
    assert.equal(report.expected.applicationId, "com.bigimong.app");
    assert.match(report.sha256, /^[a-f0-9]{64}$/);
    assert.equal(report.sizeBytes, Buffer.byteLength("deterministic fake apk bytes"));
    assert.deepEqual(JSON.parse(readFileSync(reportPath, "utf8")), report);
  });
});

test("verifyApk rejects a wrong manifest value and writes the failed report", async () => {
  await withTempApk(async ({ apkPath, reportPath }) => {
    await assert.rejects(
      verifyApk({
        apkPath,
        reportPath,
        inspect: async () => ({ ...validMetadata, minSdk: "27" }),
      }),
      /APK verification failed: minSdk expected 28 but received 27/,
    );

    const report = JSON.parse(readFileSync(reportPath, "utf8"));
    assert.equal(report.valid, false);
    assert.ok(report.errors.includes("minSdk expected 28 but received 27"));
  });
});

test("verifyApk rejects forbidden ABIs", async () => {
  await withTempApk(async ({ apkPath, reportPath }) => {
    await assert.rejects(
      verifyApk({
        apkPath,
        reportPath,
        inspect: async () => ({
          ...validMetadata,
          files: [...validMetadata.files, "lib/x86_64/libunity.so"],
        }),
      }),
      /forbidden native ABI x86_64/,
    );
  });
});

test("verifyApk rejects an APK without the ARM64 Unity library", async () => {
  await withTempApk(async ({ apkPath, reportPath }) => {
    await assert.rejects(
      verifyApk({
        apkPath,
        reportPath,
        inspect: async () => ({ ...validMetadata, files: [] }),
      }),
      /missing lib\/arm64-v8a\/libunity\.so/,
    );
  });
});

for (const [field, value, message] of [
  ["permissions", [], /CAMERA/],
  ["arCoreRequirement", undefined, /ARCore/],
  ["signatureVerified", false, /signature/],
  ["targetSdk", "34", /targetSdk/],
]) {
  test(`APK rejects missing or invalid ${field}`, () => {
    assert.match(validateMetadata({ ...validMetadata, [field]: value }).join(";"), message);
  });
}
test("APK requires the actual native ARCore provider", () => {
  assert.match(validateMetadata({ ...validMetadata, files: ["lib/arm64-v8a/libunity.so"] }).join(";"), /libUnityARCore/);
});
test("inspection invokes signature verification and reads optional ARCore metadata", async () => {
  const commands = [];
  const inspect = (failSignature = false) => inspectWithApkAnalyzer("game.apk", {
    env: { APK_ANALYZER: "/sdk/apkanalyzer", APKSIGNER: "/sdk/apksigner" },
    exists: () => true,
    execute(file, args) {
      commands.push([file, ...args]);
      if (file.endsWith("apksigner")) {
        if (failSignature) throw new Error("signature rejected");
        return "Signer #1 certificate SHA-256 digest: abc123";
      }
      if (args[1] === "print") return '<manifest><application><meta-data android:value="optional" android:name="com.google.ar.core" /></application></manifest>';
      if (args[1] === "permissions") return "android.permission.CAMERA";
      return "36";
    },
  });
  const result = await inspect();
  assert.equal(result.arCoreRequirement, "optional");
  assert.equal(result.signatureVerified, true);
  assert.deepEqual(result.permissions, ["android.permission.CAMERA"]);
  assert.ok(commands.some(c => c.join(" ") === "/sdk/apksigner verify --verbose --print-certs game.apk"));
  await assert.rejects(inspect(true), /signature rejected/);
});
