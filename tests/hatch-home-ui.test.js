import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";
const root = new URL("../unity/BigimongAR/Assets/BigimongAR/", import.meta.url);
const read = path => readFileSync(new URL(path, root), "utf8");

test("hatch home uses one reference-styled safe-area canvas", () => {
  const view = read("Scripts/HatchHomeView.cs");
  const builder = read("Editor/BigimongArSceneBuilder.cs");
  for (const text of ["TMP_Text", "비기알", "공룡", "아바타", "부화하기", "정면 보기", "걸음 연동 준비 중"])
    assert.ok(view.includes(text), text);
  for (const text of ["Hatch Home UI", "DeviceSafeArea", "HatchHomeCoordinator", "CreateHatchHome"])
    assert.ok(builder.includes(text), text);
  for (const canvas of ["AR Battle HUD", "Avatar Creator", "Avatar Editor Entry"])
    assert.match(builder, new RegExp(`new GameObject\\(\\"${canvas}\\"[^;]+typeof\\(CanvasGroup\\)`), `${canvas} owns input gating`);
  assert.doesNotMatch(builder, /GetComponent<CanvasGroup>\(\) \?\?/);
  assert.doesNotMatch(view, /LegacyRuntime\.ttf/);
});

test("home typography, side activity and scene behavior gates are wired", () => {
  const checks = read("Editor/HatchHomeSceneEditorChecks.cs");
  for (const check of ["LiteralPhasePresentation", "SideActivityRules", "FailedSaveKeepsSnapshot", "CodexIsPreviewOnly", "PresentationBindings"])
    assert.ok(checks.includes(check), check);
  assert.ok(read("Editor/BigimongAndroidBuild.cs").includes("HatchHomeSceneEditorChecks.RunSceneChecks()"));
  const fontBuilder = read("Editor/BigimongFontAssetBuilder.cs");
  assert.ok(fontBuilder.includes("TMP_FontAsset.CreateFontAsset"));
  assert.ok(fontBuilder.includes("!font.HasCharacter(character)"), "repeat font preparation adds only unresolved glyphs");
  assert.ok(fontBuilder.includes("missing glyphs after population"), "font preparation verifies the serialized result");
  assert.match(read("Scripts/HomeSideActivityService.cs"), /Snapshot = snapshot\?\.Clone\(\)/);
  assert.ok(read("Scripts/BigimongReferenceUi.cs").includes("retainedOverlayGroups"));
  assert.match(read("Scripts/BigimongReferenceUi.cs"), /if \(!homeInputOwned\) return;/);
  assert.match(read("Scripts/BigimongReferenceUi.cs"), /if \(homeInputOwned\) return;/);
  assert.ok(read("Scripts/BigimongReferenceUi.cs").includes("appliedRetainedPhase"), "exact retained phase signature");
  assert.ok(read("Scripts/BigimongReferenceUi.cs").includes("appliedRetainedFocusControls"), "exact eligible-control signature");
  for (const check of ["RejectedResultsAreIsolated", "RetainedOverlayInputOwnership", "RetainedFocusSurvivesRepeatedHide", "RealSummonToBattleRestoresFocus", "PortraitNavigationTouchTargets"])
    assert.ok(checks.includes(check), check);
  assert.ok(checks.includes("BindSceneEventSystem"), "retained focus checks explicitly bind the generated scene EventSystem");
  assert.equal((read("Editor/BigimongArSceneBuilder.cs").match(/^using TMPro;$/gm) || []).length, 1);
});

// Portable wiring smoke only. Literal behavioral assertions execute in Unity's editor gate.
test("focus stage locks approved gesture thresholds", () => {
  const classifier = read("Scripts/HomeGestureClassifier.cs");
  const orbit = read("Scripts/HomeStageOrbitInput.cs");
  assert.match(classifier, /TapSeconds = 0\.22f/);
  assert.match(classifier, /DragDp = 18f/);
  assert.match(orbit, /MaxInertiaSeconds = 0\.35f/);
  assert.match(orbit, /Screen\.dpi/);
  assert.match(orbit, /ResetFront/);
  assert.doesNotMatch(orbit, /pinch|zoom/i);
});

test("focus stage owns one active 3D subject and resource lifecycle", () => {
  const stage = read("Scripts/HomeFocusStage.cs");
  for (const symbol of ["RenderTexture", "ShowEgg", "ShowBaby", "ShowAvatar", "DestroyActiveSubject", "SetViewportSize", "OnDisable", "OnDestroy"])
    assert.ok(stage.includes(symbol), symbol);
});

test("stage behavior checks remain registered alongside prior gates", () => {
  const checks = read("Editor/HomeFocusStageEditorChecks.cs");
  for (const symbol of ["GestureBoundaries", "PointerOwnershipAndOrbit", "SubjectTransactions", "ResolutionAndCleanup", "MotionRestoresAuthoredPose", "ProceduralActorCannotCollapseCandidate"])
    assert.ok(checks.includes(symbol), symbol);
  const build = read("Editor/BigimongAndroidBuild.cs");
  for (const name of ["HomeFocusStageEditorChecks", "HomeCharacterResolverEditorChecks", "HatchSelectionEditorChecks", "ReferenceVisualEditorChecks", "OfflineBetaEditorChecks"])
    assert.ok(build.includes(`${name}.RunBehaviorChecks()`), name);
});

test("stage explicitly suppresses incompatible AR-only procedural motion", () => {
  assert.match(read("Scripts/HomeFocusStage.cs"), /SuppressArMotion/);
});

test("stage exposes private allocation and disable transactions to executable editor regressions", () => {
  const stage = read("Scripts/HomeFocusStage.cs");
  assert.ok(stage.includes("createRenderTexture"), "native allocation seam");
  assert.ok(stage.includes("DisablePreservingAuthoredPose"), "lifecycle transaction seam");
  const checks = read("Editor/HomeFocusStageEditorChecks.cs");
  assert.ok(checks.includes("AllocationFailurePreservesTarget"));
  assert.ok(checks.includes("DisableTransactionRestoresAfterRuntimeLifecycle"));
});

test("hatch sequence uses the approved durable timeline", () => {
  const sequence = read("Scripts/HatchSequenceDirector.cs");
  const coordinator = read("Scripts/HatchHomeCoordinator.cs");
  for (const value of ["0.7f", "1.8f", "2.4f", "3.2f", "4.2f", "5.0f"])
    assert.ok(sequence.includes(value), `missing timeline boundary ${value}`);
  for (const symbol of ["HatchSelectionService", "SHELL_BURST", "REVEALED", "InputLockChanged"])
    assert.ok(coordinator.includes(symbol), symbol);
  assert.doesNotMatch(sequence, /skip/i);
});

test("recoverable hatch behavioral gate is registered with bounded effects", () => {
  const checks = read("Editor/HatchSequenceEditorChecks.cs");
  for (const symbol of ["LiteralTimelineAndSettings", "DurableFailureMatrix", "RecoveryAndSubscriptions", "BoundedEffects", "SubjectRules"])
    assert.ok(checks.includes(symbol), symbol);
  assert.ok(read("Editor/BigimongAndroidBuild.cs").includes("HatchSequenceEditorChecks.RunBehaviorChecks()"));
  assert.ok(read("Scripts/EggCrackVfx.cs").includes("Reset"));
});

test("hatch presentation guards bindings and pending model switches", () => {
  const sequence = read("Scripts/HatchSequenceDirector.cs");
  assert.ok(sequence.includes("HasPresentationBindings"));
  assert.ok(sequence.includes("CompleteHomeWhenReady"));
  assert.ok(read("Editor/HatchSequenceEditorChecks.cs").includes("CallbackExceptionsAndInterruption"));
});

test("hatch review regressions retain reentrancy recovery and silhouette guards", () => {
  const director = read("Scripts/HatchSequenceDirector.cs");
  const coordinator = read("Scripts/HatchHomeCoordinator.cs");
  for (const symbol of ["runGeneration", "silhouette", "PresentFocusTransition"])
    assert.ok(director.includes(symbol), symbol);
  for (const symbol of ["RecoverTransaction", "NotifySnapshotSafely"])
    assert.ok(coordinator.includes(symbol), symbol);
});
test("shipped hatch scene binds runtime-generated audio and registers literal audio behavior checks", () => {
  const builder = read("Editor/BigimongArSceneBuilder.cs");
  assert.match(builder, /AddComponent<HatchAudioBinding>/);
  assert.match(builder, /playOnAwake = false/);
  assert.match(read("Scripts/HatchHomeView.cs"), /GetComponent<HatchAudioBinding>\(\)\.EnsureBound\(\)/);
  const binding = read("Scripts/HatchAudioBinding.cs");
  assert.match(binding, /private void Awake\(\).*EnsureBound/);
  assert.match(binding, /ConfigureAudio/);
  assert.match(binding, /AudioClip\.Create/);
  assert.match(binding, /OnDestroy/);
  const checks = read("Editor/HatchAudioEditorChecks.cs");
  for (const name of ["RuntimeBindingGeneratesUsableCues", "MuteChangesPresentationNotDurableTimeline", "RunSceneChecks"])
    assert.match(checks, new RegExp(name));
  const build = read("Editor/BigimongAndroidBuild.cs");
  assert.match(build, /HatchAudioEditorChecks\.RunBehaviorChecks\(\)/);
  assert.match(build, /HatchAudioEditorChecks\.RunSceneChecks\(\)/);
});
