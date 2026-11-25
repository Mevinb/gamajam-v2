# CPU security-booth authoring

The scripts author and verify only `ArtSource/CitySecurityBooth/` or a fresh path under `Logs/`. They do not start Unity or use GPU rendering. The existing original gate blend and its three texture images must be present.

Dry run, with no output files created:

```sh
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/city_security_booth/author_booth.py
```

Create the package and its two CPU review images in a fresh directory:

```sh
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/city_security_booth/author_booth.py -- \
  --go --cpu-previews --out Logs/city-security-booth-fresh
```

The default output is `ArtSource/CitySecurityBooth`. Existing `.blend`, FBX or production manifest files cause an intentional error before authoring. The `--cpu-previews` option enables exactly two 960×720 Cycles CPU renders at eight samples, with two fixed threads and CPU OpenImageDenoise. The source setup also retains those limits when the option is omitted. No new atlases are generated.

Verify the packed source and independently roundtrip the FBX without rendering:

```sh
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/city_security_booth/verify_booth.py -- \
  Logs/city-security-booth-fresh
```

The default verifier path is `ArtSource/CitySecurityBooth`. It reads `production-manifest.json` and `physical-contract.json`, then writes `export-verification.json` only after all checks pass. A failed outward-solid check writes `failed-solid-diagnostic.json` before exiting nonzero; that diagnostic is not a pass. Every geometry piece is normalized to positive local signed volume during authoring, and the independent importer checks every closed connected component again.

Coordinates follow the established gate export boundary: Unity `(x,y,z)` is authored in Blender as `(x,-z,y)`. Only a temporary export copy mirrors Blender Y and reverses face winding before the standard `-Z` forward / Y-up FBX export. The independent verifier decodes the imported world coordinates back to Unity as `(x,z,y)` and compares actual bounds. The saved editable source is not mirrored for export.

The atlas quadrants are concrete (0), dark steel (1), yellow physical paint (2), and dark polymer / upper dark-glass region (3). Face UVs stay within a single padded quadrant. The exact original material and packed images are reused. The FBX contains one mesh and one material; no QA/reference meshes, physics or animation are exported. Unity material reassignment and actual culling remain integration checks.
