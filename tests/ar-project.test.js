import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const root = new URL("../unity/BigimongAR/", import.meta.url);
const read = (path) => readFileSync(new URL(path, root), "utf8");

test("avatar profile is versioned, normalized, and stored outside PlayerPrefs", () => {
  const profile = read("Assets/BigimongAR/Scripts/AvatarProfile.cs");
  const store = read("Assets/BigimongAR/Scripts/AvatarProfileStore.cs");
  assert.match(profile, /public const int CurrentSchemaVersion = 1/);
  for (const field of ["bodyType", "faceShapeId", "skinToneId", "eyebrowId", "eyeColorId", "hairStyleId", "hairColorId"])
    assert.match(profile, new RegExp(`public .* ${field}`));
  assert.match(profile, /Mathf\.Clamp\(faceShapeId, 1, 5\)/);
  assert.match(profile, /Mathf\.Clamp\(hairStyleId, 1, 12\)/);
  assert.match(store, /Application\.persistentDataPath/);
  assert.match(store, /File\.WriteAllText/);
  assert.doesNotMatch(store, /PlayerPrefs/);
});

test("first-launch avatar creator exposes every approved customization range", () => {
  const catalog = read("Assets/BigimongAR/Scripts/AvatarCustomizationCatalog.cs");
  const factory = read("Assets/BigimongAR/Scripts/ProceduralAvatarFactory.cs");
  const creator = read("Assets/BigimongAR/Scripts/AvatarCreatorController.cs");
  const builder = read("Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs");
  assert.match(catalog, /SkinTones = new Color\[8\]/);
  assert.match(catalog, /EyeColors = new Color\[8\]/);
  assert.match(catalog, /HairColors = new Color\[10\]/);
  assert.match(factory, /CreateHair\(.*hairStyleId/s);
  assert.match(factory, /CreateEyebrows\(.*eyebrowId/s);
  assert.match(factory, /CreateSummoningMedallion/);
  for (const part of ["AvatarRoot", "Body", "Head", "HairRoot", "NecklaceChain", "SummoningMedallion"])
    assert.match(factory, new RegExp(`"${part}"`));
  for (const part of ["Eye", "Eyebrow", "ArmUpper", "ArmLower", "Hand"])
    assert.match(factory, new RegExp(`"${part}" \\+ suffix`));
  assert.match(factory, /var suffix = side < 0 \? "Left" : "Right"/);
  assert.match(factory, /SharedMaterials/);
  assert.match(factory, /Object\.Destroy\(collider\)/);
  assert.doesNotMatch(factory, /AddComponent<Animator>/);
  assert.match(creator, /BODY.*FACE.*SKIN.*BROWS.*EYES.*HAIR.*HAIR_COLOR/s);
  assert.match(creator, /CategoryLimits = \{ 2, 5, 8, 6, 8, 12, 10 \}/);
  assert.match(creator, /nameInput\.text\.Trim\(\)/);
  assert.match(creator, /profile\.IsComplete/);
  assert.match(creator, /AvatarProfileStore\.HasSavedProfile/);
  assert.match(builder, /battleCanvas\.SetActive\(false\)/);
});

test("Unity Editor executes avatar geometry, wrapping, layout, and lifecycle checks", () => {
  const checks = read("Assets/BigimongAR/Editor/AvatarCreatorEditorTests.cs");
  const build = read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");
  const creator = read("Assets/BigimongAR/Scripts/AvatarCreatorController.cs");
  const factory = read("Assets/BigimongAR/Scripts/ProceduralAvatarFactory.cs");
  const builder = read("Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs");

  assert.match(checks, /VerifyCatalogAndWrapping/);
  assert.match(checks, /VerifyFactoryGeometry/);
  assert.match(checks, /VerifyCreatorLifecycle/);
  assert.match(checks, /VerifySceneLayout/);
  assert.match(checks, /ProceduralAvatarFactory\.Create/);
  assert.match(build, /AvatarCreatorEditorChecks\.RunBehaviorChecks\(\)/);
  assert.match(build, /AvatarCreatorEditorChecks\.RunSceneChecks\(\)/);
  assert.match(creator, /public void OpenEditor\(\)/);
  assert.match(creator, /if \(!AvatarProfileStore\.HasSavedProfile\).*Initialize\(loadedProfile, false\)/s);
  assert.match(creator, /previewAnchor\.gameObject\.SetActive\(visible\)/);
  assert.match(creator, /battleCanvas\.SetActive\(false\)/);
  assert.doesNotMatch(creator, /battleCanvas\.SetActive\(!visible\)/);
  assert.match(factory, /EllipsoidSurfaceDepth/);
  assert.match(factory, /if \(Application\.isPlaying\) Object\.Destroy\(collider\);\s*else Object\.DestroyImmediate\(collider\)/);
  assert.match(builder, /typeof\(ScrollRect\)/);
  assert.match(builder, /"Edit Avatar"/);
});

test("Unity Editor includes executable avatar profile persistence checks for CI", () => {
  const editorTests = read("Assets/BigimongAR/Editor/AvatarProfileStoreEditorTests.cs");
  const build = read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");
  assert.match(editorTests, /class AvatarProfileStoreEditorChecks/);
  assert.match(editorTests, /public static void Run\(\)/);
  assert.doesNotMatch(editorTests, /NUnit/);
  assert.match(editorTests, /IAvatarProfileFileSystem/);
  assert.match(editorTests, /NormalizeProfileValues/);
  assert.match(editorTests, /RecoverMalformedProfile/);
  assert.match(editorTests, /PreserveProfileAfterFailedPromotion/);
  assert.match(editorTests, /CleanTemporaryFileAfterPromotionFailure/);
  assert.match(build, /AvatarProfileStoreEditorChecks\.Run\(\)/);
});

test("Unity AR project pins AR Foundation and ARCore provider versions", () => {
  const manifest = JSON.parse(read("Packages/manifest.json"));
  assert.equal(manifest.dependencies["com.unity.xr.arfoundation"], "6.1.1");
  assert.equal(manifest.dependencies["com.unity.xr.arcore"], "6.1.1");
  assert.equal(manifest.dependencies["com.unity.ugui"], "2.0.0");
  assert.match(read("ProjectSettings/ProjectVersion.txt"), /6000\.0/);
});

test("AR evolution scale is expressed in real-world meters", () => {
  const models = read("Assets/BigimongAR/Scripts/ArBattleModels.cs");
  assert.match(models, /"YOUTH" => 0\.9f/);
  assert.match(models, /"ADULT" => 2\.1f/);
  assert.match(models, /_ => 0\.3f/);
});

test("AR battle uses plane raycasts, reanchoring, native payloads, and directional motion triggers", () => {
  const arena = read("Assets/BigimongAR/Scripts/ArBattleArenaController.cs");
  const bridge = read("Assets/BigimongAR/Scripts/ArBattleNativeBridge.cs");
  const actor = read("Assets/BigimongAR/Scripts/ArBattleActor.cs");
  const tracking = read("Assets/BigimongAR/Scripts/ArTrackingRecovery.cs");
  assert.match(arena, /TrackableType\.PlaneWithinPolygon/);
  assert.match(arena, /RequestReanchor/);
  assert.match(bridge, /AR_BATTLE_PAYLOAD/);
  assert.match(bridge, /ApplyRoundJson/);
  assert.match(actor, /Attack_\{NormalizeDirection/);
  assert.match(actor, /Dodge_\{NormalizeDirection/);
  assert.match(actor, /"KO"/);
  assert.match(tracking, /ARSessionState\.SessionTracking/);
});

test("shared-world alignment has an explicit Cloud Anchor provider boundary", () => {
  const provider = read("Assets/BigimongAR/Scripts/SharedAnchorProvider.cs");
  const coordinator = read("Assets/BigimongAR/Scripts/SharedAnchorCoordinator.cs");
  assert.match(provider, /abstract void Host/);
  assert.match(provider, /abstract void Resolve/);
  assert.match(provider, /cloud/i);
  assert.match(coordinator, /CLOUD_ANCHOR_HOSTED|PublishCloudAnchorId/);
  assert.match(coordinator, /battle\.youAre != "A"/);
});

test("all catalog ids have procedural 3D stand-ins and motion fallback before final rigs arrive", () => {
  const factory = read("Assets/BigimongAR/Scripts/ProceduralDragonFactory.cs");
  const actor = read("Assets/BigimongAR/Scripts/ArBattleActor.cs");
  const director = read("Assets/BigimongAR/Scripts/ArBattleDirector.cs");
  assert.match(factory, /Mathf\.Clamp\(artId, 1, 30\)/);
  for (const feature of ["Winged", "Horned", "Plated", "LongNeck", "Aquatic", "Feathered"]) {
    assert.match(factory, new RegExp(`HashSet<int> ${feature}`));
  }
  assert.match(director, /ProceduralDragonFactory\.Create/);
  assert.match(actor, /AttackMotion/);
  assert.match(actor, /DodgeMotion/);
  assert.match(actor, /KoMotion/);
  assert.match(actor, /VictoryMotion/);
  assert.match(actor, /homeRotation = transform\.localRotation/);
});

test("elemental combat catalog preserves the approved species, skill, and dodge mapping", () => {
  const source = read("Assets/BigimongAR/Scripts/ElementSkillCatalog.cs");
  const rows = [...source.matchAll(/Profile\((\d+), Element\.(Water|Fire|Earth|Wind), "([^"]+)", "([^"]+)", "([^"]+)"/g)];
  const approved = [
    [1, "Fire", "tyrant_flame_roar", "heavy_pivot"],
    [2, "Earth", "earth_charge", "horn_guard"],
    [3, "Wind", "sky_dive", "vertical_ascent"],
    [4, "Fire", "flame_spike_storm", "tail_hop"],
    [5, "Earth", "giant_tremor", "stomp_recoil"],
    [6, "Water", "abyss_water_blade", "low_body_slide"],
    [7, "Earth", "armored_quake_hammer", "armored_curl"],
    [8, "Fire", "forest_flame_claws", "aerial_side_roll"],
    [9, "Fire", "resonant_flame_wave", "flame_trail_leap"],
    [10, "Fire", "red_horn_detonation", "low_horn_slide"],
    [11, "Water", "tidal_jaw", "dive_reappear"],
    [12, "Water", "frost_predator_combo", "ice_slide_retreat"],
    [13, "Wind", "sky_spear", "spiral_ascent"],
    [14, "Wind", "cyclone_sickle_claw", "lateral_somersault"],
    [15, "Earth", "earth_hammer", "tail_pivot_half_turn"],
    [16, "Water", "river_hunter", "crouched_quick_step"],
    [17, "Earth", "guardian_stone_pulse", "shell_shield_hide"],
    [18, "Earth", "sand_shield_charge", "sand_burrow"],
    [19, "Wind", "gale_kick", "afterimage_sprint"],
    [20, "Wind", "storm_crown", "vortex_ascent"],
    [21, "Fire", "doom_flame_jaw", "flame_wing_side_roll"],
    [22, "Water", "mist_twin_crest_mirage", "mist_clone_swap"],
    [23, "Earth", "stone_thumb_spear", "turning_deflection"],
    [24, "Earth", "twin_earth_spikes", "tail_pole_vault"],
    [25, "Water", "moon_tide_claws", "wave_backbend"],
    [26, "Wind", "shadow_sprint", "zigzag_dash"],
    [27, "Water", "resonant_pressure_wave", "circular_water_slide"],
    [28, "Wind", "four_wing_storm", "folded_wing_snap_roll"],
    [29, "Earth", "green_giant_step", "low_body_impact_flow"],
    [30, "Fire", "crimson_titan_descent", "fire_column_ascent"],
  ];
  const actual = rows.map(([, id, element, skillId, , dodgeProfile]) => [Number(id), element, skillId, dodgeProfile]);

  assert.deepEqual(actual, approved);
  assert.equal(new Set(actual.map((row) => row[2])).size, approved.length, "skills must be unique");
  assert.equal(new Set(actual.map((row) => row[3])).size, approved.length, "dodges must be unique");
  assert.deepEqual(new Set(actual.map((row) => row[1])), new Set(["Water", "Fire", "Earth", "Wind"]));
});

test("offline beta requires avatar, pet, encounter, summon, and battle gates", () => {
  const flow = read("Assets/BigimongAR/Scripts/OfflineBetaFlowController.cs");
  const demo = read("Assets/BigimongAR/Scripts/ArBattleOfflineDemo.cs");
  const bridge = read("Assets/BigimongAR/Scripts/ArBattleNativeBridge.cs");
  const arena = read("Assets/BigimongAR/Scripts/ArBattleArenaController.cs");
  const builder = read("Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs");
  const checks = read("Assets/BigimongAR/Editor/OfflineBetaEditorTests.cs");
  const build = read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");
  for (const phase of ["AvatarCreate", "PetTestSelect", "TyrannosaurEncounter", "ArScan", "SummonSequence", "Battle", "Result"])
    assert.match(flow, new RegExp(phase));
  assert.match(flow, /opponentArtId = 1/);
  assert.match(flow, /Mathf\.Clamp\(artId, 1, 30\)/);
  assert.match(flow, /AvatarProfileStore\.HasSavedProfile/);
  assert.match(flow, /UseScreenFixedFallback/);
  assert.match(flow, /director\?\.BeginSummoning\(profile\)/);
  assert.match(flow, /offlineDemo\?\.EnableTurns\(\)/);
  assert.match(flow, /Phase == OfflineBetaPhase\.Battle.*!director\.IsRoundPlaying/s);
  assert.match(flow, /offlineDemo\?\.SetTrackingPaused/);
  assert.match(demo, /BattlePrepared\?\.Invoke/);
  assert.match(demo, /BattleFinished\?\.Invoke/);
  assert.match(demo, /while \(director != null && director\.IsRoundPlaying\) yield return null/);
  assert.match(demo, /playerB = Player\([^\n]*opponentArtId/);
  assert.match(bridge, /if \(nativeTransportAvailable\) StartBattleJson\(payload\)/);
  assert.match(bridge, /else offlineFlow\?\.StartBeta\(\)/);
  assert.match(bridge, /offlineFlow\?\.DisableForOnlineHost\(\)/);
  assert.match(arena, /PlaceScreenFixed\(Transform cameraTransform\)/);
  assert.match(arena, /arenaRoot\.transform\.SetParent\(cameraTransform, false\)/);
  assert.match(builder, /"←"/);
  assert.match(builder, /"↑"/);
  assert.match(builder, /"→"/);
  assert.match(builder, /"베타 테스트 선택 · 보상 없음"/);
  assert.match(checks, /OfflineBetaEditorChecks/);
  assert.match(checks, /RunSceneChecks/);
  assert.match(checks, /FirstPhase\(false\) == OfflineBetaPhase\.AvatarCreate/);
  assert.match(checks, /targetGraphic is CircularButtonGraphic/);
  assert.match(build, /OfflineBetaEditorChecks\.RunSceneChecks\(\)/);
});

test("Unity editor scene builder creates the required AR Foundation scene graph", () => {
  const builder = read("Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs");
  assert.match(builder, /Bigimong\/Create AR Battle Scene/);
  assert.match(builder, /typeof\(ARSession\)/);
  assert.match(builder, /typeof\(XROrigin\)/);
  assert.match(builder, /typeof\(ARRaycastManager\)/);
  assert.match(builder, /typeof\(ARPlaneManager\)/);
  assert.match(builder, /CreateHud/);
  assert.match(builder, /EditorSceneManager\.SaveScene/);
});

test("AR arena keeps actor scale independent from the flattened ring visual", () => {
  const arena = read("Assets/BigimongAR/Scripts/ArBattleArenaController.cs");
  const director = read("Assets/BigimongAR/Scripts/ArBattleDirector.cs");
  assert.match(arena, /new GameObject\("Bigimong AR Arena"\)/);
  assert.match(arena, /Instantiate\(battleRingPrefab, arenaRoot\.transform\)/);
  assert.match(arena, /ConfigureBattleSize/);
  assert.match(director, /arena\?\.ConfigureBattleSize\(maxHeight\)/);
});

test("AR reconnect restores authoritative HP and sequences snapshots after round motion", () => {
  const actor = read("Assets/BigimongAR/Scripts/ArBattleActor.cs");
  const director = read("Assets/BigimongAR/Scripts/ArBattleDirector.cs");
  assert.match(actor, /ApplySnapshotHitPoints/);
  assert.match(director, /pendingSnapshot/);
  assert.match(director, /ReconcileSnapshot/);
  assert.match(director, /WaitForSecondsRealtime/);
  assert.match(director, /round\.round <= lastPlayedRound/);
});

test("gold medallion summoning stays ordered, cancellable, and battle-gated", () => {
  const sequence = read("Assets/BigimongAR/Scripts/SummonSequenceDirector.cs");
  const summoner = read("Assets/BigimongAR/Scripts/SummonerActor.cs");
  const circle = read("Assets/BigimongAR/Scripts/ProceduralMagicCircle.cs");
  const actor = read("Assets/BigimongAR/Scripts/ArBattleActor.cs");
  const director = read("Assets/BigimongAR/Scripts/ArBattleDirector.cs");
  const checks = read("Assets/BigimongAR/Editor/SummonSequenceEditorTests.cs");
  const build = read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");

  const throwAt = sequence.indexOf("yield return summoner.ThrowMedallion");
  const circleAt = sequence.indexOf("magicCircle.Expand");
  const riseAt = sequence.indexOf("yield return pet.PlaySummonRise");
  const completeAt = sequence.indexOf("SummoningCompleted?.Invoke");
  assert.ok(throwAt >= 0 && throwAt < circleAt && circleAt < riseAt && riseAt < completeAt);
  assert.match(sequence, /generation != activeGeneration/);
  assert.match(sequence, /activeGeneration\+\+/);
  assert.doesNotMatch(sequence, /Time\.timeScale/);
  assert.match(summoner, /Instantiate\(wornMedallion\.gameObject/);
  assert.doesNotMatch(summoner, /wornMedallion\.SetParent/);
  assert.match(circle, /RingCount = 2/);
  assert.match(circle, /RuneCount = 8/);
  assert.match(actor, /IEnumerator PlaySummonRise\(float seconds\)/);
  assert.match(actor, /public void PrepareSummonRise\(\)/);
  assert.match(actor, /public void ResetSummonPose\(\)/);
  assert.match(sequence, /pet\.PrepareSummonRise\(\).*yield return summoner\.ThrowMedallion/s);
  assert.match(sequence, /var petHomePosition = pet\.transform\.localPosition;.*pet\.PrepareSummonRise\(\).*avatar\.transform\.localPosition = petHomePosition/s);
  assert.match(sequence, /activePet\?\.ResetSummonPose\(\)/);
  assert.match(director, /if \(summoning\).*pendingSnapshot = snapshot/s);
  assert.match(director, /if \(summoning \|\| roundPlayback != null\).*QueuePendingRound\(round\)/s);
  assert.match(director, /TryStartPendingPlaybackOrReconcile/);
  assert.match(director, /SetTrackingLost\(bool value\).*summonDirector\?\.CancelAndReset\(\).*SetInputLocked\(true\)/s);
  assert.match(director, /OnSummoningCompleted/);
  assert.match(checks, /VerifyMagicCircleGeometry/);
  assert.match(checks, /VerifyMedallionCloneOwnership/);
  assert.match(checks, /VerifyCancellationGeneration/);
  assert.match(checks, /VerifyHudLocking/);
  assert.match(checks, /VerifyPetSummonPoseLifecycle/);
  assert.match(checks, /VerifyBattleUpdateQueueing/);
  assert.match(checks, /VerifyTrackingLossCancellation/);
  assert.match(checks, /VerifySummonerPlacementUsesPetHome/);
  assert.match(build, /SummonSequenceEditorChecks\.RunBehaviorChecks\(\)/);
  assert.match(build, /SummonSequenceEditorChecks\.RunSceneChecks\(\)/);
});

test("AR countdown uses server clock calibration and monotonic elapsed time", () => {
  const models = read("Assets/BigimongAR/Scripts/ArBattleModels.cs");
  const hud = read("Assets/BigimongAR/Scripts/ArBattleHud.cs");
  const bridge = read("Assets/BigimongAR/Scripts/ArBattleNativeBridge.cs");
  assert.match(models, /long serverNow/);
  assert.match(hud, /Time\.realtimeSinceStartup/);
  assert.match(hud, /deadlineAtMilliseconds - serverNowMilliseconds/);
  assert.match(bridge, /BeginTurn\(snapshot\.deadlineAt, snapshot\.serverNow\)/);
});

test("shared anchor reanchoring invalidates stale asynchronous callbacks", () => {
  const coordinator = read("Assets/BigimongAR/Scripts/SharedAnchorCoordinator.cs");
  assert.match(coordinator, /operationGeneration/);
  assert.match(coordinator, /generation != operationGeneration/);
  assert.match(coordinator, /public void RequestReanchor\(\)/);
  assert.match(coordinator, /activeCloudAnchorId = null/);
});

test("AR HUD exposes authoritative HP, round result, and server automatic choice", () => {
  const hud = read("Assets/BigimongAR/Scripts/ArBattleHud.cs");
  const director = read("Assets/BigimongAR/Scripts/ArBattleDirector.cs");
  const builder = read("Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs");
  assert.match(hud, /SetBattleState/);
  assert.match(hud, /ShowRoundResult/);
  assert.match(hud, /서버 자동선택/);
  assert.match(director, /hud\?\.SetBattleState/);
  assert.match(builder, /Player A HP/);
  assert.match(builder, /Round Status/);
});

test("standalone AR APK starts the gated offline beta only without an Android payload", () => {
  const bridge = read("Assets/BigimongAR/Scripts/ArBattleNativeBridge.cs");
  const demo = read("Assets/BigimongAR/Scripts/ArBattleOfflineDemo.cs");
  const sceneBuilder = read("Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs");

  assert.match(bridge, /else offlineFlow\?\.StartBeta\(\)/);
  assert.match(bridge, /offlineFlow\?\.DisableForOnlineHost\(\)/);
  assert.match(bridge, /offlineDemo != null && offlineDemo\.IsActive/);
  assert.match(bridge, /offlineDemo\.SubmitChoice\(direction\)/);
  assert.match(demo, /public sealed class ArBattleOfflineDemo : MonoBehaviour/);
  assert.match(demo, /arena\.ArenaPlaced \+= OnArenaPlaced/);
  assert.match(demo, /private const float TurnSeconds = 10f/);
  assert.match(demo, /public void SubmitChoice\(string direction\)/);
  assert.match(demo, /public void EnableTurns\(\)/);
  assert.match(demo, /attackAutomatic = automatic/);
  assert.match(demo, /status = winner == null \? "ACTIVE" : "FINISHED"/);
  assert.match(sceneBuilder, /systems\.AddComponent<ArBattleOfflineDemo>\(\)/);
  assert.match(sceneBuilder, /Assign\(bridge, "offlineDemo", offlineDemo\)/);
});

test("offline battle opens the next turn only after the round motion completes", () => {
  const demo = read("Assets/BigimongAR/Scripts/ArBattleOfflineDemo.cs");
  assert.match(demo, /private IEnumerator ResolveRound\(string direction, bool automatic\)/);
  assert.match(demo, /yield return new WaitForSecondsRealtime\(0\.9f\)/);
  assert.ok(
    demo.indexOf("yield return new WaitForSecondsRealtime(0.9f)") <
      demo.indexOf("bridge?.ApplySnapshotMessage"),
    "the authoritative snapshot and next turn must follow the round animation"
  );
});

test("procedural combat supplies bounded element VFX and profile motion", () => {
  const vfx = read("Assets/BigimongAR/Scripts/ProceduralCombatVfx.cs");
  const actor = read("Assets/BigimongAR/Scripts/ArBattleActor.cs");
  for (const name of ["PlayFire", "PlayWater", "PlayEarth", "PlayWind", "PlayImpact", "ReleaseAll"])
    assert.match(vfx, new RegExp(name));
  assert.match(vfx, /PoolCapacity = 32/);
  assert.match(vfx, /Time\.unscaledDeltaTime/);
  for (const name of ["WingedIdleMotion", "GroundedIdleMotion", "runtimeAnimatorController", "WaitForSecondsRealtime", "profile.attackProfile", "profile.idleProfile"])
    assert.ok(actor.includes(name), name);
  assert.match(actor, /DodgeMotion\(string direction, string dodgeProfile\)/);
  assert.doesNotMatch(actor, /Time\.timeScale/);
});

test("all catalog motion curves retain distinct deterministic species timing", () => {
  const catalog = read("Assets/BigimongAR/Scripts/ElementSkillCatalog.cs");
  const profiles = [...catalog.matchAll(/Profile\((\d+), Element\.\w+, "[^"]+", "[^"]+", "([^"]+)", "([^"]+)", "([^"]+)"/g)];
  assert.equal(profiles.length, 30);
  const phase = id => [...id].reduce((hash, c) => (hash * 31 + c.charCodeAt(0)) % 997, 0) / 997;
  for (const column of [2, 3, 4]) {
    const signatures = profiles.map(p => `${phase(p[column])}:${Number(p[1]) * .173}`);
    assert.equal(new Set(signatures).size, 30);
  }
  const actor = read("Assets/BigimongAR/Scripts/ArBattleActor.cs");
  assert.match(actor, /hash = \(hash \* 31 \+ c\) % 997/);
  const director = read("Assets/BigimongAR/Scripts/ArBattleDirector.cs");
  const round = director.slice(director.indexOf("private IEnumerator PlayRound"), director.indexOf("private void TryStartPendingPlaybackOrReconcile"));
  assert.ok(round.indexOf("PlaySkill") < round.indexOf("TravelTime"));
  assert.ok(round.indexOf("TravelTime") < round.indexOf("PlayImpact"));
  assert.ok(round.indexOf("PlayImpact") < round.indexOf("TryStartPendingPlaybackOrReconcile"));
  const checks = read("Assets/BigimongAR/Editor/CombatPresentationEditorTests.cs");
  for (const contract of ["AllocatedCount == 32", "ActiveCount == 0", "ActiveCount == 6", "actor.UsesAnimator", ".06f"])
    assert.ok(checks.includes(contract));
  assert.match(read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs"), /CombatPresentationEditorChecks.RunBehaviorChecks/);
});
