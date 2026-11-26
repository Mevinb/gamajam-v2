# Lake realism authoring

`author.py` opens each original packed full lake film, preserves its armature
animation, rebuilds materials/hair/scenery/cameras and saves a new packed source
under `ArtSource/RealisticFinales/Lake`. It has build and audit modes, with an
optional `--film arrival|hold|peace|dominion`. There is no render mode.

`scenery.py` authors terrain, metric photographed timber, water, linked natural
planting, grass/reeds/stones and photographic lighting. `portrait.py` adds
Head-pinned native Huang hair Curves with coherent part/side guides and temple
wisps, measured boot-ground support, eye UV proportion/focus, cameras and the explicit
Dominion iris reveal. The shared `characters/cinematic_surfaces.py` supplies the
real skin/eye/cloth/hair surface response. `framing_audit.py` reopens all new
files and checks source hashes, selected camera framing and measured focus.
`eye_uv_audit.py` starts from the immutable uncorrected source and compares
visible sclera/iris coverage under the same camera without rendering.

See `ArtSource/RealisticFinales/Lake/DELIVERY.md` for durations, quality profile,
CPU verification limits and the prioritized GPU QA frames. Scene files are
source deliverables; existing game movies and runtime wiring are untouched.
