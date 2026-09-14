import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
const read = path => readFileSync(new URL(`../unity/BigimongAR/Assets/BigimongAR/${path}`, import.meta.url), 'utf8');
test('the shipped Unity scene uses all five reference screens and real touch routing', () => {
  const scene = read('Editor/BigimongArSceneBuilder.cs');
  const build = read('Editor/BigimongAndroidBuild.cs');
  const navigation = read('Scripts/BigimongReferenceUi.cs');
  const importer = read('Editor/BigimongReferenceUiArtImporter.cs');
  assert.match(scene, /BigimongReferenceUi/);
  assert.match(build, /BigimongReferenceUiEditorChecks\.RunSceneChecks/);
  for (const asset of ['loading', 'battle-loading', 'avatar', 'egg', 'home'])
    assert.ok(navigation.includes(`ReferenceUi/${asset}`), `missing ${asset}`);
  for (const action of ['상점', '놀아주기', '1:1 대전', '홈', '도감', '퀘스트', '선물', '설정', '남자', '여자', '선택 완료'])
    assert.ok(navigation.includes(action), `missing ${action}`);
  assert.match(navigation, /Resources\.Load<Texture2D>/);
  assert.match(navigation, /button\.onClick\.AddListener/);
  assert.match(importer, /TextureImporterNPOTScale\.None/);
  assert.match(importer, /Resources\/ReferenceUi\//);
});

test('every generated button has shared tactile press motion', () => {
  const scene = read('Editor/BigimongArSceneBuilder.cs');
  const navigation = read('Scripts/BigimongReferenceUi.cs');
  const motion = read('Scripts/ButtonPressMotion.cs');

  assert.match(scene, /AddComponent<ButtonPressMotion>/);
  assert.match(navigation, /AddComponent<ButtonPressMotion>/);
  assert.match(motion, /IPointerDownHandler/);
  assert.match(motion, /IPointerUpHandler/);
  assert.match(motion, /ISubmitHandler/);
  assert.match(motion, /ICancelHandler/);
  assert.doesNotMatch(motion, /IPointerCancelHandler/);
  assert.match(motion, /Time\.unscaledDeltaTime/);
  assert.match(motion, /transform\.localScale = Vector3\.one/);
});

test('avatar cursor repair and home idle layers never intercept navigation taps', () => {
  const navigation = read('Scripts/BigimongReferenceUi.cs');
  const idle = read('Scripts/ReferenceArtIdleMotion.cs');

  assert.match(navigation, /CreateAvatarCursorRepair/);
  assert.match(navigation, /Game: Avatar Cursor Repair/);
  assert.match(navigation, /new Rect\(\.*/);
  assert.match(navigation, /CreateHomeIdleOverlay/);
  assert.match(navigation, /Game: Home Dinosaur Idle/);
  assert.match(navigation, /raycastTarget = false/);
  assert.match(idle, /Time\.unscaledTime/);
  assert.match(idle, /basePosition/);
});
