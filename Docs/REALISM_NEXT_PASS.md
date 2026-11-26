# Next realism pass — inspected 26 November 2026

This audit reads WORK source at `1cf8bfb` plus the Northline label fix staged alongside this document.
The audit itself did not run Unity, Blender, a player, a render, or a benchmark, and did not control the user's0.7.2 playtest.
Named later root-owned verification runs are recorded separately below.
Linux/URP gameplay and offline RTX-rendered Blender films remain the selected path; gameplay hardware ray tracing is absent.

## 1. Fix the Northline sign material before adding more surface detail

The actual coach capture `Logs/coach-rendered-review-20261126T0731306979010Z/interior-aisle.png` shows large mirrored,
fragmented yellow lettering over the coach interior. `RailDistrict.Sign` assigned a Rajdhani `TextMesh.font` but did not assign
its renderer a material. The existing `Shaders/WorldText.shader` already specifies `Cull Back`, `ZTest LEqual`,
`ZWrite Off` and the correct font-alpha blend. Other district signs use `WorldTextFont`; Northline omitted that binding.
This missing binding is an evidenced defect and a likely explanation for the captured artifact, not a proved sole cause.

Source now adds `Scripts/Missions/RailWorldLabelMaterial.cs` and attaches it to the same five Northline signs.
It uses the existing `WorldTextFont` dynamic-atlas refresh pattern with a wholly owned material, subscribes to actual font rebuilds,
requests dynamic characters before reading the current atlas, and never changes the font/UI material or an imported asset.
Disable/destroy unsubscribes, restores the prior renderer binding/shadow settings if still owned, and releases its material.
All sign text, positions and sizes, model bindings, colliders and other chapter geometry stay unchanged.
Root additionally changed the first three signs to yaw0 for their actual northbound/south-side approach; the inside return and
boarding signs keep explicit yaw180. Actual approach-view readability still needs a chapter capture.

The two focused EditMode ownership/atlas/handoff cases pass in `Logs/campaign-save-edit-20261126b.xml`
(24/24 total, native0 reported by root; the two label cases are independently present and passing in the actual XML).
They cover source preservation, refresh after text change, disable/re-enable/destroy lifetime and a later renderer handoff.

The first actual RTX4050/Vulkan pixel run is retained RED in `Logs/northline-label-gpu-20261126.xml` and
`Logs/northline-label-rendered-20261126T0757372855230Z`. Front glyphs are readable, and the back is culled. Its global
brightness metric counted116,250front pixels and43,760opaque-wall pixels, but the latter are all background:
the opaque wall rectangle x25..614/y75..244 contains100,300exact-black pixels and **zero** bright pixels.
All43,760bright pixels lie outside that rectangle. The existing full-screen atmosphere shader adds sky haze even when the
camera's clear colour is black (`CityAssets` injects that feature before postprocessing). This proves the old metric counted
background; it does not establish a full chapter graphics pass.

The corrected staged pixel test now captures same-camera, same-frame text-on/text-off pairs for front/back/opaque-wall
controls, preserving the production atmosphere and all quality settings. It toggles only `MeshRenderer.enabled`, leaving the
material owner active, and counts pixels whose maximum absolute RGB-channel difference is greater than20. Acceptance stays
front>100changed pixels, back<=3, opaque-wall<=3. It saves all six images and explicit metric/counts under a fresh
`Logs/northline-label-rendered-*` directory. Null explicitly skips this graphics-only test. The corrected GPU result is pending;
old red artifacts are unchanged. No FPS, final scene appearance or measured memory result is claimed.

Static verification: low-priority `Tools/compile_check.sh all` reports runtime180 and Editor33 API checks passing.
The Linux-Mono API check refuses a stale dependency cache for the PlayMode assembly definition; a future successful player
build must refresh it. The later named EditMode and first GPU runs above supersede the initial unrun-test status.
No player rebuild was attempted by this audit.

Cost: five small owned material objects and reuse of the existing shared dynamic font atlas; no new mesh/texture asset,
light, reflection, ray-march pass or physics work. Actual frame cost is unmeasured.

## 2. Improve existing Blender humans through skin surface authoring

The game already contains25 MPFB-generated skinned character variants in `Art/Characters/Models` and an actual25-entry
`Resources/Characters/CharacterCatalog.asset`. Their recorded LOD0 meshes are16,850–19,998triangles, LOD1 is6,994–7,046,
and LOD2 is2,494–2,562. `Scripts/Characters/HumanoidRig.cs` loads those prefabs and uses a capsule only as an emergency fallback.
Rebuilding anatomy from scratch would discard existing authored work without addressing the clearest source gap.

`Editor/CharacterModelImporter.cs:BuildCharacter` binds skin albedo and vein emission, with uniform0.42smoothness.
Its skin material has no normal or surface-mask binding; an actual `GuardMarcus_Skin.mat` confirms null `_BumpMap`/`_MaskMap`.
`Shaders/CharacterInput.hlsl` currently sets all character occlusion to1, and its skin warmth is an albedo tint rather than
light-dependent transmission. Cloth already has authored normal and packed material maps, so do not replace that pipeline.

Next source change: extend `Tools/blender/characters/make_character.py` and metadata for baked tangent-space skin normals
and a separately filtered smoothness/AO/thickness mask in the existing MakeHuman UV layout. Use high-frequency authoring,
not an uncalibrated albedo-to-normal filter. Extend only the skin importer path and skin shader branch; preserve cloth tint IDs,
possession veins, humanoid skeleton/animations and all three LODs. Start with Dr. Vale/Petrov and one shared generic skin set.
A small optional light-direction transmission term can use thickness later, with source-pixel comparisons required before acceptance.

Pilot budget:2K BC5 normal plus1K BC7 surface mask with full mip chains is about6.67MiB of compressed texture payload per
unique skin set (calculated, not measured VRAM). All16current unique skin sets would add about106.7MiB before allocation/
residency overhead. Reuse each skin pair across matching characters and keep four existing material slots, rather than adding
per-body draw passes. Validate skin sheen/pores at dialogue distance, normal handedness, possession readability, LOD continuity,
source immutability and measured frame/memory cost on the final target binary before expanding the pilot.

## 3. Author the laboratory's close interaction props in Blender

The original lab is still built with primitive interaction-scale geometry: `Editor/World/CityProps.cs:LabBench` uses one large
`LabPlastic` cabinet box, one flat `LabCounter` slab, monitors and a0.25m steel box. `Tank` uses concentric cylinders.
`Editor/World/CityAssets.cs` already gives major walls/floors generic PBR sets, so simply adding another global texture/resolution
slider does not address these close silhouettes. Lab walls reuse plaster at6m tiling; several lab prop materials are constant-colour Lit.

Next source change: a Blender-authored lab workstation/analysis instrument/tank support kit with bevelled silhouettes,
real vents/cable channels, gasket joins, fasteners, glass thickness and material-separated painted metal/steel/plastic/rubber.
Bake large-scale wear plus calibrated A/N/M maps into a shared2K atlas, with mask variation that distinguishes touched handles
from recesses. Integrate visuals through the existing generated `CityProps` builder or a scoped runtime authored binding;
retain the exact existing interaction points/colliders, room apertures, navigation and line-of-sight contract. Do not hand-edit scenes.
Generate scenes only later through `CityBuilder.BuildAllBatch` on an available rendered GPU session.

Initial budget: three heroes together below30kvisible triangles, a shared two-material opaque/glass split, one2K BC7 A map,
one2K BC5 N map and one2K BC7 M map (about16MiB compressed full-mip payload, calculated). These are proposal limits,
not authored counts or measured performance. Verify the actual imported kit/material state, then matched close-view images,
role access, original lab routes/collision/nav and declared native GPU timing/memory. Preserve transparency visibility and do
not disable shadows, HDR or reflection quality merely to make a verifier pass.

Root execution update: paired GPU test passes1/1/native0 onRTX4050/Vulkan (front14043/back0/occluded0 changed pixels). Fresh actual coach exterior/aisle images are inspected and no longer show mirrored glyphs through the coach. Both strict coach tests remain red on5 raw ring-buffer warnings despite completed functional assertions. [Exact history](verification/northline-world-labels-20261126.json).
