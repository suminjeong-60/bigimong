# Bigimong Playable AR Beta: Avatar, Summoning, and Elemental Combat Design

Date: 2026-09-12

## Goal

Produce an installable Android beta APK that can be tested on one phone without a backend. The player creates a personal avatar, selects a temporary test pet, accepts a Tyrannosaurus bot encounter, places an AR arena, performs a medallion summoning sequence, and completes a directional battle with visible elemental skills, distinct idle motion, dodges, hits, victory, and rematch.

This beta validates the presentation and interaction loop. It does not replace the existing server-authoritative online battle, wallet settlement, hatch, or progression contracts.

## Fixed Product Decisions

- Unity `6000.0.58f2`, AR Foundation and ARCore `6.1.1` remain pinned.
- Android package remains `com.bigimong.app`, minSdk 28, portrait, IL2CPP, ARM64 only.
- The first APK is a standalone offline AR beta.
- All 30 pets use procedural 3D stand-ins in this beta.
- The first opponent is a Tyrannosaurus bot, art ID `01`.
- Male and female avatar base bodies are available. Body choice does not affect combat stats.
- The avatar target style follows the supplied reference: friendly stylized 3D proportions, large expressive eyes, soft facial forms, and a dark sleeveless base outfit.
- Elemental effects are original Bigimong effects. No animation, silhouette, projectile, or effect is copied from another game.
- Debug pet selection is available only to test all 30 species. Production ownership and hatching rules remain unchanged.

## Beta Flow

The standalone beta uses this state machine:

1. `AVATAR_CREATE`
2. `PET_TEST_SELECT`
3. `TYRANNOSAUR_ENCOUNTER`
4. `AR_SCAN`
5. `SUMMON_SEQUENCE`
6. `BATTLE`
7. `RESULT`
8. `REMATCH` or return to pet selection

If an Android host launches Unity with an online battle payload, the existing native bridge remains authoritative and the offline beta flow does not start.

## Player Avatar

### Profile

`AvatarProfile` contains only stable IDs and colors:

- `schemaVersion`
- `displayName`
- `bodyType`: `MASCULINE` or `FEMININE`
- `faceShapeId`: 1–5
- `skinToneId`: 1–8
- `eyebrowId`: 1–6
- `eyeColorId`: 1–8
- `hairStyleId`: 1–12
- `hairColorId`: 1–10

Invalid or missing IDs are clamped to safe defaults. The beta saves the profile as versioned JSON under `Application.persistentDataPath`. A future Android/account adapter can sync the same schema to the server without changing the Unity avatar factory.

### Creation Screen

- First launch cannot skip avatar creation.
- The avatar stands at center and slowly turns for preview.
- Tabs select body, face, skin, eyebrows, eyes, hair, and hair color.
- Changes update the preview immediately.
- Name is required and trimmed before saving.
- Saving transitions to the beta pet selector.
- A later edit button reopens the same creator.

### Procedural Avatar Factory

The beta constructs a lightweight articulated avatar from Unity primitives and generated materials. Separate transforms exist for torso, head, hair pieces, eyebrows, eyes, upper arms, forearms, hands, legs, the necklace chain, and the gold summoning medallion. Face shape changes proportions; hair and eyebrow IDs select different generated assemblies; colors come from fixed accessible palettes.

The procedural avatar is an implementation stand-in. A future rigged production avatar can replace its renderer and animator while preserving `AvatarProfile`, `SummonerActor`, and the summoning events.

## Summoning Medallion

Every avatar wears a gold chain with a horse-pass-inspired fantasy medallion. The medallion carries an original circular Bigimong rune. It must read as a summoning device rather than a reproduction of a real historical object.

### Sequence

| Time | Event |
| ---: | --- |
| 0.0–0.5s | Avatar reaches for the necklace. |
| 0.5–1.2s | Hand pulls the medallion free and throws it toward the arena. |
| 1.2–1.5s | Chain follows an arc; medallion spins and lands flat. |
| 1.5–2.2s | Gold rune lines expand from the medallion into a floor-aligned magic circle. |
| 2.2–3.2s | Light column and the selected pet's elemental particles rise. |
| 2.5–3.5s | Pet rises from below the circle, fades in, and reaches full physical scale. |
| 3.5–4.0s | Avatar steps back; player pet and Tyrannosaurus enter ready poses. |
| 4.0s | Direction controls unlock. |

The circle is parented to the placed arena root so reanchoring moves the entire sequence consistently. A cancelled or lost anchor invalidates the current sequence and returns to scanning.

## Combat Presentation Architecture

### `ElementSkillCatalog`

Maps each art ID to:

- element: `WATER`, `FIRE`, `EARTH`, or `WIND`
- signature skill ID and display name
- idle profile
- attack motion profile
- dodge motion profile
- hit and victory style parameters
- effect colors, projectile form, travel arc, speed, burst size, and camera impulse

### `ProceduralCombatVfx`

Creates pooled, runtime effects from primitives, line renderers, trails, and particle systems:

- Fire: flame breath, embers, burning arcs, and impact bursts
- Water: jets, blades, waves, mist, and ice-like shards
- Earth: cracks, stones, dust, sand, shields, and ground shock rings
- Wind: spirals, pressure blades, feathers, speed trails, and air bursts

Effects are generated at runtime and use no copied third-party game assets.

### `ArBattleActor`

The existing actor remains the single animation entry point. It gains:

- summon rise and ready transitions
- continuous winged or grounded idle motion
- per-profile procedural attack and dodge routines
- local hit-stop without pausing AR tracking
- hit recoil, knockback, KO, and victory variation
- effect launch and impact event hooks

Final Animator Controllers continue to take priority when installed. Procedural motion remains the fallback.

### Idle Rules

- Winged species flap wings, hover, and move vertically with asymmetric timing.
- Grounded species breathe, shift weight, move head and tail, blink, and occasionally stamp or look around.
- Idle motion pauses during summon, attack, dodge, hit, KO, and victory, then resumes from a stable home pose.

## Species Combat Matrix

| ID | Species | Element | Signature skill | Distinct dodge |
| ---: | --- | --- | --- | --- |
| 01 | Tyrannosaurus | Fire | Tyrant Flame Roar | heavy pivot sidestep |
| 02 | Triceratops | Earth | Earth Charge | horn guard and lateral shove |
| 03 | Pterosaur | Wind | Sky Dive | vertical ascent |
| 04 | Stegosaurus | Fire | Flame Spike Storm | tail-assisted hop |
| 05 | Brachiosaurus | Earth | Giant Tremor | stomp recoil |
| 06 | Spinosaurus | Water | Abyss Water Blade | low body slide |
| 07 | Ankylosaurus | Earth | Armored Quake Hammer | armored curl |
| 08 | Velociraptor | Fire | Forest-Flame Claws | aerial side roll |
| 09 | Corythosaurus | Fire | Resonant Flame Wave | flame-trail leap |
| 10 | Carnotaurus | Fire | Red Horn Detonation | low horn slide |
| 11 | Mosasaurus | Water | Tidal Jaw | dive and reappear |
| 12 | Allosaurus | Water | Frost Predator Combo | ice-slide retreat |
| 13 | Quetzalcoatlus | Wind | Sky Spear | spiral ascent |
| 14 | Deinonychus | Wind | Cyclone Sickle Claw | lateral somersault |
| 15 | Euoplocephalus | Earth | Earth Hammer | tail-pivot half turn |
| 16 | Baryonyx | Water | River Hunter | crouched quick-step |
| 17 | Oviraptor | Earth | Guardian Stone Pulse | shell-shield hide |
| 18 | Protoceratops | Earth | Sand Shield Charge | brief sand burrow |
| 19 | Gallimimus | Wind | Gale Kick | afterimage sprint |
| 20 | Dracorex | Wind | Storm Crown | vortex ascent |
| 21 | Giganotosaurus | Fire | Doom Flame Jaw | flame-wing side roll |
| 22 | Dilophosaurus | Water | Mist Twin-Crest Mirage | mist clone swap |
| 23 | Iguanodon | Earth | Stone Thumb Spear | turning deflection |
| 24 | Kentrosaurus | Earth | Twin Earth Spikes | tail-pole vault |
| 25 | Therizinosaurus | Water | Moon-Tide Claws | wave-backbend |
| 26 | Compsognathus | Wind | Shadow Sprint | zigzag dash |
| 27 | Parasaurolophus | Water | Resonant Pressure Wave | circular water slide |
| 28 | Microraptor | Wind | Four-Wing Storm | folded-wing snap roll |
| 29 | Brontosaurus | Earth | Green Giant Step | low-body impact flow |
| 30 | Titanosaurus | Fire | Crimson Titan Descent | fire-column ascent |

## Tyrannosaurus Bot

- Uses art ID `01` and the Fire profile.
- Starts with five HP, matching maximum vitality behavior.
- Chooses directions randomly in the standalone beta.
- Uses the same ten-second turn deadline and automatic-choice indicators as online combat.
- Alternates attacker and defender roles with the player.
- Uses the same direction resolution contract: matching attack and defense directions can hit; different directions dodge; attack and evasion stats can produce critical or stat-dodge outcomes.
- Has no wallet reward, stake, ranking, or server settlement in offline beta mode.

## Mobile Battle HUD

- Three equal circular controls sit centered along the lower safe area: `←`, `↑`, `→`.
- A press scales to 92%, rebounds to 105%, then returns to 100%.
- Controls lock during summoning, round playback, hit-stop, KO, and result display.
- Player and bot HP remain at the upper safe area.
- Countdown and round result remain visible without covering the AR arena.
- The beta badge states that the battle is offline and has no settlement.
- Result screen offers rematch and return-to-pet-selection actions.

## Impact and Camera Safety

- A short actor-local freeze creates hit-stop. Global `Time.timeScale` is not changed.
- Camera impact is a screen-space impulse/vignette, not movement of the AR camera transform.
- Critical hits increase effect scale, impulse, and sound-event intensity.
- VFX objects are pooled and capped to protect mobile frame time.
- Unsupported shaders fall back to unlit transparent materials.

## AR Failure Handling

- Camera permission denial displays a direct retry instruction.
- Plane scanning displays a placement reticle and movement guidance.
- Tracking loss hides attack controls and preserves current battle state.
- Reanchor invalidates stale summon callbacks and returns to scanning.
- A prolonged scan exposes a screen-fixed tabletop fallback so the beta remains testable when AR tracking is unavailable.
- Returning from background recalibrates the countdown and does not replay a completed round.

## Persistence and Compatibility

- Avatar and last beta pet selection are stored locally as versioned JSON.
- Malformed data falls back to defaults without blocking launch.
- The online Android-to-Unity battle payload remains schema version 1.
- Offline-only fields never enter wallet or server settlement paths.
- Production hatching still determines owned pets; the 30-species selector is visibly labeled beta testing.

## Verification

### Automated

- Node regression tests continue to validate game, journey, API, Android, and AR contracts.
- New contract tests cover avatar schema defaults, all customization option ranges, all 30 complete skill profiles, unique skill and dodge IDs, beta state transitions, summon event ordering, input locking, and online/offline bridge separation.
- GitHub Actions compiles the Unity project with Unity `6000.0.58f2`.
- The APK verifier checks package, version, minSdk, ARM64-only Unity library, report JSON, and SHA-256.

### Manual device checklist

1. Install on an ARCore-capable Android phone.
2. Complete both male and female avatar paths.
3. Change every customization category and relaunch to confirm persistence.
4. Select multiple grounded and winged pets.
5. Accept the Tyrannosaurus battle and place the arena.
6. Verify medallion throw, magic circle, summon rise, and input lock.
7. Verify all three direction controls and press feedback.
8. Observe elemental attack, distinct dodge, hit, KO, victory, and rematch.
9. Let a turn expire to verify automatic play.
10. Interrupt tracking and resume to verify recovery.

## First APK Completion Criteria

The beta is complete only when:

- all automated tests pass;
- Unity C# compilation succeeds in GitHub Actions;
- an ARM64 APK is produced;
- APK verification JSON reports `valid: true`;
- SHA-256 matches the generated checksum file;
- the APK is made available for direct download;
- any behavior not yet verified on a physical phone is explicitly labeled as awaiting user beta feedback.

## Out of Scope for This APK

- Final rigged human and dinosaur meshes
- Production-quality facial blendshapes
- Clothing shop and accessories other than the fixed summoning necklace
- Real-money or Bigi settlement in offline mode
- Matchmaking, BLE pairing, and Cloud Anchor validation between two devices
- Production account sync of the avatar profile
- Final sound, music, localization, analytics, Play Integrity, and store release signing
