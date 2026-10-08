#!/usr/bin/env python3
"""Makes GreyHaven's sound effects from scratch (sine tones, no samples), so there's nothing to
license. Writes assets/sounds/*.wav: short, soft and quiet (peaks around -16 dBFS).

    python3 tools/sounds/build.py

tick     a checkbox is ticked
start    something long starts (scanning, downloading)
done     it finished well (scan done, USB stick ready, a setting changed)
problem  it didn't work (gentle, not an alarm)
"""
import math, struct, wave
from pathlib import Path

RATE = 44100
OUT = Path(__file__).resolve().parents[2] / "assets" / "sounds"


def tone(freq, length, start=0.0, level=1.0, decay=8.0, attack=0.004, harmonics=((2, 0.15),)):
    """A soft bell-like note: quick fade-in, exponential fade-out, a little of its overtones."""
    def f(t):
        t -= start
        if t < 0 or t > length: return 0.0
        env = min(1.0, t / attack) * math.exp(-decay * t) * min(1.0, (length - t) / 0.01)
        s = math.sin(2 * math.pi * freq * t) + sum(a * math.sin(2 * math.pi * freq * n * t) for n, a in harmonics)
        return level * env * s
    return f


def write(name, parts, length, peak=0.16):
    n = int(RATE * length)
    samples = [sum(p(i / RATE) for p in parts) for i in range(n)]
    top = max(abs(s) for s in samples) or 1.0
    with wave.open(str(OUT / f"{name}.wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(32767 * peak * s / top)) for s in samples))


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    write("tick", [tone(1760, 0.045, decay=70, attack=0.001, harmonics=((2, 0.3),))], 0.05, peak=0.14)
    write("start", [tone(659.25, 0.12, decay=18), tone(880, 0.16, start=0.07, decay=16)], 0.24, peak=0.11)
    write("done", [tone(783.99, 0.35, decay=9), tone(1046.5, 0.5, start=0.11, decay=7)], 0.62, peak=0.16)
    write("problem", [tone(440, 0.22, decay=10, harmonics=((3, 0.08),)), tone(369.99, 0.3, start=0.16, decay=9, harmonics=((3, 0.08),))], 0.48, peak=0.12)
    print(f"Wrote {', '.join(sorted(p.name for p in OUT.glob('*.wav')))} to {OUT}")


if __name__ == "__main__":
    main()
