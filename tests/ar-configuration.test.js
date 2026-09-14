import test from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const read = p => readFileSync(new URL('../unity/BigimongAR/Assets/BigimongAR/' + p, import.meta.url), 'utf8');
test('APK build runs executable AR configuration and pose behavior checks', () => {
  assert.match(read('Editor/BigimongAndroidBuild.cs'), /ArConfigurationEditorChecks.Run\(\)/);
  assert.match(read('Editor/BigimongArSceneBuilder.cs'), /typeof\(ArCameraPoseDriver\)/);
  const checks = read('Editor/ArConfigurationEditorChecks.cs');
  assert.match(checks, /Lost tracking must preserve pose/);
  assert.match(checks, /Exactly one ARCore loader required/);
});
