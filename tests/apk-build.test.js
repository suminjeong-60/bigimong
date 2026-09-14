import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const root = new URL("../", import.meta.url);
const read = (path) => readFileSync(new URL(path, root), "utf8");

test("Unity build entry point creates the approved ARM64 debug APK", () => {
  const source = read("unity/BigimongAR/Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");
  assert.match(source, /public static void BuildDebugApk\(\)/);
  assert.match(source, /BigimongArSceneBuilder\.CreateScene\(\)/);
  assert.match(source, /com\.bigimong\.app/);
  assert.match(source, /AndroidArchitecture\.ARM64/);
  assert.match(source, /ScriptingImplementation\.IL2CPP/);
  assert.match(source, /AndroidSdkVersions\.AndroidApiLevel28/);
  assert.match(source, /BuildOptions\.Development/);
  assert.match(source, /private const string BundleVersion = "0\.14\.0"/);
  assert.match(source, /private const int VersionCode = 15/);
  assert.match(read("scripts/verify-apk.mjs"), /versionCode: "15"/);
  assert.match(source, /Bigimong-AR-v0\.14-debug\.apk/);
});

test("GitHub Actions verifies source, builds Unity, and uploads the APK", () => {
  const workflow = read(".github/workflows/build-ar-debug-apk.yml");
  assert.match(workflow, /workflow_dispatch:/);
  assert.match(workflow, /push:\s+branches:\s+- feature\/v0\.12-offline-beta/s);
  assert.match(workflow, /npm test/);
  assert.match(workflow, /game-ci\/unity-builder@v4/);
  assert.match(workflow, /buildMethod: Bigimong\.Editor\.BigimongAndroidBuild\.BuildDebugApk/);
  assert.match(workflow, /actions\/upload-artifact@v4/);
  assert.match(workflow, /unityVersion: 6000\.0\.58f2/);
  assert.match(workflow, /node scripts\/verify-apk\.mjs/);
  assert.match(workflow, /--apk build\/Android\/Bigimong-AR-v0\.14-debug\.apk/);
  assert.match(workflow, /--report build\/Android\/Bigimong-AR-v0\.14-verification\.json/);
  assert.match(workflow, /sha256sum build\/Android\/Bigimong-AR-v0\.14-debug\.apk/);
  assert.match(workflow, /name: Bigimong-AR-v0\.14-debug/);
  assert.match(workflow, /Bigimong-AR-v0\.14-verification\.json/);
  assert.match(workflow, /Bigimong-AR-v0\.14-debug\.apk\.sha256/);
  assert.match(workflow, /if: failure\(\)/);
  assert.match(workflow, /name: Bigimong-AR-v0\.14-build-logs/);
  assert.match(workflow, /retention-days: 14/);
  assert.match(workflow, /permissions:\s+contents: read/s);
  assert.match(workflow, /secrets\.UNITY_LICENSE/);

  const verifyIndex = workflow.indexOf("name: Verify APK contract");
  const uploadIndex = workflow.indexOf("name: Upload installable APK");
  assert.ok(verifyIndex > 0 && uploadIndex > verifyIndex);
});

test("GitHub Actions uses Node 24 for built-in SQLite integration tests", () => {
  const workflow = read(".github/workflows/build-ar-debug-apk.yml");
  assert.match(workflow, /^\s*node-version:\s*24\s*$/m);
});

test("GitHub Actions does not request an npm cache without a lock file", () => {
  const workflow = read(".github/workflows/build-ar-debug-apk.yml");
  assert.doesNotMatch(workflow, /^\s*cache:\s*npm\s*$/m);
});

test("Unity manifest enables the Animation module required by ArBattleActor", () => {
  const manifest = JSON.parse(read("unity/BigimongAR/Packages/manifest.json"));
  assert.equal(manifest.dependencies["com.unity.modules.animation"], "1.0.0");
});

test("foundation package version matches the v0.15 APK", () => {
  const packageJson = JSON.parse(read("package.json"));
  assert.equal(packageJson.version, "0.15.0");
});

test("generated Android and Unity build state is excluded from source control", () => {
  const gitignore = read(".gitignore");
  assert.match(gitignore, /^\/build\/$/m);
  assert.match(gitignore, /^\/unity\/BigimongAR\/Library\/$/m);
  assert.match(gitignore, /^\/unity\/BigimongAR\/Temp\/$/m);
  assert.match(gitignore, /^\/unity\/BigimongAR\/Logs\/$/m);
});

test("v0.12 documentation distinguishes source readiness from a built APK", () => {
  const readme = read("README.md");
  const implementation = read("docs/AR_IMPLEMENTATION_v0.12.md");
  assert.match(readme, /# 비기몽 제작 기반 v0\.14/);
  assert.match(readme, /Bigimong-AR-v0\.14-verification\.json/);
  assert.match(readme, /Bigimong-AR-v0\.14-debug\.apk\.sha256/);
  assert.match(implementation, /APK 빌드 소스 준비 완료/);
  assert.match(implementation, /Unity C# 컴파일과 실제 APK 생성은 GitHub Actions 실행 전까지 미검증/);
});
