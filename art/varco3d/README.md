# VARCO 3D AR asset handoff

This folder records the VARCO-generated pilot assets that feed the Bigimong AR battle presentation. VARCO owns asset generation, PBR texturing, rigging, animation authoring, and editor transfer. Unity AR Foundation remains responsible for plane detection, anchors, tracking recovery, occlusion, and runtime placement.

The repository intentionally stores no VARCO token, OAuth credential, account asset ID, or direct download URL. Use the authenticated VARCO 3D Bridge session in the Unity Editor to transfer the assets listed in `v0.18-ar-pilot.json` by their provider titles.

## Import order

1. Install the Unity bridge downloaded from VARCO 3D Studio by selecting its `package.json` through **Package Manager > Install package from disk**.
2. Open **VARCO3D > Connect VARCO3D** and confirm that Studio shows the bridge connection in green.
3. Export the six provider titles from `v0.18-ar-pilot.json` as FBX models at their exact `unitySourceModelPath` values. The male avatar already exists in the account and must not be regenerated.
4. Let Unity finish importing. `VarcoModelImportPostprocessor` assigns Humanoid rigs to the avatars, Generic rigs to the three Bigimong stages, no rig to the egg, and applies the mobile mesh import settings.
5. `VarcoAssetImportPipeline` measures each renderer hierarchy, grounds its lowest point at local `Y = 0`, scales it to `targetHeightM`, writes the listed `unityPrefabPath`, and connects Baby, Teen, and Adult to art ID `1` in `ArCharacterCatalog`.
6. Build through `BigimongAndroidBuild.BuildDebugApk`. The custom builder prepares assets before creating the AR scene, and the shared Unity pre-build hook checks every player build for generated height, ground contact, rig, triangle count, material count, base textures, and avatar summon compatibility.

If a source FBX has not arrived yet, the build deliberately keeps the existing procedural avatar or Bigimong fallback. Once a source exists, a missing or invalid generated prefab is a build error rather than a silent visual regression. Removing or moving a source also removes only the generated prefab carrying the VARCO ownership label. Any unrelated asset occupying one of the reserved Resources paths is preserved on disk but blocks the build until moved, preventing both accidental overwrite and runtime fallback bypass.

## Animation contract

Each battle character must eventually expose these Animator states:

- `Idle`
- `Summon`
- `Attack`
- `DodgeLeft`
- `DodgeRight`
- `Hit`
- `Knockout`
- `Victory`

The egg remains a static mesh. Its polish and hatch motion should be implemented as Unity transform/material effects so the same asset can be reused across progress states.

## Mobile acceptance checks

- Character meshes stay at or below 25K triangles; the egg stays near 12K.
- Imported character geometry must remain at or below 30K triangles and the egg at or below 15K; all assets use four materials or fewer.
- One 1K PBR texture set per asset, with a non-missing base texture on every material.
- No missing textures, inverted normals, floating origin, or non-uniform prefab scale.
- Baby, Teen, and Adult retain distinct proportions rather than being scaled copies.
- The AR battle stays playable when tracking is lost and restored; no VARCO credential or web session ships in the APK.
