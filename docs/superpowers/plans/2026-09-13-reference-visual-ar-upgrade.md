# Reference Visual and AR Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Improve the playable Bigimong beta's real 3D avatars, all 30 monster appearances, and AR HUD according to the user's three attached reference images, and enable the actual ARCore camera provider in the next Android build.

**Architecture:** Keep the current factory/controller/battle flow interfaces. Add focused visual helpers and a declarative appearance catalog rather than changing combat. Configure ARCore at editor build time and track camera pose at runtime; retain the explicit screen-fixed fallback.

**Tech Stack:** Unity 6000.0.58f2, AR Foundation/ARCore 6.1.1, Unity UI, C#, Node tests and GitHub Actions.

**Spec:** `docs/art-reference/README.md` plus `docs/superpowers/specs/2026-09-12-playable-ar-beta-avatar-combat-design.md`.

## Global Constraints

- Unity `6000.0.58f2`, AR Foundation and ARCore `6.1.1` remain pinned.
- Android package remains `com.bigimong.app`, minSdk 28, portrait, IL2CPP, ARM64 only.
- Existing 01–30 species IDs, elements, stats, ten-second turns, 0.30/0.90/2.10m growth heights, wallet and ownership rules are unchanged.
- References are art direction, never counterfeit live AR photos or static full-screen stand-ins for 3D gameplay.
- Keep avatar profile schema and all customization ranges; base dark sleeveless outfit and golden medallion remain.
- Cosmetics are separate removable transforms and do not affect stats.
- Non-AR fallback must be honestly labeled; no fake Bluetooth/Cloud Anchor success.
- Preserve all current tests' behavioral intent; add executable Unity Editor checks for new runtime geometry/UI behavior. Local Node source checks are not Unity compilation or rendered visual QA.

### Task 1: Reference-directed generated models and HUD

**Files:**
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralAvatarFactory.cs`, `AvatarCreatorController.cs` if needed.
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralDragonFactory.cs`.
- Create: focused appearance catalog/geometry/accessory helpers under the same Scripts directory.
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleHud.cs`, `ArBattleArenaController.cs` if needed for rings and labels.
- Modify: `unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs` and `BigimongAndroidBuild.cs` only to wire added visual checks.
- Test: add `tests/reference-visuals.test.js` and `unity/BigimongAR/Assets/BigimongAR/Editor/ReferenceVisualEditorChecks.cs`.

**Interfaces:**
- Consumes unchanged `ProceduralAvatarFactory.Create/Apply(AvatarProfile)`, `ProceduralDragonFactory.Create(int,string)`, `ArBattleHud.SetBattleState(...)`, `ArBattleActor` named motion anchors/material ownership.
- Produces real 3D facial/body/silhouette upgrades, deterministic curated colors and detachable accessories; HUD wired to live battle state.

- [ ] Read the three actual PNG references, current factories/controllers, and editor checks. Preserve part names used by summoning and combat.
- [ ] Add failing source wiring checks and executable editor assertions. The Unity checks should create all 30 monsters in all 3 stages, assert nonzero renderer bounds and finite scales, required eye highlights, correct distinctive parts (horn/frill, plate, sail, wings/fins), and removable cosmetic root with unaffected body. Check male/female default avatars and all hair IDs remain constructible. Check HP bar fill responds to `SetBattleState` with clamping and decorative overlays do not swallow input.
```js
import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const read = p => readFileSync(new URL('../unity/BigimongAR/Assets/BigimongAR/'+p, import.meta.url),'utf8');
test('reference visual behavior checks run before Android build', () => {
  assert.match(read('Editor/BigimongAndroidBuild.cs'), /ReferenceVisualEditorChecks\.Run/);
});
```
- [ ] Run `node --test tests/reference-visuals.test.js`; require expected failure before implementing.
- [ ] Implement reference style. Use smooth coherent silhouettes, large inset layered eyes and highlights, cream bellies/muzzles, species colors, recognizable dinosaur anatomy. Replace box wings/cylinder horns with shaped geometry when feasible. Do not give water reptiles/ordinary T-rex arbitrary wings. Keep cosmetic pieces separate. Add bounds-safe framing, studio lighting/background to creator; cyan/navy translucent battle panels, green HP bars, readable labels, large directional controls, cyan rings with visible center and live camera.
- [ ] Run `npm test`, inspect changed files for resources/lifecycle and control overlap. Document inability to perform local Unity rendering if no editor is available. Commit task changes with tests and a report. Do not change version or AR loader configuration in this task.

### Task 2: Actual AR camera setup and Android delivery verification

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArBuildConfiguration.cs`.
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArCameraPoseDriver.cs` if using XR InputTracking directly.
- Modify: `BigimongAndroidBuild.cs`, `BigimongArSceneBuilder.cs`, `Packages/manifest.json` only where a direct module dependency is required.
- Test: `tests/ar-project.test.js` and executable editor assertions.
- Modify for next version consistently: package metadata, Android metadata, build workflow/verifier and documented deliverable names to 0.13.0/code 13.

**Interfaces:**
- Consumes generated scene and existing camera/ARSession; does not change visual task interfaces.
- Produces assigned `UnityEngine.XR.ARCore.ARCoreLoader`, initialize-on-startup configuration, tracked camera position/rotation, CAMERA permission in produced manifest.

- [ ] Confirm actual package APIs from pinned package source/official docs before using them. Identify why ARCore loader was not enabled.
- [ ] Add source wiring regression tests and editor checks which assert Android XR manager has an ARCore loader and the camera is pose-driven.
```js
test('Android build explicitly initializes the ARCore provider', () => {
  const setup = read('Assets/BigimongAR/Editor/BigimongArBuildConfiguration.cs');
  assert.match(setup, /ARCoreLoader/);
  assert.match(setup, /InitManagerOnStart/);
});
```
- [ ] Implement loader assignment idempotently with editor APIs, save assets, and fail the build if assignment fails. Add camera pose updates that consume XR center-eye pose only while device tracking is valid, including before-render updates, with disabled lifecycle cleanup.
- [ ] Extend APK inspection to check target SDK, CAMERA permission/AR provider metadata, and run official `apksigner verify` in CI. Add behavior tests with fixture subprocess outputs rather than just comparing source strings for the verifier.
- [ ] Bump to version 0.13.0/code 13 after all reference changes; run all Node tests and obtain reviewer sign-off.
- [ ] Publish tested changed files to the existing authorized feature branch, preserving remote base tree and concurrent edits. Trigger Unity CI and inspect compilation, editor checks, APK report, signing verification and artifact upload. Deliver actual APK if successful, with exact remaining real-device limitations.
