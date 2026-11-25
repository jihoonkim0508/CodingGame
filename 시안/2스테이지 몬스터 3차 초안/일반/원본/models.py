# The seven stage-2 normal-grunt drafts, round 3. Design units: the real robot stands 1.55 tall; a grunt about 1.0-1.1
# (two thirds of the robot), the low crawlers less. Front faces -y. Faces tilt up 10-20° so the battle camera
# (47.5° down) still meets the eyes.
import math
from mathutils import Vector, Euler
from toon import (rbox, ball, tube, prism, sweep, sheet, lathe, shell, skin, chunk, rock, rivet, blob, toon, flat, begin)
from palette import WHITE, LGREY, MGREY, CHAR, SLATE, ORANGE, EYE


def place(origin, rot, local):
    """A point given in a rotated part's own frame (rot in degrees, as the part's rot), in model space."""
    return tuple(Vector(origin) + Euler([math.radians(a) for a in rot]).to_matrix() @ Vector(local))


def slits(name, origin, rot, x, z, w=0.07, h=0.024, slant=0.022, y=-0.03, recess=True, depth=0.02):
    """Two angry eye slits on a face plate: inner ends lower, self-lit, each in a charcoal recess slightly larger."""
    for s in (-1, 1):
        pts = [(s * (x + w / 2), z + slant / 2 + h / 2), (s * (x - w / 2), z - slant / 2 + h / 2),
               (s * (x - w / 2), z - slant / 2 - h / 2), (s * (x + w / 2) * 0.96, z + slant / 2 - h / 2)]
        if s > 0: pts = pts[::-1]
        prism(f'{name}{s}', pts, depth * 0.6, place(origin, rot, (0, y - depth * 0.35, 0)), flat(EYE), r=0, rot=rot, outline=False)
        if recess:
            cx, cz = sum(p[0] for p in pts) / 4, sum(p[1] for p in pts) / 4
            grow = [(cx + (px - cx) * 1.3, cz + (pz - cz) * 1.9) for px, pz in pts]
            prism(f'{name}r{s}', grow, depth * 0.6, place(origin, rot, (0, y, 0)), toon(CHAR), r=0, rot=rot, outline=False)


# ------------------------------------------------------------------ 1  가면 신도: the reference's hooded, masked minion

def acolyte(sc=1.0, **kw):
    begin('acolyte', sc, **kw)
    S, Wt, O, C, L, M = toon(SLATE), toon(WHITE), toon(ORANGE), toon(CHAR), toon(LGREY), toon(MGREY)
    # robe: a hunched cone of folds, the hem torn into strips
    lathe('robe', [(0.31, 0.03), (0.295, 0.12), (0.26, 0.26), (0.22, 0.40), (0.19, 0.50), (0.15, 0.60)], S, seg=26,
          folds=7, fold=0.07, jag=0.09, thick=0.025, sy=0.86, lean=0.05)
    for s in (-1, 1):   # pointed boots under the hem
        chunk(f'boot{s}', [(-0.06, -0.02, 0), (0.06, -0.02, 0), (-0.05, 0.08, 0), (0.05, 0.08, 0), (0, -0.16, 0.0),
                           (-0.045, 0.0, 0.08), (0.045, 0.0, 0.08), (0, -0.10, 0.05)], (s * 0.11, -0.16, 0.0), C, r=0.012)
    # front tabard, torn at the bottom
    prism('tabard', [(-0.075, 0.0), (0.075, 0.0), (0.10, -0.30), (0.07, -0.36), (0.045, -0.31), (0.01, -0.39),
                     (-0.03, -0.32), (-0.065, -0.37), (-0.10, -0.31)], 0.018, (0, -0.236, 0.43), O, r=0.006, rot=(-12, 0, 0),
          bend=2.2)
    lathe('belt', [(0.205, 0.40), (0.218, 0.425), (0.205, 0.45)], C, seg=20, sy=0.86, thick=0.012, outline=True)
    lathe('mantle', [(0.27, 0.45), (0.255, 0.51), (0.225, 0.58), (0.17, 0.645), (0.09, 0.68)], S, seg=24,
          folds=5, fold=0.05, jag=0.06, thick=0.02, sy=0.9, lean=0.04, loc=(0, -0.02, 0))
    # stone blocks on a cord round the shoulders, two with an orange rune
    for i, (a, rot) in enumerate(((-64, 0), (64, 0), (-120, 0), (120, 0))):
        ar = math.radians(a)
        x, y = math.sin(ar) * 0.20, -math.cos(ar) * 0.18 - 0.03
        rbox(f'block{i}', (0.13, 0.10, 0.13), (x, y, 0.62), L, r=0.022, seg=2, rot=(-14 if abs(a) < 90 else 10, 0, a + rot))
        if abs(a) < 90:
            p = (x + math.sin(ar) * 0.052, y - math.cos(ar) * 0.052, 0.625)
            prism(f'rune{i}', [(-0.03, 0.035), (0.0, 0.0), (-0.03, -0.035), (-0.012, 0.0)] if a < 0 else
                  [(0.03, 0.035), (0.012, 0.0), (0.03, -0.035), (0.0, 0.0)], 0.01, p, O, r=0, rot=(-14, 0, a), outline=False)
    # hood: a deep cowl open at the front, its tip flopping back; the face inside is shadow
    hc = (0, -0.075, 0.80)
    ball('void', 0.15, (0, -0.06, 0.79), C, scale=(1, 1, 1.05), outline=False)
    shell('hood', 0.19, hc, S, lat=(-62, 90), lon=(52, 308), thick=0.03, scale=(1.0, 1.08, 1.08), rot=(-10, 0, 0), seg=(16, 8),
          kind='organic')
    sweep('hoodtip', [(0, 0.05, 0.97), (0, 0.15, 1.00), (0, 0.25, 0.97), (0, 0.31, 0.90)], [0.085, 0.07, 0.045, 0.012], S, seg=10)
    # stone mask: a tablet wrapped round the face, brow ridge, nose ridge, slit eyes, a carved fanged mouth, a rune
    mo, mr = (0, -0.245, 0.775), (-16, 0, 0)
    prism('mask', [(-0.12, 0.125), (0.12, 0.125), (0.14, 0.0), (0.11, -0.09), (0.0, -0.16), (-0.11, -0.09), (-0.14, 0.0)],
          0.045, mo, Wt, r=0.012, rot=mr, bend=2.0)
    prism('brow', [(-0.135, 0.0), (0.135, 0.0), (0.10, 0.04), (0.0, 0.012), (-0.10, 0.04)], 0.035,
          place(mo, mr, (0, -0.018, 0.075)), Wt, r=0.008, rot=mr, bend=2.0)
    prism('nose', [(-0.016, 0.0), (0.016, 0.0), (0.022, -0.075), (0, -0.09), (-0.022, -0.075)], 0.03,
          place(mo, mr, (0, -0.02, 0.07)), Wt, r=0.006, rot=mr)
    slits('eye', mo, mr, 0.062, 0.03, w=0.074, h=0.022, slant=0.03, y=-0.012)
    prism('mouth', [(-0.065, -0.062), (0.065, -0.062), (0.055, -0.08), (0.035, -0.07), (0.018, -0.095), (0.0, -0.075),
                    (-0.018, -0.095), (-0.035, -0.07), (-0.055, -0.08)], 0.01, place(mo, mr, (0, -0.022, -0.005)), C, r=0,
          rot=mr, outline=False)
    prism('rune', [(0, 0.03), (0.016, 0.0), (0, -0.03), (-0.016, 0.0)], 0.01, place(mo, mr, (0, -0.042, 0.10)), O, r=0,
          rot=mr, outline=False)
    for s in (-1, 1):     # short horns through the cowl
        sweep(f'horn{s}', [(s * 0.13, -0.05, 0.90), (s * 0.20, -0.07, 0.96), (s * 0.235, -0.11, 1.02), (s * 0.235, -0.16, 1.06)],
              [0.042, 0.032, 0.02, 0.004], L, seg=8)
    # sleeves too long for the arms, clawed charcoal hands
    for s in (-1, 1):
        sh, wr = (s * 0.19, -0.06, 0.58), (s * 0.27, -0.25, 0.42)
        tube(f'sleeve{s}', sh, wr, 0.06, S, r1=0.095)
        hand = (s * 0.285, -0.31, 0.39)
        tips = [(s * 0.30, -0.40, 0.33), (s * 0.26, -0.41, 0.35), (s * 0.24, -0.35, 0.40)]
        skin(f'hand{s}', [wr, hand] + tips, [(0, 1), (1, 2), (1, 3), (1, 4)],
             [0.04, 0.042, 0.006, 0.006, 0.006], C, subdiv=1)
    # a crude sickle in the right hand
    tube('haft', (0.29, -0.30, 0.44), (0.29, -0.36, 0.26), 0.016, C)
    sweep('blade', [(0.29, -0.37, 0.25), (0.29, -0.47, 0.22), (0.29, -0.56, 0.26), (0.29, -0.60, 0.34), (0.29, -0.58, 0.40)],
          [0.03, 0.034, 0.03, 0.02, 0.004], L, seg=8, flat=0.25)
    blob('blob', 0.46, 0.42)


# ------------------------------------------------------------------ 2  고철 보병: hunched foot soldier in mismatched scrap armour

def footman(sc=1.0, **kw):
    begin('footman', sc, **kw)
    C, M, L, O, Wt = toon(CHAR), toon(MGREY), toon(LGREY), toon(ORANGE), toon(WHITE)
    # body under the armour: one skin graph, padded charcoal cloth
    V = [(0, 0.03, 0.40), (0, 0.0, 0.50), (0, -0.05, 0.60), (0, -0.11, 0.68),
         (-0.17, -0.05, 0.62), (-0.25, -0.11, 0.47), (-0.22, -0.25, 0.42),
         (0.17, -0.05, 0.62), (0.26, -0.07, 0.47), (0.25, -0.21, 0.52),
         (-0.09, 0.03, 0.36), (-0.14, -0.08, 0.21), (-0.12, 0.0, 0.08),
         (0.09, 0.03, 0.36), (0.13, -0.05, 0.22), (0.12, 0.05, 0.08)]
    E = [(0, 1), (1, 2), (2, 3), (2, 4), (4, 5), (5, 6), (2, 7), (7, 8), (8, 9), (0, 10), (10, 11), (11, 12), (0, 13), (13, 14), (14, 15)]
    R = [(0.12, 0.10), (0.14, 0.11), (0.15, 0.11), 0.06, 0.06, 0.05, 0.042, 0.06, 0.05, 0.042, 0.06, 0.052, 0.042, 0.06, 0.052, 0.042]
    skin('body', V, E, R, C, subdiv=1)
    # legs: greaves, knee cops, boots
    for s, knee, ank in ((-1, V[11], V[12]), (1, V[14], V[15])):
        tube(f'greave{s}', (knee[0], knee[1] - 0.005, knee[2] - 0.02), (ank[0], ank[1] - 0.01, ank[2] + 0.03), 0.058, M, r1=0.05, bevel=0.01)
        ball(f'kneecop{s}', 0.055, (knee[0], knee[1] - 0.025, knee[2] + 0.01), L, scale=(1, 0.8, 1))
        chunk(f'boot{s}', [(-0.055, -0.13, 0), (0.055, -0.13, 0), (-0.06, 0.06, 0), (0.06, 0.06, 0), (-0.05, -0.06, 0.07),
                           (0.05, -0.06, 0.07), (-0.05, 0.05, 0.09), (0.05, 0.05, 0.09), (0, -0.15, 0.03)], (ank[0], ank[1] - 0.02, 0), M, r=0.012)
    # torso: dented breastplate with rivets, belt, torn orange tabard, a strap across
    shell('breast', 0.165, (0, -0.03, 0.56), M, lat=(-45, 55), lon=(-95, 95), thick=0.025, scale=(1.0, 0.85, 1.05), rot=(-18, 0, 0), seg=(10, 6))
    for i, x in enumerate((-0.09, -0.03, 0.03, 0.09)):
        rivet(f'riv{i}', (x, -0.19 + abs(x) * 0.5, 0.64), L, facing=(0, -0.8, 0.6))
    lathe('belt', [(0.135, 0.41), (0.142, 0.44), (0.135, 0.47)], C, seg=16, sy=0.85, thick=0.014, loc=(0, 0.02, 0))
    rbox('buckle', (0.06, 0.02, 0.05), (0, -0.125, 0.44), L, r=0.008, seg=1)
    prism('tabard', [(-0.065, 0.0), (0.065, 0.0), (0.085, -0.20), (0.06, -0.25), (0.035, -0.21), (0.005, -0.27), (-0.03, -0.22),
                     (-0.06, -0.26), (-0.085, -0.21)], 0.016, (0, -0.135, 0.425), O, r=0.005, seg=1, rot=(-8, 0, 0), bend=2.5)
    sweep('strap', [(-0.15, -0.10, 0.67), (-0.05, -0.19, 0.61), (0.07, -0.18, 0.52), (0.14, -0.10, 0.45)], [0.018] * 4, C, seg=6, flat=0.4)
    # left shoulder only: a layered pauldron with a spike (the right one is missing, cheap kit)
    po, pr = (-0.205, -0.04, 0.66), (0, -28, 8)
    shell('paul0', 0.10, po, L, lat=(5, 90), thick=0.02, scale=(1.15, 1.05, 0.8), rot=pr, seg=(12, 4))
    shell('paul1', 0.117, place(po, pr, (0, 0, -0.005)), L, lat=(-28, 5), lon=(-150, 150), thick=0.02, scale=(1.12, 1.05, 1), rot=pr, seg=(12, 1))
    shell('paul2', 0.13, place(po, pr, (0, 0, -0.035)), M, lat=(-28, 0), lon=(-140, 140), thick=0.02, scale=(1.1, 1.05, 1), rot=pr, seg=(12, 1))
    tube('spike', place(po, pr, (0, 0, 0.08)), place(po, pr, (-0.02, 0.0, 0.20)), 0.03, Wt, r1=0.004)
    for i, a in enumerate((-60, 0, 60)):
        ar = math.radians(a)
        rivet(f'priv{i}', place(po, pr, (math.sin(ar) * 0.118, -math.cos(ar) * 0.11, 0.03)), C, r=0.014,
              facing=Euler([math.radians(v) for v in pr]).to_matrix() @ Vector((math.sin(ar), -math.cos(ar), 0.4)))
    # forearms: bracers and clawed grey hands
    for s, el, wr in ((-1, V[5], V[6]), (1, V[8], V[9])):
        tube(f'bracer{s}', el, wr, 0.052, M, r1=0.048, bevel=0.01)
        d = Vector(wr) - Vector(el); d.normalize()
        hand = tuple(Vector(wr) + d * 0.05)
        skin(f'hand{s}', [wr, hand, tuple(Vector(hand) + d * 0.04 + Vector((0, 0, -0.02)))], [(0, 1), (1, 2)], [0.04, 0.045, 0.02], M, subdiv=1)
    # notched cleaver raised in the right hand, flat to the front, edge out
    tube('grip', (0.27, -0.25, 0.50), (0.30, -0.27, 0.63), 0.017, C)
    prism('cleaver', [(-0.03, 0.0), (-0.035, 0.24), (0.0, 0.29), (0.10, 0.28), (0.095, 0.21), (0.075, 0.195), (0.09, 0.165),
                      (0.085, 0.09), (0.065, 0.075), (0.07, 0.05), (0.045, 0.0)], 0.018, (0.30, -0.27, 0.61), L, r=0.005, seg=1,
          rot=(-10, 14, -25))
    # dented round shield low on the left forearm, three orange claw marks
    so, sr = (-0.31, -0.22, 0.40), (90, 0, -40)
    lathe('shield', [(0.15, 0.0), (0.135, 0.02), (0.09, 0.036), (0.035, 0.045), (0.004, 0.047)], M, seg=16, thick=0.016,
          loc=so, rot=sr, folds=3, fold=0.03)
    lathe('rim', [(0.152, -0.01), (0.16, 0.008), (0.148, 0.02)], L, seg=16, loc=so, rot=sr)
    ball('boss', 0.035, place(so, sr, (0, 0, 0.045)), L, scale=(1, 1, 0.6), rot=sr)
    for i, dx in enumerate((-0.075, -0.02, 0.035)):
        prism(f'claw{i}', [(dx - 0.03, 0.09), (dx - 0.01, 0.095), (dx + 0.06, -0.075), (dx + 0.04, -0.08)], 0.01,
              place(so, sr, (0, 0, 0.044)), O, r=0, rot=(0, 0, -40), outline=False)
    # head: a wide grey goblin skull, long ears out through a dented pot helm with a torn orange crest,
    # slit eyes under a heavy brow, hooked nose, tusked underbite
    ho, hr = (0, -0.17, 0.79), (-14, 0, 0)
    ball('skull', 0.15, ho, M, scale=(1.12, 1.0, 0.92), rot=hr)
    shell('helm', 0.165, place(ho, hr, (0, 0.01, 0.025)), L, lat=(8, 90), thick=0.02, scale=(1.15, 1.12, 0.95), rot=hr, seg=(16, 5))
    prism('brim', [(-0.19, 0.0), (0.19, 0.0), (0.16, 0.028), (0.0, 0.012), (-0.16, 0.028)], 0.05, place(ho, hr, (0, -0.15, 0.04)), L,
          r=0.008, seg=1, rot=hr, bend=2.4)
    rivet('hriv', place(ho, hr, (0, -0.18, 0.06)), C, r=0.014, facing=(0, -0.9, 0.4))
    prism('crest', [(-0.20, 0.0), (0.12, 0.0), (0.13, 0.06), (0.06, 0.10), (0.03, 0.07), (-0.02, 0.13), (-0.06, 0.09), (-0.11, 0.14),
                    (-0.15, 0.08), (-0.23, 0.10)], 0.04, place(ho, hr, (0, 0.03, 0.16)), O, r=0.008, seg=1, rot=(hr[0], 0, 90))
    for s in (-1, 1):
        sweep(f'ear{s}', [place(ho, hr, (s * 0.13, 0.02, 0.0)), place(ho, hr, (s * 0.22, 0.05, 0.03)),
                          place(ho, hr, (s * 0.31, 0.08, 0.07)), place(ho, hr, (s * 0.37, 0.10, 0.10))], [0.05, 0.042, 0.022, 0.003], M,
              seg=8, flat=0.35)
    slits('eye', place(ho, hr, (0, -0.135, 0.0)), hr, 0.055, 0.0, w=0.06, h=0.02, slant=0.026, y=-0.012)
    chunk('nose', [(-0.03, 0, 0.02), (0.03, 0, 0.02), (0, -0.07, -0.02), (-0.025, 0, -0.05), (0.025, 0, -0.05), (0, -0.04, -0.06)],
          place(ho, hr, (0, -0.15, -0.03)), M, r=0.01, rot=hr)
    jo = place(ho, hr, (0, -0.07, -0.12))
    chunk('jaw', [(-0.13, -0.07, 0.035), (0.13, -0.07, 0.035), (-0.12, 0.05, 0.05), (0.12, 0.05, 0.05), (-0.10, -0.08, -0.04),
                  (0.10, -0.08, -0.04), (-0.08, 0.04, -0.05), (0.08, 0.04, -0.05)], jo, M, r=0.02, seg=2, rot=hr)
    prism('lip', [(-0.10, 0.0), (0.10, 0.0), (0.08, -0.012), (-0.08, -0.012)], 0.01, place(jo, hr, (0, -0.075, 0.034)), C, r=0, rot=hr, outline=False)
    for s in (-1, 1):
        tube(f'tusk{s}', place(jo, hr, (s * 0.085, -0.06, 0.03)), place(jo, hr, (s * 0.11, -0.085, 0.12)), 0.022, Wt, r1=0.003)
    blob('blob', 0.42, 0.40)


# ------------------------------------------------------------------ 3  갑충 졸병: a six-legged beetle drone, layered shell, pincer jaws

def beetle(sc=1.12, **kw):
    begin('beetle', sc, **kw)
    S, C, M, L, O, Wt = toon(SLATE), toon(CHAR), toon(MGREY), toon(LGREY), toon(ORANGE), toon(WHITE)
    # abdomen: a charcoal belly under two gunmetal wing cases, each with a broad orange stripe; a ridged tail plate
    ac, asz = (0, 0.20, 0.30), (0.25, 0.34, 0.19)
    ball('belly', 1.0, (ac[0], ac[1], ac[2] - 0.02), C, scale=(asz[0] * 0.96, asz[1] * 0.96, asz[2] * 0.9))
    for i, (y, w) in enumerate(((0.46, 0.17), (0.53, 0.12))):
        shell(f'tail{i}', 1.0, (0, y, ac[2] - 0.05 - i * 0.02), M, lat=(-30, 90), lon=(90, 270), thick=0.15, scale=(w, w * 0.7, w * 0.55),
              seg=(10, 4))
    for s in (-1, 1):
        lon = (4, 176) if s > 0 else (-176, -4)
        shell(f'case{s}', 1.0, ac, S, lat=(-22, 89), lon=lon, thick=0.025 / 0.3, scale=asz, seg=(12, 8))
        stripe = (16, 46) if s > 0 else (-46, -16)
        shell(f'stripe{s}', 1.012, ac, O, lat=(-6, 86), lon=stripe, thick=0, scale=asz, seg=(4, 7), outline=False)
        shell(f'hem{s}', 1.015, ac, M, lat=(-22, -13), lon=lon, thick=0, scale=asz, seg=(12, 1), outline=False)
    # pronotum: a shield over the thorax with a forward horn and two side spurs
    pc = (0, -0.13, 0.34)
    ball('thorax', 0.15, (0, -0.12, 0.28), C, scale=(1.1, 1.0, 0.8))
    shell('pronotum', 0.19, pc, S, lat=(-10, 90), thick=0.02, scale=(1.18, 0.95, 0.75), rot=(-12, 0, 0), seg=(14, 6))
    sweep('horn', [(0, -0.20, 0.45), (0, -0.29, 0.51), (0, -0.38, 0.53), (0, -0.45, 0.50)], [0.045, 0.036, 0.022, 0.004], L, seg=8)
    for s in (-1, 1):
        tube(f'spur{s}', (s * 0.19, -0.16, 0.36), (s * 0.29, -0.23, 0.41), 0.032, L, r1=0.004)
    # head low in front: a brow plate over slit eyes, curved pincer jaws, short antennae
    hc = (0, -0.30, 0.24)
    ball('head', 0.115, hc, S, scale=(1.15, 1.0, 0.85))
    ho, hr = (0, -0.385, 0.27), (-25, 0, 0)
    slits('eye', ho, hr, 0.052, 0.0, w=0.058, h=0.022, slant=0.022, y=0.0, depth=0.03)
    prism('brow', [(-0.12, 0.0), (0.12, 0.0), (0.09, 0.045), (0.0, 0.02), (-0.09, 0.045)], 0.05, place(ho, hr, (0, 0.01, 0.018)), S,
          r=0.008, seg=1, rot=hr, bend=2.5)
    for s in (-1, 1):
        sweep(f'jaw{s}', [(s * 0.06, -0.36, 0.20), (s * 0.12, -0.45, 0.19), (s * 0.11, -0.55, 0.18), (s * 0.055, -0.61, 0.17),
                          (s * 0.015, -0.61, 0.165)], [0.034, 0.032, 0.024, 0.012, 0.003], Wt, seg=8, flat=0.45)
        tube(f'tooth{s}', (s * 0.115, -0.49, 0.19), (s * 0.075, -0.51, 0.19), 0.013, Wt, r1=0.002, outline=False)
        sweep(f'ant{s}', [(s * 0.07, -0.37, 0.29), (s * 0.12, -0.45, 0.37), (s * 0.17, -0.49, 0.42), (s * 0.22, -0.49, 0.43)],
              [0.012, 0.01, 0.008, 0.004], C, seg=6)
    # six short, plated legs: hip, low knee, foot planted wide, a spur on each shin
    for s in (-1, 1):
        for i, (y, fy) in enumerate(((-0.14, -0.36), (0.05, 0.05), (0.23, 0.44))):
            hip = (s * 0.17, y, 0.25)
            knee = (s * 0.31, (y + fy) / 2, 0.33)
            foot = (s * 0.40, fy, 0.0)
            ankle = (s * 0.38, fy + (y - fy) * 0.15, 0.10)
            sweep(f'femur{s}{i}', [hip, ((hip[0] + knee[0]) / 2, (hip[1] + knee[1]) / 2, (hip[2] + knee[2]) / 2 + 0.025), knee],
                  [0.055, 0.06, 0.045], M, seg=8, flat=0.6)
            sweep(f'tibia{s}{i}', [knee, ankle, foot], [0.042, 0.03, 0.008], M, seg=8, flat=0.7)
            mid = ((knee[0] + ankle[0]) / 2, (knee[1] + ankle[1]) / 2, (knee[2] + ankle[2]) / 2)
            tube(f'tspur{s}{i}', mid, (mid[0] + s * 0.05, mid[1], mid[2] + 0.035), 0.015, L, r1=0.002)
    blob('blob', 0.52, 0.62, loc=(0, 0.05, 0.003))


# ------------------------------------------------------------------ 4  뿔 마귀: a hunched pale imp, dark horns, a scrap pauldron, a spear

def imp(sc=1.0, **kw):
    begin('imp', sc, **kw)
    K, C, M, O, Wt = toon(LGREY), toon(CHAR), toon(MGREY), toon(ORANGE), toon(WHITE)
    # body: one skin graph, hunched, long dangling left arm, digitigrade legs, a whip tail
    V = [(0, 0.06, 0.36), (0, 0.02, 0.46), (0, -0.06, 0.56), (0, -0.15, 0.63),
         (-0.15, -0.06, 0.60), (-0.25, -0.05, 0.43), (-0.27, -0.18, 0.28),
         (0.15, -0.06, 0.60), (0.27, -0.09, 0.47), (0.24, -0.24, 0.50),
         (-0.09, 0.06, 0.34), (-0.14, -0.09, 0.25), (-0.13, 0.07, 0.11), (-0.14, -0.03, 0.03),
         (0.09, 0.06, 0.34), (0.14, -0.06, 0.24), (0.13, 0.10, 0.11), (0.14, 0.01, 0.03),
         (0, 0.14, 0.36), (-0.04, 0.27, 0.27), (-0.13, 0.36, 0.22), (-0.22, 0.40, 0.27)]
    E = [(0, 1), (1, 2), (2, 3), (2, 4), (4, 5), (5, 6), (2, 7), (7, 8), (8, 9), (0, 10), (10, 11), (11, 12), (12, 13),
         (0, 14), (14, 15), (15, 16), (16, 17), (0, 18), (18, 19), (19, 20), (20, 21)]
    R = [(0.10, 0.09), (0.12, 0.10), (0.15, 0.11), 0.06, 0.055, 0.04, 0.032, 0.055, 0.042, 0.034,
         0.065, 0.055, 0.035, 0.03, 0.065, 0.055, 0.035, 0.03, 0.04, 0.03, 0.02, 0.01]
    skin('body', V, E, R, K, subdiv=1)
    prism('tailtip', [(0, 0.07), (0.04, 0.0), (0.015, -0.01), (0, 0.02), (-0.015, -0.01), (-0.04, 0.0)], 0.02, (-0.235, 0.405, 0.285), C,
          r=0.004, seg=1, rot=(0, -60, -70))
    for i in range(4):     # dark spines down the hunched back
        t = i / 3
        p = (0, 0.06 - 0.20 * (1 - t) + 0.02, 0.62 - 0.20 * t)
        tube(f'spine{i}', (p[0], p[1] + 0.06, p[2] - 0.02), (p[0], p[1] + 0.15 - t * 0.02, p[2] + 0.10 - t * 0.03), 0.032 - t * 0.006, C, r1=0.003)
    # claws on the hands and three-toed feet
    for s, el, wr in ((-1, V[5], V[6]), (1, V[8], V[9])):
        d = (Vector(wr) - Vector(el)).normalized()
        palm = Vector(wr) + d * 0.05
        side = d.cross(Vector((0, 0, 1))).normalized()
        tips = [tuple(palm + d * 0.06 + side * k * 0.03 + Vector((0, 0, -0.01))) for k in (-1, 0, 1)]
        skin(f'hand{s}', [wr, tuple(palm)] + tips, [(0, 1), (1, 2), (1, 3), (1, 4)], [0.034, 0.04, 0.012, 0.012, 0.012], K, subdiv=1)
        for k, t in enumerate(tips):
            tube(f'claw{s}{k}', t, tuple(Vector(t) + d * 0.045 + Vector((0, 0, -0.015))), 0.011, C, r1=0.002, outline=False)
    for s, ft in ((-1, V[13]), (1, V[17])):
        for k in (-1, 0, 1):
            tube(f'toe{s}{k}', (ft[0] + k * 0.025, ft[1], 0.03), (ft[0] + k * 0.045, ft[1] - 0.09, 0.012), 0.018, K, r1=0.012)
            tube(f'tclaw{s}{k}', (ft[0] + k * 0.045, ft[1] - 0.085, 0.014), (ft[0] + k * 0.05, ft[1] - 0.12, 0.004), 0.011, C, r1=0.002,
                 outline=False)
    # torn orange loincloth on a cord, wrist wraps
    lathe('cord', [(0.105, 0.37), (0.112, 0.39), (0.105, 0.41)], C, seg=14, sy=0.9, thick=0.01, loc=(0, 0.05, 0))
    prism('cloth', [(-0.075, 0), (0.075, 0), (0.08, -0.15), (0.05, -0.19), (0.025, -0.15), (-0.01, -0.21), (-0.04, -0.16),
                    (-0.08, -0.17)], 0.014, (0, -0.06, 0.39), O, r=0.004, seg=1, rot=(-6, 0, 0), bend=3)
    prism('cloth2', [(-0.07, 0), (0.07, 0), (0.06, -0.14), (0.02, -0.11), (-0.02, -0.16), (-0.065, -0.12)], 0.014, (0, 0.165, 0.39), O,
          r=0.004, seg=1, rot=(8, 0, 180), bend=3)
    for s, el, wr in ((-1, V[5], V[6]), (1, V[8], V[9])):
        a = Vector(el).lerp(Vector(wr), 0.55); b = Vector(el).lerp(Vector(wr), 0.95)
        tube(f'wrap{s}', tuple(a), tuple(b), 0.043, C, r1=0.039)
    # a scrap pauldron on the right shoulder, strapped across the chest
    po, pr = (0.17, -0.05, 0.64), (0, 30, -10)
    shell('paul0', 0.10, po, M, lat=(0, 90), thick=0.018, scale=(1.15, 1.05, 0.75), rot=pr, seg=(12, 4))
    shell('paul1', 0.118, place(po, pr, (0, 0, -0.01)), M, lat=(-30, 2), lon=(-150, 150), thick=0.018, scale=(1.1, 1.05, 1), rot=pr, seg=(12, 1))
    for i, a in enumerate((-50, 10)):
        ar = math.radians(a)
        rivet(f'priv{i}', place(po, pr, (math.sin(ar) * 0.105, -math.cos(ar) * 0.10, 0.03)), C, r=0.013,
              facing=Euler([math.radians(v) for v in pr]).to_matrix() @ Vector((math.sin(ar), -math.cos(ar), 0.5)))
    sweep('strap', [(0.13, -0.13, 0.62), (0.02, -0.19, 0.53), (-0.09, -0.15, 0.45), (-0.13, -0.05, 0.40)], [0.016] * 4, C, seg=6, flat=0.4)
    # spear: dark shaft, a grey leaf head, an orange rag tied under it
    a, b = Vector((0.235, 0.10, 0.24)), Vector((0.255, -0.56, 0.74))
    tube('shaft', tuple(a), tuple(b), 0.016, C)
    d = (b - a).normalized()
    u = d.cross(Vector((1, 0, 0))).normalized(); w = d.cross(u)
    # a leaf head with a diamond section, so it reads from any side
    chunk('spearhead', [tuple(d * 0.0), tuple(d * 0.06 + u * 0.045), tuple(d * 0.06 - u * 0.045), tuple(d * 0.06 + w * 0.016),
                        tuple(d * 0.06 - w * 0.016), tuple(d * 0.15 + u * 0.022), tuple(d * 0.15 - u * 0.022), tuple(d * 0.21)],
          tuple(b - d * 0.01), M, r=0.004)
    tube('socket', tuple(b - d * 0.04), tuple(b + d * 0.01), 0.022, M)
    for i, (w, l) in enumerate(((0.05, 0.10), (0.04, 0.13))):
        prism(f'rag{i}', [(0, 0), (w, 0), (w * 0.7, -l), (w * 0.3, -l * 0.8)], 0.008, tuple(b - d * (0.04 + i * 0.02)), O, r=0, rot=(0, 0, 70 + i * 60),
              outline=False)
    # head: heavy V brow over slit eyes, a short snout, a fanged underbite, swept-back horns, small ear fins
    ho, hr = (0, -0.22, 0.71), (-12, 0, 0)
    ball('skull', 0.12, place(ho, hr, (0, 0.02, 0.01)), K, scale=(1.12, 1.0, 0.95), rot=hr)
    slits('eye', place(ho, hr, (0, -0.10, 0.005)), hr, 0.05, 0.0, w=0.055, h=0.022, slant=0.028, y=-0.005)
    prism('brow', [(-0.12, 0.0), (0.12, 0.0), (0.10, 0.045), (0.0, 0.0), (-0.10, 0.045)], 0.05, place(ho, hr, (0, -0.095, 0.032)), K,
          r=0.01, seg=1, rot=hr, bend=2.4)
    chunk('snout', [(-0.045, 0, 0.03), (0.045, 0, 0.03), (-0.035, -0.06, -0.01), (0.035, -0.06, -0.01), (-0.05, 0, -0.04),
                    (0.05, 0, -0.04), (0, -0.07, -0.03)], place(ho, hr, (0, -0.10, -0.03)), K, r=0.012, rot=hr)
    chunk('mouth', [(-0.08, -0.05, 0.0), (0.08, -0.05, 0.0), (-0.07, 0.04, 0.02), (0.07, 0.04, 0.02), (-0.07, -0.04, -0.04),
                    (0.07, -0.04, -0.04)], place(ho, hr, (0, -0.06, -0.07)), C, r=0.01, rot=hr, outline=False)
    jo = place(ho, hr, (0, -0.06, -0.11))
    chunk('jaw', [(-0.10, -0.06, 0.03), (0.10, -0.06, 0.03), (-0.09, 0.05, 0.04), (0.09, 0.05, 0.04), (-0.07, -0.07, -0.03),
                  (0.07, -0.07, -0.03), (-0.06, 0.04, -0.04), (0.06, 0.04, -0.04), (0, -0.08, -0.04)], jo, K, r=0.018, seg=2, rot=hr)
    for s in (-1, 1):
        tube(f'fang{s}', place(jo, hr, (s * 0.06, -0.055, 0.025)), place(jo, hr, (s * 0.07, -0.065, 0.085)), 0.017, Wt, r1=0.002)
        tube(f'ufang{s}', place(ho, hr, (s * 0.03, -0.13, -0.05)), place(ho, hr, (s * 0.03, -0.135, -0.085)), 0.012, Wt, r1=0.002,
             outline=False)
        sweep(f'horn{s}', [place(ho, hr, (s * 0.07, -0.05, 0.08)), place(ho, hr, (s * 0.13, 0.0, 0.15)), place(ho, hr, (s * 0.17, 0.09, 0.20)),
                           place(ho, hr, (s * 0.18, 0.20, 0.19)), place(ho, hr, (s * 0.16, 0.28, 0.24))], [0.04, 0.035, 0.026, 0.016, 0.003], C, seg=8)
        prism(f'ear{s}', [(0, 0.04), (0.11, 0.05), (0, -0.03)], 0.012, place(ho, hr, (s * 0.12, 0.02, 0.0)), K, r=0.006, seg=1,
              rot=(hr[0], 0, 0) if s > 0 else (hr[0], 0, 180))
    blob('blob', 0.42, 0.42)


# ------------------------------------------------------------------ 5  외눈 드론: a mass-produced sentry drone, reverse knees, scythe arms

def mirror(pts):
    """Points given for the right side (x > 0) plus their mirror images."""
    return [p for x, y, z in pts for p in ((x, y, z), (-x, y, z))]


def drone(sc=1.0, **kw):
    begin('drone', sc, **kw)
    Wt, C, M, L, O = toon(WHITE), toon(CHAR), toon(MGREY), toon(LGREY), toon(ORANGE)
    # torso: a charcoal frame, a faceted white chest plate with a ridge, a white back hump with vent fins
    chunk('frame', mirror([(0.13, -0.10, 0.52), (0.16, -0.11, 0.70), (0.12, 0.12, 0.74), (0.09, 0.12, 0.52)]) + [(0, -0.15, 0.62)],
          (0, 0, 0), C, r=0.015)
    chunk('chest', mirror([(0.15, -0.14, 0.73), (0.10, -0.14, 0.54), (0.155, -0.08, 0.75), (0.105, -0.08, 0.52)]) +
          [(0, -0.185, 0.67), (0, -0.17, 0.55)], (0, 0, 0), Wt, r=0.012)
    chunk('hump', mirror([(0.15, -0.06, 0.77), (0.13, 0.08, 0.84), (0.12, 0.17, 0.74), (0.09, 0.15, 0.56)]) + [(0, 0.05, 0.90), (0, 0.20, 0.66)],
          (0, 0, 0), Wt, r=0.014)
    for i, x in enumerate((-0.06, 0.0, 0.06)):
        rbox(f'fin{i}', (0.018, 0.10, 0.06), (x, 0.12, 0.86 - abs(x) * 0.3), C, r=0.005, seg=1, rot=(-35, 0, 0))
    tube('antenna', (0.08, 0.10, 0.84), (0.12, 0.22, 1.04), 0.008, C, outline=False)
    ball('antip', 0.018, (0.12, 0.22, 1.045), O, outline=False)
    for s in (-1, 1):
        tube(f'exhaust{s}', (s * 0.10, 0.17, 0.70), (s * 0.12, 0.25, 0.76), 0.022, M, bevel=0.004)
    # head: a grey wedge thrust forward under a white beaked helm, one big eye in an orange ring, a vented jaw, swept sensor fins
    chunk('neck', mirror([(0.045, -0.08, 0.66), (0.045, -0.24, 0.70), (0.045, -0.08, 0.76), (0.045, -0.24, 0.80)]), (0, 0, 0), C, r=0.01)
    chunk('head', mirror([(0.09, -0.20, 0.83), (0.10, -0.35, 0.81), (0.105, -0.37, 0.70), (0.08, -0.20, 0.68)]) + [(0, -0.39, 0.76)],
          (0, 0, 0), M, r=0.012)
    chunk('helm', mirror([(0.115, -0.17, 0.89), (0.125, -0.36, 0.86), (0.115, -0.17, 0.82), (0.125, -0.37, 0.80)]) +
          [(0, -0.44, 0.815), (0, -0.20, 0.92), (0, -0.43, 0.845)], (0, 0, 0), Wt, r=0.01)
    ho, hr = (0, -0.385, 0.75), (-18, 0, 0)
    rbox('visor', (0.20, 0.03, 0.075), ho, C, r=0.012, seg=1, rot=hr, outline=False)
    tube('ring', place(ho, hr, (0, -0.012, 0)), place(ho, hr, (0, -0.030, 0)), 0.042, O, outline=False)
    ball('eye', 0.027, place(ho, hr, (0, -0.032, 0)), flat(EYE), scale=(1, 0.45, 1), rot=hr, outline=False)
    chunk('jaw', mirror([(0.08, -0.33, 0.70), (0.06, -0.37, 0.64), (0.08, -0.22, 0.66), (0.05, -0.24, 0.61)]), (0, 0, 0), M, r=0.01)
    for i, x in enumerate((-0.03, 0.0, 0.03)):
        rbox(f'grille{i}', (0.012, 0.01, 0.04), (x, -0.36, 0.665), C, r=0.003, seg=1, rot=(-30, 0, 0), outline=False)
    for s in (-1, 1):
        prism(f'sensor{s}', [(0, 0), (0.12, 0.05), (0.13, 0.08), (0, 0.04)], 0.016, (s * 0.10, -0.22, 0.85), L, r=0.004, seg=1,
              rot=(0, 0, -20) if s > 0 else (0, 0, 200))
    # shoulders: sloped orange slabs with charcoal hazard bands; scythe arms
    for s in (-1, 1):
        ball(f'sjoint{s}', 0.05, (s * 0.20, -0.04, 0.70), M)
        chunk(f'scap{s}', [(s * x, y, z) for y in (-0.13, 0.07) for x, z in ((0.12, 0.85), (0.27, 0.73), (0.28, 0.65), (0.15, 0.75))],
              (0, 0, 0), O, r=0.012)
        for k, y in enumerate((-0.075, -0.005)):
            rbox(f'haz{s}{k}', (0.17, 0.028, 0.012), (s * 0.20, y, 0.80), C, r=0.004, seg=1, rot=(0, s * 38.7, 0), outline=False)
        el, wr = (s * 0.27, -0.12, 0.52), (s * 0.27, -0.30, 0.50)
        tube(f'uarm{s}', (s * 0.20, -0.04, 0.70), el, 0.03, C)
        ball(f'elbow{s}', 0.042, el, M)
        chunk(f'farm{s}', [(s * 0.27 + dx, y, z) for dx in (-0.04, 0.04) for y, z in ((-0.11, 0.56), (-0.12, 0.47), (-0.30, 0.53), (-0.30, 0.47))]
              + [(s * 0.32, -0.20, 0.53)], (0, 0, 0), Wt, r=0.01)
        sweep(f'scythe{s}', [(s * 0.27, -0.30, 0.50), (s * 0.27, -0.42, 0.47), (s * 0.265, -0.51, 0.38), (s * 0.26, -0.50, 0.25),
                             (s * 0.255, -0.43, 0.17)], [0.04, 0.045, 0.038, 0.022, 0.003], L, seg=8, flat=0.22)
    # reverse-jointed legs: white thigh armour, charcoal shin with a piston, three claw toes and a heel spur
    for s in (-1, 1):
        hip, knee, hock = (s * 0.11, 0.06, 0.50), (s * 0.15, -0.10, 0.32), (s * 0.15, 0.12, 0.13)
        ball(f'hip{s}', 0.055, hip, M)
        chunk(f'thigh{s}', [(s * 0.15 + dx, y, z) for dx in (-0.055, 0.05) for y, z in ((0.10, 0.52), (0.0, 0.54), (-0.13, 0.33), (-0.06, 0.29))],
              (0, 0, 0), Wt, r=0.012)
        ball(f'knee{s}', 0.045, knee, M)
        tube(f'shin{s}', knee, hock, 0.03, C)
        tube(f'piston{s}', (s * 0.19, -0.06, 0.30), (s * 0.19, 0.10, 0.16), 0.012, L, outline=False)
        ball(f'hock{s}', 0.038, hock, M)
        for k in (-1, 0, 1):
            sweep(f'toe{s}{k}', [hock, (s * 0.15 + k * 0.035, 0.0, 0.05), (s * 0.15 + k * 0.06, -0.09, 0.0)], [0.026, 0.02, 0.004], M, seg=6)
        tube(f'spur{s}', hock, (s * 0.15, 0.22, 0.0), 0.02, M, r1=0.004)
    blob('blob', 0.44, 0.44)


# ------------------------------------------------------------------ 6  돌무더기: a hunched heap of boulders bound in orange iron bands

def band(name, centre, axis, r, mat, width=0.035, sy=1.0, seg=16):
    """An iron band round a rock: a short ring of radius r around the axis through centre."""
    a = Vector(axis).normalized()
    q = Vector((0, 0, 1)).rotation_difference(a).to_euler()
    return lathe(name, [(r, -width / 2), (r * 1.04, 0), (r, width / 2)], mat, seg=seg, thick=0.012, sy=sy, loc=centre,
                 rot=tuple(math.degrees(v) for v in q))


def strap(name, ob, centre, mat, width=0.04, seg=32):
    """An iron band hugging a rock's own outline at one height (vertical axis), not a round hoop that floats off it."""
    import bmesh
    import toon as T
    c = Vector(centre) * T.SC; hw = width * T.SC / 2
    pts = [ob.matrix_basis @ v.co for v in ob.data.vertices]
    bm = bmesh.new(); rings = ([], [])
    r = None
    for k in range(seg):
        a = 2 * math.pi * k / seg
        d = Vector((math.sin(a), -math.cos(a), 0))
        near = [(p - c).to_2d().length for p in pts if abs(p.z - c.z) < hw * 2.5 and (p - c).to_2d().normalized().dot(d.to_2d()) > 0.85]
        r = (max(near) if near else r or 0.08 * T.SC) + 0.004 * T.SC
        for ring, z in zip(rings, (-hw, hw)):
            ring.append(bm.verts.new((c.x + d.x * r, c.y + d.y * r, c.z + z)))
    for k in range(seg):
        k1 = (k + 1) % seg
        bm.faces.new((rings[0][k], rings[0][k1], rings[1][k1], rings[1][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=0.012 * T.SC)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return T._obj(name, bm, mat, (0, 0, 0))


def rubble(sc=1.15, **kw):
    begin('rubble', sc, **kw)
    M, L, C, O = toon(MGREY), toon(LGREY), toon(CHAR), toon(ORANGE)
    rocks = [  # name, size, centre, colour, seed, rot
        ('pelvis', (0.17, 0.13, 0.10), (0, 0.05, 0.34), M, 3, (0, 0, 0)),
        ('chest', (0.25, 0.19, 0.21), (0, 0.0, 0.56), L, 5, (-15, 0, 0)),
        ('hump', (0.17, 0.14, 0.12), (0, 0.13, 0.73), M, 7, (20, 0, 10)),
        ('head', (0.12, 0.11, 0.10), (0, -0.23, 0.74), L, 11, (-10, 0, 0)),
        ('shR', (0.12, 0.11, 0.11), (0.26, -0.02, 0.66), M, 13, (0, 20, 0)),
        ('shL', (0.11, 0.10, 0.10), (-0.25, -0.01, 0.65), M, 17, (0, -15, 0)),
        ('uarmR', (0.085, 0.085, 0.11), (0.32, -0.06, 0.49), L, 19, (0, 0, 0)),
        ('farmR', (0.105, 0.10, 0.12), (0.34, -0.13, 0.33), M, 23, (15, 0, 0)),
        ('fistR', (0.15, 0.14, 0.12), (0.34, -0.20, 0.13), L, 29, (0, 0, 20)),
        ('uarmL', (0.07, 0.07, 0.09), (-0.30, -0.06, 0.51), L, 31, (0, 0, 0)),
        ('farmL', (0.08, 0.08, 0.09), (-0.32, -0.15, 0.39), M, 37, (20, 0, 0)),
        ('fistL', (0.085, 0.085, 0.075), (-0.31, -0.23, 0.29), L, 41, (0, 0, -15)),
        ('thighR', (0.085, 0.085, 0.09), (0.12, 0.04, 0.23), M, 43, (0, 0, 0)),
        ('thighL', (0.085, 0.085, 0.09), (-0.12, 0.04, 0.23), M, 47, (0, 0, 0)),
        ('footR', (0.10, 0.13, 0.065), (0.13, -0.03, 0.065), L, 53, (0, 0, -10)),
        ('footL', (0.10, 0.13, 0.065), (-0.13, -0.01, 0.065), L, 59, (0, 0, 12)),
    ]
    rk = {name: rock(name, size, c, mat, n=18, seed=seed, r=0.012, rot=rot) for name, size, c, mat, seed, rot in rocks}
    # iron rods holding the limbs together, seen in the gaps
    for a, b in (('shR', 'uarmR'), ('uarmR', 'farmR'), ('farmR', 'fistR'), ('shL', 'uarmL'), ('uarmL', 'farmL'), ('farmL', 'fistL'),
                 ('pelvis', 'thighR'), ('pelvis', 'thighL'), ('thighR', 'footR'), ('thighL', 'footL'), ('chest', 'head')):
        pa = next(r[2] for r in rocks if r[0] == a); pb = next(r[2] for r in rocks if r[0] == b)
        tube(f'rod{a}{b}', pa, pb, 0.022, C)
    # orange iron bands: round the chest, both forearms, the right fist and one thigh, riveted
    band('bchest', (0, -0.01, 0.52), (0, 0.27, 1), 0.235, O, width=0.05, sy=0.78, seg=20)
    strap('bfarmR', rk['farmR'], (0.34, -0.13, 0.35), O, width=0.04)
    strap('bfistR', rk['fistR'], (0.34, -0.20, 0.17), O, width=0.045)
    strap('bthigh', rk['thighL'], (-0.12, 0.04, 0.24), O, width=0.03)
    for i, x in enumerate((-0.16, -0.08, 0.08, 0.16)):
        y = -0.01 - 0.19 * math.sqrt(1 - (x / 0.245) ** 2)     # on the chest band's front
        rivet(f'riv{i}', (x, y - 0.004, 0.52 - (y + 0.01) * 0.27), C, r=0.014, facing=(x * 3, -1, 0.25))
    # a tiny core glinting in a dark crack in the belly
    chunk('socket', [(-0.06, 0, 0.03), (0.05, 0, 0.04), (0.065, 0, -0.02), (-0.04, 0, -0.04), (-0.05, 0.03, 0.0), (0.05, 0.03, 0.0),
                     (0.0, -0.012, 0.0)], (0, -0.18, 0.45), C, r=0.006, rot=(-12, 0, 0))
    ball('core', 0.022, (0, -0.202, 0.45), flat(EYE), scale=(1.5, 0.4, 0.6), rot=(-12, 0, 0), outline=False)
    # head: a heavy brow stone over two slit eyes in a cut slot
    ho, hr = (0, -0.335, 0.745), (-15, 0, 0)
    prism('slot', [(-0.09, 0.022), (0.09, 0.022), (0.075, -0.02), (-0.075, -0.02)], 0.02, ho, C, r=0, rot=hr, outline=False)
    slits('eye', ho, hr, 0.042, 0.0, w=0.05, h=0.018, slant=0.018, y=-0.008, recess=False)
    chunk('brow', mirror([(0.12, 0.0, 0.03), (0.10, 0.06, 0.06), (0.08, -0.03, 0.0)]) + [(0, -0.05, 0.035), (0, 0.03, 0.08), (0, -0.045, -0.005)],
          place(ho, hr, (0, 0.02, 0.035)), M, r=0.012, rot=hr)
    blob('blob', 0.46, 0.42)


# ------------------------------------------------------------------ 7  철갑 들개: a lean war hound, plated head, spiked orange collar

def hound(sc=1.1, **kw):
    begin('hound', sc, **kw)
    S, C, Wt, L, O = toon(SLATE), toon(CHAR), toon(WHITE), toon(LGREY), toon(ORANGE)
    # body: one skin graph, deep chest, tucked waist, head held low, one forepaw stepping out, digitigrade hind legs
    V = [(0, -0.17, 0.44), (0, 0.05, 0.45), (0, 0.25, 0.45), (0, -0.30, 0.47), (0, -0.39, 0.44),
         (-0.10, -0.20, 0.38), (-0.12, -0.25, 0.21), (-0.12, -0.27, 0.07), (-0.12, -0.32, 0.03),
         (0.10, -0.20, 0.38), (0.12, -0.31, 0.22), (0.12, -0.37, 0.08), (0.12, -0.42, 0.03),
         (-0.10, 0.27, 0.40), (-0.12, 0.17, 0.25), (-0.12, 0.33, 0.12), (-0.12, 0.28, 0.03),
         (0.10, 0.27, 0.40), (0.12, 0.19, 0.25), (0.12, 0.36, 0.12), (0.12, 0.31, 0.03),
         (0, 0.36, 0.49), (0, 0.48, 0.55), (0, 0.59, 0.54), (0, 0.66, 0.48)]
    E = [(0, 1), (1, 2), (0, 3), (3, 4), (0, 5), (5, 6), (6, 7), (7, 8), (0, 9), (9, 10), (10, 11), (11, 12),
         (2, 13), (13, 14), (14, 15), (15, 16), (2, 17), (17, 18), (18, 19), (19, 20), (2, 21), (21, 22), (22, 23), (23, 24)]
    R = [(0.14, 0.17), (0.10, 0.11), (0.11, 0.12), 0.09, 0.075,
         0.07, 0.048, 0.035, 0.042, 0.07, 0.048, 0.035, 0.042,
         0.08, 0.06, 0.035, 0.042, 0.08, 0.06, 0.035, 0.042,
         0.04, 0.03, 0.02, 0.006]
    skin('body', V, E, R, S, subdiv=1)
    for i, p in enumerate((V[8], V[12], V[16], V[20])):     # white claws
        for k in (-1, 0, 1):
            tube(f'claw{i}{k}', (p[0] + k * 0.025, p[1] - 0.03, 0.025), (p[0] + k * 0.03, p[1] - 0.075, 0.004), 0.012, Wt, r1=0.002,
                 outline=False)
    # back armour: three overlapping white plates down the spine, riveted, each with a short ridge spike
    for i, (y, r) in enumerate(((-0.13, 0.185), (0.03, 0.16), (0.19, 0.165))):
        shell(f'plate{i}', r, (0, y, 0.45), Wt, lat=(15, 90), thick=0.02, scale=(1.0, 0.72, 0.98), rot=(-8 + i * 4, 0, 0), seg=(12, 4))
        top = 0.45 + r * 0.98
        tube(f'ridge{i}', (0, y - 0.03, top - 0.01), (0, y + 0.05, top + 0.07), 0.022, L, r1=0.003)
        for s in (-1, 1):
            rivet(f'riv{i}{s}', (s * r * 0.92, y, 0.45 + r * 0.42), C, r=0.013, facing=(s, 0, 0.5))
    # spiked orange collar
    co, cr = (0, -0.29, 0.47), (75, 0, 0)
    lathe('collar', [(0.10, -0.03), (0.108, 0.0), (0.10, 0.03)], O, seg=16, thick=0.014, loc=co, rot=cr)
    for k in range(6):
        a = math.radians(k * 60 + 30)
        d = Euler([math.radians(v) for v in cr]).to_matrix() @ Vector((math.sin(a), -math.cos(a), 0))
        tube(f'cspike{k}', tuple(Vector(co) + d * 0.10), tuple(Vector(co) + d * 0.17), 0.018, L, r1=0.002)
    # head: a slate skull and snout under a white plated helm with slit eyes, swept ears, a snarl with white fangs
    ball('skull', 0.095, (0, -0.44, 0.47), S, scale=(1.05, 1.2, 0.9))
    chunk('snout', mirror([(0.05, -0.48, 0.48), (0.045, -0.64, 0.44), (0.05, -0.48, 0.40), (0.04, -0.63, 0.40)]), (0, 0, 0), S, r=0.012)
    chunk('nose', mirror([(0.02, -0.635, 0.445), (0.02, -0.655, 0.43), (0.02, -0.63, 0.415)]), (0, 0, 0), C, r=0.004, outline=False)
    chunk('mouth', mirror([(0.045, -0.47, 0.40), (0.04, -0.61, 0.40), (0.04, -0.47, 0.37), (0.035, -0.60, 0.375)]), (0, 0, 0), C, r=0.006,
          outline=False)
    chunk('jaw', mirror([(0.05, -0.45, 0.37), (0.04, -0.60, 0.36), (0.04, -0.46, 0.33), (0.03, -0.57, 0.335)]), (0, 0, 0), S, r=0.01)
    for s in (-1, 1):
        tube(f'fang{s}', (s * 0.032, -0.60, 0.41), (s * 0.034, -0.605, 0.36), 0.011, Wt, r1=0.002, outline=False)
        tube(f'lfang{s}', (s * 0.028, -0.575, 0.355), (s * 0.03, -0.58, 0.395), 0.009, Wt, r1=0.002, outline=False)
    chunk('helm', mirror([(0.095, -0.37, 0.53), (0.07, -0.57, 0.485), (0.11, -0.40, 0.45), (0.065, -0.57, 0.445), (0.055, -0.32, 0.57)]) +
          [(0, -0.615, 0.475), (0, -0.40, 0.59)], (0, 0, 0), Wt, r=0.008)
    prism('hbrow', [(-0.10, 0.0), (0.10, 0.0), (0.08, 0.03), (0.0, 0.0), (-0.08, 0.03)], 0.03, (0, -0.50, 0.515), Wt, r=0.006, seg=1,
          rot=(-35, 0, 0), bend=2.5)
    slits('eye', (0, -0.505, 0.49), (-35, 0, 0), 0.05, 0.0, w=0.045, h=0.016, slant=0.02, y=-0.015)
    for s in (-1, 1):
        prism(f'ear{s}', [(0, 0), (0.05, 0.0), (0.02, 0.14)], 0.016, (s * 0.06, -0.36, 0.54), S, r=0.005, seg=1, rot=(-55, 0, s * -20))
        tube(f'hspike{s}', (s * 0.07, -0.44, 0.52), (s * 0.12, -0.38, 0.58), 0.018, L, r1=0.003)
    blob('blob', 0.36, 0.62, loc=(0, 0.05, 0.003))


ALL = {'1': acolyte, '2': footman, '3': beetle, '4': imp, '5': drone, '6': rubble, '7': hound}
