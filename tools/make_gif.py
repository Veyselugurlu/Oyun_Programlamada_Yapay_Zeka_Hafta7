"""
Frames recorded by ExperimentDriver -> README animation and a per-generation film strip for the report.
  StayOnPlatform.exe -recordFrames <dir> -recordEvery 6 -recordCount 400 -screen-width 960 -screen-height 540 ...
  python tools/make_gif.py <dir>
At 30 frames of game time per second and every 6th frame saved, one generation (8 s) = 40 saved frames.
"""
import sys
from pathlib import Path

from PIL import Image, ImageDraw

FRAMES_PER_GENERATION = 40
GIF_GENERATIONS = 4
src = sorted(Path(sys.argv[1]).glob("frame_*.png"))
out = Path("docs/week07/images")
out.mkdir(parents=True, exist_ok=True)

frames = [Image.open(p).convert("RGB") for p in src]
small = [f.resize((432, 243), Image.LANCZOS).quantize(64) for f in frames[:GIF_GENERATIONS * FRAMES_PER_GENERATION]]
small[0].save(out / "evolution.gif", save_all=True, append_images=small[1:], duration=100, loop=0, optimize=True)

gens = [1, 2, 3, 5, 7, 10]
w, h = 400, 225
strip = Image.new("RGB", (3 * w + 40, 2 * (h + 26) + 10), (255, 255, 255))
draw = ImageDraw.Draw(strip)
for k, g in enumerate(gens):
    i = min((g - 1) * FRAMES_PER_GENERATION + 30, len(frames) - 1)  # 6 s into the generation
    x, y = 10 + (k % 3) * (w + 10), 5 + (k // 3) * (h + 26)
    strip.paste(frames[i].resize((w, h), Image.LANCZOS), (x, y))
    draw.text((x + 4, y + h + 5), f"Nesil {g}, 6. saniye", fill=(30, 30, 30))
strip.save(out / "evolution_filmstrip.png")
print((out / "evolution.gif").stat().st_size // 1024, "KB")
