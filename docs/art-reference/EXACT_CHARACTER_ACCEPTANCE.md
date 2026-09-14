# Character asset replacement — 2026-09-14 user correction

The user rejected v0.13: its procedural primitives do not resemble the supplied references. Passing code tests and APK compilation does not constitute visual acceptance. Do not describe the current geometry as matching or as finished character artwork.

## Authoritative references

The newly attached avatar pair, character roster, and seven-species three-stage evolution poster define visual identity. Original attachments stay private and are not committed to the public repository.

- Avatar: the supplied masculine/feminine faces, large brown eyes, soft cheeks, swept brown hair / brown bob, proportional limbs and hands, charcoal sleeveless top and shorts. Preserve this base identity before adding customization variants.
- Monsters: rounded continuous surfaces, integrated facial features, reference palette, cream underside, visible material detail, clothing and accessories matching the selected reference stage.
- Evolution poster explicitly supplies baby, teen and adult for 01 Tyrannosaurus, 02 Triceratops, 03 Pterosaur, 04 Stegosaurus, 16 Baryonyx, 17 Oviraptor, 18 Protoceratops. Do not claim it supplies all 90 stage designs.
- Tyrannosaurus pilot: baby red body/cream underside/yellow cap; teen red hoodie/dark cap; adult yellow-orange outerwear/yellow cap/loaded backpack. Growth changes body proportions and wardrobe, not merely uniform scaling.

## Required acceptance before another visual-delivery APK

1. Obtain actual textured 3D assets rather than another sphere/capsule assembly.
2. Pilot the two base avatars and the three Tyrannosaurus stages. Inspect actual textured model renders against the references before expanding to the remaining catalog.
3. Compare face shape, eyes, hair/snout, silhouette, limb proportions, colors, clothing and accessories. Inspect front, side and rear geometry; unseen surfaces are inferred and need review.
4. Distinguish source artwork, generated candidate images, model renders and in-game captures. A 2D preview or camera-facing card is not a completed 3D AR model.
5. Test actual Unity imports, scale, material visibility, animation readiness, attachment separation and mobile performance. Model generation alone does not establish usable rigging or customization topology.
6. Show actual in-game captures before claiming visual completion. Code/editor tests remain technical checks only.

## Current readiness

The repository contains no FBX/GLB/GLTF/BLEND/OBJ character source models. CharacterPrefabCatalog already supports species/stage prefab replacement. Avatar creation still uses ProceduralAvatarFactory and requires a real model-based customization path once usable base assets exist.

Available native image generation is raster output, not a 3D mesh/rig exporter. Plugin discovery found Fal with a 3D workflow, but it was not installed/connected at the time of this inspection. Its specific models, costs and outputs must be inspected after connection. No external generation job or charge has been started. No fidelity guarantee is made for automatic conversion.
