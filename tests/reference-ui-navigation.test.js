import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
const read = path => readFileSync(new URL(`../unity/BigimongAR/Assets/BigimongAR/${path}`, import.meta.url), 'utf8');
test('the shipped Unity scene uses all five reference screens and real touch routing', () => {
  const scene = read('Editor/BigimongArSceneBuilder.cs');
  const build = read('Editor/BigimongAndroidBuild.cs');
  const navigation = read('Scripts/BigimongReferenceUi.cs');
  assert.match(scene, /BigimongReferenceUi/);
  assert.match(build, /BigimongReferenceUiEditorChecks\.RunSceneChecks/);
  for (const asset of ['loading', 'battle-loading', 'avatar', 'egg', 'home'])
    assert.ok(navigation.includes(`ReferenceUi/${asset}`), `missing ${asset}`);
  for (const action of ['상점', '놀아주기', '1:1 대전', '홈', '도감', '퀘스트', '선물', '설정', '남자', '여자', '선택 완료'])
    assert.ok(navigation.includes(action), `missing ${action}`);
  assert.match(navigation, /Resources\.Load<Texture2D>/);
  assert.match(navigation, /button\.onClick\.AddListener/);
});
