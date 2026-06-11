#!/usr/bin/env python3
"""Generate PaperFold/Assets/Editor/LevelData.cs.

The level is hand-designed in world units from the excalidraw "Full run"
drawing (jj level export.excalidraw — inspiration, not to scale).
Character reference: paper is 1.4 units tall, single jump ~4.9 up / ~10
across (+~1 from coyote time), double jump ~9.3 up. Ball diameter 0.68 —
squeeze slots are 1.25 tall (paper's unfold check needs 1.33).

Flat platforms are (left, topY, width, height) — one rect per surface so
there are no seams. Ramps are (x1, y1, x2, y2, thickness) describing the TOP
surface line; consecutive ramp/platform surface points share vertices so the
CompositeCollider2D outline is continuous (no bumps).

Ball-chute numbers (simulated against PlayerController physics — gravity
scale 2, linearDamping 0.35, drive force 30 below 8 u/s, 24 cap):
the eased 36-degree chute + 20-degree kicker launches the ball at 18.2 u/s;
flight over the 15-wide pit to a landing 6.9 below the lip clears by +2.2
(a no-drive coast-in still clears by +1.8). Paper tops out at 14 even with
a perfect coyote jump — and the 1.25 chute mouth means paper never reaches
the lip in the first place.
"""
import math
import os

# ---------------------------------------------------------------- geometry
# Ability budget: required single-jump rises <= 4 (true max ~4.9), the
# double-jump routes use 4.5-5.5 rises (max ~9.3), required flat gaps <= 6
# (range ~10), ball squeeze slots 1.25, ball-only pit gap 15 (see docstring),
# plane glide ratio ~4.6 at boost speed -> the 110-wide / 28-deep glide is
# comfortable.  Anti-skip: ball launch 18.2 < 24 cap, a 3-tall wall stops the
# ball after the pit, every flight path is capped by terrain or kill zones.
FLATS = [
    # --- 1: the perch + intro slide (the sheet tumbles down into the world) --
    (-16.0, 6.0, 9.0, 6.0),       # start perch, x[-16..-7] (RAMP slide follows)
    # --- meadow: move/jump/fold intro, checkpoint 1, hop garden ---
    (12.0, 0.0, 34.0, 6.0),       # meadow ground, x[12..46]
    (38.0, 1.8, 2.0, 2.3),        # hop pillar 1 (rise 1.8)
    (43.0, 2.8, 2.0, 3.3),        # hop pillar 2 (rise 1.0)
    # gap x[46..50] — easy drop-hop down
    # --- 2: stairs + the R-reset slot, up to checkpoint 2 ---
    (50.0, -2.0, 32.0, 6.0),      # lower ground, x[50..82]
    (60.0, 0.0, 3.0, 2.0),        # stair 1 (rise 2)
    (66.0, 2.2, 3.0, 4.2),        # stair 2 (rise 2.2)
    # slot x[69..72]: 4.2 deep, escapable with a perfect jump — or press R
    (72.0, 5.0, 18.0, 13.0),      # checkpoint-2 ledge, x[72..90] (rise 2.8)
    # --- 3: the ball chute (RAMPS) — mouth is a 1.25 slot, ball only ---
    (86.0, 14.0, 4.0, 7.75),      # chute mouth block: slot 1.25 over the ledge,
                                  #   top 14 is beyond any paper jump
    (156.5, -27.5, 39.5, 6.0),    # pit landing platform, x[156.5..196]
    # --- 4: forced unfold, double-jump checkpoint ---
    (174.0, -24.5, 22.0, 3.0),    # stop wall + checkpoint-3 block (3-tall:
                                  #   paper hops it, the ball never can)
    # --- 5: chimney crossroads — two routes up, floor catches every fall ---
    (196.0, -26.0, 40.0, 5.0),    # climb base floor, x[196..236]
    (204.0, -8.0, 4.0, 18.0),     # left tower, x[204..208]
    (216.0, -2.0, 4.0, 24.0),     # right tower, x[216..220]
    # route A (outside left): fewer, bigger double-jumps — the fast line
    (200.5, -21.5, 2.5, 1.2),     # A1 (rise 4.5)
    (197.0, -16.0, 2.5, 1.2),     # A2 (rise 5.5, double)
    (200.5, -10.5, 2.5, 1.2),     # A3 (rise 5.5, double; left top is +2.5 more)
    # route B (inside the chimney): steady rises of 4, alternating walls
    (208.0, -22.0, 2.5, 1.2),     # B1
    (213.5, -18.0, 2.5, 1.2),     # B2
    (208.0, -14.0, 2.5, 1.2),     # B3
    (213.5, -10.0, 2.5, 1.2),     # B4
    (208.0, -6.0, 2.5, 1.2),      # B5 (right tower top is +4 more)
    # --- 6: squeeze corridor — under as a ball, or double-jump over ---
    (220.0, -2.0, 44.0, 10.0),    # corridor floor, x[220..264] (deep: seals
                                  #   the void above the runway)
    (232.0, 6.0, 18.0, 6.75),     # squeeze block: 1.25 slot over the floor
    (226.0, 1.5, 2.5, 1.2),       # over-route peg (3.5 up, then 4.5 to the top)
    # --- 7: plane runway + the long glide home ---
    (264.0, -10.0, 54.0, 6.0),    # runway, x[264..318] (drop 8 off the corridor)
    (368.0, -22.0, 10.0, 2.5),    # rest-stop island (optional — skipping is faster)
    (428.0, -38.0, 42.0, 6.0),    # final platform, x[428..470]
]

# (x1, y1, x2, y2, thickness) — top surface line, left to right.
# Both ramps are eased polylines: angle steps of <= 10 degrees so the
# composite outline reads as a smooth curve and never bounces the ball.
RAMPS = [
    # intro slide off the perch (11 -> 22 -> 26 -> 13 -> 6 degrees)
    (-7.0, 6.0, -4.0, 5.4, 3.0),
    (-4.0, 5.4, 1.0, 3.4, 3.0),
    (1.0, 3.4, 6.0, 1.0, 3.0),
    (6.0, 1.0, 9.0, 0.3, 3.0),
    (9.0, 0.3, 12.0, 0.0, 3.0),
    # the ball chute: ease in 10 -> 20 -> 32, dive at 36, ease out 28 -> 18,
    # flat valley, then the kicker curls up 11 -> 20 into the launch lip
    (90.0, 5.0, 93.0, 4.471, 3.0),
    (93.0, 4.471, 97.0, 3.015, 3.0),
    (97.0, 3.015, 102.0, -0.109, 3.0),
    (102.0, -0.109, 128.0, -18.999, 3.0),
    (128.0, -18.999, 132.0, -21.126, 3.0),
    (132.0, -21.126, 135.0, -22.101, 3.0),
    (135.0, -22.101, 136.5, -22.101, 3.0),
    (136.5, -22.101, 138.5, -21.712, 3.0),
    (138.5, -21.712, 141.5, -20.620, 3.0),  # lip — gap of 15 to the landing
]

# (pole_x, base_y, unlocksDoubleJump, isGoal)
CHECKPOINTS = [
    (32.0, 0.0, False, False),      # meadow
    (75.0, 5.0, False, False),      # before the chute
    (182.0, -24.5, True, False),    # after the pit — unlocks double jump
    (256.0, -2.0, False, False),    # after the squeeze, before the glide
    (452.0, -38.0, False, True),    # home
]

# (left, top, w, h, message)
KILLZONES = [
    (143.0, -28.0, 13.0, 18.0, "SNIP! Mind the shredder pit..."),
    (318.0, 4.0, 110.0, 3.0, "Too high! The shredder sky hums above..."),
    (472.0, -6.0, 6.0, 40.0, "The world ends here...\nand I hope you're not trying to run away"),
]

# (x, base_y, text)
SIGNS = [
    (-12.0, 6.0, "A crumpled little sheet,\na long way from home..."),
    (16.0, 0.0, "You are PAPER.\nA / D to move, SPACE to jump"),
    (26.0, 0.0, "Little one, you can fold!\nX folds you up · Z unfolds you"),
    (70.5, -2.0, "Stuck?\nPress R to return\nto your checkpoint"),
    (80.0, 5.0, "Only a BALL fits down the chute.\nPress X to fold!"),
    (85.0, 5.0, "Roll with it — speed is the\nonly way across the pit"),
    (168.0, -27.5, "Balls can't jump...\nPress Z to unfold into PAPER"),
    (188.0, -24.5, "Press SPACE in mid-air\nto DOUBLE JUMP!"),
    (211.0, -26.0, "Many ways up!\n(Press O to zoom out, I to zoom in)"),
    (228.0, -2.0, "Tight squeeze? Fold into a BALL (X)\n...or find a way OVER"),
    (302.0, -10.0, "Press C in MID-AIR\nto fold into a PLANE!"),
    (312.0, -10.0, "Glide far, sink slow.\n(Z unfolds you anytime — careful!)"),
    (371.0, -22.0, "Rest stop!\nJump + C to take off again\n(skipping it is faster...)"),
    (458.0, -38.0, "You made it!\nA single sheet can become anything ♥"),
]

PLAYER_START = (-12.0, 7.5)
GLOBAL_KILL_Y = -52.0


# ---------------------------------------------------------------- emit C#
def flat_cs(left, top, w, h):
    return f"        new Plat({left + w / 2:.3f}f, {top - h / 2:.3f}f, {w:.3f}f, {h:.3f}f, 0f),"


def ramp_cs(x1, y1, x2, y2, t):
    dx, dy = x2 - x1, y2 - y1
    length = math.hypot(dx, dy)
    ux, uy = dx / length, dy / length
    # surface midpoint, pushed half a thickness along the downward normal
    nx, ny = uy, -ux
    cx = (x1 + x2) / 2 + nx * t / 2
    cy = (y1 + y2) / 2 + ny * t / 2
    rot = math.degrees(math.atan2(dy, dx))
    return f"        new Plat({cx:.3f}f, {cy:.3f}f, {length:.3f}f, {t:.3f}f, {rot:.2f}f),"


def esc(s):
    return s.replace('"', '\\"').replace("\n", "\\n")


def main():
    lines = []
    lines.append("// GENERATED by tools/gen_leveldata.py — hand-designed level (world units).")
    lines.append("// Edit the python script and re-run it rather than editing this file.")
    lines.append("")
    lines.append("public static class LevelData")
    lines.append("{")
    lines.append("    public struct Plat { public float x, y, w, h, rot;")
    lines.append("        public Plat(float x, float y, float w, float h, float rot) { this.x = x; this.y = y; this.w = w; this.h = h; this.rot = rot; } }")
    lines.append("    public struct Check { public float x, y; public bool doubleJump, goal;")
    lines.append("        public Check(float x, float y, bool dj, bool goal) { this.x = x; this.y = y; doubleJump = dj; this.goal = goal; } }")
    lines.append("    public struct Zone { public float x, y, w, h; public string msg;")
    lines.append("        public Zone(float x, float y, float w, float h, string msg) { this.x = x; this.y = y; this.w = w; this.h = h; this.msg = msg; } }")
    lines.append("    public struct Sign { public float x, y; public string text;")
    lines.append("        public Sign(float x, float y, string text) { this.x = x; this.y = y; this.text = text; } }")
    lines.append("")
    lines.append("    public static readonly Plat[] Platforms =")
    lines.append("    {")
    for f in FLATS:
        lines.append(flat_cs(*f))
    for r in RAMPS:
        lines.append(ramp_cs(*r))
    lines.append("    };")
    lines.append("")
    lines.append("    public static readonly Check[] Checkpoints =")
    lines.append("    {")
    for (x, y, dj, goal) in CHECKPOINTS:
        lines.append(f"        new Check({x:.3f}f, {y:.3f}f, {str(dj).lower()}, {str(goal).lower()}),")
    lines.append("    };")
    lines.append("")
    lines.append("    public static readonly Zone[] KillZones =")
    lines.append("    {")
    for (left, top, w, h, msg) in KILLZONES:
        lines.append(f'        new Zone({left + w / 2:.3f}f, {top - h / 2:.3f}f, {w:.3f}f, {h:.3f}f, "{esc(msg)}"),')
    lines.append("    };")
    lines.append("")
    lines.append("    public static readonly Sign[] Signs =")
    lines.append("    {")
    for (x, y, text) in SIGNS:
        lines.append(f'        new Sign({x:.3f}f, {y:.3f}f, "{esc(text)}"),')
    lines.append("    };")
    lines.append("")
    lines.append(f"    public static readonly float PlayerStartX = {PLAYER_START[0]:.3f}f;")
    lines.append(f"    public static readonly float PlayerStartY = {PLAYER_START[1]:.3f}f;")
    lines.append(f"    public static readonly float GlobalKillY = {GLOBAL_KILL_Y:.1f}f;")
    lines.append("}")
    lines.append("")

    out = os.path.join(os.path.dirname(__file__), "..", "PaperFold", "Assets", "Editor", "LevelData.cs")
    with open(out, "w") as f:
        f.write("\n".join(lines))
    print("wrote", os.path.abspath(out))


if __name__ == "__main__":
    main()
