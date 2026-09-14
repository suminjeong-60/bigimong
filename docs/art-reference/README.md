# Bigimong visual direction — 2026-09-13

The user explicitly requested continuing the approved playable AR beta and actively incorporating these three references. The original PNGs are privately supplied visual references and are intentionally excluded from the public repository. Their local filenames below identify the three inputs for authorized local work. They are not full-screen backgrounds that replace real 3D characters.

- `avatar-base.png`: male/female friendly stylized 3D avatars, large brown eyes with white catchlights, soft cheeks/nose/smile, swept brown hair and bob hair, dark sleeveless top and shorts, coherent bare limbs, soft studio lighting. Preserve all approved customization ranges and the detachable golden summon medallion.
- `ar-battle.png`: live camera background, generous unobstructed arena, cyan outlined translucent navy panels, name/height markers, glowing cyan floor rings, green HP bars, three large bottom directional controls. Adapt the landscape example to the existing portrait APK. Do not claim Bluetooth connectivity when offline; do not change the approved directional battle into a different rock-paper-scissors rule.
- `monster-roster.png`: round toy-like 3D dinosaurs, oversized expressive eyes, recognizable species silhouettes, species-specific color palettes. Red Tyrannosaurus, blue Triceratops, purple winged Pterosaur, orange Stegosaurus, green Brachiosaurus, blue Spinosaurus, blue/brown armored Ankylosaurus, feathered birds, aquatic flippers. Hats/glasses/backpacks/clothes must be separate removable accessory objects. Existing 01–30 IDs/species and elemental/stat rules win over inconsistent image text.

This pass upgrades the generated 3D beta models and UI. Final artist-authored skinned meshes/textures are a separate fidelity milestone; do not present this pass as visually identical to the illustration. Retain the final prefab replacement boundary.

The delivered 0.12 APK lacked a CAMERA permission and its project has no committed XR loader configuration. The next build must explicitly configure the ARCore loader and camera pose tracking and verify those conditions. Real phone tracking still requires device validation.
