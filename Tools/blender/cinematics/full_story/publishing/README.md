# Full-story render, encoding and publication

This is the retained production pipeline for the five real Blender films. It copies the pipeline used for the November 25 production into tracked tools; the original ignored production scripts and frozen render inputs remain unchanged.

Run from the repository root. Python 3, Pillow, FFmpeg with `libvpx`, Blender 4.5.16 and the intended RTX 4050/OptiX device are required for a new production. Encoding uses CPU FFmpeg with explicit two-thread settings. All raw frames, checkpoints, contracts, encoded candidates and logs stay beneath ignored `Logs/` directories.

## Render or resume

The default command only reads the source audits, prints the plan and estimates disk space. It creates no render output and launches no Blender process:

```bash
python3 Tools/blender/cinematics/full_story/publishing/render_production.py
```

After coordinating exclusive GPU use with Unity and other render jobs, start a named production explicitly:

```bash
python3 Tools/blender/cinematics/full_story/publishing/render_production.py \
  --go --out Logs/full-cinematic-renders/my-full-production \
  --blender "$HOME/Data/Blender/current/blender"
```

The supervisor uses the audited packed sources, 1080p24, 48 samples, OptiX and GPU OIDN. Its ending-first order is arrival, neutral hold, Peace, Dominion, then the 80-second opening. It checks input hashes, complete-frame provenance, single-writer locks and a 4 GiB reserve; insufficient estimated disk space prevents launch. Do not modify the packed sources, render driver or policy while a production is running.

Create `Logs/full-cinematic-renders/my-full-production/STOP` to request a stop after the current complete frame. Wait for the actual Blender and supervisor processes to exit before using the GPU elsewhere. To resume, remove that STOP file and repeat the identical command with the same output directory. Existing frames and provenance are checked rather than replaced.

## Freeze and validate the actual production

The following commands use actual completed production profiles. A contract binds the exact source inventory, packed-source and renderer hashes, GPU and quality profile, color settings and authored camera cuts. It does not substitute pilot images for production frames.

```bash
python3 Tools/blender/cinematics/full_story/publishing/freeze_contract.py \
  --plan Logs/full-cinematic-renders/my-full-production/production-plan.json \
  --opening-profile Logs/full-cinematic-renders/my-full-production/OpeningFull/job-provenance.json \
  --lake-profile Logs/full-cinematic-renders/my-full-production/LakeArrival/job-provenance.json \
  --out Logs/full-film-publication/my-contract.json
python3 Tools/blender/cinematics/full_story/publishing/validate_frames.py \
  --production Logs/full-cinematic-renders/my-full-production \
  --contract Logs/full-film-publication/my-contract.json --film OpeningFull \
  --report Logs/full-film-publication/my-opening-frame-gate.json
```

The frame gate requires the supervisor and renderer checkpoint to be complete with exit code 0, exact unique frames 1 through N, matching source/device/quality evidence, correct camera cuts and poses, and every PNG's actual byte count, SHA256, format and CRC structure. Partial renders, CPU previews, gaps, extra frames and stale inputs fail.

## Encode and assemble the review bundle

Run this command once for each of `OpeningFull`, `LakeArrival`, `LakeHold`, `LakePeace` and `LakeDominion` after that film completes:

```bash
python3 Tools/blender/cinematics/full_story/publishing/encode.py \
  --production Logs/full-cinematic-renders/my-full-production \
  --contract Logs/full-film-publication/my-contract.json --film OpeningFull \
  --out Logs/full-film-publication/my-encoded-candidate
```

The encoder preserves exact frame counts and 1080p24 timing. It produces silent VP8/WebM with BT709 metadata, checks the actual frame count with ffprobe, decodes the entire stream with FFmpeg and rechecks source images after encoding. Each movie must be below 95,000,000 bytes. Existing candidate files are never overwritten. Captions, dialogue and music remain live game audio.

After all five pass:

```bash
python3 Tools/blender/cinematics/full_story/publishing/bundle.py \
  --directory Logs/full-film-publication/my-encoded-candidate \
  --production Logs/full-cinematic-renders/my-full-production \
  --contract Logs/full-film-publication/my-contract.json --group full
python3 Tools/blender/cinematics/full_story/publishing/publish.py \
  --bundle Logs/full-film-publication/my-encoded-candidate/bundle-full.json
```

The last command defaults to a dry-run recheck. After reviewing the movies and exact printed bundle hash, add `--go --approved-bundle-sha REVIEWED_SHA256` to publish. Publication takes the serialized Unity lock, verifies copies and atomically exchanges the complete runtime directory while retaining the old tree in an ignored backup. Existing `.meta` GUIDs are preserved.

The separate `--group lake` bundle covers only four lake movies. It cannot certify the full opening or replace a previously published full opening.

## Verification scope

```bash
python3 -m unittest discover -s Tools/blender/cinematics/full_story/publishing/tests -v
```

The nine CPU tests cover evidence rejection, exact cut boundaries, encoding command invariants, distinct lake/full groups and atomic directory exchange. They launch no renderer or encoder. Real production checks are separate from these tests.

Successful frame/codec/publication gates establish source provenance and complete encoded streams. Actual visual continuity, normal-speed presentation, live voice synchronization, release-aware skip, gameplay restoration and native media retirement require separate Unity and built-player execution. The November 25 opening's portable evidence is in `ArtSource/FullOpeningFilm`.
