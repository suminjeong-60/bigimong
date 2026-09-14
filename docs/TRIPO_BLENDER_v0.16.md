# Tripo → Blender → Unity v0.16

This pipeline is prepared for the five approved pilot models: two avatars and the baby/teen/adult red Tyrannosaurus. It intentionally does not execute billable generation while `billableExecutionEnabled` and every job's `enabled` value are `false`.

## Validate the reproducible pilot

```bash
node scripts/validate-tripo-manifest.mjs art/tripo/v0.16-pilot-jobs.json
python3 scripts/blender_prepare_bigimong.py --self-test
```

Before a Tripo call, crop the two avatars with each job's top-left pixel rectangle. Dinosaur sources are already isolated WebP catalog assets. Use the manifest defaults with the listed `tripo3d/h3.1/image-to-3d` endpoint. Do not put Fal or Tripo credentials in this repository, screenshots, logs, or result manifests. Record returned job IDs and immutable result URLs outside source control until the files are downloaded.

A single-view mesh is provisional. For final reference fidelity, render or draw front/left/back/right views, change the endpoint to `tripo3d/h3.1/multiview-to-3d`, and submit images in that exact order.

## Download naming contract

Download each completed GLB into an input directory using its `outputStem`, for example `avatar_male_v016.glb` and `tyrannosaurus_baby_v016.glb`. Verify the provider download hash before Blender import. Generated binary models stay out of the Git repository until their rights, size, mobile performance and fixed turntable renders pass review.

## Blender batch

```bash
blender --background --python scripts/blender_prepare_bigimong.py -- \
  --manifest art/tripo/v0.16-pilot-jobs.json \
  --input-dir build/tripo-v016/incoming \
  --output-dir build/tripo-v016/unity \
  --blend build/tripo-v016/bigimong-v016-review.blend
```

The script imports GLB/GLTF/FBX/OBJ, applies mesh rotation and scale, creates a model root at the combined lowest point, moves ground contact to `Z=0`, scales to the approved AR height, exports one Unity GLB per model, arranges three models per review row, saves a Blender review scene, and emits `bigimong-v016-model-report.json`.

After visual, rig and mobile checks pass, copy the accepted FBX files into `Assets/BigimongAR/Resources/GeneratedCharacters/` with the stable names `Avatar_Masculine`, `Avatar_Feminine`, `Bigimong_01_Baby`, `Bigimong_01_Teen`, and `Bigimong_01_Adult`. The runtime loads those accepted prefabs first and retains procedural fallbacks for every missing model.

## Visual approval gate

For each model compare a fixed-camera front, left, back and right render against the supplied reference. Reject swapped colors, wrong eye size/spacing, missing accessories, adult-as-scaled-baby anatomy, floating feet, baked background geometry, merged teeth/eyes, broken UVs, or a bottom-origin error over 1 mm. Only accepted assets replace `ProceduralAvatarFactory` or `ProceduralDragonFactory`; fallbacks remain for every missing catalog model.
