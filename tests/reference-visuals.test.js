import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import test from "node:test";

const root = new URL("../unity/BigimongAR/Assets/BigimongAR/", import.meta.url);
const read = (path) => readFileSync(new URL(path, root), "utf8");

test("reference visual behavior checks run before Android build", () => {
  const build = read("Editor/BigimongAndroidBuild.cs");
  const behavior = build.indexOf("ReferenceVisualEditorChecks.RunBehaviorChecks()");
  const scene = build.indexOf("BigimongArSceneBuilder.CreateScene()");
  const sceneChecks = build.indexOf("ReferenceVisualEditorChecks.RunSceneChecks()");
  assert.ok(behavior >= 0 && behavior < scene, "generated geometry checks must run before scene generation");
  assert.ok(sceneChecks > scene, "HUD scene checks must run after scene generation");
});

test("generated characters expose layered faces, species anatomy, and removable cosmetics", () => {
  const avatar = read("Scripts/ProceduralAvatarFactory.cs");
  const dragon = read("Scripts/ProceduralDragonFactory.cs");
  const checks = read("Editor/ReferenceVisualEditorChecks.cs");

  for (const part of ["Nose", "Smile", "CheekLeft", "CheekRight", "CosmeticRoot"])
    assert.match(avatar, new RegExp(`"${part}"`));
  assert.match(avatar, /"EyeHighlight" \+ suffix/);
  assert.match(dragon, /BigimongAppearanceCatalog\.Resolve/);
  assert.match(dragon, /CreateWingMesh/);
  assert.match(dragon, /CreateConeMesh/);
  assert.match(dragon, /"EyeHighlight" \+ suffix/);
  assert.match(dragon, /"Belly"/);
  assert.match(dragon, /"CosmeticRoot"/);
  assert.doesNotMatch(dragon, /HashSet<int> Winged = new\(\) \{[^}]*\b11\b/);

  assert.match(checks, /for \(var artId = 1; artId <= 30; artId\+\+\)/);
  assert.match(checks, /foreach \(var stage in Stages\)/);
  assert.match(checks, /VerifyDistinctiveAnatomy/);
  assert.match(checks, /VerifyAvatarConstruction/);
  assert.match(checks, /RendererBounds/);
  assert.match(checks, /float\.IsNaN|float\.IsInfinity/);
});

test("battle HUD uses live clamped fills and decorative overlays do not intercept input", () => {
  const hud = read("Scripts/ArBattleHud.cs");
  const builder = read("Editor/BigimongArSceneBuilder.cs");
  const checks = read("Editor/ReferenceVisualEditorChecks.cs");

  assert.match(hud, /Image playerAHpFill/);
  assert.match(hud, /Image playerBHpFill/);
  assert.match(hud, /Mathf\.Clamp01\(hpA \/ 5f\)/);
  assert.match(hud, /Mathf\.Clamp01\(hpB \/ 5f\)/);
  assert.match(builder, /CreateHudPanel/);
  assert.match(builder, /CreateHpBar/);
  assert.match(builder, /new Color\(\.08f, \.92f, \.98f/);
  assert.match(builder, /raycastTarget = false/);
  assert.match(checks, /VerifyHudState/);
  assert.match(checks, /fillAmount/);
  assert.match(checks, /raycastTarget/);
});

test("battle ring is cyan and keeps its center transparent", () => {
  const builder = read("Editor/BigimongArSceneBuilder.cs");
  const checks = read("Editor/ReferenceVisualEditorChecks.cs");
  assert.match(builder, /"Ring Center Cutout"/);
  assert.match(builder, /new Color\(\.08f, \.92f, \.98f/);
  assert.match(checks, /VerifyBattleRing/);
  assert.match(checks, /Ring Center Cutout/);
});
