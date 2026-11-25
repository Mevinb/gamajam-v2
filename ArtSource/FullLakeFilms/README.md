# Full lake cinematic films

Four silent 1080p24 VP8 films were rendered from the packed sources in `ArtSource/FullCinematics/Lake`, using the RTX 4050 OptiX backend, 48 samples and GPU OIDN. Runtime movies are in `Assets/Parasite/Resources/Cinematics/FullStory`.

`production-manifest.json` pins source, rendered-frame, encode and final movie hashes. Real frame/count/provenance gates, ffprobe and full ffmpeg decode passed before atomic publication. The selected actual Unity decoder, loop, explicit-choice and narration tests passed; this does not convert unrelated failures in the combined XML into passes. Built-player full-lake acceptance remains a separate gate.

The arrival is 15 seconds, neutral choice hold 6 seconds, Peace 7 seconds and Dominion 7 seconds. Waiting at the hold never chooses an outcome. Dialogue and music remain live game audio; encoded files have no audio track. The 80-second opening is still rendering and is not part of this four-film publication.
