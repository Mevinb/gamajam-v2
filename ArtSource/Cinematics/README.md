# Blender film sets

Original meter-scale meshes and procedural 1024px PBR textures, authored in Blender 4.5 LTS for PARASITE.
The `.blend` files are editable source; Unity consumes FBX exports from `Assets/Parasite/Resources/Cinematics`.

Rebuild from the project root:

```bash
Tools/blender/cinematics/build_sets.sh
```

This regenerates four sets with a fixed random seed:

- `ContainmentLaboratory`: bolted pressure vessel, machined rings, tiled floor, drawers, displays and service pipes.
- `ServiceConduit`: ribbed metal escape duct with recessed guide lights.
- `LakesideShore`: irregular shore, rounded rocks, reeds, plank jetty with mooring posts and rope, eroded headlands.
- `LakesideBench`: weathered slats, rounded iron frame, armrests and bolt heads. Seat contact is 0.52m above origin.

Timber, brushed steel, pebbly shore and stone include albedo, tangent normal, and packed metallic/smoothness textures.
Runtime `CinematicArt.Instantiate` assigns URP materials by the exported surface names and shares them within each instance.
Film sets have no colliders or gameplay characters. Callers own and dispose the returned materials.

No downloaded artwork is used in these sets. Existing historical actors use the project's separately licensed character/animation pipeline.


## RTX-rendered establishing films

`OpeningFilm.blend` and `EndingFilm.blend` preserve camera animation, lights, shaders and packed textures.
The opening features the living specimen behind physically traced borosilicate glass; the ending features the
seated lake character, timber jetty, a low sun and its reflected light. Cycles uses the RTX4050 OptiX device
with CPU fallback disabled, 32 adaptive samples and denoising, 1280×720, 24fps, 72frames (3 seconds each).

```bash
Tools/blender/cinematics/build_films.sh
```

The script renders PNG sequences into ignored `Logs/cinematic-films`, then encodes VP8 WebM films in
`Resources/Cinematics/Films`. These short establishing inserts run beneath story captions and yield to the
Blender-authored interactive sets. The 80-second opening remains skippable, and the lake's Peace/Dominion
choice remains explicit. Video decoding uses unscaled time and is tested separately on a GPU player.
Headless logic acceptance skips video preparation because it has no graphics device.

For a direct Linux-player decoder check after building, use a real graphics device:

```bash
Builds/Linux/Parasite.x86_64 -batchmode -cinematicTestOut "$PWD/Logs/standalone-films.txt"
```

The explicit test argument is required; normal boot does not create the test harness. The player checks both
clips at `timeScale = 0`, reads back actual decoded pixels, writes PNG evidence beside the report, and exits
with status 0 only when both clips decode and advance. The EditMode asset checks verify mesh scale/orientation
and PBR bindings. GPU PlayMode tests capture the real opening and lakeside arrival beneath the live captions.

## Unity export orientation and publication

The authoring functions express Unity-style coordinates while building a native Blender film scene. Actual
Unity bounds checks showed that the FBX importer preserves X/Y but reverses the authored Z direction.
The generator now reflects Blender world Y and corrects face winding only at the FBX export boundary.
Editable Blender scenes and completed packed movie scenes keep their original camera coordinates.
The conduit runs along +Z, the jetty stays at +X and extends over +Z water, and the shore extends toward -Z.

Mesh/texture generation and CPU roundtrip validation happen in an ignored staging directory. Each complete
validated FBX/PNG is published by atomic replacement, with no partial files written into Assets. Saved set
`.blend` files use repository-relative image paths. CPU validation rejects collapsed faces and wrong asymmetric
anchors; Unity EditMode checks independently verify the imported positions and PBR materials.
