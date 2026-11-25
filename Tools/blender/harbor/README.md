# Harbor CPU authoring

Run from the project root. The author script is dry by default. It reads the original packed gate material/atlas and writes only its owned output directory or a fresh path under `Logs/`. Existing source blend or production manifest files are protected from overwrite.

```sh
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/harbor/author_harbor.py
```

Author into a fresh output, including the four CPU review views:

```sh
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/harbor/author_harbor.py -- --go --cpu-previews \
  --out Logs/harbor-fresh-source
```

Verify without rendering:

```sh
~/Data/Blender/current/blender -b --factory-startup -t 2 --python-exit-code 1 \
  --python Tools/blender/harbor/verify_harbor.py -- Logs/harbor-fresh-source
```

The default output/verification directory is `ArtSource/Harbor`. Both actual production commands used fixed two-thread CPU execution; the four views use 960×720, eight Cycles samples and CPU OpenImageDenoise. No Unity or GPU jobs are started by these scripts.

The established gate boundary maps Unity `(x,y,z)` to Blender `(x,-z,y)`. Only temporary export copies mirror Blender Y and reverse winding before the standard FBX `-Z` forward / Y-up baked-space export. Independent imported world coordinates decode to Unity `(x,z,y)`; bounds and every closed component's winding are checked. The importer may express an axis rotation in its object transform; the packed editable source has identity transforms and world-space export bounds account for that axis conversion.

After parent coordination, `Parasite.EditorTools.HarborArtImporter.PrepareBatch` imports only the nine owned Harbor FBXs and prepares an owned resource material using existing gate map references. It never changes gate textures/materials or other import policies. Actual Unity import and native gameplay checks remain separate from Blender verification.
