#!/usr/bin/env python3
"""
Extract just the red plaque + gold shield + stars from the user-supplied
planes_logo.png, dropping the baked-in sky/clouds so the logo sits cleanly
on top of the in-game sky background.

Strategy: flood-fill from the four corners; any pixel reachable from a
corner via a "sky blue-ish" color run is transparency. The logo + its
white halo form a single island disconnected from the corners and are
preserved. Small floating cloud puffs and sparkles inside the walked
region also go transparent along the way. Then we crop tight to the
surviving pixels and save a 1024x1024 PNG centred on that bounding box.
"""
import os, sys
from PIL import Image
from collections import deque

SRC = "/Users/penguinpecker/Downloads/planes_logo.png"
OUT = os.path.join(os.path.dirname(__file__), "..",
                   "Assets", "Sprites", "planes_banner_plaque.png")
OUT = os.path.abspath(OUT)

img = Image.open(SRC).convert("RGBA")
W, H = img.size
print(f"source: {SRC}  ({W}x{H})")

px = img.load()

def is_sky(r, g, b, a):
    """True when the pixel looks like sky blue or on-sky cloud white.
    - Sky blue: B noticeably higher than R, G mid-to-high.
    - Cloud white: R,G,B all high but with bluish tint (B >= R-4, B >= G-4).
    Red plaque and gold shield fail both.
    """
    if a < 8: return True
    # sky blue
    if b > 150 and b > r + 20 and b > g + 5 and r < 230:
        return True
    # soft pastel blue
    if b >= 200 and r < b - 5 and g < b - 2:
        return True
    # cloud white/very-light-blue (>=235 on all; slight B tint OK)
    if r >= 225 and g >= 225 and b >= 235 and (b >= r - 6) and (b >= g - 6):
        return True
    return False

# Flood fill from all border pixels
visited = bytearray(W * H)
q = deque()
for x in range(W):
    q.append((x, 0))
    q.append((x, H - 1))
for y in range(H):
    q.append((0, y))
    q.append((W - 1, y))

cleared = 0
while q:
    x, y = q.popleft()
    if x < 0 or y < 0 or x >= W or y >= H: continue
    idx = y * W + x
    if visited[idx]: continue
    r, g, b, a = px[x, y]
    if not is_sky(r, g, b, a): continue
    visited[idx] = 1
    px[x, y] = (0, 0, 0, 0)
    cleared += 1
    q.append((x + 1, y))
    q.append((x - 1, y))
    q.append((x, y + 1))
    q.append((x, y - 1))

print(f"cleared {cleared} sky pixels")

# Crop to surviving (non-transparent) content
bbox = img.getbbox()
print(f"content bbox: {bbox}")
logo = img.crop(bbox)
lw, lh = logo.size

# Fit into a 1024x1024 transparent canvas, centred, with 40px padding.
TARGET = 1024
PAD = 40
scale = min((TARGET - 2*PAD) / lw, (TARGET - 2*PAD) / lh)
nw, nh = int(lw * scale), int(lh * scale)
logo = logo.resize((nw, nh), Image.LANCZOS)
canvas = Image.new("RGBA", (TARGET, TARGET), (0, 0, 0, 0))
canvas.paste(logo, ((TARGET - nw) // 2, (TARGET - nh) // 2), logo)
canvas.save(OUT)
print(f"wrote {OUT} ({os.path.getsize(OUT)} bytes)")
