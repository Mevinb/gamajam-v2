# Northline CPU authoring and review

Run from the project root after coordinating with the job owner. These scripts do not launch Unity or a player. Do not overlap a frozen native test, Editor or another Blender job. The actual verified runs used a guarded 600-second owned-child watchdog and preserved the exact existing user preview; never stop or signal someone else's process.

`author_rail.py` is the unchanged frozen recipe, SHA256 `5cdf907b8acc3f51c8fba972efe52f97fec4d7a1f34ac331783240ac1840f6dc`. It reads the existing packed gate material/atlas and is dry by default. It writes only to a fresh directory under Logs and refuses an existing output path.

```sh
~/Data/Blender/current/blender --background --threads 2 --python-exit-code 1 \
  --python Tools/blender/northline/author_rail.py -- \
  --project "$PWD" --out "$PWD/Logs/rail-author-runs/dry-unused"
```

For real CPU authoring, choose fresh output paths and isolated config/cache directories. This example uses an owned-process timeout and disables CUDA visibility. The recipe itself selects Cycles CPU and CPU denoising; empty CUDA visibility alone does not prove the rendering device.

```sh
task_tag="$(date -u +%Y%m%dT%H%M%SZ)-$$"
task_env="$PWD/Logs/northline-env-$task_tag"
author_out="$PWD/Logs/rail-author-runs/reproduce-$task_tag"
review_out="$PWD/Logs/rail-review-runs/reproduce-$task_tag"
mkdir -p "$task_env/config" "$task_env/cache" "$task_env/data" "$task_env/tmp"
env -u DISPLAY -u WAYLAND_DISPLAY CUDA_VISIBLE_DEVICES= OMP_NUM_THREADS=2 OPENBLAS_NUM_THREADS=2 \
  XDG_CONFIG_HOME="$task_env/config" XDG_CACHE_HOME="$task_env/cache" \
  XDG_DATA_HOME="$task_env/data" TMPDIR="$task_env/tmp" BLENDER_USER_CONFIG="$task_env/config/blender" \
  timeout --signal=TERM --kill-after=15s 600s \
  "$HOME/Data/Blender/current/blender" --background --threads 2 --python-exit-code 1 \
  --python Tools/blender/northline/author_rail.py -- \
  --project "$PWD" --out "$author_out" --go
```

The optional `--cpu-previews` reproduces the original author cameras, including their recorded lighting/framing limitations. Use the separate review tool for the brighter coach, detail, whole-kit and tram views.

```sh
env -u DISPLAY -u WAYLAND_DISPLAY CUDA_VISIBLE_DEVICES= OMP_NUM_THREADS=2 OPENBLAS_NUM_THREADS=2 \
  XDG_CONFIG_HOME="$task_env/config" XDG_CACHE_HOME="$task_env/cache" \
  XDG_DATA_HOME="$task_env/data" TMPDIR="$task_env/tmp" BLENDER_USER_CONFIG="$task_env/config/blender" \
  timeout --signal=TERM --kill-after=15s 600s \
  "$HOME/Data/Blender/current/blender" --background --threads 2 --python-exit-code 1 \
  --python Tools/blender/northline/review_and_verify.py -- \
  --project "$PWD" --source "$author_out" --out "$review_out"
```

To inspect the promoted source instead, pass `--source "$PWD/ArtSource/NorthlineRail"` with a fresh review output. The source must be that exact package or a directory under `Logs/rail-author-runs`; output must be fresh and under Logs. The promoted package intentionally omits original author PNGs. The portable tool records those omitted provenance-only image references while continuing to require every source mesh/blend/manifest hash. It does not skip geometry or UV/normal/bounds checks.

The independently executed ancestor, `Logs/rail-review-stage/review_and_verify.py`, passed all eleven FBX checks and four CPU reviews. The tracked copy changes only the accepted promoted-input path and explicit reporting of omitted original author PNGs. Those portability branches are syntax-checked, not separately run through Blender during the active Editor diagnostic. Geometry checks and review code are otherwise unchanged. Reproduction proves geometry/UV/material contracts; Blender file metadata, relative external image paths, output paths and timing are not promised to be byte-identical.

The established gate/booth handedness boundary, exact bounds, material provenance and remaining Unity/native scope are documented in [the source package](../../../ArtSource/NorthlineRail/README.md). No map regeneration, runtime collider import or source-asset material mutation is performed.


## Coach-only manufactured detail extension

The separate `coach_upgrade.py` is dry by default and pins the original packed source/manifest. Its companion `coach_geometry.py` defines real framed apertures, flanged wheel/bearing/brake/spring details and eight reachable bench fittings; `coach_surfaces.py` defines original 2K rail paint/steel/rubber/vinyl maps. It exports only the upgraded coach and copies the other ten FBXs byte for byte. `verify_coach_upgrade.py` is a separate strict no-render CPU verifier for this schema; the original source author/verifier and historical results are retained.

This extension requires reviewed explicit two-slot opaque/glass runtime import and binding contracts, plus eight intentional runtime seat colliders and actual movement/LOS/departure/lifecycle acceptance. It does not modify those live systems or imply graphics/performance acceptance. Use Root-reviewed fresh Logs paths, isolated user config/cache, two threads and an owned-child watchdog. No Blender authoring may start beside an active native guard; rendered playtests retain GPU ownership. There is no render option in either extension script.

```sh
# First: stdlib-only dry plan, no Blender or outputs.
python3 Tools/blender/northline/coach_upgrade.py --project "$PWD" --out "$PWD/Logs/coach-unused-dry"
# Later, only after authoring approval/serialization, with isolated environment:
# blender --background --threads 2 --python-exit-code 1 --python Tools/blender/northline/coach_upgrade.py -- --project "$PWD" --out "$fresh_author_out" --go
# blender --background --threads 2 --python-exit-code 1 --python Tools/blender/northline/verify_coach_upgrade.py -- --project "$PWD" --source "$fresh_author_out" --out "$fresh_verify_out"
```
