# Bigimong Playable AR Beta Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build and deliver an installable single-phone Bigimong beta APK containing first-launch avatar creation, a 30-species test selector, a Tyrannosaurus encounter, gold-medallion AR summoning, and lively four-element directional combat.

**Architecture:** Keep the online Android-to-Unity schema and server-authoritative battle path intact. Add an offline-only flow controller whose avatar, summon, skill catalog, procedural VFX, and actor motion units have narrow interfaces and can later accept rigged assets. Local Node contract tests provide immediate RED/GREEN feedback; GitHub Actions provides the authoritative Unity compile and APK verification boundary.

**Tech Stack:** Unity 6000.0.58f2, C#, AR Foundation 6.1.1, ARCore 6.1.1, Unity UI, procedural meshes/materials/particles, Node.js 24 tests, GitHub Actions game-ci, IL2CPP ARM64 Android.

**Spec:** `docs/superpowers/specs/2026-09-12-playable-ar-beta-avatar-combat-design.md`

## Global Constraints

- Preserve package `com.bigimong.app`, minSdk 28, portrait, IL2CPP, and ARM64-only output.
- Preserve online battle payload schema version 1 and server-authoritative settlement behavior.
- Start the offline beta only when no Android host battle payload is present.
- Keep 30-species selection visibly beta-only; never grant production ownership or rewards.
- Use original Bigimong effects; do not copy another game's animation, projectile, silhouette, or effect.
- Never pause AR tracking through global `Time.timeScale` changes.
- Final Animator Controllers override procedural animation when present.
- Store avatar profiles as versioned JSON and recover malformed files with safe defaults.
- The APK is complete only after Unity compilation, verifier JSON `valid: true`, and SHA-256 agreement.

---

### Task 1: Avatar Profile, Validation, and Local Persistence

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarProfile.cs`
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarProfileStore.cs`
- Modify: `tests/ar-project.test.js`

**Interfaces:**
- Produces: `AvatarProfile.CreateDefault()`, `AvatarProfile.Normalize()`, `AvatarProfile.IsComplete`, `AvatarProfileStore.Load()`, `AvatarProfileStore.Save(AvatarProfile)`.
- Consumes: `Application.persistentDataPath`, `JsonUtility`.

- [ ] **Step 1: Write the failing avatar persistence contract test**

Add this test to `tests/ar-project.test.js`:

```js
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
```

- [ ] **Step 2: Run the focused test and verify RED**

Run: `node --test tests/ar-project.test.js`

Expected: FAIL because `AvatarProfile.cs` and `AvatarProfileStore.cs` do not exist.

- [ ] **Step 3: Implement the profile and store**

Use this public shape in `AvatarProfile.cs`:

```csharp
[Serializable]
public sealed class AvatarProfile
{
    public const int CurrentSchemaVersion = 1;
    public int schemaVersion = CurrentSchemaVersion;
    public string displayName = "플레이어";
    public string bodyType = "MASCULINE";
    public int faceShapeId = 1;
    public int skinToneId = 1;
    public int eyebrowId = 1;
    public int eyeColorId = 1;
    public int hairStyleId = 1;
    public int hairColorId = 1;

    public bool IsComplete => !string.IsNullOrWhiteSpace(displayName);
    public static AvatarProfile CreateDefault() => new();
    public void Normalize()
    {
        schemaVersion = CurrentSchemaVersion;
        displayName = string.IsNullOrWhiteSpace(displayName) ? "플레이어" : displayName.Trim();
        bodyType = bodyType == "FEMININE" ? "FEMININE" : "MASCULINE";
        faceShapeId = Mathf.Clamp(faceShapeId, 1, 5);
        skinToneId = Mathf.Clamp(skinToneId, 1, 8);
        eyebrowId = Mathf.Clamp(eyebrowId, 1, 6);
        eyeColorId = Mathf.Clamp(eyeColorId, 1, 8);
        hairStyleId = Mathf.Clamp(hairStyleId, 1, 12);
        hairColorId = Mathf.Clamp(hairColorId, 1, 10);
    }
}
```

`AvatarProfileStore` writes atomically to `avatar-profile-v1.json` using a temporary file, catches I/O and malformed JSON errors, and returns `CreateDefault()` plus `HasSavedProfile == false` after recovery.

- [ ] **Step 4: Run focused and full tests**

Run: `node --test tests/ar-project.test.js && npm test`

Expected: all tests PASS.

- [ ] **Step 5: Commit**

```bash
git add tests/ar-project.test.js unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarProfile.cs unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarProfileStore.cs
git commit -m "feat: add persistent beta avatar profiles"
```

---

### Task 2: Procedural Avatar and First-Launch Creator

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarCustomizationCatalog.cs`
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralAvatarFactory.cs`
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarCreatorController.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs`
- Modify: `tests/ar-project.test.js`

**Interfaces:**
- Consumes: `AvatarProfile`, `AvatarProfileStore.Save(AvatarProfile)`.
- Produces: `AvatarCustomizationCatalog.ColorFor*`, `ProceduralAvatarFactory.Create(AvatarProfile)`, `ProceduralAvatarFactory.Apply(GameObject, AvatarProfile)`, `AvatarCreatorController.Completed`.

- [ ] **Step 1: Write the failing customization test**

```js
test("first-launch avatar creator exposes every approved customization range", () => {
  const catalog = read("Assets/BigimongAR/Scripts/AvatarCustomizationCatalog.cs");
  const factory = read("Assets/BigimongAR/Scripts/ProceduralAvatarFactory.cs");
  const creator = read("Assets/BigimongAR/Scripts/AvatarCreatorController.cs");
  assert.match(catalog, /SkinTones = new Color\[8\]/);
  assert.match(catalog, /EyeColors = new Color\[8\]/);
  assert.match(catalog, /HairColors = new Color\[10\]/);
  assert.match(factory, /CreateHair\(.*hairStyleId/s);
  assert.match(factory, /CreateEyebrows\(.*eyebrowId/s);
  assert.match(factory, /CreateSummoningMedallion/);
  assert.match(creator, /BODY.*FACE.*SKIN.*BROWS.*EYES.*HAIR.*HAIR_COLOR/s);
  assert.match(creator, /profile\.IsComplete/);
});
```

- [ ] **Step 2: Run focused test and verify RED**

Run: `node --test tests/ar-project.test.js`

Expected: FAIL on the first missing customization source.

- [ ] **Step 3: Implement palettes and procedural avatar factory**

Create fixed arrays with exactly 8 skin tones, 8 eye colors, and 10 hair colors. `ProceduralAvatarFactory.Create` builds named child transforms:

```csharp
AvatarRoot
  Body
  Head
  EyeLeft
  EyeRight
  EyebrowLeft
  EyebrowRight
  HairRoot
  ArmUpperRight
  ArmLowerRight
  HandRight
  NecklaceChain
  SummoningMedallion
```

Implement five head scale profiles, six eyebrow curve/rotation profiles, twelve hair assemblies, male/female torso proportions, the fixed dark sleeveless outfit, and a gold chain/medallion. Remove colliders from cosmetic primitives and reuse shared generated materials.

- [ ] **Step 4: Implement creator controls**

`AvatarCreatorController` owns a working profile copy. Category arrows wrap within exact limits, update labels, and call `ProceduralAvatarFactory.Apply`. Save is disabled until `displayName.Trim().Length > 0`; successful save emits `Completed(profile)`.

The editor scene builder creates a safe-area creator canvas containing preview, name field, category tabs, previous/next controls, body buttons, and save button. It keeps the battle canvas disabled until creation completes.

- [ ] **Step 5: Verify tests and commit**

Run: `node --test tests/ar-project.test.js && npm test`

```bash
git add tests/ar-project.test.js unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarCustomizationCatalog.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralAvatarFactory.cs unity/BigimongAR/Assets/BigimongAR/Scripts/AvatarCreatorController.cs unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs
git commit -m "feat: add first-launch avatar creator"
```

---

### Task 3: Complete 30-Species Element and Motion Catalog

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/ElementSkillCatalog.cs`
- Modify: `tests/ar-project.test.js`

**Interfaces:**
- Produces: `ElementSkillProfile ElementSkillCatalog.Resolve(int artId)` and `IReadOnlyList<ElementSkillProfile> ElementSkillCatalog.All`.
- `ElementSkillProfile` fields: `artId`, `element`, `skillId`, `displayName`, `idleProfile`, `attackProfile`, `dodgeProfile`, `primaryColor`, `secondaryColor`, `projectileSpeed`, `impactScale`, `cameraImpulse`.

- [ ] **Step 1: Write the failing catalog completeness test**

```js
test("all thirty beta pets have unique skills and dodge profiles across four elements", () => {
  const source = read("Assets/BigimongAR/Scripts/ElementSkillCatalog.cs");
  const rows = [...source.matchAll(/Profile\((\d+), Element\.(Water|Fire|Earth|Wind), "([^"]+)", "([^"]+)", "([^"]+)"/g)];
  assert.equal(rows.length, 30);
  assert.deepEqual(rows.map(row => Number(row[1])), Array.from({ length: 30 }, (_, index) => index + 1));
  assert.equal(new Set(rows.map(row => row[3])).size, 30);
  assert.equal(new Set(rows.map(row => row[5])).size, 30);
  assert.deepEqual(new Set(rows.map(row => row[2])), new Set(["Water", "Fire", "Earth", "Wind"]));
});
```

- [ ] **Step 2: Run focused test and verify RED**

Run: `node --test tests/ar-project.test.js`

Expected: FAIL because `ElementSkillCatalog.cs` is missing.

- [ ] **Step 3: Implement the exact approved mapping**

Create entries 1–30 in numerical order using the spec's matrix. Use stable IDs such as `tyrant_flame_roar`, `earth_charge`, `sky_dive`, through `crimson_titan_descent`; use distinct dodge IDs such as `heavy_pivot`, `horn_guard`, `vertical_ascent`, through `fire_column_ascent`. `Resolve` clamps art ID to 1–30 and never returns null.

- [ ] **Step 4: Verify and commit**

Run: `node --test tests/ar-project.test.js && npm test`

```bash
git add tests/ar-project.test.js unity/BigimongAR/Assets/BigimongAR/Scripts/ElementSkillCatalog.cs
git commit -m "feat: define thirty elemental combat profiles"
```

---

### Task 4: Summoner Actor, Gold Magic Circle, and Ordered Summoning

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/SummonerActor.cs`
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/SummonSequenceDirector.cs`
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralMagicCircle.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleDirector.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs`
- Modify: `tests/ar-project.test.js`

**Interfaces:**
- Consumes: `AvatarProfile`, arena root, spawned `ArBattleActor`, player `ElementSkillProfile`.
- Produces: `IEnumerator SummonSequenceDirector.Play(AvatarProfile, ArBattleActor)`, `SummoningStarted`, `SummoningCompleted`, `CancelAndReset()`.

- [ ] **Step 1: Write the failing summon ordering test**

```js
test("medallion throw, magic circle, pet rise, and input unlock stay ordered", () => {
  const sequence = read("Assets/BigimongAR/Scripts/SummonSequenceDirector.cs");
  const throwAt = sequence.indexOf("yield return summoner.ThrowMedallion");
  const circleAt = sequence.indexOf("magicCircle.Expand");
  const riseAt = sequence.indexOf("yield return pet.PlaySummonRise");
  const completeAt = sequence.indexOf("SummoningCompleted?.Invoke");
  assert.ok(throwAt >= 0 && throwAt < circleAt && circleAt < riseAt && riseAt < completeAt);
  assert.match(sequence, /generation != activeGeneration/);
  assert.doesNotMatch(sequence, /Time\.timeScale/);
});
```

- [ ] **Step 2: Run focused test and verify RED**

Run: `node --test tests/ar-project.test.js`

Expected: FAIL because `SummonSequenceDirector.cs` is missing.

- [ ] **Step 3: Implement procedural magic circle and medallion throw**

`ProceduralMagicCircle` creates two rotating rings, eight radial runes, and a center disc using thin cylinder/cube primitives with emissive gold materials. `Expand(float seconds)` scales from zero to the arena diameter and pulses emission.

`SummonerActor.ThrowMedallion(Transform target)` animates the right arm in three keyed phases, detaches the medallion into the arena root, and moves it along `Vector3.Lerp(start, target, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arcHeight` while rotating it.

- [ ] **Step 4: Implement ordered sequence and cancellation**

`SummonSequenceDirector` increments `activeGeneration` for every play or cancellation. Every yield boundary exits when its captured generation is stale. It locks the HUD, creates the avatar behind the player slot, throws the medallion, expands the circle, launches the element-colored light column, calls `pet.PlaySummonRise(1f)`, steps the avatar back, and invokes completion.

`ArBattleDirector` delays battle readiness until `SummoningCompleted`, while online snapshots received during summoning remain queued.

- [ ] **Step 5: Verify and commit**

Run: `node --test tests/ar-project.test.js && npm test`

```bash
git add tests/ar-project.test.js unity/BigimongAR/Assets/BigimongAR/Scripts/SummonerActor.cs unity/BigimongAR/Assets/BigimongAR/Scripts/SummonSequenceDirector.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralMagicCircle.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleDirector.cs unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs
git commit -m "feat: add gold medallion pet summoning"
```

---

### Task 5: Elemental VFX and Species-Specific Actor Motion

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralCombatVfx.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleActor.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralDragonFactory.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleDirector.cs`
- Modify: `tests/ar-project.test.js`

**Interfaces:**
- Consumes: `ElementSkillProfile`, attacker/defender transforms, direction, hit outcome.
- Produces: `ProceduralCombatVfx.PlaySkill(...)`, `PlayImpact(...)`, `IEnumerator ArBattleActor.PlaySummonRise(float seconds)`, `SetIdleEnabled(bool)`, and profile-driven `PlayAttack`/`PlayDodge`.

- [ ] **Step 1: Write the failing actor/VFX contract test**

```js
test("procedural combat supplies element VFX, winged idle, grounded idle, local hit stop, and distinct dodges", () => {
  const vfx = read("Assets/BigimongAR/Scripts/ProceduralCombatVfx.cs");
  const actor = read("Assets/BigimongAR/Scripts/ArBattleActor.cs");
  for (const method of ["PlayFire", "PlayWater", "PlayEarth", "PlayWind", "PlayImpact"])
    assert.match(vfx, new RegExp(method));
  assert.match(actor, /WingedIdleMotion/);
  assert.match(actor, /GroundedIdleMotion/);
  assert.match(actor, /DodgeMotion\(string direction, string dodgeProfile\)/);
  assert.match(actor, /WaitForSecondsRealtime/);
  assert.doesNotMatch(actor, /Time\.timeScale/);
});
```

- [ ] **Step 2: Run focused test and verify RED**

Run: `node --test tests/ar-project.test.js`

Expected: FAIL because `ProceduralCombatVfx.cs` is missing.

- [ ] **Step 3: Implement pooled elemental effects**

Build a maximum-32-item effect pool. Fire uses ember particles and expanding orange spheres; water uses cyan trails, flattened wave rings, mist, and shard impacts; earth uses dust, stones, cracks, and shock rings; wind uses translucent spiral line renderers, feathers, and pressure rings. Destroy or return every effect to the pool after a bounded unscaled-time duration.

- [ ] **Step 4: Implement lively actor motion**

Resolve `ElementSkillProfile` during `Initialize`. Detect named `Wing` descendants for winged idle. Grounded idle combines breathing scale, head yaw, tail sway, and periodic weight shift. Winged idle adds wing rotations and asymmetric vertical hover. Use the catalog's attack and dodge profile IDs to select parameters and motion families; art ID offsets timing so no two species share an identical curve.

On hit, freeze only the actor's procedural routine for 0.06 seconds using `WaitForSecondsRealtime`, apply directional knockback and color flash, then return to the home pose. On KO or victory, stop idle and preserve the terminal pose.

- [ ] **Step 5: Integrate launch/impact events and verify**

`ArBattleDirector.PlayRound` launches the attacker effect at attack anticipation, waits for its profile travel time, plays dodge or impact, then reconciles the queued snapshot. Ensure snapshot ordering remains protected by the existing regression test.

Run: `node --test tests/ar-project.test.js && npm test`

- [ ] **Step 6: Commit**

```bash
git add tests/ar-project.test.js unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralCombatVfx.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleActor.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ProceduralDragonFactory.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleDirector.cs
git commit -m "feat: animate elemental procedural combat"
```

---

### Task 6: Beta Flow, Tyrannosaurus Encounter, Pet Selector, and Mobile HUD

**Files:**
- Create: `unity/BigimongAR/Assets/BigimongAR/Scripts/OfflineBetaFlowController.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleOfflineDemo.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleHud.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleNativeBridge.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleArenaController.cs`
- Modify: `unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs`
- Modify: `tests/ar-project.test.js`

**Interfaces:**
- Consumes: avatar completion, selected art ID, arena placement, summon completion, offline round completion.
- Produces: `OfflineBetaPhase`, `SelectPet(int)`, `AcceptEncounter()`, `UseScreenFixedFallback()`, `Rematch()`, `ReturnToPetSelection()`.

- [ ] **Step 1: Write the failing beta state and HUD test**

```js
test("offline beta gates avatar, encounter, AR placement, summoning, battle, and result", () => {
  const flow = read("Assets/BigimongAR/Scripts/OfflineBetaFlowController.cs");
  const hud = read("Assets/BigimongAR/Scripts/ArBattleHud.cs");
  for (const phase of ["AvatarCreate", "PetTestSelect", "TyrannosaurEncounter", "ArScan", "SummonSequence", "Battle", "Result"])
    assert.match(flow, new RegExp(phase));
  assert.match(flow, /opponentArtId = 1/);
  assert.match(flow, /Mathf\.Clamp\(artId, 1, 30\)/);
  assert.match(hud, /"←"/);
  assert.match(hud, /"↑"/);
  assert.match(hud, /"→"/);
  assert.match(hud, /PressFeedback/);
  assert.match(flow, /UseScreenFixedFallback/);
});
```

- [ ] **Step 2: Run focused test and verify RED**

Run: `node --test tests/ar-project.test.js`

Expected: FAIL because `OfflineBetaFlowController.cs` is missing.

- [ ] **Step 3: Implement the flow controller and encounter**

Load the avatar profile on startup. Missing profile opens creator; saved profile opens beta pet selection. Pet selection wraps 1–30 and labels itself `베타 테스트 선택 · 보상 없음`. Accepting the fixed Tyrannosaurus encounter enables AR scanning; arena placement starts summoning; summoning completion enables battle input; the final snapshot opens result.

Move screen ownership out of `ArBattleOfflineDemo`; leave it responsible only for battle state and direction resolution. Keep the existing native payload branch untouched.

- [ ] **Step 4: Implement safe-area mobile controls and feedback**

The scene builder changes labels to `←`, `↑`, `→`, creates three equal circular buttons centered in the lower safe area, and adds a component-driven press routine:

```csharp
private IEnumerator PressFeedback(RectTransform target)
{
    yield return Scale(target, 1f, 0.92f, 0.06f);
    yield return Scale(target, 0.92f, 1.05f, 0.08f);
    yield return Scale(target, 1.05f, 1f, 0.06f);
}
```

Input remains disabled unless phase is `Battle` and no round animation is playing.

- [ ] **Step 5: Implement screen-fixed fallback**

After the scan guidance threshold, show a user-initiated `화면 고정 테스트` button. `UseScreenFixedFallback` places the arena 1.8 meters in front of the camera at a stable horizontal pose, marks the beta as non-AR-positioned, and proceeds through the same summon and battle sequence.

- [ ] **Step 6: Verify and commit**

Run: `node --test tests/ar-project.test.js && npm test`

```bash
git add tests/ar-project.test.js unity/BigimongAR/Assets/BigimongAR/Scripts/OfflineBetaFlowController.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleOfflineDemo.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleHud.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleNativeBridge.cs unity/BigimongAR/Assets/BigimongAR/Scripts/ArBattleArenaController.cs unity/BigimongAR/Assets/BigimongAR/Editor/BigimongArSceneBuilder.cs
git commit -m "feat: complete the offline AR beta flow"
```

---

### Task 7: CI Publication, Unity Compile, APK Verification, and Delivery

**Files:**
- Modify only when diagnostics prove necessary: `.github/workflows/build-ar-debug-apk.yml`
- Modify only when compiler diagnostics prove necessary: Unity C# files named by the failing log
- Modify after success: `README.md`
- Modify after success: `docs/AR_IMPLEMENTATION_v0.12.md`
- Test: `tests/apk-build.test.js`, `tests/apk-verifier.test.js`

**Interfaces:**
- Consumes: GitHub branch `feature/v0.12-offline-beta`, configured Unity Actions secrets, build method `Bigimong.Editor.BigimongAndroidBuild.BuildDebugApk`.
- Produces: `Bigimong-AR-v0.12-debug.apk`, `Bigimong-AR-v0.12-verification.json`, `Bigimong-AR-v0.12-debug.apk.sha256`.

- [ ] **Step 1: Run local pre-publication verification**

Run:

```bash
npm test
git diff --check
git status --short --branch
```

Expected: all tests PASS, no whitespace errors, and only intentional committed changes.

- [ ] **Step 2: Publish the branch through the connected GitHub app**

Create blobs for files changed from `origin/main`, create a tree based on the current remote main tree, create one commit with current remote main as parent, and create/update `feature/v0.12-offline-beta` without force. Confirm the resulting remote commit contains the workflow trigger and all Unity sources.

- [ ] **Step 3: Observe the automatic Actions run**

The branch push triggers `Build Bigimong AR debug APK`. Inspect verify and build jobs. If it fails, download diagnostic logs, use `superpowers:systematic-debugging`, write a failing local contract test for the proven root cause when possible, make one fix, republish, and rerun. Do not guess at multiple fixes.

- [ ] **Step 4: Download and verify the successful artifact**

Extract the artifact into `build/delivery/v0.12`. Run:

```bash
node scripts/verify-apk.mjs --apk build/delivery/v0.12/Bigimong-AR-v0.12-debug.apk --report build/delivery/v0.12/local-verification.json
sha256sum -c build/delivery/v0.12/Bigimong-AR-v0.12-debug.apk.sha256
```

Expected: verifier exits 0, JSON contains `"valid": true`, and checksum reports `OK`.

- [ ] **Step 5: Update documentation only after artifact evidence exists**

Change status from `APK 빌드 소스 준비 완료` to `오프라인 AR 베타 APK 빌드 및 자동 검증 완료`. Record the GitHub commit, Actions run, artifact filename, SHA-256, included beta flow, and the explicit limitation that physical-device behavior awaits the user's test.

- [ ] **Step 6: Run final verification and commit docs**

Run: `npm test && git diff --check`

```bash
git add README.md docs/AR_IMPLEMENTATION_v0.12.md tests/apk-build.test.js
git commit -m "docs: record verified v0.12 beta APK"
```

- [ ] **Step 7: Deliver**

Save the verified APK as a persistent user-facing file and provide a direct download link, SHA-256, Android installation instructions, and the ten-item physical-device checklist from the spec. Do not describe the APK as production-ready.
