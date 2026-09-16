import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
const root = new URL("../unity/BigimongAR/Assets/BigimongAR/", import.meta.url);
const read = path => readFileSync(new URL(path, root), "utf8");

// Portable wiring smoke only. Literal Unity editor behavior checks are the acceptance gate.
test("home quality tiers preserve identity while reducing effects", () => {
  const source = read("Scripts/HomeStageQualityController.cs");
  assert.match(source, /Application\.targetFrameRate = 60/);
  assert.match(source, /HIGH, MEDIUM, LOW/);
  assert.match(source, /1f, 0\.75f, 0\.6f/);
  assert.match(source, /33\.333/);
  assert.match(source, /120/);
  assert.doesNotMatch(source, /localScale.*quality|bodyPlan.*quality|System.Linq/i);
  assert.match(source, /Application.lowMemory \+=/);
  assert.match(source, /Application.lowMemory -=/);
  assert.match(source, /Profiler.GetTotalAllocatedMemoryLong/);
  assert.match(source, /DEVELOPMENT_BUILD \|\| UNITY_EDITOR/);
});

test("quality integrates bounded effects, pending ownership and build behavior gates", () => {
  assert.match(read("Scripts/HomeFocusStage.cs"), /HandleLowMemory/);
  assert.match(read("Scripts/HomeFocusStage.cs"), /switchGeneration/);
  assert.match(read("Scripts/HatchSequenceDirector.cs"), /ReleaseTransientEffects/);
  assert.match(read("Scripts/EggCrackVfx.cs"), /SetFragmentBudget/);
  const checks = read("Editor/HomeStagePerformanceEditorChecks.cs");
  for (const name of ["PolicyBoundaries", "TierIdentityAndLod", "MemoryRecovery", "Diagnostics", "ContextLoss"])
    assert.match(checks, new RegExp(name));
  assert.match(read("Editor/BigimongAndroidBuild.cs"), /HomeStagePerformanceEditorChecks.RunBehaviorChecks\(\)/);
});

test("low-memory burst shedding preserves shell presentation", () => {
  assert.match(read("Scripts/EggCrackVfx.cs"), /public void ReleaseTransientPool\(\)/);
  const release = read("Scripts/HatchSequenceDirector.cs").match(/public void ReleaseTransientEffects\(\)[\s\S]*?\n        }/)[0];
  assert.match(release, /effects\?\.ReleaseTransientPool\(\)/);
  assert.doesNotMatch(release, /effects\?\.Reset\(\)/);
  assert.match(read("Editor/HomeStagePerformanceEditorChecks.cs"), /BurstWindowMemoryPressure/);
});

test("all framing enables require a native-created render target", () => {
  const source = read("Scripts/HomeFocusStage.cs");
  assert.match(source, /private void RefreshCameraEnabled\(\)/);
  assert.match(source, /stageCamera.enabled = hasBounds && Texture != null && Texture.IsCreated\(\)/);
  assert.doesNotMatch(source, /stageCamera.enabled = Texture != null;/);
  assert.match(read("Editor/HomeStagePerformanceEditorChecks.cs"), /ordinary switch with valid native target stays rendered/);
});

test("fallible quality admission stays inside bounded pre-publication retry", () => {
  const quality = read("Scripts/HomeStageQualityController.cs");
  const stage = read("Scripts/HomeFocusStage.cs");
  assert.match(quality, /collectLodGroups/);
  assert.match(quality, /SubjectAdmission PrepareSubject\(GameObject subject\)/);
  assert.match(stage, /admission = quality != null \? quality.PrepareSubject\(candidate\)/);
  assert.match(stage, /quality\?\.CommitSubject\(admission\)/);
  assert.doesNotMatch(stage, /quality\?\.BindSubject\(candidate\)/);
  const checks = read("Editor/HomeStagePerformanceEditorChecks.cs");
  assert.match(checks, /AdmissionAllocationRecovery\(\);/);
  assert.match(checks, /PressureBetweenBurstAndWarm\(\);/);
});
