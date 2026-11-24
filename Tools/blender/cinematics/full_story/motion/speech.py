"""Small, deterministic jaw-amplitude lookup derived from shipped opening audio.

This is an audio-energy envelope, not phoneme detection or viseme/lip-sync data.
Playback uses only Python's standard library; source audio and ffmpeg are needed
only when rebuilding the cache with build_speech.py. No scene/object edits.
"""
import sys
sys.dont_write_bytecode = True
import json
import math
import re
from pathlib import Path


def normalize(value):
    return re.sub(r'\s+', ' ', re.sub(r'<[^>]+>', '', str(value or ''))).strip()


ALIASES = {
    'DRPETROV': 'DR. HANA PETROV', 'DRHANAPETROV': 'DR. HANA PETROV',
    'DRVALE': 'DR. VALE',
    'MAINTELI': 'MAINTENANCE WORKER', 'MAINTENANCEWORKER': 'MAINTENANCE WORKER',
}


def canonical_speaker(speaker):
    cleaned = normalize(speaker).upper()
    return ALIASES.get(re.sub(r'[^A-Z0-9]', '', cleaned), cleaned or 'NARRATOR')


def voice_key(speaker, line):
    """VoiceOver.KeyFor-compatible FNV-1a64 after resolving actor-name aliases."""
    value = 0xCBF29CE484222325
    for byte in (canonical_speaker(speaker) + '|' + normalize(line)).encode('utf-8'):
        value = ((value ^ byte) * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return f'vo_{value:016x}'


class SpeechPerformance:
    """One reusable CPU lookup for the eight actual actor lines in the opening.

    amplitude(caption_speaker_or_actor_id, exact_line, time_since_cue) is 0..1.
    It returns 0 before onset, after actual clip duration, or for unknown lines.
    Multiply it by the chosen maximum jaw opening angle; do not add a constant
    opening or periodic sine. The caller owns cue selection, jaw hinge sign,
    caption replacement/fade policy, and all audio playback.
    """
    def __init__(self, project, optional_cache_path=None):
        self.project = Path(project).resolve()
        self.cache_path = Path(optional_cache_path) if optional_cache_path else (
            self.project / 'ArtSource/FullCinematics/Motion/opening_speech_envelopes.json')
        self.data = json.loads(self.cache_path.read_text())
        self._records = {entry['key']: entry for entry in self.data['lines']}
        self.available_lines = tuple((v['speaker'], v['line']) for v in self.data['lines'])
        self.cues = tuple({'at': v['cue_seconds'], 'speaker': v['speaker'], 'line': v['line'],
                           'duration_seconds': v['duration_seconds'], 'key': v['key']}
                          for v in self.data['lines'])

    def amplitude(self, speaker, line, time_since_cue):
        entry = self._records.get(voice_key(speaker, line))
        time = float(time_since_cue)
        if entry is None or not math.isfinite(time) or time < 0 or time >= entry['duration_seconds']:
            return 0.0
        position = time * entry['envelope_hz']
        first = min(len(entry['amplitude']) - 1, int(position))
        second = min(len(entry['amplitude']) - 1, first + 1)
        weight = position - first
        value = entry['amplitude'][first] * (1 - weight) + entry['amplitude'][second] * weight
        return max(0.0, min(1.0, value))

    def info(self, speaker, line):
        """Measured duration/source mapping, without the amplitude sample array."""
        entry = self._records.get(voice_key(speaker, line))
        return {k: v for k, v in entry.items() if k != 'amplitude'} if entry else None

    def duration(self, speaker, line):
        entry = self._records.get(voice_key(speaker, line))
        return entry['duration_seconds'] if entry else 0.0
