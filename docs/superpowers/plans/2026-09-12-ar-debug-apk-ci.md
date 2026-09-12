# Bigimong AR Debug APK CI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce a GitHub Actions artifact containing an installable ARM64 Bigimong Unity AR debug APK while preserving the existing Kotlin/Compose plus Unity-as-a-Library production architecture.

**Architecture:** Add a Unity Editor build entry point that creates the existing AR battle scene and builds a standalone device-test APK with the approved package identity. A manually triggered GameCI workflow runs the verified Node contract tests first, then invokes Unity 6000.0.58f2 and uploads the APK. This APK is a device-test deliverable only; the existing `android` application and its Unity-as-a-Library handoff remain unchanged for the combined production build.

**Tech Stack:** Unity 6.0.58f2, AR Foundation 6.1.1, ARCore XR Plugin 6.1.1, C# Editor build API, Node.js 20 test runner, GitHub Actions, GameCI Unity Builder v4.

**Spec:** `docs/AR_BATTLE_SPEC_v0.8.md`

## Global Constraints

- Keep package/application ID `com.bigimong.app`.
- Build a development APK for `arm64-v8a` only.
- Keep Android `minSdk = 28` and the existing ARCore-optional Compose fallback source unchanged.
- Preserve the existing 0.3m, 0.9m, and 2.1m evolution-stage heights.
- Preserve the existing authoritative server battle flow and ten-second automatic-choice behavior.
- Do not implement or fake Cloud Anchor hosting/resolving without Google Cloud and ARCore Extensions credentials.
- Never commit Unity account credentials or license contents; read them only from GitHub Actions Secrets.

---

### Task 1: Lock the APK build contract with tests

**Files:**
- Create: `tests/apk-build.test.js`
- Test: `tests/apk-build.test.js`

**Interfaces:**
- Consumes: Existing Unity project path `unity/BigimongAR` and Node `npm test` command.
- Produces: File-level contract for `Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk` and `.github/workflows/build-ar-debug-apk.yml`.

- [x] **Step 1: Write the failing test**

```javascript
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
```

- [x] **Step 2: Run test to verify it fails**

Run: `node --test tests/apk-build.test.js`

Expected: FAIL because `BigimongAndroidBuild.cs` and the workflow do not exist.

### Task 2: Add the Unity Android debug builder

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Editor/BigimongAndroidBuild.cs`
- Test: `tests/apk-build.test.js`

**Interfaces:**
- Consumes: `BigimongArSceneBuilder.CreateScene()` and `Assets/BigimongAR/Scenes/ArBattle.unity`.
- Produces: `public static void Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk()` and `build/Android/Bigimong-AR-v0.11-debug.apk`.

- [x] **Step 1: Implement the minimal editor build method**

Create a static Editor method that calls the scene builder, sets `com.bigimong.app`, version `0.11.0`, version code `11`, ARM64/IL2CPP, development and script-debugging build options, builds to the repository-level `build/Android/Bigimong-AR-v0.11-debug.apk`, and throws `BuildFailedException` unless `BuildResult.Succeeded`.

- [x] **Step 2: Run the focused test**

Run: `node --test tests/apk-build.test.js`

Expected: Unity entry-point test passes; workflow test still fails because the workflow does not exist.

### Task 3: Add the manually triggered GitHub Actions build

**Files:**
- Create: `.github/workflows/build-ar-debug-apk.yml`
- Test: `tests/apk-build.test.js`

**Interfaces:**
- Consumes: GitHub Secrets `UNITY_LICENSE`, `UNITY_EMAIL`, and `UNITY_PASSWORD` and build method `Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk`.
- Produces: GitHub artifact `Bigimong-AR-v0.11-debug` containing `build/Android/Bigimong-AR-v0.11-debug.apk`.

- [x] **Step 1: Add the workflow**

Use `workflow_dispatch`, `actions/checkout@v4`, `actions/setup-node@v4`, `npm test`, `actions/cache@v4` for `unity/BigimongAR/Library`, `game-ci/unity-builder@v4` with `targetPlatform: Android`, `unityVersion: 6000.0.58f2`, `projectPath: unity/BigimongAR`, and the custom build method, followed by `actions/upload-artifact@v4` with `if: success()` and a 14-day retention period.

- [x] **Step 2: Run the focused test**

Run: `node --test tests/apk-build.test.js`

Expected: 2 tests pass, 0 fail.

- [x] **Step 3: Run the full regression suite**

Run: `npm test`

Expected: 50 tests pass, 0 fail after the version-contract test in Task 4 is added.

### Task 4: Document the device-test build and bump the foundation version

**Files:**
- Modify: `README.md`
- Modify: `unity/BigimongAR/README.md`
- Modify: `package.json`
- Modify: `android/app/build.gradle.kts`
- Modify: `tests/android-project.test.js`

**Interfaces:**
- Consumes: Artifact name and APK path from Task 3.
- Produces: v0.11 documentation explaining that the artifact is a standalone AR device-test APK and that the production Compose integration remains Unity as a Library.

- [x] **Step 1: Add a failing version assertion**

Change the Android project test to require `versionName = "0.11.0"` and add a package test requiring `"version": "0.11.0"`.

- [x] **Step 2: Run the version test to verify it fails**

Run: `node --test tests/android-project.test.js tests/apk-build.test.js`

Expected: FAIL because project files still declare v0.10.

- [x] **Step 3: Apply the v0.11 version and documentation**

Set Android `versionCode = 11`, `versionName = "0.11.0"`, package version `0.11.0`, update the main heading to v0.11, and document Actions → Build Bigimong AR debug APK → Run workflow → Artifacts download. State that Unity Personal activation values belong only in repository Actions Secrets.

- [x] **Step 4: Run the complete verification**

Run: `npm test`

Expected: 50 tests pass, 0 fail.

Run: `git diff --check` when the local Git repository is available.

Expected: no whitespace errors.
