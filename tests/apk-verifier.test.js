import assert from "node:assert/strict";
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import test from "node:test";
import { validateMetadata, verifyApk } from "../scripts/verify-apk.mjs";

const validMetadata = {
  applicationId: "com.bigimong.app",
  versionName: "0.12.0",
  versionCode: "12",
  minSdk: "28",
  files: ["lib/arm64-v8a/libunity.so", "assets/bin/Data/globalgamemanagers"],
};

test("APK analyzer root-absolute paths match the ARM64 Unity library", () => {
  assert.deepEqual(validateMetadata({
    ...validMetadata,
    files: ["/lib/arm64-v8a/libunity.so", "/assets/bin/Data/globalgamemanagers"],
  }), []);
});

test("APK analyzer root-absolute paths still reject forbidden ABIs", () => {
  assert.deepEqual(validateMetadata({
    ...validMetadata,
    files: ["/lib/arm64-v8a/libunity.so", "/lib/x86_64/libunity.so"],
  }), ["forbidden native ABI x86_64"]);
});

function withTempApk(run) {
  const directory = mkdtempSync(join(tmpdir(), "bigimong-apk-"));
  const apkPath = join(directory, "Bigimong-AR-v0.12-debug.apk");
  const reportPath = join(directory, "Bigimong-AR-v0.12-verification.json");
  writeFileSync(apkPath, "deterministic fake apk bytes");
  return Promise.resolve(run({ apkPath, reportPath })).finally(() => {
    rmSync(directory, { recursive: true, force: true });
  });
}

test("verifyApk writes a passing v0.12 report with SHA-256", async () => {
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
