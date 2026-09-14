import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import test from "node:test";

const read = (path) => readFileSync(new URL(`../android/${path}`, import.meta.url), "utf8");

test("Android manifest declares health, optional AR camera, and scoped nearby permissions", () => {
  const manifest = read("app/src/main/AndroidManifest.xml");
  assert.match(manifest, /android\.permission\.INTERNET/);
  assert.match(manifest, /android\.permission\.health\.READ_STEPS/);
  assert.match(manifest, /android\.permission\.CAMERA/);
  assert.match(manifest, /android\.permission\.BLUETOOTH_SCAN/);
  assert.match(manifest, /android\.permission\.BLUETOOTH_CONNECT/);
  assert.match(manifest, /android\.permission\.BLUETOOTH_ADVERTISE/);
  assert.match(manifest, /ACCESS_FINE_LOCATION" android:maxSdkVersion="30"/);
  assert.match(manifest, /android\.hardware\.camera\.ar" android:required="false"/);
  assert.doesNotMatch(manifest, /READ_HEART_RATE|WRITE_STEPS|ACCESS_BACKGROUND_LOCATION/);
  assert.match(manifest, /ACTION_SHOW_PERMISSIONS_RATIONALE/);
});

test("Android Health Connect integration aggregates today's steps", () => {
  const source = read("app/src/main/java/com/bigidragon/app/health/HealthConnectStepReader.kt");
  assert.match(source, /StepsRecord\.COUNT_TOTAL/);
  assert.match(source, /TimeRangeFilter\.between/);
  assert.match(source, /Asia\/Seoul/);
  assert.match(source, /getGrantedPermissions/);
});

test("Android session token is encrypted with an Android Keystore AES-GCM key", () => {
  const source = read("app/src/main/java/com/bigidragon/app/data/TokenVault.kt");
  assert.match(source, /AndroidKeyStore/);
  assert.match(source, /AES\/GCM\/NoPadding/);
  assert.match(source, /KeyProperties\.PURPOSE_ENCRYPT/);
});

test("Android API sends cumulative steps with an idempotency key", () => {
  const source = read("app/src/main/java/com/bigidragon/app/data/BigiApiClient.kt");
  assert.match(source, /\/api\/v1\/steps\/sync/);
  assert.match(source, /Idempotency-Key/);
  assert.match(source, /hc:\$day:\$totalSteps/);
});

test("Android journey UI includes gift, egg, ready, reveal, and home phases", () => {
  const ui = read("app/src/main/java/com/bigidragon/app/MainActivity.kt");
  const viewModel = read("app/src/main/java/com/bigidragon/app/MainViewModel.kt");
  const api = read("app/src/main/java/com/bigidragon/app/data/BigiApiClient.kt");
  for (const phase of ["GIFT", "EGG", "READY_TO_HATCH", "HATCHED"]) {
    assert.match(ui, new RegExp(`"${phase}"`));
  }
  assert.match(ui, /30,000포인트 달성/);
  assert.match(ui, /알 닦기 · \+500/);
  assert.match(api, /journey\/gift\/open/);
  assert.match(api, /journey\/egg\/clean/);
  assert.match(api, /journey\/egg\/hatch/);
});

test("Android v0.13 uses the Bigimong brand, application id, and build version", () => {
  const strings = read("app/src/main/res/values/strings.xml");
  const gradle = read("app/build.gradle.kts");
  const ui = read("app/src/main/java/com/bigidragon/app/MainActivity.kt");
  assert.match(strings, /비기몽/);
  assert.match(ui, /Text\("비기몽"/);
  assert.match(gradle, /applicationId = "com\.bigimong\.app"/);
  assert.match(gradle, /versionCode = 13/);
  assert.match(gradle, /versionName = "0\.13\.0"/);
});

test("Android packages 30 characters across three stages and resolves them by art id", () => {
  const artwork = read("app/src/main/java/com/bigidragon/app/CharacterArtwork.kt");
  const api = read("app/src/main/java/com/bigidragon/app/data/BigiApiClient.kt");
  const models = read("app/src/main/java/com/bigidragon/app/data/BigiModels.kt");
  const directory = new URL("../android/app/src/main/res/drawable-nodpi/", import.meta.url);
  const files = readdirSync(directory).filter((name) => /^bigimong_\d{2}_(growth|youth|adult)\.webp$/.test(name));

  assert.equal(files.length, 90);
  for (let artId = 1; artId <= 30; artId += 1) {
    const prefix = `bigimong_${String(artId).padStart(2, "0")}_`;
    assert.deepEqual(
      files.filter((name) => name.startsWith(prefix)).sort(),
      [`${prefix}adult.webp`, `${prefix}growth.webp`, `${prefix}youth.webp`]
    );
  }
  assert.match(artwork, /artId\.coerceIn\(1, 30\)/);
  assert.match(artwork, /"YOUTH" -> "youth"/);
  assert.match(artwork, /"ADULT" -> "adult"/);
  assert.match(api, /artId = dragon\.optInt\("artId", 1\)/);
  assert.match(models, /val artId: Int/);
});

test("Android battle client supports rooms, ten-second turns, polling, and server autoplay", () => {
  const ui = read("app/src/main/java/com/bigidragon/app/MainActivity.kt");
  const api = read("app/src/main/java/com/bigidragon/app/data/BigiApiClient.kt");
  const viewModel = read("app/src/main/java/com/bigidragon/app/MainViewModel.kt");
  const models = read("app/src/main/java/com/bigidragon/app/data/BigiModels.kt");

  assert.match(ui, /listOf\(0, 10, 50, 100\)/);
  for (const direction of ["LEFT", "CENTER", "RIGHT"]) {
    assert.match(ui, new RegExp(`"${direction}"`));
  }
  assert.match(ui, /시간이 끝나면 서버가 자동 선택/);
  assert.match(ui, /deadlineAt/);
  assert.match(ui, /event\.motion\.attacker/);
  assert.match(api, /\/api\/v1\/matchmaking/);
  assert.match(api, /\/api\/v1\/battles\/\$battleId\/choice/);
  assert.match(viewModel, /delay\(1_000\)/);
  assert.match(viewModel, /delay\(500\)/);
  assert.match(viewModel, /restoreBattleOrQueue/);
  assert.match(models, /data class BattleState/);
});

test("Android AR handoff carries battle identity, both art ids, stages, and server deadline", () => {
  const launcher = read("app/src/main/java/com/bigidragon/app/ar/ArBattleLauncher.kt");
  const permissions = read("app/src/main/java/com/bigidragon/app/ar/ArBattlePermissionPolicy.kt");
  const discovery = read("app/src/main/java/com/bigidragon/app/ar/BleNearbyDiscovery.kt");
  const ui = read("app/src/main/java/com/bigidragon/app/MainActivity.kt");
  const viewModel = read("app/src/main/java/com/bigidragon/app/MainViewModel.kt");
  const api = read("app/src/main/java/com/bigidragon/app/data/BigiApiClient.kt");

  for (const key of ["battleId", "deadlineAt", "playerA", "playerB", "artId", "stage"]) {
    assert.match(launcher, new RegExp(`put\\(\"${key}\"`));
  }
  assert.match(launcher, /UnityPlayerGameActivity/);
  assert.match(permissions, /Manifest\.permission\.CAMERA/);
  assert.match(discovery, /bluetoothLeAdvertiser/);
  assert.match(discovery, /bluetoothLeScanner/);
  assert.match(discovery, /pairing code/i);
  assert.match(ui, /카메라로 AR 대전 보기/);
  assert.match(viewModel, /UnityBattleMessenger\.send\("ApplySnapshotJson"/);
  assert.match(viewModel, /UnityBattleMessenger\.send\("SetTransportState", "DISCONNECTED"\)/);
  assert.match(api, /joinNearbyBattle/);
  assert.match(api, /put\("pairingCode", pairingCode\)/);
  assert.match(api, /publishCloudAnchor/);
  assert.match(launcher, /put\("cloudAnchorId"/);
  assert.match(launcher, /put\("serverNow"/);
  assert.ok(
    viewModel.indexOf('UnityBattleMessenger.send("ApplyRoundJson"') <
      viewModel.indexOf('UnityBattleMessenger.send("ApplySnapshotJson"'),
    "round motion must be sent before the authoritative snapshot"
  );
});
