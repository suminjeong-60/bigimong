import assert from "node:assert/strict";
import { existsSync, readFileSync } from "node:fs";
import test from "node:test";

const root = new URL("../unity/BigimongAR/", import.meta.url);
const file = (path) => new URL(path, root);
const read = (path) => readFileSync(file(path), "utf8");

test("Unity build prepares accepted VARCO prefabs before creating the AR scene", () => {
  const pipelinePath = "Assets/BigimongAR/Editor/VarcoAssetImportPipeline.cs";
  assert.equal(existsSync(file(pipelinePath)), true, "VARCO import pipeline must exist");

  const pipeline = read(pipelinePath);
  const build = read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");
  const builder = read("Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs");

  assert.match(pipeline, /public static void BuildAcceptedPrefabs\(\)/);
  assert.match(build, /VarcoAssetImportEditorChecks\.PrepareAndValidate\(\)/);
  assert.match(builder, /Resources\.Load<CharacterPrefabCatalog>\("ArCharacterCatalog"\)/);
  assert.match(builder, /Assign\(director, "characterCatalog", characterCatalog\)/);

  const prepareAt = build.indexOf("VarcoAssetImportEditorChecks.PrepareAndValidate()");
  const sceneAt = build.indexOf("BigimongArSceneBuilder.CreateScene()");
  assert.ok(prepareAt >= 0 && prepareAt < sceneAt, "VARCO prefabs must be prepared before scene creation");
});

test("VARCO source models receive rig-specific mobile import settings", () => {
  const pipeline = read("Assets/BigimongAR/Editor/VarcoAssetImportPipeline.cs");

  assert.match(pipeline, /sealed class VarcoModelImportPostprocessor : AssetPostprocessor/);
  assert.match(pipeline, /private void OnPreprocessModel\(\)/);
  assert.match(pipeline, /FindBySourceModelPath\(assetPath\)/);
  assert.match(pipeline, /ModelImporterAnimationType\.Human/);
  assert.match(pipeline, /ModelImporterAnimationType\.Generic/);
  assert.match(pipeline, /ModelImporterAnimationType\.None/);
  assert.match(pipeline, /importCameras = false/);
  assert.match(pipeline, /importLights = false/);
  assert.match(pipeline, /meshCompression = ModelImporterMeshCompression\.Medium/);
  assert.match(pipeline, /optimizeMeshPolygons = true/);
  assert.match(pipeline, /optimizeMeshVertices = true/);
});

test("VARCO prefab builder normalizes ground contact and updates artId one", () => {
  const pipeline = read("Assets/BigimongAR/Editor/VarcoAssetImportPipeline.cs");

  assert.match(pipeline, /CalculateNormalization\(float minY, float maxY, float targetHeightM\)/);
  assert.match(pipeline, /targetHeightM \/ height/);
  assert.match(pipeline, /-minY \* scale/);
  assert.match(pipeline, /GetComponentsInChildren<Renderer>\(true\)/);
  assert.match(pipeline, /PrefabUtility\.SaveAsPrefabAsset/);
  assert.match(pipeline, /UpdateCharacterCatalog/);
  assert.match(pipeline, /FindPropertyRelative\("artId"\)\.intValue = 1/);
  assert.match(pipeline, /FindPropertyRelative\("baby"\)/);
  assert.match(pipeline, /FindPropertyRelative\("teen"\)/);
  assert.match(pipeline, /FindPropertyRelative\("adult"\)/);
});

test("Unity build gates imported VARCO quality while absent sources keep procedural fallback", () => {
  const checksPath = "Assets/BigimongAR/Editor/VarcoAssetImportEditorChecks.cs";
  assert.equal(existsSync(file(checksPath)), true, "VARCO editor checks must exist");

  const checks = read(checksPath);
  const build = read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");

  assert.match(checks, /public static void RunBehaviorChecks\(\)/);
  assert.match(checks, /public static void RunImportedAssetChecks\(\)/);
  assert.match(checks, /if \(!File\.Exists\(sourcePath\)\) continue/);
  assert.match(checks, /triangleLimit/);
  assert.match(checks, /materialLimit/);
  assert.match(checks, /mainTexture/);
  assert.match(checks, /targetHeightM/);
  assert.match(checks, /avatar\.isValid/);
  assert.match(checks, /avatar\.isHuman/);
  assert.match(checks, /definition\.rig == "none"/);
  assert.match(checks, /ValidateSummonerCompatibility/);
  assert.match(build, /VarcoAssetImportEditorChecks\.PrepareAndValidate\(\)/);

  const prepareAt = build.indexOf("VarcoAssetImportEditorChecks.PrepareAndValidate()");
  const sceneAt = build.indexOf("BigimongArSceneBuilder.CreateScene()");
  assert.ok(prepareAt >= 0 && prepareAt < sceneAt, "VARCO validation must run before scene creation");
});

test("VARCO generated outputs cannot survive a removed or moved source", () => {
  const pipeline = read("Assets/BigimongAR/Editor/VarcoAssetImportPipeline.cs");

  assert.match(pipeline, /AssetDatabase\.GetLabels/);
  assert.match(pipeline, /AssetDatabase\.DeleteAsset/);
  assert.match(pipeline, /deletedAssets/);
  assert.match(pipeline, /movedFromAssetPaths/);
  assert.match(pipeline, /stageProperty\.objectReferenceValue = prefab/);
});

test("VARCO output ownership prevents collisions and excludes unowned fallback prefabs", () => {
  const pipeline = read("Assets/BigimongAR/Editor/VarcoAssetImportPipeline.cs");

  assert.match(pipeline, /private static bool IsOwnedGeneratedAsset/);
  assert.match(pipeline, /AssetDatabase\.LoadMainAssetAtPath/);
  assert.match(pipeline, /private static void RequireOwnedOutputPath/);
  assert.match(pipeline, /reserved VARCO output path/);
  assert.match(pipeline, /AnyOwnedGeneratedPrefabExists/);
  assert.match(pipeline, /private static GameObject LoadOwnedPrefabForCatalog/);
  assert.match(pipeline, /File\.Exists\(AbsoluteProjectPath\(definition\.unitySourceModelPath\)\)/);
  assert.match(pipeline, /IsOwnedGeneratedAsset\(prefab\) \? prefab : null/);

  const collisionAt = pipeline.indexOf("RequireOwnedOutputPath(definition.unityPrefabPath)");
  const missingSourceAt = pipeline.indexOf("if (!File.Exists(AbsoluteProjectPath(definition.unitySourceModelPath)))");
  assert.ok(collisionAt >= 0 && collisionAt < missingSourceAt,
    "reserved output collisions must fail before missing sources can select a Resources fallback");
});

test("every Unity player build runs the same VARCO pre-build gate", () => {
  const checks = read("Assets/BigimongAR/Editor/VarcoAssetImportEditorChecks.cs");
  const build = read("Assets/BigimongAR/Editor/BigimongAndroidBuild.cs");

  assert.match(checks, /sealed class VarcoAssetBuildPreprocessor : IPreprocessBuildWithReport/);
  assert.match(checks, /public void OnPreprocessBuild\(BuildReport report\)/);
  assert.match(checks, /PrepareAndValidate\(\)/);
  assert.match(build, /VarcoAssetImportEditorChecks\.PrepareAndValidate\(\)/);
});

test("imported humanoid avatars retain the summon pose and runtime medallion contract", () => {
  const summoner = read("Assets/BigimongAR/Scripts/SummonerActor.cs");

  assert.match(summoner, /GetBoneTransform\(HumanBodyBones\.RightUpperArm\)/);
  assert.match(summoner, /GetBoneTransform\(HumanBodyBones\.RightLowerArm\)/);
  assert.match(summoner, /GetBoneTransform\(HumanBodyBones\.RightHand\)/);
  assert.match(summoner, /CreateRuntimeMedallion/);
  assert.match(summoner, /public bool HasThrowPoseRig/);
});
