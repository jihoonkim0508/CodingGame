"""Synthesizes alarm.ogg (low 'jing... jing...' warning) and hover.ogg (soft UI tick). make_sounds.py <out folder>"""
import sys, os, subprocess, wave
import numpy as np

SR = 44100
out = sys.argv[1]

def write(name, x):
    x = x / max(1e-9, np.abs(x).max()) * .89
    wav = os.path.join(out, name + ".wav")
    with wave.open(wav, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-i", wav, "-c:a", "libvorbis", "-q:a", "5", os.path.join(out, name + ".ogg")], check=True)
    os.remove(wav)

def env(t, attack, length, release):
    e = np.clip(t / attack, 0, 1)
    tail = np.clip((t - length) / release, 0, 1)
    return e * (1 - tail) ** 2

# alarm: two low buzzing pulses, each a detuned 98 Hz saw-ish tone through a soft low-pass, with a slow 6 Hz throb and a
# slight fall in pitch at the end, like a heavy machine warning ("징... 징...").
def pulse(length=.52, release=.32):
    t = np.arange(int((length + release) * SR)) / SR
    f = 98 * (1 - .035 * np.clip((t - length * .6) / (length + release), 0, 1))
    phase = 2 * np.pi * np.cumsum(f) / SR
    tone = sum(np.sin(k * phase) / k ** 1.25 for k in range(1, 14))          # saw-like, softened highs
    tone += .6 * np.sin(phase * .5)                                       # sub octave
    tone += .35 * sum(np.sin(k * phase * 1.006) / k ** 1.25 for k in range(1, 9))  # detune beat
    tone *= 1 + .18 * np.sin(2 * np.pi * 6 * t)
    tone = np.tanh(tone * 1.4)
    # one-pole low-pass at ~1.1 kHz
    a = np.exp(-2 * np.pi * 1100 / SR); y = np.zeros_like(tone); acc = 0.
    for i, v in enumerate(tone): acc = (1 - a) * v + a * acc; y[i] = acc
    return y * env(t, .03, length, release)

gap = .78
p = pulse()
alarm = np.zeros(int((gap + len(p) / SR + .15) * SR))
for start in (0, gap):
    i = int(start * SR); alarm[i:i + len(p)] += p
# short room tail
d = int(.06 * SR); alarm[d:] += .22 * alarm[:-d]
write("alarm", alarm)

# hover: a short soft wooden tick, 1.6 kHz falling to 1.1 kHz over 40 ms with a quick decay plus a tiny click on top.
t = np.arange(int(.07 * SR)) / SR
f = 1100 + 500 * np.exp(-t / .012)
tick = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / .018)
tick += .25 * np.sin(2 * np.pi * 3300 * t) * np.exp(-t / .004)
tick *= np.clip(t / .002, 0, 1)
write("hover", tick)
print("ok")
