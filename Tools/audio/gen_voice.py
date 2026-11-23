#!/usr/bin/env python3
"""PARASITE voice-over pipeline (offline, Kokoro-82M TTS — Apache-2.0 weights and voices).

1. Collects every (speaker, line) pair that reaches GameEvents.RaiseSubtitle:
   - C# sources under Assets/Parasite/{Scripts,Editor}: RaiseSubtitle("WHO", "line"), ("WHO", "line") dialogue tuples,
     opening CaptionCue new(t, "WHO", "line"), EavesdropZone speaker/lines blocks, lakeside narration captions;
   - Logs/voice_lines_seen.tsv, written by the runtime VoiceOver (Editor/development) for subtitles without a clip.
2. Casts a consistent voice per speaker (rules below + optional Tools/audio/voice_cast.json overrides).
3. Synthesises only MISSING clips into Assets/Parasite/Resources/Audio/Voice/vo_<fnv1a64(SPEAKER|line)>.ogg
   and writes Assets/Parasite/Audio/voice_manifest.json (+ a timing report against the subtitle display windows).
4. --vocals also renders the gameplay vocal sets (guard barks, radio dispatch, civilian reactions, scientist murmurs)
   into Resources/Audio/Vocals/ and crowd "walla" stems for the ambience generator.

The key MUST match Parasite.Audio.VoiceOver.KeyFor (C#): FNV-1a 64-bit over UTF-8 of
  Upper(CollapseWs(StripTags(speaker))) + "|" + CollapseWs(StripTags(line)).
"""
from __future__ import annotations

import argparse
import glob
import hashlib
import json
import os
import re
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import common as C  # noqa: E402

VSR = 24000
VOICE_DIR = os.path.join(C.AUDIO_RES, "Voice")
VOCAL_DIR = os.path.join(C.AUDIO_RES, "Vocals")
MANIFEST = os.path.join(C.ROOT, "Assets", "Parasite", "Audio", "voice_manifest.json")
SEEN = os.path.join(C.ROOT, "Logs", "voice_lines_seen.tsv")
CAST_OVERRIDES = os.path.join(os.path.dirname(__file__), "voice_cast.json")
RENDER_VERSION = 3  # bump to force regeneration of every clip after changing casting/processing


# ----------------------------------------------------------------------------- keys
def collapse(s: str) -> str:
    s = re.sub(r"<[^>]+>", "", s)
    return re.sub(r"\s+", " ", s).strip()


def voice_key(speaker: str, line: str) -> str:
    data = (collapse(speaker).upper() + "|" + collapse(line)).encode("utf-8")
    h = 0xCBF29CE484222325
    for b in data:
        h ^= b
        h = (h * 0x100000001B3) & 0xFFFFFFFFFFFFFFFF
    return f"vo_{h:016x}"


# ----------------------------------------------------------------------------- collection
STR = r'"((?:[^"\\]|\\.)*)"'


def unescape(s: str) -> str:
    def rep(m):
        e = m.group(1)
        if e[0] == "u":
            return chr(int(e[1:5], 16))
        return {"n": "\n", "t": "\t", "r": "", '"': '"', "\\": "\\", "'": "'", "0": ""}.get(e[0], e)
    return re.sub(r"\\(u[0-9a-fA-F]{4}|.)", rep, s)


SPEAKER_RE = re.compile(r"^[A-Z][A-Z0-9 .·'’\-/&]*[A-Z0-9.)]( \([^)]*\))?$")


def looks_like_speaker(s: str) -> bool:
    return len(s) >= 3 and bool(SPEAKER_RE.match(s)) and any(ch.isalpha() for ch in s)


def dialogue_window(line: str) -> float:
    # MissionManager.Dialogue: dur = 1.6 + line.Length * 0.045 (skippable after 0.5 s)
    return 1.6 + len(line) * 0.045


def collect():
    items = {}

    def add(speaker, line, source, window=None):
        line = unescape(line)
        speaker = unescape(speaker)
        if not collapse(line) or "{" in line:
            return
        if not collapse(speaker):
            speaker = "NARRATOR"
        k = voice_key(speaker, line)
        if k not in items:
            items[k] = dict(key=k, speaker=collapse(speaker), line=collapse(line), sources=[], window=window)
        it = items[k]
        if source not in it["sources"]:
            it["sources"].append(source)
        if window is not None:
            it["window"] = window if it["window"] is None else min(it["window"], window)

    files = sorted(glob.glob(os.path.join(C.ROOT, "Assets/Parasite/Scripts/**/*.cs"), recursive=True) +
                   glob.glob(os.path.join(C.ROOT, "Assets/Parasite/Editor/**/*.cs"), recursive=True))
    for path in files:
        rel = os.path.relpath(path, C.ROOT)
        src = open(path, encoding="utf-8").read()
        name = os.path.basename(path)
        # opening captions: new(time, "WHO", "text") — window = time until the next cue
        cues = [(float(m.group(1)), m.group(2), m.group(3)) for m in
                re.finditer(r"new\s*\(\s*([0-9.]+)f?\s*,\s*" + STR + r"\s*,\s*" + STR + r"\s*\)", src)]
        if cues:
            for i, (t, who, text) in enumerate(cues):
                nxt = cues[i + 1][0] if i + 1 < len(cues) else t + 6.0
                add(who, text, f"{rel} @{t:g}s", window=nxt - t)
        # explicit RaiseSubtitle("WHO", "line")
        for m in re.finditer(r"RaiseSubtitle\s*\(\s*" + STR + r"\s*,\s*" + STR + r"\s*\)", src):
            add(m.group(1), m.group(2), rel)
        # speech helpers: Bark("WHO", "line"), Say("WHO", "line"), Speak(...), Talk(...)
        for m in re.finditer(r"\b\w*(?:Bark|Say|Speak|Talk|Subtitle|Line)\w*\s*\(\s*" + STR + r"\s*,\s*" + STR + r"\s*[,)]", src):
            if looks_like_speaker(m.group(1)):
                add(m.group(1), m.group(2), rel)
        # dialogue tuples ("WHO", "line") (not function-call arguments)
        for m in re.finditer(r"(?<![\w>\]])\(\s*" + STR + r"\s*,\s*" + STR + r"\s*\)", src):
            who, text = m.group(1), m.group(2)
            if looks_like_speaker(who) and " " in text:
                add(who, text, rel, window=dialogue_window(unescape(text)) if "Missions" in rel else None)
        # EavesdropZone blocks: x.speaker = "WHO"; x.lines = new[] { ... }; (window = seconds / count)
        for m in re.finditer(r"(\w+)\.speaker\s*=\s*" + STR + r"\s*;", src):
            var, who = m.group(1), m.group(2)
            lm = re.search(re.escape(var) + r"\.lines\s*=\s*new\s*(?:string)?\s*\[\s*\]\s*\{(.*?)\}\s*;", src[m.end():], re.S)
            if not lm:
                continue
            lines = [x.group(1) for x in re.finditer(STR, lm.group(1))]
            sm = re.search(re.escape(var) + r"\.seconds\s*=\s*([0-9.]+)f?", src)
            secs = float(sm.group(1)) if sm else 7.0
            for text in lines:
                add(who, text, rel, window=secs / max(1, len(lines)))
        # lakeside narration (captions/beats are voiced by LakesideNarration as "NARRATOR")
        if name == "LakesideEpilogue.cs":
            for m in re.finditer(r"(?:caption|beat)\.text\s*=\s*([^;]+);", src):
                for s in re.finditer(STR, m.group(1)):
                    add("NARRATOR", s.group(1), rel, window=5.0)
            for m in re.finditer(r'Txt\(\s*root\s*,\s*"Caption"\s*,\s*' + STR, src):
                add("NARRATOR", m.group(1), rel, window=5.0)
    if os.path.exists(SEEN):
        for row in open(SEEN, encoding="utf-8"):
            parts = row.rstrip("\n").split("\t")
            if len(parts) >= 2:
                add(parts[0], parts[1].replace("\\n", "\n"), "runtime:voice_lines_seen.tsv")
    return items


# ----------------------------------------------------------------------------- casting
PROFILES = {
    # name: voice (Kokoro id or comma blend), lang ('a' US / 'b' UK), speed, fx chain
    "vale":      dict(voice="bm_george", lang="b", speed=0.95, fx="vale"),
    "petrov":    dict(voice="bf_emma", lang="b", speed=1.02, fx="clean"),
    "specimen":  dict(voice="af_nicole,af_sky", lang="a", speed=0.98, fx="specimen", pitch=-1.6),
    "narrator":  dict(voice="af_heart", lang="a", speed=0.94, fx="narrator"),
    "news":      dict(voice="am_michael", lang="a", speed=1.05, fx="broadcast"),
    "worker":    dict(voice="am_fenrir", lang="a", speed=1.0, fx="clean"),
    "guard_a":   dict(voice="am_onyx", lang="a", speed=1.0, fx="gruff"),
    "guard_b":   dict(voice="bm_lewis", lang="b", speed=1.02, fx="gruff"),
    "guard_c":   dict(voice="am_eric", lang="a", speed=1.04, fx="gruff"),
    "radio":     dict(voice="am_onyx", lang="a", speed=1.06, fx="radio"),
    "dispatch":  dict(voice="af_kore", lang="a", speed=1.08, fx="radio"),
    "sci_a":     dict(voice="am_liam", lang="a", speed=1.0, fx="clean"),
    "sci_b":     dict(voice="af_sarah", lang="a", speed=1.0, fx="clean"),
    "sci_c":     dict(voice="bf_isabella", lang="b", speed=1.0, fx="clean"),
    "sci_d":     dict(voice="bm_daniel", lang="b", speed=1.0, fx="clean"),
    "civ_fa":    dict(voice="af_bella", lang="a", speed=1.08, fx="clean"),
    "civ_fb":    dict(voice="af_jessica", lang="a", speed=1.08, fx="clean"),
    "civ_ma":    dict(voice="am_puck", lang="a", speed=1.08, fx="clean"),
    "civ_mb":    dict(voice="am_adam", lang="a", speed=1.06, fx="clean"),
    "girl":      dict(voice="af_river", lang="a", speed=1.0, fx="clean"),
    "system":    dict(voice="af_nova", lang="a", speed=1.0, fx="pa"),
}
GENERIC_POOL = ["sci_a", "sci_b", "civ_ma", "civ_fa", "sci_d", "civ_fb", "worker", "sci_c"]


def stable_index(s: str, n: int) -> int:
    return int(hashlib.sha1(s.encode("utf-8")).hexdigest(), 16) % n


def cast(speaker: str, overrides: dict) -> str:
    base = cast_base(speaker, overrides)
    s = speaker.upper()
    if "+" not in base and PROFILES[base]["fx"] != "radio":
        if any(k in s for k in ("(PA)", "INTERCOM", "LOUDSPEAKER", "PA SYSTEM")):
            return base + "+pa"
        if "(RADIO)" in s:
            return base + "+radio"
    return base


def profile(name: str) -> dict:
    if "+" in name:
        base, fx = name.split("+", 1)
        p = dict(PROFILES[base])
        p["fx"] = fx
        return p
    return PROFILES[name]


def cast_base(speaker: str, overrides: dict) -> str:
    s = speaker.upper()
    if speaker in overrides:
        return overrides[speaker]
    if s in overrides:
        return overrides[s]
    if "VALE" in s:
        return "vale"
    if "PETROV" in s or "HANA" in s:
        return "petrov"
    if "SPECIMEN" in s or "AMOEBA" in s or "MEMORY" in s:
        return "specimen"
    if "NARRATOR" in s:
        return "narrator"
    if any(k in s for k in ("NEWS", "ANCHOR", "BROADCAST", "REPORTER", "TV ")):
        return "news"
    if "DISPATCH" in s or "OPERATOR" in s:
        return "dispatch"
    if any(k in s for k in ("GRID", "SYSTEM", "COMPUTER", "TERMINAL", "AUTOMATED", "MAINFRAME")):
        return "system"
    if any(k in s for k in ("RADIO", "INTERCOM", "PA SYSTEM", "LOUDSPEAKER", "ANNOUNCEMENT")):
        return "radio"
    if any(k in s for k in ("GUARD", "SECURITY", "HUNTER", "OFFICER", "CAPTAIN", "SERGEANT", "POLICE", "COP")):
        return ["guard_a", "guard_b", "guard_c"][stable_index(s, 3)]
    if any(k in s for k in ("MAINTENANCE", "WORKER", "TECH", "JANITOR", "ENGINEER", "MECHANIC")):
        return "worker"
    if "GIRL" in s or "CHILD" in s:
        return "girl"
    if any(k in s for k in ("DR.", "DOCTOR", "SCIENTIST", "PROFESSOR", "RESEARCHER", "LAB ")):
        return ["sci_a", "sci_b", "sci_c", "sci_d"][stable_index(s, 4)]
    return GENERIC_POOL[stable_index(s, len(GENERIC_POOL))]


# ----------------------------------------------------------------------------- TTS
class Tts:
    def __init__(self):
        os.environ.setdefault("HF_HOME", os.path.join(C.WORK, "hf"))
        import warnings
        warnings.filterwarnings("ignore")
        import torch
        from kokoro import KPipeline
        dev = "cuda" if torch.cuda.is_available() else "cpu"
        self.torch = torch
        self.pipes = {"a": KPipeline(lang_code="a", repo_id="hexgrad/Kokoro-82M", device=dev)}
        self.pipes["b"] = KPipeline(lang_code="b", repo_id="hexgrad/Kokoro-82M", model=self.pipes["a"].model)

    def say(self, text, voice, lang, speed):
        pipe = self.pipes[lang]
        out = []
        for res in pipe(text, voice=voice, speed=speed, split_pattern=r"\n+"):
            audio = res[2] if isinstance(res, tuple) else res.audio
            if audio is None:
                continue
            a = audio.detach().cpu().numpy() if hasattr(audio, "detach") else np.asarray(audio)
            out.append(a.astype(np.float64))
            out.append(np.zeros(int(0.12 * VSR)))
        return np.concatenate(out) if out else np.zeros(VSR // 4)


def tts_text(line: str) -> str:
    t = collapse(line)
    t = t.replace("…", "... ").replace("—", ", ").replace("–", ", ").replace("·", ",")
    t = re.sub(r"^\.\.\.\s*", "", t)
    t = re.sub(r"\bDr\.\s", "Doctor ", t)
    t = re.sub(r"\bSpecimen 7\b", "Specimen Seven", t)
    t = re.sub(r"\bE\. Vale\b", "E. Vale", t)
    t = t.replace("kill-switch", "kill switch")
    return re.sub(r"\s+", " ", t).strip()


# ----------------------------------------------------------------------------- post-processing (24 kHz mono)
def trim(x, lead=0.03, tail=0.09, thresh_db=-42):
    if len(x) == 0:
        return x
    frame = 240
    n = len(x) // frame
    if n == 0:
        return x
    rms = np.sqrt(np.mean(x[:n * frame].reshape(n, frame) ** 2, axis=1) + 1e-12)
    th = np.max(rms) * C.undb(thresh_db)
    on = np.where(rms > th)[0]
    if len(on) == 0:
        return x
    a = max(0, on[0] * frame - int(lead * VSR))
    b = min(len(x), (on[-1] + 1) * frame + int(tail * VSR))
    y = x[a:b].copy()
    f = int(0.004 * VSR)
    y[:f] *= np.linspace(0, 1, f)
    y[-f:] *= np.linspace(1, 0, f)
    return y


def comp(x, thresh=-20, ratio=3, attack=0.005, release=0.12):
    y = C._compress(np.stack([x, x], 1).astype(np.float64), VSR, thresh, ratio, attack, release, 6.0)
    return y[:, 0]


def room(x, sec=0.6, decay=0.45, wet=0.08, damp=5000, seed=3):
    ir = C.make_ir(sec, decay, 0.008, damp, early=10, seed=seed, sr=VSR)[:, 0]
    from scipy import signal
    w = signal.oaconvolve(x, ir)[: len(x) + int(sec * VSR)]
    y = np.zeros(len(w))
    y[: len(x)] += x
    return y + w * wet


def pitch_resample(x, semitones):
    from scipy import signal
    ratio = 2 ** (-semitones / 12)  # >1 = longer/lower
    up, down = int(round(ratio * 1000)), 1000
    g = np.gcd(up, down)
    return signal.resample_poly(x, up // g, down // g)


def radio_fx(x, seed=0):
    r = np.random.default_rng(seed)
    y = C.bandpass(x, 380, 3100, order=3, sr=VSR)
    y = np.tanh(y / (np.max(np.abs(y)) + 1e-9) * 2.6)
    y = comp(y, -24, 4)
    pre, post = int(0.09 * VSR), int(0.16 * VSR)
    out = np.zeros(len(y) + pre + post)
    out[pre:pre + len(y)] = y * 0.8
    hiss = C.bandpass(r.uniform(-1, 1, len(out)), 900, 5200, order=2, sr=VSR) * 0.035
    crackle = (r.random(len(out)) > 0.9993) * r.uniform(-0.4, 0.4, len(out))
    out += hiss + C.bandpass(crackle, 1500, 6000, sr=VSR)
    # key-up / key-down squelch bursts
    for s0, ln in ((0, int(0.07 * VSR)), (pre + len(y) + int(0.02 * VSR), int(0.11 * VSR))):
        e = np.exp(-np.arange(ln) / (ln * 0.35))
        out[s0:s0 + ln] += C.bandpass(r.uniform(-1, 1, ln), 1200, 6500, sr=VSR) * e * 0.55
    t = np.arange(int(0.05 * VSR)) / VSR
    beep = np.sin(2 * np.pi * 1850 * t) * 0.18 * np.minimum(1, (0.05 - t) / 0.01)
    C.mix_into(out, beep, pre + len(y) + int(0.01 * VSR))
    return out


def process(x, fx, seed=0):
    x = trim(x)
    x = C.highpass(x, 70, sr=VSR)
    if fx == "vale":
        x = C.shelf(x, 200, 2.5, high=False, sr=VSR)
        x = C.peaking(x, 3200, 1.5, 0.9, sr=VSR)
        x = comp(x, -22, 2.5)
        x = room(x, 0.7, 0.5, 0.07, 4500)
    elif fx == "specimen":
        dbl = np.concatenate([np.zeros(int(0.021 * VSR)), C.lowpass(x, 3500, sr=VSR)])[: len(x)] * 0.5
        lo = pitch_resample(x, -12)[: len(x)] * 0.12
        x = x + dbl + np.pad(lo, (0, max(0, len(x) - len(lo))))
        x = C.highpass(x, 110, sr=VSR)
        x = C.shelf(x, 6000, 3, high=True, sr=VSR)
        x = comp(x, -24, 3)
        x = room(x, 2.6, 2.2, 0.2, 7000, seed=9)
    elif fx == "narrator":
        x = C.shelf(x, 180, 1.5, high=False, sr=VSR)
        x = comp(x, -22, 2)
        x = room(x, 1.2, 0.9, 0.06, 6000, seed=5)
    elif fx == "broadcast":
        x = C.bandpass(x, 140, 7000, order=2, sr=VSR)
        x = comp(x, -26, 5, 0.002, 0.08)
        x = np.tanh(x / (np.max(np.abs(x)) + 1e-9) * 1.6)
    elif fx == "radio":
        x = radio_fx(x, seed)
    elif fx == "pa":
        x = C.bandpass(x, 260, 4800, order=2, sr=VSR)
        x = np.tanh(x / (np.max(np.abs(x)) + 1e-9) * 1.8)
        x = comp(x, -24, 4)
        x = room(x, 2.4, 1.8, 0.22, 3500, seed=11)
    elif fx == "gruff":
        x = C.shelf(x, 220, 2.5, high=False, sr=VSR)
        x = np.tanh(x / (np.max(np.abs(x)) + 1e-9) * 1.3) * 0.9
        x = comp(x, -22, 3)
    else:
        x = comp(x, -22, 2.2)
    if fx in ("specimen", "narrator", "vale", "pa"):
        x = trim(x, 0.02, 0.12, -55)
    target = -20.0 if fx == "radio" else -18.0
    try:
        x = C.normalize_lufs(x, target, -1.5, sr=VSR)
    except Exception:
        x = C.normalize_peak(x, -3)
    return x


def render(tts: Tts, text: str, prof: dict, seed=0, max_dur=None):
    speed = prof["speed"]
    pitch = prof.get("pitch", 0.0)
    eff = speed * (2 ** (-pitch / 12)) if pitch else speed  # pitch-down by resampling slows; pre-compensate
    raw = tts.say(text, prof["voice"], prof["lang"], eff)
    if pitch:
        raw = pitch_resample(raw, pitch)
    y = process(raw, prof["fx"], seed)
    if max_dur and len(y) / VSR > max_dur:
        # speed up (≤ 12 %) to fit the subtitle display window
        factor = min(1.12, (len(y) / VSR) / max_dur)
        if factor > 1.01:
            raw = tts.say(text, prof["voice"], prof["lang"], eff * factor)
            if pitch:
                raw = pitch_resample(raw, pitch)
            y = process(raw, prof["fx"], seed)
    return y


def write_voice(path, y, q=4):
    C.write_ogg(path, y, q=q, sr=VSR)


# ----------------------------------------------------------------------------- gameplay vocal sets
VOCALS = {
    # category: (profiles, lines)
    "investigate": (["guard_a", "guard_b", "guard_c"],
                    ["Hey!", "Who's there?", "Huh? What was that?", "Hello? Somebody there?", "I heard something.",
                     "Hold on... something's off."]),
    "pursue": (["guard_a", "guard_b", "guard_c"],
               ["Stop right there!", "Freeze!", "Don't move!", "There! Get it!", "You! Stop!", "Intruder! Over here!"]),
    "lost": (["guard_a", "guard_b", "guard_c"],
             ["Must've been nothing.", "Lost it.", "Nothing here. Back to patrol.", "Huh. Guess it was the wind.",
              "Where did it go?"]),
    "capture": (["guard_a", "guard_b", "guard_c"],
                ["Got you!", "Hold still!", "Contain it, now!", "I've got one!"]),
    "dispatch": (["dispatch"],
                 ["All units, possible intruder reported. Check your sectors.",
                  "Dispatch to campus security, report status.",
                  "Copy that. Sending backup to your position.",
                  "Units respond, suspicious activity near the north block.",
                  "All units, suspect is on the move. Lock it down.",
                  "Dispatch. Stand down, all clear. Resume patrol.",
                  "Gate two, radio check.",
                  "Be advised, cameras are reporting movement."]),
    "chatter": (["radio"],
                ["Copy, dispatch. Sector three is quiet.", "Roger. Heading to the lobby.",
                 "Checking the service yard now.", "Nothing on my end. Over.", "Ten four. Coffee run after this."]),
    "civ_gasp_f": (["civ_fa", "civ_fb"], ["Oh!", "What the...?", "Oh my God!", "Ah!"]),
    "civ_gasp_m": (["civ_ma", "civ_mb"], ["Whoa!", "What the hell?", "Hey, what is that?", "Huh?!"]),
    "civ_flee_f": (["civ_fa", "civ_fb"], ["Help! Somebody help!", "Get away from me!", "Call security!"]),
    "civ_flee_m": (["civ_ma", "civ_mb"], ["Run! Run!", "Get back! Get back!", "Somebody call the police!"]),
    "sci_murmur": (["sci_a", "sci_b", "sci_c", "sci_d"],
                   ["The cultures are still unstable.", "Has anyone seen Doctor Vale tonight?",
                    "Run the sequence one more time.", "Readings are spiking in containment again.",
                    "Don't touch that, it isn't calibrated.", "I haven't slept in two days.",
                    "Log it and move on.", "Who left the centrifuge running?"]),
}

WALLA = [
    "So I told him, if you want the table, you have to book it.", "Is it going to rain all night again?",
    "No, the other café, the one by the fountain.", "I'll have the usual, thanks.", "Did you see the news about the lab?",
    "We should go before the traffic gets bad.", "Honestly, I don't believe a word they say.",
    "That's what I keep telling her.", "Can you pass the sugar?", "My bus is late again.",
    "It was so funny, you should have been there.", "Wait, where did you park?", "Two coffees, please. One with milk.",
    "I heard they work all night up there.", "It's not that far, we can walk.", "Oh, I love this song.",
]
WALLA_PROFILES = ["civ_fa", "civ_ma", "civ_fb", "civ_mb", "sci_a", "sci_b", "sci_c", "sci_d", "petrov", "worker"]


def gen_vocals(tts: Tts, force=False):
    os.makedirs(VOCAL_DIR, exist_ok=True)
    made = 0
    for cat, (profiles, lines) in VOCALS.items():
        for p_i, pname in enumerate(profiles):
            prof = PROFILES[pname]
            tag = pname.split("_")[-1] if pname.startswith(("guard_", "sci_")) else ""
            prefix = f"{cat}_{tag}" if cat in ("investigate", "pursue", "lost", "capture") else cat
            for i, line in enumerate(lines):
                idx = i + 1 if prefix != cat or len(profiles) == 1 else p_i * len(lines) + i + 1
                path = os.path.join(VOCAL_DIR, f"{prefix}_{idx}.ogg")
                if os.path.exists(path) and not force:
                    continue
                y = render(tts, tts_text(line), prof, seed=i + 17 * p_i)
                if cat.startswith("civ_flee") or cat.startswith("civ_gasp"):
                    y = C.normalize_lufs(y, -16.0, -1.0, sr=VSR)
                write_voice(path, y)
                made += 1
    wdir = os.path.join(C.WORK, "walla")
    os.makedirs(wdir, exist_ok=True)
    for i, line in enumerate(WALLA):
        for j in range(2):
            pname = WALLA_PROFILES[(i * 3 + j * 5) % len(WALLA_PROFILES)]
            path = os.path.join(wdir, f"walla_{i:02d}_{j}.wav")
            if os.path.exists(path) and not force:
                continue
            prof = dict(PROFILES[pname])
            prof["fx"] = "clean"
            y = render(tts, line, prof, seed=i)
            import soundfile as sf
            sf.write(path, y.astype(np.float32), VSR)
    return made


# ----------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--force", action="store_true", help="re-render every dialogue clip")
    ap.add_argument("--prune", action="store_true", help="delete vo_*.ogg clips no longer referenced")
    ap.add_argument("--vocals", action="store_true", help="also (re)generate missing gameplay vocal sets + walla")
    ap.add_argument("--list", action="store_true", help="only print the collected lines")
    args = ap.parse_args()

    items = collect()
    overrides = json.load(open(CAST_OVERRIDES)) if os.path.exists(CAST_OVERRIDES) else {}
    for it in items.values():
        it["profile"] = cast(it["speaker"], overrides)
    if args.list:
        for it in sorted(items.values(), key=lambda d: (d["speaker"], d["line"])):
            print(f'{it["key"]}  [{it["profile"]:9s}] {it["speaker"]}: {it["line"]}')
        print(f"{len(items)} lines")
        return

    old = {}
    if os.path.exists(MANIFEST):
        try:
            old = {e["key"]: e for e in json.load(open(MANIFEST))["lines"]}
        except Exception:
            old = {}
    os.makedirs(VOICE_DIR, exist_ok=True)
    tts = None
    made = 0
    for it in sorted(items.values(), key=lambda d: d["key"]):
        path = os.path.join(VOICE_DIR, it["key"] + ".ogg")
        prof = profile(it["profile"])
        prev = old.get(it["key"])
        stale = prev is not None and (prev.get("profile") != it["profile"] or prev.get("render") != RENDER_VERSION)
        if os.path.exists(path) and not args.force and not stale:
            it["duration"] = prev.get("duration") if prev else None
            if it["duration"] is None:
                import soundfile as sf
                it["duration"] = round(sf.info(path).duration, 3)
            continue
        if tts is None:
            print("loading Kokoro-82M …", flush=True)
            tts = Tts()
        y = render(tts, tts_text(it["line"]), prof, seed=int(it["key"][3:11], 16) & 0xFFFF,
                   max_dur=(it["window"] - 0.15) if it.get("window") else None)
        write_voice(path, y)
        it["duration"] = round(len(y) / VSR, 3)
        made += 1
        print(f'  + {it["key"]} {it["duration"]:5.2f}s [{it["profile"]}] {it["speaker"]}: {it["line"][:70]}', flush=True)

    if args.vocals:
        if tts is None:
            print("loading Kokoro-82M …", flush=True)
            tts = Tts()
        print(f"vocal sets: {gen_vocals(tts)} new clips")

    keys = set(items)
    stale_files = [f for f in glob.glob(os.path.join(VOICE_DIR, "vo_*.ogg")) if os.path.basename(f)[:-4] not in keys]
    for f in stale_files:
        if args.prune:
            os.remove(f)
            if os.path.exists(f + ".meta"):
                os.remove(f + ".meta")
    entries = []
    report = []
    for it in sorted(items.values(), key=lambda d: (d["sources"][0], d["speaker"])):
        p = profile(it["profile"])
        e = dict(key=it["key"], speaker=it["speaker"], line=it["line"], profile=it["profile"], voice=p["voice"],
                 duration=it.get("duration"), window=round(it["window"], 2) if it.get("window") else None,
                 sources=it["sources"], render=RENDER_VERSION)
        entries.append(e)
        if e["window"] and e["duration"] and e["duration"] > e["window"]:
            report.append(e)
    os.makedirs(os.path.dirname(MANIFEST), exist_ok=True)
    with open(MANIFEST, "w", encoding="utf-8") as f:
        json.dump(dict(generator="Tools/audio/gen_voice.sh", tts="Kokoro-82M v1.0 (Apache-2.0)", key="fnv1a64(SPEAKER|line)",
                       lines=entries), f, indent=1, ensure_ascii=False)
    print(f"{len(items)} lines, {made} synthesised, {len(stale_files)} stale clip(s){' pruned' if args.prune else ''}")
    if report:
        print("\nSUBTITLE TIMING — clip longer than its display window:")
        for e in report:
            print(f'  {e["duration"]:5.2f}s > {e["window"]:5.2f}s  {e["speaker"]}: {e["line"][:80]}  ({e["sources"][0]})')


if __name__ == "__main__":
    main()
