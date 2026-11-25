# Seven stage-2 tank monster drafts in the robots' colour scheme (white body, greys, charcoal joints, one warm accent).
# Design units: the melee robot stands 1.55 (~118 px in battle). Front is -y. Each build takes its size multiplier (sc).
import math
from toon import rbox, ball, tube, prism, arcplate, blob, toon, flat, begin, group, ungroup, real_robot
from palette import WHITE, LGREY, MGREY, CHAR, ORANGE, CORAL, MAGENTA, YELLOW, CRIMSON, AMBER, RASPBERRY

def m(rgb):
    return toon(rgb)


def face(y, z, w, h, acc, ew=0.075, eh=0.1, gap=None):
    """A charcoal face plate with two small accent eye dots (flat colour, no glow past it)."""
    rbox('visor', (w, 0.05, h), (0, y, z), m(CHAR), r=min(w, h) * 0.42, outline=False)
    g = gap if gap is not None else w * 0.24
    for s in (-1, 1):
        rbox(f'eye{s}', (ew, 0.03, eh), (s * g, y - 0.03, z), flat(acc), r=ew * 0.48, outline=False)


# ------------------------------------------------------------------ 1 방패 돌격병: riot shield, braced push

def t1(sc=0.88, **kw):
    begin('t1', sc, **kw); A = ORANGE
    # braced stance: the pushing leg stretched back on the side the 3/4 view sees, the other bent under the shield
    legs = (((-0.26, 0.16, 0.50), (-0.36, 0.44, 0.30), (-0.40, 0.66, 0.12)),
            ((0.26, 0.12, 0.50), (0.36, -0.14, 0.32), (0.40, -0.22, 0.12)))
    for i, (hip, knee, foot) in enumerate(legs):
        tube(f'thigh{i}', hip, knee, 0.12, m(MGREY))
        ball(f'knee{i}', 0.13, knee, m(CHAR), outline=False)
        tube(f'shin{i}', knee, foot, 0.11, m(MGREY))
        rbox(f'boot{i}', (0.32, 0.44, 0.22), (foot[0], foot[1] - 0.04, 0.11), m(WHITE), r=0.08)
    rbox('hips', (0.70, 0.46, 0.26), (0, 0.14, 0.52), m(MGREY), r=0.08)
    group('upper', (0, 0.14, 0.56), (12, 0, 0))           # leaning into the shield
    rbox('torso', (0.88, 0.60, 0.56), (0, -0.04, 0.30), m(WHITE), r=0.14)
    rbox('pack', (0.62, 0.26, 0.46), (0, 0.36, 0.34), m(LGREY), r=0.10)
    for s in (-1, 1):
        rbox(f'pad{s}', (0.36, 0.56, 0.30), (s * 0.56, -0.04, 0.52), m(A), r=0.12)
        tube(f'arm{s}', (s * 0.50, -0.10, 0.38), (s * 0.34, -0.50, 0.30), 0.08, m(CHAR))
    rbox('head', (0.60, 0.52, 0.46), (0, -0.16, 0.78), m(WHITE), r=0.15)
    face(-0.43, 0.80, 0.46, 0.20, A, ew=0.09, eh=0.12)
    ungroup()
    # the tall shield, a little curved, with a thick orange rim and band (centre of curvature at y = 0.68)
    arcplate('rim', (1.22, 0.06, 1.08), (0, -0.58, 0.60), m(A), bend=1.26, r=0.05)
    arcplate('shield', (1.12, 0.09, 0.98), (0, -0.62, 0.60), m(WHITE), bend=1.30, r=0.05)
    arcplate('band', (1.06, 0.04, 0.20), (0, -0.675, 0.82), m(A), bend=1.355, r=0.02, outline=False)
    blob('blob', 0.85, 0.75)


# ------------------------------------------------------------------ 2 블록 골렘: stacked blocks, knuckles down

def t2(sc=0.76, **kw):
    begin('t2', sc, **kw); A = CORAL
    for s in (-1, 1):
        rbox(f'leg{s}', (0.30, 0.32, 0.30), (s * 0.30, 0.24, 0.26), m(MGREY), r=0.08)
        rbox(f'foot{s}', (0.38, 0.48, 0.16), (s * 0.30, 0.20, 0.08), m(WHITE), r=0.06)
    rbox('pelvis', (0.74, 0.50, 0.26), (0, 0.24, 0.46), m(MGREY), r=0.08)
    group('torso', (0, 0.20, 0.50), (8, 0, 0))
    rbox('torso', (1.00, 0.74, 0.70), (0, -0.04, 0.38), m(WHITE), r=0.16)
    rbox('chest', (0.60, 0.05, 0.26), (0, -0.42, 0.26), m(LGREY), r=0.06, outline=False)
    for s in (-1, 1):
        rbox(f'shoulder{s}', (0.50, 0.58, 0.48), (s * 0.66, -0.04, 0.60), m(A), r=0.12)
    # the head sits forward between the shoulders, its face toward the high battle camera
    rbox('head', (0.50, 0.44, 0.42), (0, -0.40, 0.70), m(WHITE), r=0.14)
    face(-0.63, 0.70, 0.40, 0.18, A, ew=0.08, eh=0.11)
    ungroup()
    for s in (-1, 1):   # fists planted just in front, so arms, shoulders and fists read as one block
        tube(f'arm{s}', (s * 0.68, -0.14, 0.90), (s * 0.64, -0.52, 0.48), 0.18, m(MGREY), segs=12)
        tube(f'wrist{s}', (s * 0.64, -0.53, 0.40), (s * 0.64, -0.54, 0.52), 0.20, m(CHAR), segs=12)
        rbox(f'fist{s}', (0.56, 0.54, 0.46), (s * 0.64, -0.56, 0.23), m(WHITE), r=0.13)
        rbox(f'knuckle{s}', (0.48, 0.10, 0.16), (s * 0.64, -0.85, 0.31), m(LGREY), r=0.05)
    blob('blob', 1.15, 0.8)


# ------------------------------------------------------------------ 3 돔 거북: faceted dome shell on four stumps

def t3(sc=0.95, **kw):
    begin('t3', sc, **kw); A = MAGENTA
    for sx in (-1, 1):
        for sy in (-1, 1):
            tube(f'leg{sx}{sy}', (sx * 0.46, sy * 0.36, 0.0), (sx * 0.46, sy * 0.36, 0.32), 0.17, m(WHITE), bevel=0.03, segs=12)
            tube(f'cuff{sx}{sy}', (sx * 0.46, sy * 0.36, 0.30), (sx * 0.46, sy * 0.36, 0.38), 0.15, m(CHAR), segs=12)
    tube('belly', (0, 0, 0.28), (0, 0, 0.42), 0.66, m(MGREY), segs=12)
    tube('rim', (0, 0.04, 0.36), (0, 0.04, 0.50), 0.80, m(LGREY), bevel=0.04, segs=12)
    ball('dome', 0.76, (0, 0.04, 0.48), m(WHITE), scale=(1, 0.92, 0.78), seg=(12, 8), smooth=False, half=True)
    tube('crown', (0, 0.04, 0.94), (0, 0.04, 1.12), 0.42, m(A), bevel=0.04, segs=6)   # one big hexagonal scute
    tube('neck', (0, -0.50, 0.42), (0, -0.68, 0.44), 0.12, m(CHAR))
    rbox('head', (0.46, 0.42, 0.36), (0, -0.80, 0.44), m(WHITE), r=0.14)
    rbox('brow', (0.52, 0.36, 0.10), (0, -0.78, 0.64), m(A), r=0.05)
    face(-1.01, 0.43, 0.34, 0.15, A, ew=0.065, eh=0.09)
    blob('blob', 0.95, 0.95)


# ------------------------------------------------------------------ 4 불도저: tracks and a big pushing blade

def t4(sc=0.73, **kw):
    begin('t4', sc, **kw); A = YELLOW
    for s in (-1, 1):
        rbox(f'track{s}', (0.36, 1.10, 0.44), (s * 0.58, 0.06, 0.22), m(MGREY), r=0.19, seg=3)
        for y in (-0.24, 0.06, 0.36):
            tube(f'wheel{s}{y}', (s * 0.75, y, 0.22), (s * 0.78, y, 0.22), 0.12, m(LGREY), segs=12, outline=False)
        tube(f'push{s}', (s * 0.40, -0.20, 0.46), (s * 0.50, -0.60, 0.34), 0.08, m(CHAR))
    rbox('body', (0.86, 0.92, 0.46), (0, 0.10, 0.54), m(WHITE), r=0.12)
    rbox('cab', (0.66, 0.56, 0.48), (0, 0.22, 1.00), m(WHITE), r=0.14)
    rbox('roof', (0.72, 0.62, 0.09), (0, 0.22, 1.26), m(A), r=0.04)
    face(-0.08, 1.01, 0.52, 0.21, A, ew=0.085, eh=0.115)
    tube('stack', (0.30, 0.46, 0.74), (0.30, 0.46, 1.16), 0.06, m(LGREY), segs=12)
    arcplate('blade', (1.72, 0.10, 0.62), (0, -0.68, 0.34), m(A), bend=-1.5, r=0.04)
    blob('blob', 1.0, 0.9)


# ------------------------------------------------------------------ 5 중갑 기사: big layered pauldrons, squat stance

def t5(sc=0.84, **kw):
    begin('t5', sc, **kw); A = CRIMSON
    for s in (-1, 1):
        rbox(f'boot{s}', (0.36, 0.50, 0.24), (s * 0.34, -0.04, 0.12), m(WHITE), r=0.10)
        tube(f'leg{s}', (s * 0.33, 0.0, 0.22), (s * 0.28, 0.04, 0.52), 0.13, m(MGREY))
    rbox('fauld', (0.86, 0.62, 0.24), (0, 0.04, 0.52), m(LGREY), r=0.10)
    rbox('torso', (0.96, 0.80, 0.60), (0, 0.04, 0.86), m(WHITE), r=0.18, taper=(1.12, 1.05))
    for s in (-1, 1):
        tube(f'arm{s}', (s * 0.62, 0.04, 0.92), (s * 0.70, -0.04, 0.62), 0.11, m(MGREY))
        rbox(f'gaunt{s}', (0.34, 0.36, 0.36), (s * 0.72, -0.06, 0.50), m(WHITE), r=0.12)
        rbox(f'cuff{s}', (0.38, 0.40, 0.10), (s * 0.72, -0.06, 0.70), m(A), r=0.04)
        ball(f'pad_lo{s}', 0.40, (s * 0.64, 0.04, 0.90), m(LGREY), scale=(1, 1.0, 0.62), seg=(16, 10), half=True)
        ball(f'pad{s}', 0.36, (s * 0.60, 0.04, 1.00), m(A), scale=(1, 1.02, 0.78), seg=(16, 10), half=True)
        ball(f'stud{s}', 0.08, (s * 0.60, 0.04, 1.28), m(LGREY), seg=(10, 6))
    rbox('helm', (0.50, 0.50, 0.50), (0, 0.0, 1.30), m(WHITE), r=0.17)
    face(-0.255, 1.28, 0.36, 0.16, A)
    for s in (-1, 1):
        tube(f'horn{s}', (s * 0.20, 0.0, 1.46), (s * 0.36, 0.0, 1.66), 0.075, m(LGREY), r1=0.035, segs=12)
        ball(f'tip{s}', 0.04, (s * 0.36, 0.0, 1.66), m(LGREY), seg=(10, 6))
    blob('blob', 0.95, 0.7)


# ------------------------------------------------------------------ 6 무게추: a walking ton weight

def t6(sc=0.90, **kw):
    begin('t6', sc, **kw); A = AMBER
    for sx in (-1, 1):
        for sy in (-1, 1):
            rbox(f'foot{sx}{sy}', (0.30, 0.32, 0.20), (sx * 0.46, sy * 0.26, 0.10), m(MGREY), r=0.08)
    rbox('body', (1.50, 1.00, 0.78), (0, 0, 0.59), m(WHITE), r=0.12, taper=(0.68, 0.70))
    rbox('band', (1.56, 1.06, 0.20), (0, 0, 0.29), m(A), r=0.07, taper=(0.95, 0.94))
    rbox('top', (1.08, 0.76, 0.08), (0, 0, 0.99), m(LGREY), r=0.04)
    ro, ri, n = 0.34, 0.18, 10
    arch = [(ro * math.cos(t), ro * math.sin(t)) for t in (math.pi * i / n for i in range(n + 1))]
    arch += [(ri * math.cos(t), ri * math.sin(t)) for t in (math.pi * (n - i) / n for i in range(n + 1))]
    prism('handle', [(x, z) for x, z in arch], 0.16, (0, 0, 1.0), m(MGREY), r=0.02)
    group('front', (0, -0.43, 0.62), (-10.9, 0, 0))       # on the sloped front face
    face(0, 0, 0.66, 0.24, A, ew=0.085, eh=0.12)
    ungroup()
    blob('blob', 0.95, 0.75)


# ------------------------------------------------------------------ 7 씨름 장사: round heavy body, wide low stance, belt

def t7(sc=0.87, **kw):
    begin('t7', sc, **kw); A = RASPBERRY
    for s in (-1, 1):
        hip, knee, foot = (s * 0.30, 0.04, 0.46), (s * 0.58, -0.06, 0.32), (s * 0.62, 0.0, 0.12)
        tube(f'thigh{s}', hip, knee, 0.15, m(MGREY), segs=12)
        ball(f'knee{s}', 0.15, knee, m(CHAR), seg=(12, 8), outline=False)
        tube(f'shin{s}', knee, foot, 0.13, m(MGREY), segs=12)
        rbox(f'foot{s}', (0.38, 0.48, 0.20), (s * 0.64, -0.04, 0.10), m(WHITE), r=0.08)
    ball('body', 0.60, (0, 0.04, 0.86), m(WHITE), scale=(1.18, 0.95, 0.92), seg=(16, 10), smooth=False)
    ball('belt', 0.64, (0, 0.04, 0.56), m(A), scale=(1.12, 0.92, 0.25), seg=(16, 6), smooth=False)
    rbox('knot', (0.28, 0.14, 0.28), (0, -0.56, 0.54), m(A), r=0.07)
    for s in (-1, 1):
        ball(f'shoulder{s}', 0.21, (s * 0.62, 0.02, 1.06), m(LGREY), seg=(12, 8))
        tube(f'arm{s}', (s * 0.64, 0.0, 1.02), (s * 0.46, -0.58, 0.88), 0.13, m(MGREY), segs=12)
        rbox(f'palm{s}', (0.32, 0.20, 0.36), (s * 0.44, -0.66, 0.88), m(WHITE), r=0.10)   # pushing palms forward
    rbox('head', (0.50, 0.46, 0.40), (0, -0.22, 1.40), m(WHITE), r=0.15)
    face(-0.455, 1.39, 0.38, 0.16, A)
    blob('blob', 0.95, 0.75)


def robot_melee(**kw):
    real_robot('melee', **kw)


def robot_tank(**kw):
    real_robot('tank', frame=30, **kw)   # its guard pose, shield up front


ALL = {'t1': t1, 't2': t2, 't3': t3, 't4': t4, 't5': t5, 't6': t6, 't7': t7,
       'robot_melee': robot_melee, 'robot_tank': robot_tank}
