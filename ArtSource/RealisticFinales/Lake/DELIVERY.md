# Current lake sources — revision 5

The current delivery uses `Lake_Arrival_v5.blend`, `Lake_Hold_v5.blend`,
`Lake_Peace_v5.blend` and `Lake_Dominion_v5.blend`. Original revision 4 sources
and their stopped 131-frame production job remain preserved.

Revision 5 adds a standard inverted vertex-group Mask over 104 covered torso
vertices, hiding 128 Skin faces beneath the opaque sweater. The raw mesh,
cloth, face, eyes, hair, materials, original weights and rig actions remain
unchanged. Targeted CPU rays resolve 119 original skin-first pixels at Arrival
127/165; fresh saved-source checks pass for all four films. Root reviewed
192-sample 1080p OptiX frames Arrival127, Hold73, Peace168 and Dominion52.
Full finished-film motion/encoding review remains pending.

`garment-occlusion-v5.json` records the exact recipe, source hashes and review
receipts. The derived copies preserve the base V4 scenery/groom metadata;
the Mask and versioned filenames identify this later garment revision. The
updated replay author labels a full new build version5 and writes
`audit-v5.json`, preserving the old audit.

Freeze a new job using the explicit V5 source map from the current handoff.
The unchanged producer's default map is historicalV4. Never reuse old or QA
frames under the new source hashes. No new game movies have been published
by this source update.

The revision 4 authoring and review details below are retained as history.

# Realistic lake finale source — revision4 candidate

Four standalone compressed, packed Blender4.5 sources preserve the existing
story boundaries and original LakeGirl armature animation:

| Film | Duration | Frames | Authored review points |
| --- | --- | --- | --- |
| Arrival | 15s | 1–360 | 1,100 wide;165 portrait;280,360 choice approach |
| Hold | 6s looping | 1–144 | 1,73,144 |
| Peace | 7s | 1–168 | 62 face;103 hands;168 pullback |
| Dominion | 7s | 1–168 | 52 iris;94 deliberate curl;155 altered gaze |

Arrival and Hold have ordinary eyes and no automatic outcome. Peace and
Dominion are intended only after explicit choices. Dominion retains the
existing delayed iris-only reveal, under a wet corneal reflection; the previous
42 glowing hearts and populated bank are removed. These files change no
runtime mission/save/trigger logic or existing game video.

Dominion's eye material has a later iris-revision2 correction: its former
luminance mask colored pupils and shaded sclera in the actual frame52 review.
The replacement uses measured UV annuli, preserving dark pupils and ordinary
sclera, photographic iris detail and a restrained jade signal. The mask is0
inside42px radius and beyond116px, with smooth42–46/112–116px fades. Only
Dominion's packed source changed; the other three hashes remain exact. The old
V4 source/receipts are preserved under
`Logs/realistic-lake-author/dominion-v4-iris-before`. Corrected frame52 passed
the root agent's RTX pixel review at
`Logs/full-cinematic-renders/realistic-lake-qa-v5/dominion-iris/frame-000052.png`.

The rebuild uses shared Blender-baked age-appropriate skin micro normals and
surface response, physical1.25mm random-walk skin scattering, a tear-film eye
coat, the original cloth normal/mask/tints and subdued wool relief. The original
11769-vertex/19631-face mesh and armature are retained, with render subdivision2.
31211 native Cycles hair Curves follow an explicit Head-bone pin, including
7200 part-to-side guide fibres,800 face-framing fibres and600 flyaways.
The Huang BSDF uses true strand tangent/radius/random response; these are
hair primitives rather than beveled Curve tubes. Revision4 conforms to the
same evaluated subdivision2 surface used by the rendered cap, terminates
ordinary guides at the surface boundary, staggers/tapers their ends, lowers
ordinary fibre lift to0.15–0.7mm and matches near-black colour to the cap.
Human diameters range60–130µm with tapered ends. No armature action is
retargeted or replaced. Central eye
UV fields expand1.42× to expose the original sclera while retaining its outer
coordinates and the photographed iris. Actor and bench lower together37.452mm
to meet the actual ground at6.111mm minimum boot clearance.

The shore now has rolling land, a gravel path, a sloping visible shallow bottom,
wet edge variation, low individual grass blades, broken human-scale reed clumps,
shore stones, irregular linked tree/understory clusters and a distant wooded
bank. The lake is dielectric IOR1.333 with transmission, weak absorption and
millimetre geometry/normals at several scales. Bench/pier timber uses existing
photographic wood textures projected in metre UV units with1.5mm bevels.
Existing tree/shrub/wood/gravel/sky assets retain their project credits.
Actual scene counts are4741 linked grass placements sharing8 meshes,
5462 centimetre-scale leaf-litter leaves,1010 far trees sharing2 tree meshes,
210 far fringe plants,88 near trees and271 small shore shrubs sharing1 mesh.

Lighting uses the existing CC0 Belfast sunset4K HDRI, restrained physical low
sun and a broad neutral38W portrait bounce. The oversized4.2m visible sun and
old colour-changing sunlight are removed. AgX exposure0, photographic28mm
arrival and60–65mm portraits, measured eye focus, f/3.5 portraits/f/4 hands and
f/5.6 shore views replace the old framing. The Peace ending pulls away from the
ordinary human shore.

Saved quality:192samples, adaptive threshold.008, denoising,10bounces,
8transmission bounces,12transparent bounces and0.5shutter motion blur,1080p24fps.
The four files total244,963,575bytes. All used images are packed. The root agent
rendered/reviewed earlier RTX revisions: revision1 exposed eye, hair, bare bank,
grass and floating-foot defects; revision2 improved the sclera and its wide
shore/forest/scale were accepted for this pass. Revision3 established visible
native strand response but exposed overly brown raised guide bands. Revision4
changes only that groom. The root agent's actual RTX Arrival165 review accepted
revision4 for this pass: the raised ledge/brown layer are fixed, and ordinary
eyes/skin/cloth/bench are coherent. Hold73, Peace168 and the corrected Dominion52
also passed the declared keyframe review. The pinned production job is
`Logs/full-cinematic-renders/realistic-finales-production-20261127`; it includes
these four films and the revised train departure. Full motion, codec and native
runtime verification remain pending. The authoring commands below do not render
production frames.

`audit.json` verifies saved packing/settings, unchanged rig animation and
branch isolation, native hair data and measured ground contact. Hold's rig
endpoint matrix delta is2.56e-18; water, grass
and eye-focus endpoint delta is0. `framing-audit.json` verifies measured eye
focus and on-screen eye placement for all listed non-hand views, and confirms
that all four original packed source hashes remain unchanged. These are CPU
data checks, not a rendered-quality pass.
Revision1/2 receipts are retained under `Logs/realistic-lake-author/v1-receipts`
and `v2-receipts`/`v3-receipts`. `eye_uv_audit.py` reconstructs the review camera from the
immutable original source and measures the same800 visible eye rays: sclera
classification27% before versus52.75% after the central UV correction, with
identical visible geometry. This CPU atlas/ray metric does not replace pixels.

Rebuild/audit with Blender only; the commands never render:

```bash
nice -n 15 /home/mevlec/Data/Blender/current/blender -b -t 2 \
  --python-exit-code 1 --python Tools/blender/cinematics/realistic_lake/author.py -- build
nice -n 15 /home/mevlec/Data/Blender/current/blender -b -t 2 \
  --python-exit-code 1 --python Tools/blender/cinematics/realistic_lake/framing_audit.py
```

Original read-only sources are `ArtSource/FullCinematics/Lake/Lake_*.blend`.
Per-film authoring receipts contain the original/new SHA256, shot lists,
geometry counts and QA frames. Authoring log: `Logs/realistic-lake-author/`.
