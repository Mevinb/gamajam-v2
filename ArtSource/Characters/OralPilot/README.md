# Isolated LakeGirl oral anatomy pilot

This folder is outside `Assets`. Nothing here has been promoted to the game
catalog, materials, prefabs or generated scenes. The default character build
pipeline is unchanged. Authoring requires an explicit option:

```bash
BLENDER_USER_RESOURCES=/home/mevlec/Data/parasite-work/characters/blender_user \
nice -n 15 /home/mevlec/Data/Blender/current/blender -b -t 2 \
 --python-exit-code 1 --python Tools/blender/characters/oral_pilot.py -- --author
```

The existing saved LakeGirl rig, lip silhouette and every original LOD vertex,
face and bone weight remain unchanged. The same fitted MPFB rig reconstructs
the398 deleted oral-cavity faces, teeth and tongue; fitted rest matrices match
the original rig exactly (maximum delta0). Teeth retain their Head/Jaw split;
tongue uses a lower-mouth Head0.15/Jaw0.85 blend. The source dental/tongue assets
and photos are installed MakeHuman CC0 assets; their paths/hashes and credits
are recorded in `audit.json`.

Added oral triangles:2156 atLOD0,709 atLOD1 and100 atLOD2. All three LODs retain
four material slots. Teeth/tongue occupy untinted, nonmetallic portions of the
Cloth PBR atlas, with flat normal data and enamel/mucosa smoothness. Existing
cloth colour/normal/mask texels are copied exactly (measured error0) into the
left half of a4096x2048 review atlas; old cloth UVs undergo only the matching
affine horizontal scale. This larger atlas is a prototype, not an accepted
shipping resolution/memory decision.

`LakeGirl_OralPilot.blend` is compressed and packed, with neutral studio light,
1600x1000 frontal and three-quarter100mm cameras. Frames1/25/49 demonstrate
local Jaw X0/15/27degrees. These are review-only examples; FBX animation is
disabled, leaving runtime animations/avatar authoring separate. No render has
been produced by this tool. Before promotion, review inner-lip attachment,
dental occlusion, tongue movement, close-view surface appearance, original
animation/LOD transitions, and the actual Unity jaw-mapping control.

The root-only RTX still recipe is dry-validated:

```bash
nice -n 15 /home/mevlec/Data/Blender/current/blender \
 -b ArtSource/Characters/OralPilot/LakeGirl_OralPilot.blend -t 2 \
 --python-exit-code 1 --python Tools/blender/characters/render_oral_pilot.py \
 -- --execute --camera front --frames 1 25 49 --samples 128 \
 --output Logs/full-cinematic-renders/oral-pilot-qa-v1/front
```

Use `--camera threequarter` and a fresh output directory for the alternate
camera. Omitting `--execute` only checks/prints the plan. The source is24fps,
fps_base1 and all external images are packed. The recipe verifies the frozen
source hash, requires a real RTX OptiX device, refuses output overwrites and
records per-frame timing/image hashes. It never saves source or modifies Assets.
