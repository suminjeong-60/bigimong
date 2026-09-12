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
  assert.match(source, /Bigimong-AR-v0\.11-debug\.apk/);
});

test("GitHub Actions verifies source, builds Unity, and uploads the APK", () => {
  const workflow = read(".github/workflows/build-ar-debug-apk.yml");
  assert.match(workflow, /workflow_dispatch:/);
  assert.match(workflow, /npm test/);
  assert.match(workflow, /game-ci\/unity-builder@v4/);
  assert.match(workflow, /buildMethod: Bigimong\.Editor\.BigimongAndroidBuild\.BuildDebugApk/);
  assert.match(workflow, /actions\/upload-artifact@v4/);
  assert.match(workflow, /Bigimong-AR-v0\.11-debug\.apk/);
  assert.match(workflow, /secrets\.UNITY_LICENSE/);
});

test("GitHub Actions uses Node 24 for built-in SQLite integration tests", () => {
  const workflow = read(".github/workflows/build-ar-debug-apk.yml");
  assert.match(workflow, /^\s*node-version:\s*24\s*$/m);
});

test("GitHub Actions does not request an npm cache without a lock file", () => {
  const workflow = read(".github/workflows/build-ar-debug-apk.yml");
  assert.doesNotMatch(workflow, /^\s*cache:\s*npm\s*$/m);
});

test("foundation package version matches the v0.11 APK", () => {
  const packageJson = JSON.parse(read("package.json"));
  assert.equal(packageJson.version, "0.11.0");
});

test("generated Android and Unity build state is excluded from source control", () => {
  const gitignore = read(".gitignore");
  assert.match(gitignore, /^\/build\/$/m);
  assert.match(gitignore, /^\/unity\/BigimongAR\/Library\/$/m);
  assert.match(gitignore, /^\/unity\/BigimongAR\/Temp\/$/m);
  assert.match(gitignore, /^\/unity\/BigimongAR\/Logs\/$/m);
});
