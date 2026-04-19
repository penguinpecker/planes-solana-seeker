#!/usr/bin/env python3
"""
Draw the two ability icons used for the in-game power-up pickups +
timer HUD: a classic horseshoe magnet and a squircle shield. Output
is 256x256 transparent PNGs, theme-matched to the red plaque +
navy accent colours used elsewhere in the UI.
"""
import os
from PIL import Image, ImageDraw

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Sprites")
OUT_DIR = os.path.abspath(OUT_DIR)

RED      = (215, 38, 61, 255)
RED_DARK = (138, 12, 28, 255)
NAVY     = (42, 49, 74, 255)
NAVY_DK  = (25, 30, 50, 255)
WHITE    = (250, 250, 250, 255)
TRANS    = (0, 0, 0, 0)

def outlined_poly(draw, pts, fill, outline, outline_width=6):
    draw.polygon(pts, fill=outline)
    # shrink toward centroid
    cx = sum(p[0] for p in pts) / len(pts)
    cy = sum(p[1] for p in pts) / len(pts)
    inner = [(p[0] + (cx - p[0]) * (outline_width / 100.0),
              p[1] + (cy - p[1]) * (outline_width / 100.0)) for p in pts]
    draw.polygon(inner, fill=fill)

# -------- MAGNET --------
# Classic horseshoe: two red arms on top, navy curved body, white highlights.
def draw_magnet():
    img = Image.new("RGBA", (256, 256), TRANS)
    d = ImageDraw.Draw(img)

    # Overall horseshoe outline (white halo for contrast)
    halo = [(28, 30), (228, 30), (228, 170), (190, 170),
            (190, 80), (66, 80), (66, 170), (28, 170)]
    d.polygon(halo, fill=WHITE)

    # Navy body (the arc + legs) -- draw as thick rectangle arms with arch.
    # Top arch (the curve of the horseshoe)
    d.rectangle((40, 40, 216, 86), fill=NAVY)
    d.ellipse((34, 28, 222, 120), fill=NAVY)
    # Hollow the inside
    d.rectangle((70, 60, 186, 90), fill=TRANS)
    d.ellipse((66, 50, 190, 110), fill=TRANS)

    # Left arm
    d.rectangle((40, 80, 82, 210), fill=NAVY)
    # Right arm
    d.rectangle((174, 80, 216, 210), fill=NAVY)

    # Red pole tips (bottom of each arm)
    d.rectangle((40, 165, 82, 210), fill=RED)
    d.rectangle((174, 165, 216, 210), fill=RED)
    # Thin dark-red rim under each pole
    d.rectangle((40, 206, 82, 212), fill=RED_DARK)
    d.rectangle((174, 206, 216, 212), fill=RED_DARK)

    # White "N" / "S" marker blocks inset into the red poles
    d.rectangle((52, 178, 70, 198), fill=WHITE)
    d.rectangle((186, 178, 204, 198), fill=WHITE)
    # letters
    d.text((54, 178), "N", fill=RED_DARK)
    d.text((188, 178), "S", fill=RED_DARK)

    # Top highlight shimmer
    d.polygon([(50, 52), (70, 48), (110, 46), (110, 58), (70, 60), (52, 64)],
              fill=(255, 255, 255, 110))

    out = os.path.join(OUT_DIR, "ability_magnet.png")
    img.save(out)
    print(f"wrote {out} ({os.path.getsize(out)} bytes)")

# -------- SHIELD --------
# Classic shield emblem: white halo -> navy body -> red inner shield.
def draw_shield():
    img = Image.new("RGBA", (256, 256), TRANS)
    d = ImageDraw.Draw(img)

    # Outer white halo
    outer = [(40, 40), (216, 40), (216, 140),
             (128, 220), (40, 140)]
    d.polygon(outer, fill=WHITE)

    # Navy shield
    navy_shield = [(52, 52), (204, 52), (204, 134),
                   (128, 202), (52, 134)]
    d.polygon(navy_shield, fill=NAVY)

    # Red inner shield
    red_shield = [(72, 72), (184, 72), (184, 128),
                  (128, 178), (72, 128)]
    d.polygon(red_shield, fill=RED)

    # White cross / bolt
    d.rectangle((118, 92, 138, 160), fill=WHITE)
    d.rectangle((92, 116, 164, 134), fill=WHITE)

    # Thin dark rim inside red
    rim = [(78, 80), (178, 80), (178, 126),
           (128, 170), (78, 126)]
    d.polygon(rim, outline=RED_DARK, width=3)

    # Top highlight stripe
    d.polygon([(58, 58), (198, 58), (195, 70), (62, 70)], fill=(255, 255, 255, 110))

    out = os.path.join(OUT_DIR, "ability_shield.png")
    img.save(out)
    print(f"wrote {out} ({os.path.getsize(out)} bytes)")

# -------- SHIELD BUBBLE (ring, shown around the plane) --------
def draw_shield_bubble():
    img = Image.new("RGBA", (512, 512), TRANS)
    d = ImageDraw.Draw(img)

    cx, cy = 256, 256
    outer_r = 240
    inner_r = 208

    # Outer navy ring
    d.ellipse((cx - outer_r, cy - outer_r, cx + outer_r, cy + outer_r), fill=(70, 120, 200, 100))
    d.ellipse((cx - inner_r, cy - inner_r, cx + inner_r, cy + inner_r), fill=TRANS)

    # Thin bright-blue inner edge
    d.ellipse((cx - inner_r - 6, cy - inner_r - 6, cx + inner_r + 6, cy + inner_r + 6),
              outline=(120, 180, 240, 180), width=4)
    # Thin white outer highlight
    d.ellipse((cx - outer_r, cy - outer_r, cx + outer_r, cy + outer_r),
              outline=(255, 255, 255, 140), width=3)

    out = os.path.join(OUT_DIR, "ability_shield_bubble.png")
    img.save(out)
    print(f"wrote {out} ({os.path.getsize(out)} bytes)")

if __name__ == "__main__":
    draw_magnet()
    draw_shield()
    draw_shield_bubble()
