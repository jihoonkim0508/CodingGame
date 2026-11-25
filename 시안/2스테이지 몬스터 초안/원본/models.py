# The eight stage-2 monster drafts (A: forked-antenna bug-bots, B: flowchart error symbols) and a white/blue robot
# stand-in for scale. Design units: the robot is 1.55 tall; each build() takes its own size multiplier (SC).
from toon import rbox, ball, tube, disc, prism, text, blob, toon, flat, begin
from palette import CORAL, PEACH, SLATE, VISOR, BUTTER, LILAC, LAVENDER, POWDER, EYE, WHITE, BLUE, GREY


def eyes(x, y, z, w=0.12, h=0.18, mat=None):
    for s in (-1, 1):
        rbox(f'eye{s}', (w, 0.03, h), (s * x, y, z), mat or flat(EYE), r=w * 0.48, seg=4, outline=False)


def yfork(base, stem, spread, rise, r, tips, name='ant'):
    """The if/else antenna: a stem that splits into two, a ball on each branch."""
    bx, by, bz = base
    top = (bx, by, bz + stem)
    tube(name + 's', base, top, r, toon(SLATE))
    ball(name + 'j', r * 1.35, top, toon(SLATE))
    for s, c in zip((-1, 1), tips):
        end = (bx + s * spread, by, bz + stem + rise)
        tube(name + f'b{s}', top, end, r * 0.9, toon(SLATE))
        ball(name + f't{s}', r * 2.0, end, toon(c))


# ------------------------------------------------------------------ A: 갈림길 버그봇

def normal_a(sc=0.7, **kw):
    begin('normal_a', sc, **kw)
    rbox('belly', (0.86, 0.72, 0.30), (0, 0, 0.44), toon(PEACH), r=0.12)
    rbox('head', (0.98, 0.82, 0.58), (0, 0, 0.84), toon(CORAL), r=0.17)
    rbox('visor', (0.70, 0.06, 0.32), (0, -0.405, 0.85), toon(VISOR), r=0.09, outline=False)
    eyes(0.16, -0.44, 0.86)
    for s in (-1, 1):
        tube(f'leg{s}', (s * 0.21, 0, 0.32), (s * 0.21, -0.02, 0.10), 0.075, toon(SLATE))
        rbox(f'foot{s}', (0.22, 0.30, 0.11), (s * 0.21, -0.05, 0.055), toon(PEACH), r=0.05)
        tube(f'arm{s}', (s * 0.46, 0, 0.62), (s * 0.60, -0.06, 0.52), 0.06, toon(SLATE))
        ball(f'hand{s}', 0.08, (s * 0.62, -0.07, 0.50), toon(PEACH))
    yfork((0, 0, 1.12), 0.17, 0.15, 0.15, 0.035, (BUTTER, LILAC))
    blob('blob', 0.62, 0.52)


def fast_a(sc=0.68, **kw):
    begin('fast_a', sc, **kw)
    rbox('body', (0.64, 0.92, 0.44), (0, 0.02, 0.66), toon(CORAL), r=0.17)
    rbox('belly', (0.50, 0.74, 0.14), (0, 0.04, 0.43), toon(PEACH), r=0.06)
    rbox('visor', (0.48, 0.06, 0.24), (0, -0.44, 0.67), toon(VISOR), r=0.08, outline=False)
    eyes(0.11, -0.475, 0.68, w=0.09, h=0.14)
    for s in (-1, 1):
        ball(f'wing{s}', 0.30, (s * 0.36, 0.26, 0.93), toon(LILAC), scale=(1.0, 0.55, 0.12), rot=(0, -s * 20, s * 32))
        tube(f'thr{s}', (s * 0.33, 0.10, 0.50), (s * 0.37, 0.22, 0.34), 0.08, toon(SLATE), r1=0.095)
    tube('tail', (0, 0.46, 0.66), (0, 0.72, 0.70), 0.12, toon(PEACH), r1=0.035)
    yfork((0, -0.2, 0.88), 0.12, 0.10, 0.10, 0.028, (BUTTER, LILAC))
    blob('blob', 0.45, 0.6)


def tank_a(sc=0.8, **kw):
    begin('tank_a', sc, **kw)
    rbox('body', (1.46, 1.12, 0.40), (0, 0.06, 0.46), toon(SLATE), r=0.14)
    cols = [[CORAL, PEACH, CORAL], [PEACH, BUTTER, PEACH]]
    for j, y in enumerate((-0.18, 0.32)):
        for i, x in enumerate((-0.46, 0, 0.46)):
            rbox(f'blk{i}{j}', (0.42, 0.46, 0.24), (x, y, 0.76), toon(cols[j][i]), r=0.08)
    rbox('head', (0.64, 0.40, 0.40), (0, -0.62, 0.52), toon(CORAL), r=0.13)
    rbox('visor', (0.48, 0.06, 0.20), (0, -0.82, 0.53), toon(VISOR), r=0.07, outline=False)
    eyes(0.11, -0.855, 0.54, w=0.08, h=0.11)
    yfork((0, -0.62, 0.72), 0.10, 0.10, 0.09, 0.03, (BUTTER, LILAC))
    for sx in (-1, 1):
        for sy in (-1, 1):
            tube(f'leg{sx}{sy}', (sx * 0.56, sy * 0.34 + 0.06, 0.34), (sx * 0.66, sy * 0.40 + 0.06, 0.11), 0.11, toon(SLATE))
            rbox(f'foot{sx}{sy}', (0.28, 0.32, 0.11), (sx * 0.67, sy * 0.40 + 0.03, 0.055), toon(PEACH), r=0.05)
    blob('blob', 0.95, 0.8)


def boss_a(sc=0.86, **kw):
    begin('boss_a', sc, **kw)
    rbox('belly', (1.44, 1.16, 0.42), (0, 0, 0.72), toon(PEACH), r=0.16)
    rbox('head', (1.66, 1.30, 0.92), (0, 0, 1.32), toon(CORAL), r=0.26)
    rbox('visor', (1.16, 0.08, 0.50), (0, -0.645, 1.34), toon(VISOR), r=0.14, outline=False)
    eyes(0.27, -0.69, 1.36, w=0.17, h=0.27)
    # branching-tree crown: a decision diamond, two branches, four leaves
    tube('stem', (0, 0, 1.76), (0, 0, 2.02), 0.08, toon(SLATE))
    prism('node', dia(0.24), 0.20, (0, 0, 2.18), toon(LILAC), r=0.05)
    tips = [BUTTER, PEACH, LILAC, BUTTER]
    k = 0
    for s in (-1, 1):
        mid = (s * 0.58, 0, 2.42)
        tube(f'br{s}', (s * 0.16, 0, 2.24), mid, 0.065, toon(SLATE))
        ball(f'bj{s}', 0.09, mid, toon(SLATE))
        for t in (-1, 1):
            end = (s * 0.58 + t * 0.24, 0, 2.74)
            tube(f'tw{s}{t}', mid, end, 0.055, toon(SLATE))
            ball(f'lf{s}{t}', 0.14, end, toon(tips[k])); k += 1
    for s in (-1, 1):
        for i, y in enumerate((-0.38, 0.06, 0.48)):
            hip, knee, foot = (s * 0.70, y, 0.74), (s * 1.02, y * 1.12, 0.66), (s * 1.10, y * 1.2, 0.10)
            tube(f'th{s}{i}', hip, knee, 0.12, toon(SLATE))
            ball(f'kn{s}{i}', 0.15, knee, toon(PEACH))
            tube(f'sh{s}{i}', knee, foot, 0.11, toon(SLATE), r1=0.10)
            rbox(f'ft{s}{i}', (0.24, 0.26, 0.10), (s * 1.12, y * 1.2, 0.05), toon(PEACH), r=0.045)
    blob('blob', 1.45, 1.1)


# ------------------------------------------------------------------ B: 순서도 오류 기호

def normal_b(sc=0.72, **kw):
    begin('normal_b', sc, **kw)
    rbox('box', (0.92, 0.82, 0.64), (0, 0, 0.62), toon(LAVENDER), r=0.12)
    rbox('inside', (0.80, 0.70, 0.04), (0, 0.0, 0.945), toon(VISOR), r=0.02, outline=False)
    rbox('lid', (1.00, 0.90, 0.13), (0, 0.06, 1.05), toon(POWDER), r=0.06, rot=(-13, 0, 0))
    rbox('tag', (0.40, 0.34, 0.04), (0, 0.04, 1.135), toon(BUTTER), r=0.08, rot=(-13, 0, 0), outline=False)
    # turned 45°: seen from about 40° off the front (sheet 3/4 view, battle camera) it reads as an x, not a +
    text('x', 'x', 0.34, (0, 0.04, 1.165), toon(VISOR), rot=(-13, 0, 45), depth=0.01)
    rbox('visor', (0.68, 0.06, 0.30), (0, -0.405, 0.64), toon(VISOR), r=0.09, outline=False)
    eyes(0.15, -0.44, 0.65, w=0.13, h=0.13)
    for s in (-1, 1):
        tube(f'leg{s}', (s * 0.22, 0, 0.32), (s * 0.22, -0.02, 0.10), 0.075, toon(SLATE))
        rbox(f'foot{s}', (0.22, 0.30, 0.11), (s * 0.22, -0.05, 0.055), toon(POWDER), r=0.05)
        tube(f'arm{s}', (s * 0.44, 0, 0.58), (s * 0.58, -0.06, 0.46), 0.06, toon(SLATE))
        ball(f'hand{s}', 0.08, (s * 0.60, -0.07, 0.44), toon(POWDER))
    blob('blob', 0.62, 0.52)


DIAMOND = [(0, 1), (1, 0), (0, -1), (-1, 0)]


def dia(r):
    return [(x * r, z * r) for x, z in DIAMOND]


def fast_b(sc=0.72, **kw):
    begin('fast_b', sc, **kw)
    prism('body', dia(0.44), 0.46, (0, 0.02, 0.80), toon(LAVENDER), r=0.07)
    prism('face', dia(0.28), 0.03, (0, -0.235, 0.80), toon(VISOR), r=0.0, outline=False)
    eyes(0.085, -0.255, 0.80, w=0.08, h=0.11)
    ball('lamp', 0.07, (0, 0.02, 1.27), flat(BUTTER))
    for s, c, letter in ((-1, BUTTER, 'Y'), (1, PEACH, 'N')):
        prism(f'fin{s}', [(0, 0.17), (s * 0.50, 0.10), (s * 0.44, -0.15), (0, -0.13)], 0.06, (s * 0.36, 0.14, 0.80), toon(c), r=0.03, rot=(0, 0, s * 32))
        text(f'l{s}', letter, 0.19, (s * 0.60, 0.255, 0.80), toon(VISOR), rot=(90, 0, s * 32))
        tube(f'thr{s}', (s * 0.30, 0.08, 0.56), (s * 0.33, 0.22, 0.42), 0.07, toon(SLATE), r1=0.085)
    blob('blob', 0.5, 0.42)


def tank_b(sc=0.8, **kw):
    begin('tank_b', sc, **kw)
    rbox('body', (1.36, 0.92, 0.48), (0, 0.22, 0.56), toon(LAVENDER), r=0.18)
    # leans back so the warning sign still faces the high battle camera
    prism('shield', [(-0.64, 0), (0.64, 0), (0, 0.86)], 0.14, (0, -0.62, 0.16), toon(BUTTER), r=0.11, rot=(-20, 0, 0))
    text('warn', '!', 0.46, (0, -0.59, 0.47), toon(CORAL), rot=(70, 0, 0), depth=0.02)
    for s in (-1, 1):
        tube(f'stalk{s}', (s * 0.26, 0.1, 0.78), (s * 0.30, 0.04, 1.10), 0.045, toon(SLATE))
        ball(f'eye{s}', 0.11, (s * 0.30, 0.03, 1.14), toon(POWDER))
        disc(f'pupil{s}', 0.05, (s * 0.30, -0.075, 1.14), flat(VISOR))
        tube(f'arm{s}', (s * 0.62, 0.05, 0.56), (s * 0.80, -0.30, 0.46), 0.07, toon(SLATE))
        for t in (-1, 1):
            rbox(f'pin{s}{t}', (0.10, 0.26, 0.10), (s * 0.82 + t * 0.07, -0.46, 0.46), toon(POWDER), r=0.045, rot=(0, 0, -t * 18))
        for i, y in enumerate((0.06, 0.32, 0.58)):
            hip, knee, foot = (s * 0.62, y, 0.48), (s * 0.84, y, 0.40), (s * 0.90, y, 0.08)
            tube(f'th{s}{i}', hip, knee, 0.095, toon(SLATE))
            ball(f'kn{s}{i}', 0.11, knee, toon(LAVENDER))
            tube(f'sh{s}{i}', knee, foot, 0.09, toon(SLATE), r1=0.08)
            ball(f'ft{s}{i}', 0.09, (s * 0.90, y, 0.07), toon(POWDER), scale=(1.2, 1.2, 0.8))
    blob('blob', 1.15, 0.75)


def boss_b(sc=0.76, **kw):
    begin('boss_b', sc, **kw)
    rbox('body', (1.52, 1.06, 1.10), (0, 0, 1.12), toon(LAVENDER), r=0.24)
    rbox('chest', (0.70, 0.05, 0.34), (0, -0.53, 1.14), toon(BUTTER), r=0.08, outline=False)
    text('nan', 'NaN', 0.22, (0, -0.555, 1.14), toon(VISOR))
    prism('head', dia(0.66), 0.64, (0, 0, 2.22), toon(POWDER), r=0.11)
    prism('face', dia(0.43), 0.03, (0, -0.325, 2.22), toon(VISOR), r=0.0, outline=False)
    eyes(0.14, -0.345, 2.22, w=0.12, h=0.18)
    ball('lamp', 0.11, (0, 0, 2.94), flat(BUTTER))
    for s in (-1, 1):
        ball(f'sh{s}', 0.25, (s * 0.84, 0, 1.52), toon(POWDER))
        tube(f'ua{s}', (s * 0.88, 0, 1.44), (s * 1.08, -0.10, 1.06), 0.13, toon(SLATE))
        ball(f'el{s}', 0.15, (s * 1.08, -0.10, 1.06), toon(LAVENDER))
        tube(f'fa{s}', (s * 1.08, -0.10, 1.06), (s * 1.12, -0.20, 0.78), 0.12, toon(SLATE))
        ball(f'wr{s}', 0.12, (s * 1.12, -0.20, 0.76), toon(SLATE))
        for t, c in ((-1, BUTTER), (1, PEACH)):
            end = (s * 1.12 + t * 0.20, -0.26, 0.50)
            tube(f'pr{s}{t}', (s * 1.12, -0.20, 0.76), end, 0.07, toon(SLATE))
            ball(f'tp{s}{t}', 0.13, end, toon(c))
        tube(f'leg{s}', (s * 0.40, 0, 0.64), (s * 0.44, -0.02, 0.18), 0.19, toon(SLATE))
        rbox(f'foot{s}', (0.52, 0.66, 0.20), (s * 0.44, -0.08, 0.10), toon(POWDER), r=0.08)
    blob('blob', 1.35, 0.95)


# ------------------------------------------------------------------ robot stand-in (Warrior-like)

def robot(sc=1.0, **kw):
    begin('robot', sc, **kw)
    tube('ring', (0, 0, 0.0), (0, 0, 0.12), 0.46, toon(BLUE), bevel=0.03)
    tube('base', (0, 0, 0.08), (0, 0, 0.20), 0.40, toon(WHITE), bevel=0.04)
    tube('body', (0, 0, 0.20), (0, 0, 0.50), 0.30, toon(WHITE), r1=0.26, bevel=0.04)
    tube('neck', (0, 0, 0.48), (0, 0, 0.60), 0.15, toon(GREY))
    rbox('head', (0.70, 0.66, 0.64), (0, 0, 0.92), toon(WHITE), r=0.12)
    tube('lensring', (0, -0.32, 0.91), (0, -0.39, 0.91), 0.21, toon(GREY))
    disc('lens', 0.14, (0, -0.395, 0.91), toon((150, 160, 175)), depth=0.01)
    for s in (-1, 1):
        rbox(f'ear{s}', (0.06, 0.32, 0.32), (s * 0.36, 0, 0.92), toon(BLUE), r=0.025)
    tube('ant', (0.12, 0.12, 1.24), (0.12, 0.12, 1.48), 0.035, toon(GREY))
    blob('blob', 0.62, 0.62)


ALL = {
    'normal_a': normal_a, 'fast_a': fast_a, 'tank_a': tank_a, 'boss_a': boss_a,
    'normal_b': normal_b, 'fast_b': fast_b, 'tank_b': tank_b, 'boss_b': boss_b,
    'robot': robot,
}
