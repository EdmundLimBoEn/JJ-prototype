#!/usr/bin/env python3
"""Generate cute pixel-art sprites for the Paper Fold platformer.

Output: PaperFold/Assets/Sprites/*.png
Character sheets are horizontal strips of fixed-size cells; the Unity
AssetPostprocessor (SpriteImportSetup.cs) slices them by cell size encoded
in the filename suffix, e.g. paper_16x24.png -> 16x24 cells.
"""
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), "..", "PaperFold", "Assets", "Sprites")

# Palette
PAPER = (244, 239, 230, 255)      # paper white
PAPER_SHADE = (216, 207, 192, 255)
INK = (58, 51, 64, 255)           # outline / eyes
BLUSH = (240, 170, 160, 255)
RED = (224, 49, 49, 255)
RED_DARK = (160, 30, 30, 255)
GROUND = (140, 122, 107, 255)     # ground fill
GROUND_DARK = (112, 96, 84, 255)
GRASS = (158, 199, 145, 255)      # paper-scrap "grass"
GRASS_DARK = (120, 165, 110, 255)
WOOD = (181, 144, 100, 255)
WOOD_DARK = (140, 108, 72, 255)
FLAG_GRAY = (150, 150, 155, 255)
GOLD = (245, 197, 92, 255)
CLEAR = (0, 0, 0, 0)


def new_strip(cell_w, cell_h, frames):
    img = Image.new("RGBA", (cell_w * frames, cell_h), CLEAR)
    return img, ImageDraw.Draw(img)


def save_frames(img, cell_w, names):
    """Split a horizontal strip into individual frame PNGs."""
    for i, name in enumerate(names):
        frame = img.crop((i * cell_w, 0, (i + 1) * cell_w, img.height))
        frame.save(os.path.join(OUT, f"{name}.png"))


def px(d, ox, x, y, c):
    d.point((ox + x, y), fill=c)


def rect(d, ox, x0, y0, x1, y1, c):
    d.rectangle((ox + x0, y0, ox + x1, y1), fill=c)


def outline_rect(d, ox, x0, y0, x1, y1, fill, line):
    rect(d, ox, x0, y0, x1, y1, fill)
    d.rectangle((ox + x0, y0, ox + x1, y1), outline=line)


def draw_face(d, ox, cx, cy, blink=False):
    """Dot eyes + tiny smile centered around (cx, cy)."""
    if blink:
        px(d, ox, cx - 2, cy, INK)
        px(d, ox, cx + 2, cy, INK)
    else:
        d.point((ox + cx - 2, cy - 1), fill=INK)
        d.point((ox + cx - 2, cy), fill=INK)
        d.point((ox + cx + 2, cy - 1), fill=INK)
        d.point((ox + cx + 2, cy), fill=INK)
    # smile
    px(d, ox, cx - 1, cy + 2, INK)
    px(d, ox, cx, cy + 3, INK)
    px(d, ox, cx + 1, cy + 2, INK)
    # blush
    px(d, ox, cx - 4, cy + 1, BLUSH)
    px(d, ox, cx + 4, cy + 1, BLUSH)


def paper_body(d, ox, sway=0, crouch=0):
    """A standing sheet of paper, 16x24 cell. sway shifts the top edge."""
    top = 2 + crouch
    # body sheet (slightly wavy edges to feel papery)
    rect(d, ox, 4, top, 11, 21, PAPER)
    # top edge sway
    rect(d, ox, 4 + sway, top, 11 + sway, top + 2, PAPER)
    # shading right edge + curl
    rect(d, ox, 11, top + 2, 11, 21, PAPER_SHADE)
    rect(d, ox, 4, 20, 11, 21, PAPER_SHADE)
    # outline
    d.rectangle((ox + 4, top, ox + 11, 21), outline=INK)
    # corner fold (dog-ear) top-right
    px(d, ox, 11 + sway, top, CLEAR)
    px(d, ox, 10 + sway, top, INK)
    px(d, ox, 11 + sway, top + 1, INK)
    px(d, ox, 10 + sway, top + 1, PAPER_SHADE)


def gen_paper():
    # 6 frames: idle0, idle1(blink+sway), walk0, walk1, fold0(half), fold1(near-ball)
    w, h, n = 16, 24, 6
    img, d = new_strip(w, h, n)
    # idle 0
    paper_body(d, 0, sway=0)
    draw_face(d, 0, 7, 9)
    # idle 1 — gentle sway + blink
    paper_body(d, w, sway=1)
    draw_face(d, w, 7, 10, blink=True)
    # walk 0 — lean forward, bottom corners as "feet"
    paper_body(d, 2 * w, sway=1)
    draw_face(d, 2 * w, 8, 9)
    rect(d, 2 * w, 3, 21, 4, 22, INK)   # back foot
    rect(d, 2 * w, 11, 21, 12, 22, INK)  # front foot
    # walk 1
    paper_body(d, 3 * w, sway=-1)
    draw_face(d, 3 * w, 8, 9)
    rect(d, 3 * w, 6, 21, 7, 22, INK)
    rect(d, 3 * w, 9, 21, 10, 22, INK)
    # fold 0 — folded in half (wide, short)
    ox = 4 * w
    rect(d, ox, 2, 12, 13, 21, PAPER)
    rect(d, ox, 2, 19, 13, 21, PAPER_SHADE)
    d.rectangle((ox + 2, 12, ox + 13, 21), outline=INK)
    d.line((ox + 7, 12, ox + 7, 21), fill=PAPER_SHADE)
    draw_face(d, ox, 7, 16)
    # fold 1 — crumpling toward ball
    ox = 5 * w
    d.ellipse((ox + 3, 8, ox + 12, 21), fill=PAPER, outline=INK)
    d.line((ox + 5, 12, ox + 9, 14), fill=PAPER_SHADE)
    d.line((ox + 6, 17, ox + 10, 16), fill=PAPER_SHADE)
    draw_face(d, ox, 7, 14)
    save_frames(img, w, ["paper_idle_0", "paper_idle_1", "paper_walk_0", "paper_walk_1", "paper_fold_0", "paper_fold_1"])


def gen_ball():
    # 4 rotation frames of a crumpled paper ball, 16x16
    w, h, n = 16, 16, 4
    img, d = new_strip(w, h, n)
    for i in range(n):
        ox = i * w
        d.ellipse((ox + 2, 2, ox + 13, 13), fill=PAPER, outline=INK)
        # crumple creases rotate per frame
        creases = [
            [(5, 5, 9, 7), (6, 10, 11, 9)],
            [(6, 4, 10, 6), (5, 9, 9, 11)],
            [(8, 4, 6, 8), (10, 9, 7, 11)],
            [(4, 7, 8, 5), (7, 11, 11, 8)],
        ][i]
        for (x0, y0, x1, y1) in creases:
            d.line((ox + x0, y0, ox + x1, y1), fill=PAPER_SHADE)
        # face only on frame 0/1 so it reads as rolling
        if i < 2:
            draw_face(d, ox, 7, 7 + i)
        d.point((ox + 4, 4 + i), fill=PAPER_SHADE)
    save_frames(img, w, ["ball_0", "ball_1", "ball_2", "ball_3"])


def gen_plane():
    # 2 frames (level, banking), 24x16
    w, h, n = 24, 16, 2
    img, d = new_strip(w, h, n)
    for i in range(n):
        ox = i * w
        dy = i  # banking dips the nose a touch
        nose = (ox + 22, 9 + dy)
        # upper wing: long shallow triangle to the nose
        d.polygon([(ox + 1, 2 + dy), nose, (ox + 4, 9 + dy)], fill=PAPER)
        # body fold below
        d.polygon([(ox + 4, 9 + dy), nose, (ox + 4, 13 + dy)], fill=PAPER_SHADE)
        # thin ink edges instead of full outline (keeps it light)
        d.line([(ox + 1, 2 + dy), nose], fill=INK)
        d.line([(ox + 4, 9 + dy), nose], fill=INK)
        d.line([(ox + 4, 13 + dy), nose], fill=INK)
        d.line((ox + 1, 2 + dy, ox + 4, 13 + dy), fill=INK)
        # eyes on the upper wing near the nose
        d.point((ox + 16, 6 + dy), fill=INK)
        d.point((ox + 18, 6 + dy), fill=INK)
        px(d, ox, 14, 7 + dy, BLUSH)
    save_frames(img, w, ["plane_0", "plane_1"])


def gen_tiles():
    # ground_top: grassy paper-scrap top; ground: plain dirt. 16x16 each
    for name, top in (("ground_top", True), ("ground", False)):
        img = Image.new("RGBA", (16, 16), GROUND)
        d = ImageDraw.Draw(img)
        # dirt speckles
        for (x, y) in [(3, 6), (9, 9), (13, 5), (5, 12), (11, 13), (2, 10), (14, 11)]:
            d.point((x, y), fill=GROUND_DARK)
        if top:
            d.rectangle((0, 0, 15, 3), fill=GRASS)
            # ragged grass bottom edge
            for x in range(16):
                if x % 3 == 0:
                    d.point((x, 4), fill=GRASS)
                d.point((x, 3 if x % 4 else 4), fill=GRASS_DARK)
            # little paper scraps in the grass
            d.point((4, 1), fill=PAPER)
            d.point((12, 2), fill=PAPER)
        img.save(os.path.join(OUT, f"{name}.png"))


def gen_flag():
    # 2 frames: inactive (gray) / active (red), 16x32
    w, h, n = 16, 32, 2
    img, d = new_strip(w, h, n)
    for i in range(n):
        ox = i * w
        col = FLAG_GRAY if i == 0 else RED
        dark = (110, 110, 115, 255) if i == 0 else RED_DARK
        # pole
        rect(d, ox, 7, 2, 8, 29, INK)
        rect(d, ox, 6, 29, 9, 30, INK)
        # flag cloth (paper pennant)
        d.polygon([(ox + 9, 3), (ox + 15, 6), (ox + 9, 10)], fill=col, outline=dark)
        if i == 1:
            # tiny sparkle when active
            d.point((ox + 3, 5), fill=GOLD)
            d.point((ox + 2, 8), fill=GOLD)
            d.point((ox + 13, 13), fill=GOLD)
    save_frames(img, w, ["flag_off", "flag_on"])


def gen_kill():
    # red hazard tile, 16x16 — jagged "torn paper shredder" teeth
    img = Image.new("RGBA", (16, 16), (200, 60, 60, 200))
    d = ImageDraw.Draw(img)
    for x in range(0, 16, 4):
        d.polygon([(x, 6), (x + 2, 0), (x + 4, 6)], fill=RED)
    d.rectangle((0, 6, 15, 15), fill=RED)
    for (x, y) in [(3, 9), (8, 12), (12, 8), (5, 13)]:
        d.point((x, y), fill=RED_DARK)
    img.save(os.path.join(OUT, "kill.png"))


def gen_sign():
    # small wooden sign, 16x16
    img = Image.new("RGBA", (16, 16), CLEAR)
    d = ImageDraw.Draw(img)
    d.rectangle((7, 8, 8, 14), fill=WOOD_DARK)           # post
    d.rectangle((2, 2, 13, 8), fill=WOOD, outline=WOOD_DARK)  # board
    d.line((4, 4, 11, 4), fill=WOOD_DARK)
    d.line((4, 6, 9, 6), fill=WOOD_DARK)
    img.save(os.path.join(OUT, "sign.png"))


def gen_bg():
    # soft sky tile with a paper-cloud, 32x32
    img = Image.new("RGBA", (32, 32), (190, 227, 240, 255))
    d = ImageDraw.Draw(img)
    d.ellipse((6, 8, 20, 16), fill=(255, 255, 255, 230))
    d.ellipse((12, 5, 26, 14), fill=(255, 255, 255, 230))
    img.save(os.path.join(OUT, "cloud.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    gen_paper()
    gen_ball()
    gen_plane()
    gen_tiles()
    gen_flag()
    gen_kill()
    gen_sign()
    gen_bg()
    print("Sprites written to", os.path.abspath(OUT))


if __name__ == "__main__":
    main()
