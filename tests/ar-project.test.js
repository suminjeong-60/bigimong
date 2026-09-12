import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const root = new URL("../unity/BigimongAR/", import.meta.url);
const read = (path) => readFileSync(new URL(path, root), "utf8");

test("Unity AR project pins AR Foundation and ARCore provider versions", () => {
  const manifest = JSON.parse(read("Packages/manifest.json"));
  assert.equal(manifest.dependencies["com.unity.xr.arfoundation"], "6.1.1");
  assert.equal(manifest.dependencies["com.unity.xr.arcore"], "6.1.1");
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
