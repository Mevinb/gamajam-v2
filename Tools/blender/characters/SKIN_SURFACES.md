# Skin surface authoring

The 25 existing MPFB characters retain their FBX meshes, three LODs, humanoid
skeletons, four material slots, cloth tint IDs and possession-vein UVs. Their
metadata now selects one of three colour-independent, shared body-UV surface
sets: Adult (12 characters, including LakeGirl), Mature (11, including Petrov),
Elder (2, including Vale). Existing skin photographs remain the albedo inputs.

`bake_skin_surface.py` builds a canonical full MPFB body for each age profile,
authors physical 3D pores and localised forehead/eye creases, and bakes through
Cycles on the CPU with two threads. No albedo-to-normal conversion is used.
The script performs inward BVH rays on the full body before any clothing/hide
operation, and bakes true local-geometry occlusion with a 35 mm radius.

Maps under `Assets/Parasite/Art/Characters/Textures/Skins/Surface`:

| File | Interpretation |
| --- | --- |
| `<profile>_N.png` | 2048² tangent normal, MikkTSpace +X/+Y/+Z, no green flip |
| `<profile>_M.png` | 1024² linear RGB: smoothness, local AO, optical thickness |

Thickness is inward ray distance divided by 30 mm, saturated to 0–1. Missing
opposing surfaces conservatively become thick. These are shared anatomical
approximations, not a per-character thickness simulation. Smoothness varies
across nose, forehead, lips, hands and microscopic relief. Normal relief is
70–95 µm for pores and 25–145 µm for age-dependent creases, baked into the
normal map. Source PNGs total 41,933,551 bytes. BC5 normal + BC7 mask with
full mips has approximately 20 MiB of texture payload for all three sets;
this is calculated storage, not a measured GPU allocation.

`CharacterModelImporter.RebuildBatch` imports the maps once per unique path,
using linear trilinear sampling, mips and streaming, explicitly BC5/BC7 for
Standalone. Skin uses low dielectric reflectance, mapped smoothness/AO and
the baked normal. `CharacterForwardPass.hlsl` preserves installed URP17.3
Lit inputs/GI/decals/fog/rendering layers and adds a small, shadow-aware
main-light thin-skin transmission term. That term is a raster approximation.
Eyes use the same material slot with URP's energy-conserving clear-coat lobe.
Skin/eye photographic albedo now explicitly imports as StandaloneBC7/HQ;
eyes retain their authored1024px. Raw linear F0 is0.028 skin/0.025 eyes (plain
Color properties). LakeGirl alone opts into a1.42 central iris UV correction;
other catalog eye proportions await individual rendered review.

Hair alone uses transparent forward alpha coverage: SrcAlpha/OneMinusSrcAlpha,
ZWriteOff, low0.02 forward cutoff. Its shadow/depth cutoff stays0.4, and the
opaque skin/eye/cloth defaults remain One/Zero with ZWriteOn. This preserves
fine brow/lash coverage discarded by hard clipping. LakeGirl wool normal
strength is0.30; other cloth is0.65. These later shader/import changes require
root-owned matched GPU review; the initial compilation pass is not a visual pass.

Rebuild the shared maps (existing MPFB isolated profile required):

```bash
BLENDER_USER_RESOURCES=/home/mevlec/Data/parasite-work/characters/blender_user \
nice -n 15 /home/mevlec/Data/Blender/current/blender -b -t 2 \
  --python-exit-code 1 --python Tools/blender/characters/bake_skin_surface.py \
  -- Assets/Parasite/Art/Characters --size 2048 --update-metadata
```

The character build/install scripts also preserve this shared surface mapping.
Normal and mask data must be read as Non-Color; the original albedo stays sRGB.

For offline scenes, apply the same maps and full cloth/eye/hair response:

```python
sys.path.insert(0, str(PROJECT / 'Tools/blender/characters'))
from cinematic_surfaces import upgrade_character
upgrade_character(hero_mesh, 'LakeGirl', PROJECT)
```

The helper copies/rebinds existing material slots. It changes no mesh, rig,
weights, modifier, animation or polygon-slot index. Skin random-walk scattering
uses 1.25 mm scale and thickness-dependent weight; Cycles handles geometric AO
instead of multiplying baked AO into albedo. Eyes retain textured iris/sclera
under a water-IOR tear coat. Hair keeps alpha with UV anisotropy and varying
roughness. Cloth binds its original normal/mask/tints; LakeGirl's wool bump is
subdued. Hair silhouette/strand authoring is a separate cinematic scene task.

Executed 27 November 2026: CPU bake124.54 s, source PNG reload/range checks,
actual LakeGirl material-API binding with mesh/weights/modifiers/slot indices
unchanged, all25metadata invariants, Python syntax, shell syntax and diff checks.
Evidence: `Logs/skin-realism-20261127/`, plus tracked bake provenance beside the
maps. The initial CPU helper probe caught the incorrect Blender anisotropy
socket name; it was fixed to the installed4.5 API and the retry passed.
Unity shader compilation/import, rendered skin/eye/hair quality, possession
readability, LOD appearance, frame time and GPU memory require root integration.
