# Main-gate visual pilot — authoring candidate

This is an **unintegrated authoring candidate**, not a shipped graphics upgrade. It contains the editable, packed `CityGatePilot.blend`, the visual-only `Exports/CityGatePilot.fbx`, one shared 2K PBR atlas set, and two low-resolution CPU authoring previews. No Assets, C# runtime, generated scene, collider, LOS rule or NavMesh was changed by this package.

The original geometry and texture procedures are in `Tools/blender/city_gate_pilot/author_gate.py`. The set adds precast seams/tie marks/water streaks, manufactured post caps and face plates, embedded conduit/clips/fasteners, a folded coping/drip lip, and a solid sliding leaf with stiffeners and a non-emissive badge-reader plate. The existing sign, lights, cameras, booth and road markings are outside this export and remain existing game elements.

## Actual outputs and checks

- Two visual meshes, one shared material, **3,808 triangles**. No exported animation, armature, rigid body or collision object.
- Three original 2048×2048 maps: opaque sRGB albedo (`A`), linear tangent-space normal (`N`), and linear metallic RGB / smoothness alpha (`M`). Each atlas has four padded material tiles with 992 useful texels per tile; this is not 2K per material.
- CPU FBX reimport verifies exact static/moving bounds, finite and non-degenerate UVs/triangles, closed consistently oriented edges, positive volume for every disconnected solid and one material.
- 323 samples each prove the static/open aperture is clear and the closed leaf is opaque. Twenty-one samples verify the original 3.05 m smoothstep translation with the documented child-scale compensation.
- Exact source/geometry/map hashes are in `production-manifest.json`, `physical-contract.json`, `piece-map.json` and `export-verification.json`. Raw output occupies about 18 MB, below the 100 MB authoring cap.

`Review/closed-authoring-cpu.png` and `open-authoring-cpu.png` are **CPU AUTHORING QA ONLY**: 640×360, eight Cycles samples, two CPU threads and a neutral QA camera/ground/lights. They do not show the game, its lighting or HUD, and establish neither a native visual improvement nor performance. The original game sign/light/CCTV meshes are not rendered in this isolated QA set. Actual Unity import, matching before/after gameplay cameras, physical door/access/cover/LOS checks and a current-player GPU benchmark remain required before integration can be called verified.

## Transform and physics contract

The game gate root is `(0, .15, 57.5)` in Unity metres, with local `-Z` facing outside. Its opening is x `[-1.2, 1.2]` and reaches world y `3.15`. Existing post boxes are x `±[1.2,1.7]`, world y `[0,4.5]`, z `[57.1,57.9]`. `physical-contract.json` distinguishes cropped visual pilot volumes from the **complete** original wall collider boxes; a pilot crop must not replace an entire existing collider.

`GateStaticShell` uses the gate root's local metre coordinates. Its bounds are `(-8,-.15,-.4)` to `(8,4.35,.4)`.

`GateMovingLeaf` is authored around the existing panel centre, bounds `(-1.45,-1.5,-.13)` to `(1.45,1.5,.1)`. The original panel remains at local position `(0,1.5,0)` and scale `(2.9,3,.2)`, with its original BoxCollider and carving obstacle. Do not replace or rescale that transform. For a new metre-authored visual child under it use:

```
localPosition = (0, 0, 0)
localRotation = identity
localScale = (1/2.9, 1/3, 1/.2)
```

The original `AccessDoor` continues to move that same parent by `(+3.05,0,0)` over 1.1 seconds. The logical interact point remains `(0,1.2,-.45)`. The reader plate is visual dressing; no new interaction, status light or permission logic is supplied.

When integrating through the builders, hide only the old panel renderer and replaced dark-metal decorative ribs. Retain the existing warning-light decoration and its transform. The existing static wall surfaces are batched: **do not disable an entire WorldKit material-cell renderer** to hide this area. Separate the pilot's visual authoring from the exact original `Solid` boxes, drawing old wall/coping visuals outside the crop while keeping the original collider inventory, bounds and generation order. Regenerate through `CityBuilder.BuildAllBatch`; do not hand-edit scenes.

The FBX uses the project's established temporary Blender-world-Y reflection plus winding reversal. CPU reimport measures the expected mapping `(Blender X, Blender Z, Blender Y)` into Unity coordinates. This does not replace actual Unity importer verification. Imported object names are recorded exactly in the verification file; select the static/moving parts by their semantic prefix, not an incidental export-copy suffix.

## Required Unity material/import setup (not applied)

Keep FBX-generated colliders off and use one explicit shared opaque URP material. Assign `A` to Base Map (sRGB), `N` as a Texture2D normal map, and `M` to the metallic/smoothness map. Set metallic and smoothness multipliers to 1 so the packed maps retain their authored values. Use scoped 2K imports for this new prefix; do not raise every city texture or modify UI/voice/movie importers. Verify normal-map orientation, compression, exact sizes and actual shader appearance in Unity. A three-map BC7 full-mip set would be about 16 MiB, but actual imported/runtime residency has not been measured.

## Reproduce without overwriting editable work

Default dry run (no output authored):

```bash
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/city_gate_pilot/author_gate.py
```

Author into a **fresh** ignored directory and run the actual CPU round-trip checks:

```bash
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/city_gate_pilot/author_gate.py -- \
  --go --out Logs/city-gate-pilot/reproduction --cpu-previews
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/city_gate_pilot/verify_gate.py -- Logs/city-gate-pilot/reproduction
```

The author refuses to overwrite an existing editable blend. The verifier performs no render or publication. CPU preview settings are never evidence of RTX rendering. See `CREDITS.md` for ownership.
