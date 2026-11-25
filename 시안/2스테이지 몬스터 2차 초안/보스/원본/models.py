# The five stage-2 boss drafts and a grunt-sized block for scale. Design units: the real robot stands 1.55 tall.
# Front faces -y. Every boss is built at sc=1 and stands about 3.3-3.8 tall (2.1-2.5x the robot, 3x+ a grunt).
import math
from toon import rbox, ball, tube, disc, prism, sweep, sheet, blob, toon, flat, begin
from palette import WHITE, LGREY, MGREY, CHAR, SLATE, RED, GOLD, ORANGE, MAGENTA, YELLOW, PURPLE, EYE


def eyes(x, y, z, w=0.13, h=0.12, tilt=14, mat=None):
    """Two rounded eye slots, tilted inward at the top: stern, still toy-like."""
    for s in (-1, 1):
        rbox(f'eye{s}', (w, 0.03, h), (s * x, y, z), mat or flat(EYE), r=min(w, h) * 0.45, rot=(0, s * tilt, 0), outline=False)


# ------------------------------------------------------------------ 1. 철갑 대왕: armoured king, sword planted, red cape

def king(sc=1.0, **kw):
    begin('king', sc, **kw)
    W, R, C, G, L = toon(WHITE), toon(RED), toon(CHAR), toon(GOLD), toon(LGREY)
    for s in (-1, 1):
        rbox(f'boot{s}', (0.46, 0.66, 0.28), (s * 0.36, -0.06, 0.14), W, r=0.11)
        tube(f'leg{s}', (s * 0.34, 0, 0.26), (s * 0.34, 0, 0.72), 0.16, C)
    tube('skirt', (0, 0, 0.56), (0, 0, 1.10), 0.70, L, r1=0.54, bevel=0.05)
    tube('belt', (0, 0, 1.06), (0, 0, 1.20), 0.58, C)
    rbox('torso', (1.26, 0.92, 0.88), (0, 0, 1.60), W, r=0.26)
    prism('emblem', [(0, 0.16), (0.14, 0), (0, -0.16), (-0.14, 0)], 0.06, (0, -0.47, 1.62), R, r=0.03)
    tube('collar', (0, 0, 1.96), (0, 0, 2.10), 0.50, L, r1=0.42, bevel=0.04)
    for s in (-1, 1):
        ball(f'pauldron{s}', 0.44, (s * 0.80, 0.02, 1.98), W, scale=(1.15, 1.0, 0.78))
        tube(f'prim{s}', (s * 0.80, 0.02, 1.80), (s * 0.80, 0.02, 1.88), 0.48, L)
        tube(f'uarm{s}', (s * 0.86, 0, 1.76), (s * 0.66, -0.40, 1.30), 0.14, C)
        ball(f'elbow{s}', 0.17, (s * 0.66, -0.40, 1.30), C)
        tube(f'gaunt{s}', (s * 0.66, -0.40, 1.30), (s * 0.30, -0.80, 1.12), 0.18, W, r1=0.20, bevel=0.04)
        ball(f'hand{s}', 0.19, (s * 0.24, -0.84, 1.10), W)
    # the greatsword, point down in front, hands on the crossguard
    prism('blade', [(-0.22, 1.00), (0.22, 1.00), (0.22, 0.30), (0, 0.02), (-0.22, 0.30)], 0.10, (0, -0.86, 0), L, r=0.03)
    rbox('guard', (0.86, 0.18, 0.15), (0, -0.86, 1.02), G, r=0.06)
    tube('grip', (0, -0.86, 1.08), (0, -0.86, 1.30), 0.06, C)
    ball('pommel', 0.11, (0, -0.86, 1.36), G)
    # helmet, visor, crown
    rbox('helm', (0.84, 0.80, 0.76), (0, -0.02, 2.40), W, r=0.24)
    rbox('visor', (0.64, 0.06, 0.26), (0, -0.41, 2.36), toon(CHAR), r=0.11, outline=False)
    eyes(0.15, -0.445, 2.37, w=0.15, h=0.11, tilt=16)
    tube('crownband', (0, -0.02, 2.72), (0, -0.02, 2.90), 0.40, G, r1=0.42, bevel=0.03)
    for i in range(5):
        a = math.radians(i * 72 - 90)          # spike 0 at the front, the tallest
        x, y = math.cos(a) * 0.36, math.sin(a) * 0.36 - 0.02
        h = 0.40 if i == 0 else 0.30
        tube(f'spike{i}', (x, y, 2.86), (x * 1.12, y * 1.12 - 0.002, 2.86 + h), 0.10, G, r1=0.025)
        ball(f'spiketip{i}', 0.065, (x * 1.12, y * 1.12, 2.88 + h), G)
    ball('gem', 0.08, (0, -0.44, 2.81), R, scale=(1, 0.6, 1))
    sheet('cape', 2.02, 0.04, 0.66, 1.20, 98, R, y=0.10, thick=0.07, fold=0.05)
    sheet('collar', 2.04, 2.78, 0.50, 0.70, 66, R, y=0.04, thick=0.06, rows=3, cols=10)   # stand-up collar framing the helmet
    blob('blob', 1.35, 1.2)


# ------------------------------------------------------------------ 2. 중장 메카: heavy mech, cannon arm + crusher fist, one big eye

def mech(sc=1.0, **kw):
    begin('mech', sc, **kw)
    O, W, C, M = toon(ORANGE), toon(WHITE), toon(CHAR), toon(MGREY)
    for s in (-1, 1):
        rbox(f'foot{s}', (0.64, 0.92, 0.30), (s * 0.56, -0.10, 0.15), M, r=0.10)
        rbox(f'shin{s}', (0.46, 0.52, 0.56), (s * 0.56, -0.02, 0.56), W, r=0.12)
        rbox(f'knee{s}', (0.36, 0.16, 0.34), (s * 0.56, -0.30, 0.80), O, r=0.08)
        tube(f'thigh{s}', (s * 0.52, 0, 1.24), (s * 0.56, -0.02, 0.84), 0.17, C)
    rbox('pelvis', (0.96, 0.66, 0.34), (0, 0.02, 1.26), C, r=0.10)
    rbox('chest', (1.76, 1.22, 1.04), (0, 0.06, 1.90), O, r=0.28)
    rbox('chestplate', (0.98, 0.10, 0.52), (0, -0.56, 1.80), W, r=0.12)
    for s in (-1, 1):
        tube(f'stack{s}', (s * 0.42, 0.52, 2.20), (s * 0.46, 0.62, 3.02), 0.13, M)
        tube(f'stackcap{s}', (s * 0.46, 0.62, 2.96), (s * 0.465, 0.625, 3.10), 0.17, C)
    # cockpit head sunk between the shoulders, one big eye
    rbox('head', (0.80, 0.72, 0.56), (0, -0.30, 2.54), W, r=0.20)
    tube('lensring', (0, -0.62, 2.54), (0, -0.70, 2.54), 0.22, C)
    disc('lens', 0.15, (0, -0.705, 2.54), flat(EYE), depth=0.01)
    rbox('crest', (0.12, 0.56, 0.20), (0, -0.30, 2.86), O, r=0.05)
    for s in (-1, 1):
        rbox(f'shoulder{s}', (0.80, 0.92, 0.70), (s * 1.18, 0.04, 2.26), W, r=0.22, rot=(0, s * -8, 0))
        rbox(f'sband{s}', (0.82, 0.94, 0.16), (s * 1.18, 0.04, 2.06), O, r=0.06, rot=(0, s * -8, 0))
    # missile pod on the left shoulder
    rbox('pod', (0.60, 0.62, 0.44), (-1.22, 0.10, 2.80), C, r=0.10)
    for i, (dx, dz) in enumerate(((-0.13, 0.09), (0.13, 0.09), (-0.13, -0.09), (0.13, -0.09))):
        ball(f'missile{i}', 0.085, (-1.22 + dx, -0.21, 2.80 + dz), O)
    # right: arm cannon pointing forward
    tube('uarmR', (1.22, 0, 2.00), (1.30, -0.10, 1.50), 0.16, C)
    ball('elbowR', 0.20, (1.30, -0.10, 1.50), M)
    tube('cannon', (1.30, 0.10, 1.46), (1.30, -1.06, 1.30), 0.27, W, r1=0.25, bevel=0.05)
    for i, t in enumerate((0.35, 0.75)):
        y = 0.10 - 1.16 * t; z = 1.46 - 0.16 * t
        tube(f'cband{i}', (1.30, y + 0.06, z + 0.008), (1.30, y - 0.06, z - 0.008), 0.29, O)
    disc('muzzle', 0.15, (1.30, -1.07, 1.30), toon(CHAR), facing=(0, -1, -0.14), depth=0.01)
    # left: crusher fist hanging low
    tube('uarmL', (-1.22, 0, 2.00), (-1.32, -0.08, 1.52), 0.16, C)
    ball('elbowL', 0.20, (-1.32, -0.08, 1.52), M)
    rbox('forearmL', (0.56, 0.58, 0.72), (-1.34, -0.14, 1.12), O, r=0.16)
    rbox('fistL', (0.66, 0.66, 0.54), (-1.36, -0.20, 0.58), W, r=0.18)
    rbox('knuckL', (0.58, 0.14, 0.18), (-1.36, -0.53, 0.66), M, r=0.06)
    blob('blob', 1.75, 1.25)


# ------------------------------------------------------------------ 3. 야수 군주: four-legged beast lord, plate mane, big horns

def beast(sc=1.0, **kw):
    begin('beast', sc, **kw)
    S, Mg, L, W, C = toon(SLATE), toon(MAGENTA), toon(LGREY), toon(WHITE), toon(CHAR)
    rbox('body', (1.62, 2.10, 1.08), (0, 0.32, 1.32), S, r=0.38, rot=(-7, 0, 0))
    rbox('saddle', (1.30, 1.24, 0.18), (0, 0.42, 1.90), L, r=0.09, rot=(-7, 0, 0))
    for i, y in enumerate((0.10, 0.58, 1.04)):
        prism(f'fin{i}', [(-0.24, 0), (0.24, 0), (0.05, 0.50 - i * 0.08), (-0.05, 0.50 - i * 0.08)], 0.16, (0, y, 1.96 - i * 0.06), Mg,
              r=0.06, rot=(0, 0, 90))
    for s in (-1, 1):
        # front legs with armoured shoulders
        ball(f'fsh{s}', 0.50, (s * 0.74, -0.40, 1.42), L, scale=(0.9, 1.05, 1.0))
        tube(f'fleg{s}', (s * 0.74, -0.46, 1.20), (s * 0.78, -0.62, 0.30), 0.29, S, r1=0.26)
        rbox(f'fpaw{s}', (0.66, 0.74, 0.34), (s * 0.78, -0.76, 0.17), L, r=0.13)
        for k in (-1, 0, 1):
            ball(f'fclaw{s}{k}', 0.08, (s * 0.78 + k * 0.19, -1.13, 0.13), W)
        # hind legs
        ball(f'haunch{s}', 0.52, (s * 0.64, 1.02, 1.14), S)
        tube(f'hleg{s}', (s * 0.68, 1.04, 0.96), (s * 0.72, 1.08, 0.28), 0.27, S, r1=0.24)
        rbox(f'hpaw{s}', (0.60, 0.68, 0.32), (s * 0.72, 0.98, 0.16), L, r=0.12)
    tail = [(0, 1.30, 1.50), (0, 1.62, 1.66), (0, 1.86, 1.98), (0, 1.96, 2.34)]
    sweep('tail', tail, [0.16, 0.14, 0.12, 0.10], S)
    ball('tailtip', 0.22, (0, 1.98, 2.48), Mg)
    # head: a ruff of plates flaring back behind the face (seen from above too), big horns, muzzle
    hc = (0, -1.08, 2.06)
    for i in range(12):
        a = math.radians(i * 30 + 15)
        x, z = math.cos(a), math.sin(a)
        prism(f'mane{i}', [(-0.25, 0), (0.25, 0), (0, 0.60)], 0.22, (x * 0.62, -0.82, hc[2] + z * 0.62), Mg, r=0.06,
              rot=(-38, 90 - math.degrees(a), 0))
    rbox('head', (1.16, 0.96, 0.92), hc, S, r=0.30)
    rbox('muzzle', (0.76, 0.48, 0.42), (0, -1.58, 1.82), L, r=0.16)
    rbox('nose', (0.28, 0.08, 0.15), (0, -1.83, 1.93), toon(CHAR), r=0.05, outline=False)
    for s in (-1, 1):
        tube(f'fang{s}', (s * 0.22, -1.75, 1.66), (s * 0.22, -1.77, 1.46), 0.065, W, r1=0.015)
        rbox(f'brow{s}', (0.38, 0.13, 0.11), (s * 0.27, -1.56, 2.30), Mg, r=0.045, rot=(0, s * 18, 0))
    eyes(0.27, -1.565, 2.13, w=0.19, h=0.13, tilt=14)
    for s in (-1, 1):
        horn = [(s * 0.38, -1.02, 2.40), (s * 0.68, -1.02, 2.68), (s * 0.90, -0.94, 3.06), (s * 0.92, -0.76, 3.42), (s * 0.82, -0.62, 3.60)]
        sweep(f'horn{s}', horn, [0.19, 0.17, 0.13, 0.09, 0.04], W)
    blob('blob', 1.5, 2.0, loc=(0, 0.2, 0.003))


# ------------------------------------------------------------------ 4. 보행 사령관: commander riding a four-legged walker

def walker(sc=1.0, **kw):
    begin('walker', sc, **kw)
    L, Y, C, W, M = toon(LGREY), toon(YELLOW), toon(CHAR), toon(WHITE), toon(MGREY)
    ball('belly', 0.98, (0, 0, 1.28), C, scale=(1, 1, 0.42))
    tube('hull', (0, 0, 1.22), (0, 0, 1.72), 1.18, L, r1=1.04, bevel=0.06)
    tube('rim', (0, 0, 1.62), (0, 0, 1.80), 1.08, Y, r1=1.02, bevel=0.03)
    for s in (-1, 1):
        rbox(f'lamp{s}', (0.18, 0.05, 0.12), (s * 0.30, -1.15, 1.44), flat(EYE), r=0.05, outline=False, rot=(0, 0, s * -15))
    for k, ang in enumerate((-135, -45, 45, 135)):
        a = math.radians(ang)
        cx, cy = math.cos(a), math.sin(a)
        hip, knee, foot = (cx * 0.92, cy * 0.92, 1.42), (cx * 1.62, cy * 1.62, 2.04), (cx * 2.00, cy * 2.00, 0.10)
        ball(f'hip{k}', 0.24, hip, M)
        tube(f'thigh{k}', hip, knee, 0.19, M)
        ball(f'knee{k}', 0.27, knee, Y)
        tube(f'shin{k}', knee, foot, 0.21, W, r1=0.14)
        rbox(f'foot{k}', (0.48, 0.48, 0.22), (foot[0], foot[1], 0.11), C, r=0.09)
    # throne and the commander
    rbox('seat', (0.96, 0.78, 0.22), (0, 0.18, 1.90), C, r=0.08)
    rbox('throne', (1.34, 0.26, 1.24), (0, 0.64, 2.44), Y, r=0.14)
    for s in (-1, 1):
        tube(f'thpost{s}', (s * 0.60, 0.64, 3.00), (s * 0.66, 0.64, 3.34), 0.08, Y, r1=0.03)
    tube('robe', (0, 0.06, 1.92), (0, 0.06, 2.66), 0.54, C, r1=0.36, bevel=0.05)
    sheet('ccape', 2.62, 1.96, 0.36, 0.62, 70, Y, y=0.08, thick=0.05, fold=0.04, rows=4, cols=12)
    rbox('chead', (0.70, 0.64, 0.60), (0, 0.0, 2.95), W, r=0.20)
    rbox('cvisor', (0.52, 0.05, 0.22), (0, -0.325, 2.93), toon(CHAR), r=0.08, outline=False)
    eyes(0.12, -0.355, 2.94, w=0.13, h=0.10, tilt=16)
    # peaked commander cap with a tall crest
    tube('cap', (0, 0.02, 3.20), (0, 0.02, 3.42), 0.37, Y, r1=0.44, bevel=0.04)
    rbox('brim', (0.68, 0.32, 0.07), (0, -0.37, 3.22), C, r=0.03, rot=(-12, 0, 0))
    prism('crest', [(-0.12, 0), (0.12, 0), (0.27, 0.50), (-0.02, 0.36)], 0.10, (0, 0.04, 3.38), Y, r=0.03, rot=(0, 0, 90))
    # baton arm pointing ahead
    tube('carm', (0.40, -0.02, 2.52), (0.66, -0.48, 2.78), 0.11, C)
    ball('chand', 0.13, (0.68, -0.52, 2.80), W)
    tube('baton', (0.68, -0.52, 2.80), (0.88, -1.02, 3.14), 0.06, Y)
    ball('batontip', 0.10, (0.89, -1.04, 3.16), Y)
    # two banner poles at the back
    for s in (-1, 1):
        tube(f'pole{s}', (s * 0.86, 0.70, 1.70), (s * 0.86, 0.70, 3.80), 0.05, M)
        ball(f'polecap{s}', 0.08, (s * 0.86, 0.70, 3.83), Y)
        prism(f'flag{s}', [(0, 0), (0, 0.48), (s * 0.66, 0.24)], 0.05, (s * 0.88, 0.70, 3.24), Y, r=0.015, rot=(0, 0, s * 25))
    blob('blob', 2.1, 2.1)


# ------------------------------------------------------------------ 5. 철벽 거신: hunched stone colossus, huge fists, purple core

def colossus(sc=1.0, **kw):
    begin('colossus', sc, **kw)
    S, M, P, L, C = toon(SLATE), toon(MGREY), toon(PURPLE), toon(LGREY), toon(CHAR)
    for s in (-1, 1):
        tube(f'leg{s}', (s * 0.52, 0.20, 1.24), (s * 0.56, 0.20, 0.34), 0.32, S)
        rbox(f'foot{s}', (0.62, 0.78, 0.34), (s * 0.58, 0.08, 0.17), M, r=0.12)
    rbox('hips', (1.30, 0.90, 0.46), (0, 0.22, 1.28), M, r=0.14)
    rbox('torso', (2.04, 1.34, 1.36), (0, 0.10, 2.04), S, r=0.36, rot=(10, 0, 0))
    rbox('chestplate', (1.30, 0.12, 0.80), (0, -0.60, 1.96), L, r=0.18, rot=(10, 0, 0))
    tube('corering', (0, -0.62, 1.92), (0, -0.72, 1.94), 0.27, M)
    ball('core', 0.20, (0, -0.72, 1.94), flat((190, 140, 255)), scale=(1, 0.6, 1))
    # small head sunk at the front of the shoulders
    rbox('head', (0.70, 0.60, 0.52), (0, -0.56, 2.72), M, r=0.18)
    rbox('brow', (0.72, 0.16, 0.10), (0, -0.80, 2.94), S, r=0.05)       # thin, so the battle camera still sees the eyes
    eyes(0.15, -0.865, 2.74, w=0.17, h=0.10, tilt=14)
    for s in (-1, 1):
        ball(f'shoulder{s}', 0.60, (s * 1.16, 0.04, 2.46), M, scale=(1.0, 1.0, 0.86))
        for k, (dx, dy, h) in enumerate(((0.10, -0.10, 0.52), (0.28, 0.20, 0.38))):
            b = (s * (1.16 + dx * 0.4), dy, 2.80)
            tube(f'crys{s}{k}', b, (s * (1.16 + dx * 1.4), dy + 0.04, 2.80 + h), 0.15, P, r1=0.03)
        tube(f'uarm{s}', (s * 1.22, 0, 2.20), (s * 1.42, -0.22, 1.42), 0.30, S)
        tube(f'farm{s}', (s * 1.42, -0.22, 1.42), (s * 1.48, -0.42, 0.66), 0.38, M, r1=0.44, bevel=0.06)
        rbox(f'fist{s}', (0.84, 0.84, 0.66), (s * 1.48, -0.46, 0.33), S, r=0.24)
        rbox(f'knuck{s}', (0.72, 0.16, 0.22), (s * 1.48, -0.86, 0.42), P, r=0.07)
    # back monoliths: a crown of standing stones, each tipped with a purple crystal point
    for i, (x, h, tilt) in enumerate(((-0.62, 1.00, -16), (0, 1.26, 0), (0.62, 1.00, 16))):
        rbox(f'stone{i}', (0.44, 0.44, h), (x, 0.66, 2.50 + h / 2), S, r=0.10, rot=(-8, tilt, 0))
        ax = (math.sin(math.radians(tilt)) * 0.99, 0.139, math.cos(math.radians(tilt)) * 0.99)
        top = (x + ax[0] * h / 2, 0.66 + ax[1] * h / 2, 2.50 + h / 2 + ax[2] * h / 2)
        tube(f'stonetip{i}', (top[0] - ax[0] * 0.04, top[1] - ax[1] * 0.04, top[2] - ax[2] * 0.04),
             (top[0] + ax[0] * 0.50, top[1] + ax[1] * 0.50, top[2] + ax[2] * 0.50), 0.22, P, r1=0.02)
    blob('blob', 2.0, 1.4)


# ------------------------------------------------------------------ a grunt-sized block, for scale only

def grunt(sc=1.0, **kw):
    begin('grunt', sc, **kw)
    G = toon((150, 150, 156))
    rbox('gbody', (0.62, 0.52, 0.58), (0, 0, 0.36), G, r=0.16)
    rbox('ghead', (0.66, 0.58, 0.50), (0, 0, 0.84), G, r=0.18)
    blob('blob', 0.5, 0.45)


BOSSES = {1: king, 2: mech, 3: beast, 4: walker, 5: colossus}     # names and captions live in sheets.py
