# City gate pilot tooling

`author_gate.py` creates original visual-only static/moving meshes and shared 2K PBR maps. It writes only a new `ArtSource/CityGatePilot` tree or a fresh `Logs/` output, refuses to overwrite an existing blend, uses two CPU threads and requires `--go`. `--cpu-previews` renders two explicitly limited CPU QA views; no RTX/GPU render is requested.

`verify_gate.py` performs actual FBX reimport and byte/geometry/UV/winding/door-motion checks without rendering. See [the asset README](../../../ArtSource/CityGatePilot/README.md) for exact commands, physics/import invariants and limitations. No Assets, runtime scripts, builders or scenes are changed by either tool.
