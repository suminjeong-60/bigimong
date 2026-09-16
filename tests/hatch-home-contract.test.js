import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";

const root = new URL("../unity/BigimongAR/Assets/BigimongAR/", import.meta.url);
const read = path => readFileSync(new URL(path, root), "utf8");

test("home resolution prefers stable imported resources and strips optional equipment", () => {
  const source = read("Scripts/HomeCharacterResolver.cs");
  assert.match(source, /GeneratedCharacters\/Bigimong_Egg/);
  assert.match(source, /GeneratedCharacters\/Avatar_Masculine/);
  assert.match(source, /GeneratedCharacters\/Avatar_Feminine/);
  assert.match(source, /ProceduralEggFactory\.Create/);
  assert.match(source, /ProceduralDragonFactory\.Create\(artId, "BABY"\)/);
  assert.match(source, /HomeBaseAppearance\.Apply/);
  assert.doesNotMatch(source, /VARCO.*generate|authorize|token/i);
  assert.match(read("Editor/BigimongAndroidBuild.cs"), /HomeCharacterResolverEditorChecks\.RunBehaviorChecks\(\)/);
});

test("home asset smoke gates point to executable identity and import budget fixtures", () => {
  const checks = read("Editor/HomeCharacterResolverEditorChecks.cs");
  for (const name of ["ImportedPrecedenceAndSourceIsolation", "AllBabyIdentities", "EggGeometryAndReuse", "AvatarBaseAndArDefaults"])
    assert.match(checks, new RegExp(name));
  const pipeline = read("Editor/VarcoAssetImportPipeline.cs");
  assert.match(pipeline, /TryValidateProductionQuality/);
  assert.match(pipeline, /GetSourceTextureWidthAndHeight/);
  const imports = read("Editor/VarcoAssetImportEditorChecks.cs");
  assert.match(imports, /ProductionBudgetBoundaries/);
  assert.match(imports, /LodAcceptanceBoundaries/);
  assert.match(pipeline, /if \(!group\.enabled\)/);
  assert.match(imports, /DisabledLodGroupsAreNotAccepted/);
  assert.match(checks, /NestedProtectedRootsRemainActive/);
  assert.match(read("Scripts/HomeBaseAppearance.cs"), /HasProtectedSubtree/);
});

test("v0.19 defines a five-phase durable hatch state", () => {
  assert.equal(existsSync(new URL("Scripts/HatchHomeState.cs", root)), true);
  const source = read("Scripts/HatchHomeState.cs");
  for (const token of ["EGG_ACTIVE", "HATCH_READY", "HATCHING", "REVEAL", "HOME"])
    assert.ok(source.includes(token), `missing phase ${token}`);
  assert.match(source, /public const int HatchTarget = 30000/);
  assert.match(source, /public int selectedArtId/);
  assert.match(source, /public List<StepProviderCursor> stepProviderCursors/);
  assert.match(source, /public List<string> processedStepEvents/);
});

test("Unity owns one complete Korean 30-species catalog", () => {
  const source = read("Scripts/BigimongSpeciesCatalog.cs");
  assert.match(source, /public const int Count = 30/);
  assert.match(source, /티라노사우루스/);
  assert.match(source, /티타노사우루스/);
  assert.match(source, /등급|grade/);
});

test("hatch persistence owns primary backup temp and v1 migration paths", () => {
  const source = read("Scripts/HatchHomeStore.cs");
  assert.match(source, /bigimong-hatch-home-v2\.json/);
  assert.match(source, /bigimong-hatch-home-v2\.backup\.json/);
  assert.match(source, /bigimong-hatch-home-v2\.tmp\.json/);
  assert.match(source, /bigimong-offline-progress-v1\.json/);
  assert.match(source, /TryMigrateV1/);
  assert.match(source, /TrySave/);
});

test("hatch persistence editor checks exercise each atomic I/O failure boundary", () => {
  const source = read("Editor/HatchHomeStoreEditorChecks.cs");
  for (const check of [
    "TempWriteFailurePreservesValidPrimary",
    "TempReadFailurePreservesValidPrimaryAndCleansTemp",
    "ExistingPrimaryReadFailurePreservesValidPrimaryAndCleansTemp",
    "BackupPromotionFailurePreservesPrimaryBackupAndCleansTemp",
    "PrimaryPromotionFailureRecoversNewerBackup",
    "FirstSaveBackupFailureDoesNotPromotePrimary",
    "CleanupDeleteFailureIsBestEffort",
  ]) assert.ok(source.includes(check), `missing editor behavior check ${check}`);
});

test("redundant selection recovery is revisioned and has real-store interruption fixtures", () => {
  assert.match(read("Scripts/HatchHomeState.cs"), /public long revision/);
  assert.match(read("Scripts/HatchHomeStore.cs"), /backup\.revision > primary\.revision/);
  const checks = read("Editor/HatchHomeStoreEditorChecks.cs");
  for (const name of ["Selected17SurvivesPrimaryCorruptionBeforeCheckpoint", "EveryRedundancyBoundaryPreservesSelection",
    "NewerBackupWinsAndIsProtectedBeforeNextWrite", "LegacyV2RevisionZeroAndEqualRevisionPrimaryWin"])
    assert.match(checks, new RegExp(name));
});

test("care and verified steps are the only hatch progress sources", () => {
  const source = read("Scripts/HatchProgressService.cs");
  assert.match(source, /CareAmount = 500/);
  assert.match(source, /TimeSpan\.FromHours\(2\)/);
  assert.match(source, /ApplyVerifiedSteps/);
  assert.match(source, /processedStepEvents/);
  assert.match(source, /stepProviderCursors/);
  assert.doesNotMatch(source, /ClaimGift|ClaimQuest|BuySnack|Feed|Play\(/);
});

// Portable source smoke only; the registered C# checks execute the real store/services.
test("store commit truth and raw phase progress invariants have executable regression gates", () => {
  const store = read("Scripts/HatchHomeStore.cs");
  assert.match(store, /return candidateCommitted/);
  assert.match(store, /CandidateIsDurable/);
  assert.match(store, /HatchSaveOutcomeUnknownException/);
  assert.match(read("Scripts/HatchHomeCoordinator.cs"), /ReloadAfterUnknownSave/);
  const state = read("Scripts/HatchHomeState.cs");
  assert.match(state, /parsedPhase == HatchHomePhase\.EGG_ACTIVE/);
  assert.match(state, /eggProgress < HatchTarget/);
  assert.match(state, /eggProgress == HatchTarget/);
  const checks = read("Editor/HatchHomeStoreEditorChecks.cs");
  for (const name of ["CommitResultTracksEveryPersistenceBoundary", "CommittedCareStepsAndSideActivitiesPublishNewestState",
    "RawPhaseProgressMismatchLosesToValidReplica", "InvalidPhaseProgressCannotBeSavedAsOwnership",
    "UnknownOutcomeRequiresReloadBeforeAnyFurtherWrite", "CoordinatorRecoversUnknownOutcomeBeforeRetry"])
    assert.match(checks, new RegExp(`${name}\\(\\)`));
});

test("restart recovery behavior is registered in the executable Unity store checks", () => {
  const checks = read("Editor/HatchHomeStoreEditorChecks.cs");
  const runBody = checks.match(/public static void RunBehaviorChecks\(\)\s*\{([\s\S]*?)\n\s*\}/)?.[1] ?? "";
  for (const name of ["RestartedStoreFailsClosedUntilNewestReplicaRecovers", "CoordinatorInitialLoadRetriesNewestReplica",
    "SystemFileSystemExistsPreservesProbeErrors"])
    assert.match(runBody, new RegExp(`${name}\\(\\)`));
  assert.match(read("Editor/BigimongAndroidBuild.cs"), /HatchHomeStoreEditorChecks\.RunBehaviorChecks\(\)/);
});

test("hatch selection saves one equal-pool art id before playback", () => {
  const source = read("Scripts/HatchSelectionService.cs");
  assert.match(source, /Random\.Range\(1, 31\)/);
  assert.match(source, /HATCH_READY/);
  assert.match(source, /HATCHING/);
  assert.match(source, /STARTED/);
  assert.match(source, /TrySave/);
  assert.match(source, /store\.Load\(\)/);
});
