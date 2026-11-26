# LakeGirl oral source and portable runtime recipe

## Portable runtime revision4

The immutable V2 studio master and3 source PNGs below are the canonical inputs
for `Tools/blender/characters/rebuild_lakegirl_oral_runtime.py`, governed by
the adjacent `runtime-integration.json`. No new master is required. The portable
tool reads no Logs baseline, MPFB install, home-directory master or current
runtime asset. Run it with reviewed Blender4.5.16 and a fresh entire output
directory outside Assets/ArtSource:

```bash
nice -n 15 /home/mevlec/Data/Blender/current/blender -b -t 2 \
 --python-exit-code 1 \
 --python Tools/blender/characters/rebuild_lakegirl_oral_runtime.py -- \
 --output /home/mevlec/.local/share/parasite-work/northline/Logs/oral-runtime-portable-replay-v2
```

That exact CPU replay completed0. Use a new output path for subsequent runs;
the tool refuses any existing output directory. Its source SHA256 is
`e6b9b026905e60b74f8f7c6ab1dca4069f84afb3d1c567b14988aee7d577d544`;
the contract SHA256 is
`d0ed5d1cde2fdb498a617f7959a8e4802c03c0b094753f9074ffca9bee99edf4`.

The rebuilt A/N2048x4096 and M1024x2048 PNGs and LakeGirl metadata match live
runtime bytes exactly. Independent Blender imports also pass exact equivalence
against runtime FBX`8e2e0220990b55751e1ba6954938dc75fa057f2cfac957114bf92d7264f3ed03`:
56 bone names/parents/local+world rest matrices;60 raw model unit/transforms;
all geometry, normals, topology, weights, UV layers and4 material slots.
Triangles remain28234/8073/2597. All168 raw bind clusters and model hierarchy
also match exactly. Full exported tangent/binormal buffers have maximum
component delta1.1921e-7, within the explicitly accepted1e-6 tolerance; LOD1/2
are exact. Normal directions are finite/unit with maximum length error1.364e-7.
The initial strict-float attempts remain in the replay verification directory.
The source rest contract hash is explicitly asserted. Both final CPU Blender
jobs exited0 without rendering,
importing into Unity or copying anything back into runtime Assets.

Evidence is retained at
`Logs/oral-runtime-portable-replay-v2/verification/pot-authoring.json` and
`Logs/oral-runtime-portable-replay-v2/verification/semantic-runtime-comparison.json`.
The replay FBX is`eda3a0c9d381898dea867d0dd3b375b669f5c81c5e69621e6db72677732f4361`;
export header/time bytes can differ while all compared semantic fields match.
Its textures are padded without resampling, with V×33/64 from the studio atlas;
original cloth V becomes1/2. Runtime policy requires BC7/BC5/BC7,13/13/1 mips,
Clamp and max sizes4096/4096/2048. Actual Unity/GPU acceptance remains a separate
Root-owned gate. This recipe covers LakeGirl only.

The following historical V2 review notes describe the preserved studio source,
including its NPOT atlases and former studio FBX export. Runtime exports use
the portable recipe's `FBX_SCALE_UNITS` policy; the old studio FBX remains
unchanged as historical evidence.

## Historical compact oral pilot V2

V1 is unchanged and unpromoted. This folder stays outside `Assets`; the default
catalog builder, current game materials and accepted lake films are untouched.

The V1 RTX images exposed an output transform error: the scene used AgX, but
image output overrode it with Standard. V2 explicitly uses FOLLOW_SCENE AgX,
medium-high contrast, exposure0 and restrained neutral5W key/2W fill lighting.
No V2 rendered-quality claim is made until the root agent reviews actual pixels.

The original rig/rest is identical (fitted delta0). Original outside vertices,
faces, bone weights and rest lip silhouette remain exact, with four material
slots. Upper dental geometry alone shifts1.5mm down/0.3mm forward within the
mouth; lower MPFB weights remain consistent with the unchanged lower face.
Dental normals are smooth; visible anterior edges receive0.12mm bevels. New
dental n-gons are triangulated before export, preserving tangent export for the
whole mesh. The higher-detail review adds8237/1078/100 oral triangles atLOD0/1/2;
the dense LOD0 budget still needs a shipping decision after visual review.

A/N atlases are2048x2112; M is1024x1056. Original cloth V scales by exactly32/33.
Old decoded colour/normal/mask texels copy with measured error0. Strip guards
are8px for A/N and4px for M. Oral regions use tintID0, metal0 and calibrated
enamel/mucosa smoothness; they bypass wool sheen and its minimum roughness.
Intended later Unity import is NPOT=None, A/N max4096, M max2048, BC7 albedo,
BC5 normal and BC7 point-sampled/no-mip mask. No import has been performed.
Standard regenerated NPOT low mips are not bit-identical to the old square
chain; preserve the guards and inspect distant LODs before catalog rollout.

The copied/extended photos are installed MakeHuman CC0 dental/tongue assets.
Source paths/hashes, geometry and texel checks are in `audit.json`. CPU arch
bounds and central tooth-to-lip visibility checks at local Jaw0/15/27degrees
are in `Logs/skin-realism-20261127/oral-v2-occlusion.json`. Upper incisors now
have some visible central rays; most dental geometry remains occluded by lips.
Those metrics do not replace actual neutral/open-mouth and side-view images.

Root-only RTX review (dry-verified, no render from the authoring agent):

```bash
nice -n 15 /home/mevlec/Data/Blender/current/blender \
 -b ArtSource/Characters/OralPilotV2/LakeGirl_OralPilotV2.blend -t 2 \
 --python-exit-code 1 --python Tools/blender/characters/render_oral_pilot_v2.py \
 -- --execute --camera front --frames 1 25 49 --samples 128 \
 --output Logs/full-cinematic-renders/oral-pilot-qa-v2/front
```

Use `threequarter` with a fresh output folder for the alternate camera. All
file images are packed; source24fps,1600x1000. The recipe verifies its source
hash and actual RTX OptiX device, refuses overwrites, records receipts and
never saves the source. Re-author only with the explicit standalone command
`oral_pilot_compact.py -- --author`; V1 authoring/outputs remain separate.
