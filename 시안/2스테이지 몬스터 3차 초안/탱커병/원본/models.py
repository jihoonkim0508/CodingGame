# Seven stage-2 tank monster drafts, round 3: armoured brutes and siege beasts at the boss drafts' maturity
# (slit eyes under a brow, layered plates, horns, claws), 4-8k triangles each. The robots' scheme, amber accent on all.
# Design units: the melee robot stands 1.55 (~118 px in battle). Front is -y. Each build returns its root empty.
import math
from mathutils import Vector
from toon import (rbox, sbox, ball, tube, prism, bentprism, bent_y, shell, rock, skin, sweep, torus, blob, toon, flat,
                  begin, group, ungroup, real_robot)
from palette import WHITE, LGREY, MGREY, CHAR, GUN, AMBER, EYE


def m(rgb):
    return toon(rgb)


def slit_eyes(x, y, z, w=0.12, h=0.05, depth=0.03, pitch=0, yaw=0, tag='eye'):
    """Two scowling slits, low at the nose and high at the temples; x is where each starts from the middle.
    yaw turns each slit outward (eyes on the sides of a head)."""
    for s in (-1, 1):
        pts = [(0, -h * 0.5), (s * w, -h * 0.02), (s * w, h * 0.5), (0, h * 0.02)]
        prism(f'{tag}{s}', pts, depth, (s * x, y, z), flat(EYE), r=0.0, outline=False, rot=(pitch, 0, s * yaw))


def rivets(name, pts, r=0.024, mat=None):
    for i, p in enumerate(pts):
        ball(f'{name}{i}', r, p, mat or m(LGREY), seg=(6, 4), outline=False)


def limb(name, p0, p1, w, d, mat, r=0.06, levels=1, taper=(1, 1), extend=0.0):
    """A soft box from p0 to p1 (its top face, tapered, at p1): greaves, vambraces, plated limbs."""
    p0, p1 = Vector(p0), Vector(p1); v = p1 - p0
    e = Vector((0, 0, 1)).rotation_difference(v.normalized()).to_euler()
    size, rot = (w, d, v.length + extend), tuple(math.degrees(a) for a in e)
    if levels == 0: return rbox(name, size, tuple((p0 + p1) / 2), mat, r=r, taper=taper, rot=rot)
    return sbox(name, size, tuple((p0 + p1) / 2), mat, r=r, levels=levels, taper=taper, rot=rot)


def pauldron(tag, c, rad, cap, lame, side=-1, layers=2, horn=None):
    """A solid domed cap over the shoulder and lames tucked under its rim, stepping down and out: layered plate."""
    ball(f'{tag}cap', rad, c, cap, scale=(1.0, 0.92, 0.8), seg=(16, 10), half=True)
    lon = (-165, -15) if side < 0 else (15, 165)
    for i in range(layers):
        k = 1.03 + 0.06 * i
        cc = (c[0] + side * 0.03 * (i + 1), c[1], c[2] - 0.08 * (i + 1))
        shell(f'{tag}lame{i}', (rad * k, rad * 0.92 * k, rad * 0.8 * k), cc, lame, lon=lon, lat=(-28, 14), thick=0.05, seg=(12, 3))
    if horn:
        sweep(f'{tag}horn', [(c[0] + side * rad * 0.3, c[1], c[2] + rad * 0.66), (c[0] + side * rad * 0.5, c[1] + 0.02, c[2] + rad * 1.0),
                             (c[0] + side * rad * 0.56, c[1] + 0.08, c[2] + rad * 1.3)], [0.07, 0.045, 0.01], horn, seg=10)


def band(name, R, arc, depth, loc, mat, tilt=0.0, thick=0.07, tip=0.0):
    """An armour band arched over a back (radius R, arc degrees, depth along y): armadillo and pill-bug plates.
    tip > 0 runs each end out into a point (an isopod's side plates)."""
    L = R * math.radians(arc)
    pts = [(-L / 2, -depth / 2), (L / 2, -depth / 2)]
    if tip: pts.append((L / 2 + tip, depth * 0.15))
    pts += [(L / 2, depth / 2), (-L / 2, depth / 2)]
    if tip: pts.append((-L / 2 - tip, depth * 0.15))
    return bentprism(name, pts, thick, loc, mat, bend=R, r=0.022, rot=(-90 + tilt, 0, 0), cuts=12, seg=1)


def ring_on(name, p0, p1, t, R, r, mat, flatz=2.5, seg=(18, 6)):
    """A flat band ring round the limb p0-p1 at fraction t: bracers, cuffs, wraps."""
    p0, p1 = Vector(p0), Vector(p1)
    return torus(name, R, r, tuple(p0 + (p1 - p0) * t), mat, axis=p1 - p0, scale=(1, 1, flatz), seg=seg)


def horn(name, base, out, up, fwd, r, mat, seg=12):
    """A curved horn from base: out sideways, then up, then hooking forward; smooth enough not to pinch."""
    b = Vector(base)
    path = [b, b + Vector(out) * 0.45, b + Vector(out) * 0.8 + Vector(up) * 0.35, b + Vector(out) + Vector(up) * 0.75 + Vector(fwd) * 0.3,
            b + Vector(out) * 1.02 + Vector(up) + Vector(fwd)]
    return sweep(name, [tuple(p) for p in path], [r, r * 0.86, r * 0.66, r * 0.42, r * 0.12], mat, seg=seg)


# ------------------------------------------------------------------ 1 철방패 거병: riot-push brute behind a tower shield

def t1(sc=1.0, **kw):
    root = begin('t1', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    # braced stance: left leg bent under the shield, right leg stretched back pushing
    for s, hip, knee, foot in ((-1, (-0.32, 0.10, 0.64), (-0.48, -0.16, 0.40), (-0.52, -0.20, 0.0)),
                               (1, (0.32, 0.14, 0.64), (0.46, 0.34, 0.38), (0.54, 0.72, 0.0))):
        tube(f'thigh{s}', hip, knee, 0.18, G, r1=0.15, segs=12)
        limb(f'greave{s}', (foot[0], foot[1] + 0.02, 0.14), knee, 0.24, 0.26, W, r=0.07, taper=(1.15, 1.1))
        sbox(f'knee{s}', (0.24, 0.16, 0.22), (knee[0], knee[1] - 0.13, knee[2] + 0.04), L, r=0.06, levels=1, rot=(-12, 0, 0), taper=(0.8, 1))
        sweep(f'kspike{s}', [(knee[0], knee[1] - 0.20, knee[2] + 0.06), (knee[0], knee[1] - 0.30, knee[2] + 0.13)], [0.045, 0.008], L, seg=8)
        sbox(f'sabaton{s}', (0.32, 0.48, 0.18), (foot[0], foot[1] - 0.08, 0.09), G, r=0.06, levels=1, taper=(0.85, 0.7))
        rbox(f'toe{s}', (0.28, 0.16, 0.12), (foot[0], foot[1] - 0.32, 0.07), Mg, r=0.04)
    sbox('hips', (0.84, 0.56, 0.30), (0, 0.12, 0.68), G, r=0.10, levels=1)
    for s in (-1, 1):
        shell(f'tasset{s}', (0.48, 0.40, 0.40), (s * 0.06, 0.10, 0.74), W, lon=(30, 110) if s > 0 else (-110, -30),
              lat=(-62, -8), thick=0.04, seg=(7, 4))
    # torso pitched forward into the shield, a white back plate with a raised spine ridge
    sbox('chest', (1.02, 0.80, 0.76), (0, 0.0, 1.06), G, r=0.20, levels=1, rot=(14, 0, 0), taper=(1.1, 1.0))
    shell('backplate', (0.56, 0.46, 0.50), (0, 0.08, 1.08), W, lon=(122, 238), lat=(-46, 70), thick=0.06, seg=(10, 6), rot=(14, 0, 0))
    sweep('spine', [(0, 0.40, 0.80), (0, 0.55, 1.02), (0, 0.52, 1.30), (0, 0.36, 1.50)], [(0.05, 0.03), (0.06, 0.06), (0.06, 0.06), (0.04, 0.03)],
          L, seg=8)
    for s in (-1, 1):
        pauldron(f'pd{s}', (s * 0.62, 0.02, 1.36), 0.33, W, L, side=s)
    # head low and forward, over the shield: horned helm, white face plate, T visor, scowling brow, tusked jaw guard
    sbox('helm', (0.50, 0.52, 0.46), (0, -0.40, 1.46), G, r=0.12, levels=1, taper=(0.9, 0.95))
    shell('face', (0.26, 0.18, 0.26), (0, -0.50, 1.44), W, lon=(-72, 72), lat=(-60, 40), thick=0.04, seg=(10, 6))
    rbox('visor', (0.38, 0.05, 0.08), (0, -0.675, 1.48), m(CHAR), r=0.025, outline=False)
    slit_eyes(0.035, -0.70, 1.485, w=0.15, h=0.06)
    for s in (-1, 1):
        rbox(f'brow{s}', (0.28, 0.14, 0.08), (s * 0.13, -0.62, 1.57), G, r=0.035, rot=(-12, s * -16, 0))
        horn(f'horn{s}', (s * 0.21, -0.42, 1.62), (s * 0.28, 0.04, 0.0), (0, 0, 0.30), (0, -0.30, 0), 0.07, W, seg=10)
    sbox('jaw', (0.44, 0.24, 0.14), (0, -0.62, 1.31), G, r=0.05, levels=1, taper=(0.8, 1))
    for s in (-1, 1):
        sweep(f'tusk{s}', [(s * 0.15, -0.72, 1.30), (s * 0.17, -0.78, 1.38), (s * 0.15, -0.79, 1.45)], [0.04, 0.03, 0.008], W, seg=8)
    # left arm reaches round to grip the shield's edge with three claws
    tube('uarmL', (-0.64, 0.0, 1.20), (-0.80, -0.28, 0.90), 0.14, G, segs=12)
    ball('elbowL', 0.15, (-0.80, -0.28, 0.90), Mg, seg=(12, 8))
    limb('farmL', (-0.80, -0.28, 0.90), (-0.66, -0.58, 0.80), 0.26, 0.26, W, r=0.07, taper=(0.85, 0.85))
    sbox('handL', (0.18, 0.20, 0.22), (-0.64, -0.62, 0.80), G, r=0.06, levels=1)
    for i, z in enumerate((0.72, 0.80, 0.88)):
        sweep(f'claw{i}', [(-0.62, -0.66, z), (-0.60, -0.75, z + 0.01), (-0.52, -0.81, z), (-0.45, -0.80, z - 0.02)],
              [0.042, 0.04, 0.03, 0.008], L, seg=8)
    # the tower shield: thick curved pavise, amber rim, white face with a raised inner panel, grey straps, domed boss
    B = 1.5
    out = [(-0.60, -0.50), (-0.36, -0.60), (0, -0.68), (0.36, -0.60), (0.60, -0.50), (0.62, 0.40), (0.44, 0.54), (0, 0.60),
           (-0.44, 0.54), (-0.62, 0.40)]
    group('shield', (0.10, -0.82, 0.70), (-6, 0, 0))
    bentprism('rim', [(x * 1.07, z * 1.06) for x, z in out], 0.08, (0, 0.03, 0), A, bend=B, r=0.025, cuts=8, seg=1)
    bentprism('face', out, 0.06, (0, -0.02, 0), W, bend=B, r=0.02, cuts=8, seg=1)
    bentprism('panel', [(x * 0.80, z * 0.82) for x, z in out], 0.03, (0, -0.055, 0), W, bend=B - 0.03, r=0.012, cuts=6, seg=1)
    for x in (-0.36, 0.36):
        bentprism(f'strap{x}', [(x - 0.055, -0.56), (x + 0.055, -0.56), (x + 0.055, 0.48), (x - 0.055, 0.48)], 0.03,
                  (0, -0.075, 0), L, bend=B - 0.06, r=0.01, cuts=2)
        rivets(f'srv{x}', [(x, -0.09 + bent_y(x, B), z) for z in (-0.44, -0.12, 0.20, 0.42)])
    tube('bossring', (0, -0.06, -0.06), (0, -0.13, -0.06), 0.22, Mg, bevel=0.02, segs=20)
    ball('boss', 0.16, (0, -0.12, -0.06), G, scale=(1, 0.75, 1), seg=(16, 10))
    sweep('bossspike', [(0, -0.22, -0.06), (0, -0.36, -0.06)], [0.065, 0.008], L, seg=10)
    rivets('bstud', [(math.cos(math.radians(a)) * 0.19, -0.135, -0.06 + math.sin(math.radians(a)) * 0.19) for a in range(0, 360, 45)])
    ungroup()
    blob('blob', 1.05, 1.0)
    return root


# ------------------------------------------------------------------ 2 공성 코뿔소: four-legged siege rhino, banded back, ram frill

def t2(sc=0.95, **kw):
    root = begin('t2', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    sbox('body', (1.16, 1.80, 0.86), (0, 0.14, 0.86), G, r=0.30, levels=1, taper=(0.86, 0.92))
    # armadillo bands over the back, each a little lower toward the ends, a grey knob on each spine
    for i, (y, R, z) in enumerate(((-0.52, 0.72, 1.30), (-0.18, 0.70, 1.36), (0.14, 0.68, 1.36), (0.46, 0.64, 1.31), (0.76, 0.58, 1.22))):
        band(f'band{i}', R, 150, 0.40, (0, y, z), W, tilt=-6)
        if i == 0:
            band('bandedge', R + 0.025, 146, 0.10, (0, y - 0.17, z - 0.015), A, tilt=-6, thick=0.05)
        ball(f'knob{i}', 0.07, (0, y + 0.04, z + 0.035), L, seg=(10, 6), half=True)
        rivets(f'brv{i}', [(s * R * math.sin(math.radians(62)), y - 0.10, z - R * (1 - math.cos(math.radians(62))) + 0.05) for s in (-1, 1)], r=0.026)
    # legs: thick pillars, angular white knee guards, grey hooves with toe claws
    for s in (-1, 1):
        for y, tag in ((-0.50, 'f'), (0.78, 'b')):
            hip, knee = (s * 0.46, y, 0.90), (s * 0.55, y - 0.02, 0.44)
            tube(f'{tag}thigh{s}', hip, knee, 0.26, G, r1=0.21, segs=14)
            shell(f'{tag}legplate{s}', (0.31, 0.31, 0.30), (s * 0.50, y, 0.76), W, lon=(20, 160) if s > 0 else (-160, -20), lat=(-42, 34),
                  thick=0.05, seg=(9, 5))
            torus(f'{tag}kneering{s}', 0.21, 0.04, (knee[0], knee[1], knee[2] - 0.02), L, scale=(1, 1, 1.6), seg=(16, 6))
            sbox(f'{tag}shin{s}', (0.38, 0.40, 0.32), (knee[0], knee[1], 0.24), G, r=0.08, levels=1, taper=(1.08, 1.08))
            sbox(f'{tag}hoof{s}', (0.46, 0.50, 0.15), (knee[0], knee[1] - 0.05, 0.075), Mg, r=0.05, levels=1)
            for k in (-1, 0, 1):
                sweep(f'{tag}toe{s}{k}', [(knee[0] + k * 0.13, knee[1] - 0.28, 0.09), (knee[0] + k * 0.15, knee[1] - 0.36, 0.03)],
                      [0.05, 0.012], L, seg=8)
    # tail with a studded club
    sweep('tail', [(0, 0.96, 0.98), (0, 1.22, 0.86), (0, 1.40, 0.66)], [0.13, 0.10, 0.07], G, seg=10)
    ball('club', 0.17, (0, 1.46, 0.60), G, seg=(14, 10))
    rivets('clubstud', [(0.15, 1.48, 0.62), (-0.15, 1.48, 0.62), (0, 1.50, 0.77), (0, 1.62, 0.58)], r=0.05)
    # head lowered to charge: white helm cap and nose plate, a big ram horn, a frill crest with an amber band
    group('head', (0, -1.14, 0.80), (10, 0, 0))
    # wedge skull and narrower snout in side profile (prism outlines in y, z, turned to run along y)
    prism('skull', [(0.42, 0.30), (-0.18, 0.22), (-0.30, 0.04), (-0.24, -0.26), (0.40, -0.28)], 0.72, (0, 0, 0), G, r=0.07, rot=(0, 0, 90))
    prism('snout', [(-0.10, 0.18), (-0.60, 0.04), (-0.64, -0.12), (-0.46, -0.24), (-0.10, -0.22)], 0.48, (0, 0, 0), G, r=0.06, rot=(0, 0, 90))
    prism('jaw', [(-0.12, -0.20), (-0.50, -0.22), (-0.46, -0.36), (-0.12, -0.36)], 0.42, (0, 0, 0), Mg, r=0.04, rot=(0, 0, 90))
    # a white forehead plate running to a point over the snout, a nose plate, the ram horn
    bentprism('brow', [(-0.40, 0.36), (0.40, 0.36), (0.42, -0.04), (0.18, -0.28), (0, -0.34), (-0.18, -0.28), (-0.42, -0.04)], 0.06,
              (0, -0.06, 0.31), W, bend=0.5, r=0.018, rot=(-82, 0, 0), cuts=8)
    bentprism('noseplate', [(-0.20, 0.16), (0.20, 0.16), (0.16, -0.16), (0, -0.22), (-0.16, -0.16)], 0.05, (0, -0.44, 0.17), W, bend=0.32,
              r=0.015, rot=(-72, 0, 0), cuts=6)
    sweep('horn', [(0, -0.50, 0.16), (0, -0.60, 0.32), (0, -0.67, 0.52), (0, -0.66, 0.70), (0, -0.59, 0.82)],
          [0.12, 0.10, 0.075, 0.045, 0.012], W, seg=14)
    sweep('horn2', [(0, -0.16, 0.32), (0, -0.21, 0.46), (0, -0.17, 0.56)], [0.08, 0.05, 0.012], W, seg=10)
    slit_eyes(0.34, -0.21, 0.12, w=0.14, h=0.06, yaw=55)
    for s in (-1, 1):
        sweep(f'tusk{s}', [(s * 0.17, -0.46, -0.30), (s * 0.20, -0.58, -0.22), (s * 0.19, -0.63, -0.08)], [0.045, 0.035, 0.008], W, seg=8)
    ungroup()
    blob('blob', 1.05, 1.55, loc=(0, 0.1, 0.003))
    return root


# ------------------------------------------------------------------ 3 철띠 골렘: rock golem bound in iron bands, amber core

def t3(sc=0.85, **kw):
    root = begin('t3', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    n = [0]

    def R(size, loc, mat, rot=None, cuts=12):
        n[0] += 1
        return rock(f'rock{n[0]}', size, loc, mat, seed=n[0] * 7 + 3, cuts=cuts, depth=0.5, chamfer=0.018, rot=rot)

    for s in (-1, 1):
        R((0.44, 0.54, 0.24), (s * 0.40, -0.06, 0.12), Mg)
        R((0.38, 0.40, 0.42), (s * 0.40, 0.04, 0.40), L, rot=(0, s * 6, 0))
        R((0.40, 0.42, 0.36), (s * 0.34, 0.08, 0.70), L, rot=(0, s * 12, 0))
    R((0.76, 0.54, 0.32), (0, 0.10, 0.86), Mg)
    R((0.66, 0.52, 0.38), (0, 0.02, 1.10), L)
    # chest: two pale pec slabs split by a glowing crack, a big back slab, boulder shoulders with a few shards
    for s in (-1, 1):
        R((0.54, 0.48, 0.52), (s * 0.28, -0.10, 1.42), W, rot=(8, 0, s * 8))
    R((1.06, 0.60, 0.66), (0, 0.22, 1.44), L)
    torus('corering', 0.085, 0.03, (0, -0.33, 1.34), G, axis=(0, 1, 0), seg=(14, 6))
    ball('core', 0.07, (0, -0.335, 1.34), flat(EYE), scale=(1, 0.55, 1), seg=(12, 8), outline=False)
    for s in (-1, 1):
        R((0.66, 0.64, 0.58), (s * 0.74, 0.06, 1.60), L, rot=(0, s * 14, 0))
        R((0.40, 0.36, 0.30), (s * 0.70, -0.12, 1.86), W, rot=(10, s * 20, 0), cuts=9)
        sweep(f'shard{s}', [(s * 0.86, 0.14, 1.80), (s * 0.98, 0.20, 2.06)], [(0.09, 0.07), 0.01], Mg, seg=5)
        R((0.38, 0.38, 0.46), (s * 0.88, 0.02, 1.18), Mg, rot=(0, s * 10, 0))
        R((0.54, 0.54, 0.40), (s * 0.94, -0.08, 0.86), L, rot=(0, s * -6, 0))
        R((0.48, 0.50, 0.30), (s * 0.95, -0.10, 0.56), W, rot=(0, s * 8, 4))
        # iron bracer with an amber band between the forearm stones
        torus(f'bracer{s}', 0.33, 0.045, (s * 0.95, -0.09, 0.70), G, scale=(1, 1, 2.6), seg=(20, 6))
        torus(f'bracera{s}', 0.355, 0.03, (s * 0.95, -0.09, 0.70), A, scale=(1, 1, 1.5), seg=(20, 6))
        R((0.58, 0.60, 0.40), (s * 0.97, -0.14, 0.22), L)
        for k in (-1, 0, 1):
            R((0.18, 0.20, 0.17), (s * 0.97 + k * 0.17, -0.47, 0.12), W, cuts=8)
    for i in range(3):
        torus(f'chain{i}', 0.07, 0.022, (-1.28, -0.10, 0.62 - i * 0.12), G, rot=(0, 90, 0) if i % 2 else (90, 0, 0), seg=(12, 6), scale=(1, 1.5, 1))
    # small head sunk between the shoulders: heavy brow stones, amber slit eyes, iron collar
    torus('collar', 0.30, 0.04, (0, -0.06, 1.70), G, scale=(1.2, 1, 1.8), seg=(20, 6))
    R((0.46, 0.42, 0.38), (0, -0.30, 1.82), Mg)
    for s in (-1, 1):
        R((0.28, 0.18, 0.12), (s * 0.12, -0.51, 1.93), L, rot=(-15, s * -18, 0), cuts=6)
    slit_eyes(0.03, -0.515, 1.835, w=0.14, h=0.06)
    R((0.34, 0.18, 0.13), (0, -0.48, 1.67), L, cuts=6)
    sweep('backstrap', [(0, 0.50, 1.08), (0, 0.56, 1.32), (0, 0.54, 1.62)], [(0.08, 0.02), (0.08, 0.02), (0.08, 0.02)], G, seg=6)
    blob('blob', 1.25, 0.9)
    return root


# ------------------------------------------------------------------ 4 강철 전갈: steel scorpion, both pincers locked in front like shields

def claw(tag, side, at, mats, size=1.0):
    """A plated pincer held up in front: a broad palm facing forward with an amber plate, two toothed fingers hooking inward."""
    W, L, A = mats
    side = -side                                   # fingers hook inward, toward the other pincer
    group(tag, at, (0, 0, side * 14))
    palm = [(-0.40, -0.28), (0.18, -0.34), (0.36, -0.14), (0.36, 0.16), (0.20, 0.36), (-0.34, 0.34), (-0.46, 0.02)]
    k = size
    palm = [(side * x * k, z * k) for x, z in palm]
    prism(f'{tag}palm', palm, 0.34 * k, (0, 0, 0), W, r=0.06)
    prism(f'{tag}plate', [(x * 0.74 - side * 0.03 * k, z * 0.72) for x, z in palm], 0.05, (0, -0.185 * k, 0), A, r=0.02)
    rivets(f'{tag}rv', [(side * x * k, -0.215 * k, z * k) for x in (-0.28, 0.16) for z in (-0.17, 0.19)], r=0.03)
    for f, (z0, z1, z2) in (('dactyl', (0.22, 0.30, 0.20)), ('pollex', (-0.18, -0.24, -0.12))):
        sweep(f'{tag}{f}', [(side * 0.30 * k, 0, z0 * k), (side * 0.50 * k, -0.02, z1 * k), (side * 0.68 * k, 0, z2 * k),
                            (side * 0.74 * k, 0.02, (z2 * 0.3) * k)], [(0.12 * k, 0.09 * k), (0.10 * k, 0.08 * k), (0.06 * k, 0.05 * k), 0.014],
              L, seg=10)
    for i, (x, z, dz) in enumerate(((0.46, 0.19, -0.06), (0.60, 0.17, -0.06), (0.46, -0.13, 0.06), (0.58, -0.10, 0.06))):
        sweep(f'{tag}tooth{i}', [(side * x * k, 0, z * k), (side * (x + 0.02) * k, 0, (z + dz) * k)], [0.03, 0.006], W, seg=6)
    ungroup()


def t4(sc=0.9, **kw):
    root = begin('t4', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    sbox('under', (0.84, 1.40, 0.30), (0, 0.12, 0.42), G, r=0.12, levels=1)
    # head shield and four abdomen bands: dark plates, each with a white trailing rim
    band('head', 0.52, 150, 0.56, (0, -0.44, 0.74), G, tilt=-8)
    band('headrim', 0.50, 150, 0.08, (0, -0.21, 0.71), W, tilt=-8, thick=0.05)
    for i, (y, R, z) in enumerate(((-0.08, 0.54, 0.80), (0.20, 0.56, 0.82), (0.46, 0.54, 0.78), (0.70, 0.48, 0.70))):
        band(f'seg{i}', R, 150, 0.30, (0, y, z), G, tilt=-8)
        band(f'segrim{i}', R - 0.02, 150, 0.07, (0, y + 0.13, z - 0.02), W, tilt=-8, thick=0.05)
    # face: a brow ridge over two slit eyes, small fangs
    rbox('face', (0.52, 0.22, 0.24), (0, -0.74, 0.56), G, r=0.06)
    for s in (-1, 1):
        rbox(f'brow{s}', (0.26, 0.12, 0.08), (s * 0.12, -0.84, 0.70), W, r=0.03, rot=(-14, s * -18, 0))
        sweep(f'fang{s}', [(s * 0.10, -0.84, 0.46), (s * 0.12, -0.92, 0.40), (s * 0.06, -0.95, 0.36)], [0.04, 0.03, 0.008], L, seg=8)
    slit_eyes(0.03, -0.86, 0.615, w=0.14, h=0.055)
    # legs: three short flattened pairs tucked under the bands
    for s in (-1, 1):
        for i, y in enumerate((-0.10, 0.22, 0.52)):
            hip, knee, tip = (s * 0.40, y, 0.46), (s * 0.64, y * 1.1, 0.56), (s * 0.74, y * 1.15, 0.0)
            sweep(f'leg{s}{i}', [hip, knee], [(0.10, 0.06), (0.09, 0.05)], Mg, seg=8)
            sweep(f'shin{s}{i}', [knee, (s * 0.72, y * 1.13, 0.28), tip], [(0.08, 0.05), (0.06, 0.04), 0.012], Mg, seg=8)
    # pincer arms up to two broad pincers locked edge to edge in front of the face
    for s in (-1, 1):
        sweep(f'arm{s}', [(s * 0.36, -0.56, 0.52), (s * 0.76, -0.74, 0.62), (s * 0.80, -1.00, 0.68)], [0.11, 0.10, 0.09], G, seg=10)
        shell(f'armplate{s}', (0.17, 0.18, 0.15), (s * 0.76, -0.74, 0.64), W, lon=(-150, -10) if s < 0 else (10, 150), lat=(-10, 80),
              thick=0.04, seg=(7, 4))
        claw(f'claw{s}', s, (s * 0.66, -1.12, 0.70), (W, L, A), size=0.78)
    # tail arching over the back: segments shrinking toward a white bulb and an amber sting
    path = [(0, 0.84, 0.62), (0, 1.12, 0.84), (0, 1.22, 1.14), (0, 1.12, 1.42), (0, 0.88, 1.58), (0, 0.62, 1.56)]
    radii = [0.17, 0.15, 0.13, 0.12, 0.11]
    for i in range(5):
        sweep(f'tail{i}', [path[i], path[i + 1]], [radii[i], radii[i] * 0.86], G if i % 2 == 0 else Mg, seg=12)
        if i: torus(f'tailring{i}', radii[i] * 0.96, 0.03, path[i], W, axis=tuple(Vector(path[i + 1]) - Vector(path[i - 1])), seg=(14, 4))
    ball('bulb', 0.13, (0, 0.56, 1.53), G, scale=(0.9, 1.3, 0.9), seg=(14, 8), rot=(-25, 0, 0))
    torus('bulbring', 0.11, 0.025, (0, 0.62, 1.55), W, axis=(0, -1, -0.3), seg=(14, 4))
    sweep('sting', [(0, 0.44, 1.50), (0, 0.32, 1.46), (0, 0.24, 1.34), (0, 0.24, 1.20)], [0.08, 0.06, 0.035, 0.008], A, seg=10)
    blob('blob', 1.05, 1.35, loc=(0, 0.0, 0.003))
    return root


# ------------------------------------------------------------------ 5 철완 오우거: knuckle-walking ogre in giant gauntlets

def t5(sc=0.9, **kw):
    root = begin('t5', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    pts = [(0, 0.20, 0.80), (0, 0.08, 1.04), (0, -0.02, 1.30), (0, -0.18, 1.44),
           (-0.56, 0.02, 1.38), (-0.86, -0.10, 1.00), (-0.86, -0.34, 0.56),
           (0.56, 0.02, 1.38), (0.86, -0.10, 1.00), (0.86, -0.34, 0.56),
           (-0.30, 0.24, 0.70), (-0.44, 0.06, 0.40), (-0.46, 0.16, 0.12),
           (0.30, 0.24, 0.70), (0.44, 0.06, 0.40), (0.46, 0.16, 0.12)]
    edges = [(0, 1), (1, 2), (2, 3), (2, 4), (4, 5), (5, 6), (2, 7), (7, 8), (8, 9), (0, 10), (10, 11), (11, 12), (0, 13), (13, 14), (14, 15)]
    radii = [0.36, 0.42, 0.50, 0.22, 0.30, 0.21, 0.19, 0.30, 0.21, 0.19, 0.22, 0.18, 0.15, 0.22, 0.18, 0.15]
    skin('body', pts, edges, radii, G, levels=2)
    # giant gauntlets: white vambraces widening to the wrist, an amber band, grey wraps, blocky fists with curled fingers
    for s in (-1, 1):
        e, w = (s * 0.86, -0.12, 1.00), (s * 0.86, -0.36, 0.48)
        limb(f'vamb{s}', e, w, 0.46, 0.46, W, r=0.05, taper=(1.18, 1.18))
        limb(f'vplate{s}', (s * 1.10, -0.12, 0.94), (s * 1.13, -0.36, 0.50), 0.10, 0.30, L, r=0.03, taper=(1, 1.15), extend=-0.08, levels=0)
        rivets(f'vrv{s}', [(s * 1.17, -0.18 - 0.2 * t, 0.86 - 0.36 * t) for t in (0.0, 0.5, 1.0)], r=0.03)
        limb(f'band{s}', tuple(Vector(e).lerp(Vector(w), 0.40)), tuple(Vector(e).lerp(Vector(w), 0.72)), 0.58, 0.58, A, r=0.03, levels=0)
        ring_on(f'wrap{s}a', (s * 0.86, -0.06, 1.16), e, 0.3, 0.21, 0.03, L, flatz=2.0, seg=(14, 4))
        sbox(f'fist{s}', (0.50, 0.46, 0.36), (s * 0.88, -0.44, 0.20), G, r=0.06, levels=1, taper=(0.95, 0.92))
        rbox(f'handback{s}', (0.54, 0.46, 0.12), (s * 0.88, -0.42, 0.40), W, r=0.04, taper=(0.9, 0.85))
        for k in range(3):
            rbox(f'finger{s}{k}', (0.15, 0.18, 0.17), (s * 0.88 + (k - 1) * 0.165, -0.70, 0.10), G, r=0.05)
        rbox(f'knuck{s}', (0.52, 0.14, 0.13), (s * 0.88, -0.68, 0.25), Mg, r=0.04)
        rbox(f'foot{s}', (0.32, 0.42, 0.16), (s * 0.47, 0.06, 0.08), G, r=0.06)
        for k in (-1, 0, 1):
            sweep(f'toe{s}{k}', [(s * 0.47 + k * 0.10, -0.12, 0.08), (s * 0.48 + k * 0.11, -0.22, 0.02)], [0.04, 0.01], L, seg=6)
    # one big layered pauldron, a diagonal strap, belt with an amber-striped front plate
    shell('pd', (0.40, 0.38, 0.32), (-0.60, 0.02, 1.48), W, lon=(-172, -8), lat=(-34, 88), thick=0.06, seg=(12, 6))
    shell('pdrim', (0.43, 0.41, 0.30), (-0.62, 0.02, 1.40), L, lon=(-165, -15), lat=(-30, 2), thick=0.05, seg=(12, 2))
    rivets('pdrv', [(-0.60 - 0.30 * math.sin(math.radians(a)), 0.02 - 0.30 * math.cos(math.radians(a)) * 0.95, 1.62) for a in (40, 90, 140)],
           r=0.035)
    sweep('strap', [(0.50, -0.30, 1.46), (0.20, -0.48, 1.20), (-0.10, -0.44, 0.96), (-0.34, -0.28, 0.80)], [(0.07, 0.02)] * 4, Mg, seg=6)
    torus('belt', 0.40, 0.04, (0, 0.14, 0.88), Mg, scale=(1, 0.95, 2.0), seg=(18, 4))
    rbox('fauld', (0.44, 0.08, 0.32), (0, -0.26, 0.74), W, r=0.04, rot=(8, 0, 0))
    rbox('fauldstripe', (0.46, 0.09, 0.07), (0, -0.265, 0.80), A, r=0.02, rot=(8, 0, 0))
    for i, (y, z) in enumerate(((0.36, 1.48), (0.46, 1.26), (0.48, 1.02))):
        shell(f'back{i}', (0.42 - i * 0.04, 0.30, 0.22), (0, y - 0.22, z), W if i != 1 else L, lon=(130, 230), lat=(-30, 70), thick=0.05, seg=(7, 4))
    # head sunk forward between the shoulders: heavy brow, short horns, one scowling slit eye, underbite with tusks
    sbox('head', (0.52, 0.48, 0.42), (0, -0.42, 1.50), G, r=0.10, levels=1, taper=(0.9, 0.9))
    shell('mask', (0.29, 0.27, 0.25), (0, -0.44, 1.54), W, lon=(-84, 84), lat=(4, 86), thick=0.05, seg=(10, 5))
    for s in (-1, 1):
        sbox(f'brow{s}', (0.30, 0.17, 0.11), (s * 0.13, -0.64, 1.64), W, r=0.04, levels=1, rot=(-12, s * -16, 0))
        horn(f'horn{s}', (s * 0.22, -0.48, 1.68), (s * 0.14, 0, 0), (0, 0, 0.20), (0, -0.12, 0), 0.065, L, seg=10)
    slit_eyes(0.0, -0.665, 1.555, w=0.16, h=0.065)
    rbox('jaw', (0.48, 0.32, 0.17), (0, -0.56, 1.35), L, r=0.05)
    for s in (-1, 1):
        sweep(f'tusk{s}', [(s * 0.16, -0.71, 1.40), (s * 0.18, -0.75, 1.52), (s * 0.15, -0.75, 1.61)], [0.055, 0.038, 0.008], W, seg=8)
    blob('blob', 1.25, 0.95)
    return root


# ------------------------------------------------------------------ 6 흑철 기사: horned black knight with a planted war maul

def t6(sc=1.0, **kw):
    root = begin('t6', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    for s in (-1, 1):
        rbox(f'sabaton{s}', (0.30, 0.50, 0.16), (s * 0.36, -0.06, 0.08), G, r=0.05, taper=(0.9, 0.8))
        rbox(f'toecap{s}', (0.26, 0.16, 0.10), (s * 0.36, -0.30, 0.07), W, r=0.04)
        limb(f'greave{s}', (s * 0.36, 0, 0.12), (s * 0.34, 0, 0.56), 0.28, 0.30, G, r=0.06, taper=(1.12, 1.1))
        rbox(f'knee{s}', (0.26, 0.14, 0.24), (s * 0.34, -0.17, 0.58), W, r=0.05, rot=(-10, 0, 0), taper=(0.8, 1))
        rbox(f'greaveplate{s}', (0.20, 0.08, 0.32), (s * 0.355, -0.16, 0.32), W, r=0.03, rot=(-3, 0, 0), taper=(1.15, 1))
        limb(f'thigh{s}', (s * 0.34, 0.0, 0.58), (s * 0.28, 0.02, 0.94), 0.30, 0.32, G, r=0.08, taper=(1.1, 1.1))
    # two rows of tassets round the hips, an amber tabard hanging in front
    for i, (z, k, mat) in enumerate(((0.98, 1.0, W), (0.86, 1.08, G))):
        for j, lon in enumerate(((-55, 55), (-125, -45), (45, 125), (125, 235))):
            shell(f'tasset{i}{j}', (0.52 * k, 0.42 * k, 0.40), (0, 0.04, z), mat, lon=lon, lat=(-58, -8), thick=0.04, seg=(7, 4))
    prism('tabard', [(-0.17, 0.0), (0.17, 0.0), (0.17, -0.46), (0, -0.56), (-0.17, -0.46)], 0.03, (0, -0.47, 1.04), A, r=0.01, rot=(6, 0, 0), seg=1)
    # torso: wide dark cuirass, white pectoral plates meeting in a ridge, layered belly plates
    sbox('chest', (0.96, 0.66, 0.72), (0, 0.02, 1.32), G, r=0.12, levels=1, taper=(1.2, 1.05))
    bentprism('breast', [(-0.42, 0.26), (0.42, 0.26), (0.46, -0.02), (0.24, -0.24), (0, -0.30), (-0.24, -0.24), (-0.46, -0.02)], 0.07,
              (0, -0.355, 1.42), W, bend=1.1, r=0.02, rot=(-6, 0, 0), cuts=8, seg=1)
    sbox('breastridge', (0.05, 0.06, 0.46), (0, -0.37, 1.40), L, r=0.02, levels=1, rot=(-6, 0, 0))
    for i, (z, w) in enumerate(((1.14, 0.56), (1.02, 0.50))):
        rbox(f'fauld{i}', (w, 0.12, 0.16), (0, -0.33 + i * 0.01, z), W if i == 0 else L, r=0.04, rot=(-6, 0, 0))
    rbox('backplate', (0.78, 0.08, 0.54), (0, 0.36, 1.36), W, r=0.03, rot=(6, 0, 0), taper=(1.12, 1))
    torus('gorget', 0.26, 0.05, (0, 0.0, 1.68), G, scale=(1.1, 1, 1.6), seg=(16, 4))
    for s in (-1, 1):
        # shoulders: three slabs stepping down and out like roof tiles, a raised ridge on the top one
        for i in range(3):
            rbox(f'pd{s}{i}', (0.50 - i * 0.04, 0.60 - i * 0.05, 0.12), (s * (0.62 + i * 0.07), 0.02, 1.70 - i * 0.12),
                 G if i == 0 else W, r=0.04, rot=(0, s * (24 + i * 10), 0))
        rbox(f'pdridge{s}', (0.36, 0.08, 0.08), (s * 0.62, 0.02, 1.78), L, r=0.03, rot=(0, s * 24, 0))
        tube(f'uarm{s}', (s * 0.66, 0.0, 1.46), (s * 0.60, -0.24, 1.08), 0.13, G, segs=12)
        limb(f'farm{s}', (s * 0.60, -0.24, 1.08), (s * 0.18, -0.54, 1.14), 0.22, 0.22, G, r=0.05, taper=(1.2, 1.2))
        rbox(f'gaunt{s}', (0.22, 0.24, 0.22), (s * 0.12, -0.60, 1.16), G, r=0.05)
        ring_on(f'cuff{s}', (s * 0.60, -0.24, 1.08), (s * 0.18, -0.54, 1.14), 0.9, 0.15, 0.035, W, flatz=2.0, seg=(14, 4))
    # the maul: dark haft, an octagonal white head resting on the ground, amber band, grey end caps
    tube('haft', (0, -0.62, 1.32), (0, -0.62, 0.40), 0.05, G, segs=10)
    ball('pommel', 0.08, (0, -0.62, 1.34), L, seg=(10, 6))
    tube('maulhead', (-0.32, -0.62, 0.22), (0.32, -0.62, 0.22), 0.21, W, bevel=0.03, segs=8)
    tube('maulband', (-0.09, -0.62, 0.22), (0.09, -0.62, 0.22), 0.225, A, bevel=0.015, segs=8)
    for s in (-1, 1):
        tube(f'maulcap{s}', (s * 0.30, -0.62, 0.22), (s * 0.38, -0.62, 0.22), 0.23, L, bevel=0.02, segs=8)
    # great helm: tall dark helm, white face plate with a ridge and slit, brow, big forward horns
    sbox('helm', (0.42, 0.46, 0.48), (0, -0.04, 1.94), G, r=0.08, levels=1, taper=(0.86, 0.9))
    shell('faceplate', (0.22, 0.17, 0.27), (0, -0.12, 1.88), W, lon=(-70, 70), lat=(-60, 30), thick=0.04, seg=(10, 6))
    rbox('facering', (0.04, 0.06, 0.20), (0, -0.30, 1.82), L, r=0.015)
    rbox('slit', (0.36, 0.05, 0.07), (0, -0.30, 1.94), m(CHAR), r=0.025, outline=False)
    slit_eyes(0.03, -0.325, 1.945, w=0.14, h=0.05)
    for s in (-1, 1):
        rbox(f'brow{s}', (0.24, 0.12, 0.08), (s * 0.11, -0.26, 2.02), G, r=0.03, rot=(-12, s * -14, 0))
        horn(f'horn{s}', (s * 0.20, -0.04, 2.02), (s * 0.30, 0.0, 0.0), (0, 0, 0.26), (0, -0.28, 0), 0.085, W)
    blob('blob', 1.0, 0.85)
    return root


# ------------------------------------------------------------------ 7 갑주 공벌레: armoured pill-bug, banded shell, dozer head plate

def t7(sc=1.0, **kw):
    root = begin('t7', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    sbox('under', (0.96, 1.86, 0.34), (0, 0.06, 0.34), G, r=0.14, levels=1)
    segs = ((-0.62, 0.62, 0.86), (-0.35, 0.68, 0.98), (-0.08, 0.72, 1.04), (0.19, 0.72, 1.06), (0.46, 0.70, 1.02), (0.73, 0.64, 0.94),
            (0.98, 0.56, 0.82))
    for i, (y, R, z) in enumerate(segs):
        band(f'seg{i}', R, 150, 0.34, (0, y, z), W, tilt=-7, tip=0.16)
        band(f'segrim{i}', R - 0.03, 150, 0.08, (0, y + 0.15, z - 0.03), L, tilt=-7, thick=0.05)
    sweep('spine', [(0, y, z + 0.085) for y, R, z in segs], [(0.06, 0.03)] * len(segs), A, seg=8)
    for i, (y, R, z) in enumerate(segs):
        rivets(f'segrv{i}', [(s * R * math.sin(math.radians(48)), y - 0.06, z - R * (1 - math.cos(math.radians(48))) + 0.06) for s in (-1, 1)],
               r=0.028)
    # tail plates
    for s in (-1, 1):
        sweep(f'uropod{s}', [(s * 0.24, 1.08, 0.40), (s * 0.34, 1.30, 0.30), (s * 0.40, 1.40, 0.24)], [(0.08, 0.04), (0.06, 0.03), 0.01], L, seg=8)
    # legs: short claws peeking under the shell
    for i, y in enumerate((-0.46, -0.16, 0.14, 0.44, 0.74)):
        for s in (-1, 1):
            sweep(f'leg{s}{i}', [(s * 0.40, y, 0.30), (s * 0.60, y - 0.04, 0.18), (s * 0.66, y - 0.10, 0.0)], [0.065, 0.05, 0.012], Mg, seg=8)
    # head: dark capsule, a curved white dozer plate with an amber edge stripe, slit eyes, mandibles, antennae
    sbox('head', (0.74, 0.40, 0.36), (0, -0.84, 0.46), G, r=0.10, levels=1)
    dozer = [(-0.66, -0.22), (0.66, -0.22), (0.72, 0.08), (0.50, 0.24), (0, 0.30), (-0.50, 0.24), (-0.72, 0.08)]
    bentprism('dozer', dozer, 0.08, (0, -0.98, 0.68), W, bend=0.8, r=0.02, rot=(-24, 0, 0))
    bentprism('dozerstripe', [(-0.66, -0.19), (0.66, -0.19), (0.66, -0.09), (-0.66, -0.09)], 0.03, (0, -1.02, 0.68), A, bend=0.76,
              r=0.008, rot=(-24, 0, 0))
    rivets('dozerrv', [(x, -1.04 + bent_y(x, 0.8), 0.74) for x in (-0.44, -0.22, 0.0, 0.22, 0.44)], r=0.028)
    slit_eyes(0.05, -1.05, 0.44, w=0.15, h=0.055)
    for s in (-1, 1):
        sweep(f'mandible{s}', [(s * 0.20, -0.98, 0.32), (s * 0.18, -1.14, 0.27), (s * 0.04, -1.20, 0.28)], [0.05, 0.035, 0.01], L, seg=8)
        sweep(f'antenna{s}', [(s * 0.30, -0.98, 0.52), (s * 0.52, -1.22, 0.70), (s * 0.66, -1.32, 0.92), (s * 0.70, -1.30, 1.06)],
              [0.04, 0.03, 0.022, 0.012], Mg, seg=8)
    blob('blob', 1.0, 1.45, loc=(0, 0.1, 0.003))
    return root


def robot_melee(**kw):
    return real_robot('melee', **kw)


def robot_tank(**kw):
    return real_robot('tank', frame=30, **kw)   # its guard pose, shield up front


ALL = {'t1': t1, 't2': t2, 't3': t3, 't4': t4, 't5': t5, 't6': t6, 't7': t7, 'robot_melee': robot_melee, 'robot_tank': robot_tank}
