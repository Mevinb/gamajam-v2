# The Far Shore — physical island asset brief

This original Blender kit is imported under `Assets/Parasite/Resources/Island`. Placements and clearances follow the included `approved-layout-contract.json`, in local Unity metres with world origin `(620,0,-220)`. The art does not by itself implement missions, interactions or a complete playable chapter.

The visual direction continues the existing weathered teal marine metal, pale relief-clinic enamel, practical galvanized fittings and mineral concrete. Original 512px albedo, tangent-normal and packed metallic/smoothness maps share a coherent palette and metre-scale UV projection. There are no billboard trees, decorative terminal screens or freestanding card walls in this kit.

| Assembly | Physical purpose | Authored clearance / scope |
|---|---|---|
| IslandTidalCulvert | Actual low tunnel, flush aprons, buttresses, recessed service pipe and lateral dry bay | 13.8m outer length; 12m covered; 1.4m clear width; 1.2m clear height. Blob capsule fits; human height is physically excluded. Apron detail rises only18mm. |
| IslandTideIndicator | Mechanical float gauge with protected tube, graduations and warning lenses | Portal-header mount faces-Z above the tunnel's clear volume. Static art only; phase/readability/lighting wiring remains runtime work. |
| IslandRefugeClinic | Full floor/walls, open entrance, clerestory, real treatment beds/IV supports, sink and supplies |14×12m;2.6m doorway;3.2m main aisle; all three survivor destinations retain1.2m clear-radius volumes. Furniture stays in the rear/side bays. |
| IslandHandoffGate | Physical steel passage frame and flush lane inlays |3.5m total width;3.1m unobstructed lane; no console or invented hold mechanic. Reused at the three agreed lane anchors. |
| IslandServiceLatch | Mechanical wheel, spindle, witness mark and gate linkage | Front-Z grip; immediate authorized maintenance interaction remains gameplay-owned. |
| IslandFlareCache / IslandSignalFlare / IslandFlareMount | Waterproof case with carry grip, separately carried flare and compact receiving tripod | Cache fits approved4×4m reserve; mount remains outside the main jetty centerline. Flare is inert art; firing/VFX occurs only after gameplay's protected assembly gate. |

The existing verified6×8m `RiversideFerry` is reused at the approved arrival/rescue origins. Do not introduce a new hull, moving-platform NavMesh or negative runtime scale. Keep the interactive deck static; use proper yaw180 for the rescue vessel. Arrival gangway length is4.6m and rescue3.6m, both1.8m wide at7°; their physical footing belongs to the layout builder.

## Independent navigation review incorporated by the campaign agent

- Extending the visible quarantine wings to localx±53 closes the otherwise8m walkable bypass around the oldx±44 wall ends.
- A3m clear handoff lane cannot also have a3m total post footprint. The reserve is now3.5m; post bases never intrude into the clear lane.
- The original3.6m arrival gangway left~0.2m unsupported gaps. The agreed4.6m span creates overlap at both land and ferry ends after its7° tilt.
- The culvert is deliberately omitted from human-height path assumptions; do not move a human NavMesh proxy through it to claim a blob route passed.
- Original city/riverside security restoration, checkpoint selection and survivor ownership still require the campaign plan's scoped integration. These assets do not solve those state rules.

## Contents and verification

`Assets/Parasite/Resources/Island/` contains eight FBXs and eighteen PNG maps. `ArtSource/Island/IslandArchitecture.blend` is the packed editable lineup; `IslandMaterials.blend` is the material library. `asset_manifest.json` records bounds,27,124triangles, material slots, collision-box recipes, interaction anchors and navigation clearances. `placement-review.json` records approved prefab origins/yaws and ferry reuse. The sheltered bay retry point is culvert-local(1.2,0,6.15), used only after physical visitation; entry remains the initial retry, so no wash teleports past the gate. The collision recipe is data for the future runtime builder; the FBX exports contain no automatic colliders.

`Tools/blender/island/build_island.py` reuses only function definitions from the verified riverside exporter, records its SHA and keeps publication inside ignored Logs. Native Blender authoring uses `(X,-Z,Y)`; export-only Y reflection plus reversed winding matches the measured Unity import. Each surface is triangulated with zero-area faces removed, metre UVs retained and positive-scale local origins.

CPU Blender FBX round-trip verification passed8/8, preserving exact triangle/material counts and bounds within a few micrometres. Finite UV/vertex data and triangle area checks pass. The declared simple collision recipes also preserve the blob-only tunnel, clinic doorway/aisle/safe slots and handoff passage. **This is not an actual Unity capsule, NavMesh, camera or escort test.**

Review images are CPU-only, low-sample stills of the exported FBX, using the measured target-axis mapping; front lettering is readable. The clinic image is a roof cutaway for circulation review, not a gameplay capture. The ambient-only culvert still is intentionally dark without the player's bioluminescence or runtime fixtures; in-game tunnel/tide-warning readability needs a real camera/lighting check before publication.

## Before any live publication

1. Review architecture/anchors against the staged island code, keeping collider boxes and open passages physically consistent. Validate warning-gauge visibility; do not rely on a hidden indicator alone.
2. Import with the existing metre/Z/winding convention and original PBR channel settings, then check asymmetric fronts and readable lettering from actual Unity cameras.
3. Exercise actual blob passage/human denial, tide warning/retry, both entry routes, all survivor/courier destinations and ferry boarding on real physical footing. Validate capsule/camera clearance and complete human routes separately from the low culvert.
4. Only root publishes reviewed assets and sources, runs the serialized Unity/player suites, captures both futures and commits a verified milestone. No current release depends on this staging directory.

## Current integration evidence

CPU round-trip, winding and declared aperture checks pass for all eight assemblies. The live working-tree island entry
has passed four route/outcome traversals on both headless logic and GPU runs; its rendered floor/sea/UV/PBR fixture passes.
Those results do not certify the full island campaign: the low-camera correction still needs its focused GPU rerun, and
later moving survivor/courier missions remain staged. The user preview build contains this kit; the retained 0.4.2 release
archive does not. Build/source-specific details remain in `Docs/TEST_REPORT.md`.

Regenerate reviewed outputs into an ignored staging folder, never directly over live Assets:

```bash
~/Data/Blender/current/blender -b -t 2 --python-exit-code 1 --python Tools/blender/island/build_island.py -- "$PWD" "$PWD/Logs/island-rebuild"
~/Data/Blender/current/blender -b -t 2 --python-exit-code 1 --python Tools/blender/island/verify_island.py -- "$PWD" "$PWD/Logs/island-rebuild"
```

Only publish verified outputs after corresponding runtime/collider review. Rendering jobs must remain stopped while the
user's native playtest is active; these commands are documentation, not an instruction to start a job automatically.
