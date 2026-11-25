# 탱커병 1 철방패 거병 with its tower shield 1.7x as large (round 3's t1, otherwise unchanged).
# The shield is rebuilt about its own pivot at K times the build scale (so bevels, rivets and the boss grow with it and
# the outline keeps its pixel width), lifted so its bottom tip sits on the ground as before, and pushed forward to clear
# the tusks; the left arm reaches out to its new edge and the claws wrap the thicker rim.
import math
from mathutils import Vector
import toon as T
from toon import rbox, sbox, ball, tube, shell, sweep, bentprism, bent_y, blob, begin, group, ungroup
from models import m, slit_eyes, rivets, limb, pauldron, horn
from palette import WHITE, LGREY, MGREY, CHAR, GUN, AMBER

K = 1.7
GY_MID = -1.00                             # the 1.3x reference: pushed forward just enough to clear the tusks
TILT = -6                                  # the shield leans back at the top, as in round 3
G0 = Vector((0.10, -0.82, 0.70))           # round 3's shield pivot
OUT = [(-0.60, -0.50), (-0.36, -0.60), (0, -0.68), (0.36, -0.60), (0.60, -0.50), (0.62, 0.40), (0.44, 0.54), (0, 0.60),
       (-0.44, 0.54), (-0.62, 0.40)]
GRIP = 0.80                                # the claws' height above the ground (round 3)


def pivot(k, gy):
    """The shield pivot for scale k: same x, lifted so the rim's bottom tip stays at round 3's height."""
    c, low = math.cos(math.radians(TILT)), -0.68 * 1.06
    return Vector((G0.x, gy, G0.z + (-low) * c * (k - 1)))


def edge(k, g, zw=GRIP):
    """Where the rim's left edge is (middle of its thickness) at world height zw, for shield scale k at pivot g."""
    t = math.radians(TILT)
    zl = (zw - g.z) / math.cos(t)
    zu = zl / (k * 1.06)
    xu = -0.60 - 0.02 * (zu + 0.50) / 0.90                # the outline's left side between (-0.60,-0.50) and (-0.62,0.40)
    R, x = 1.5 * k, xu * 1.07 * k
    X, Y = R * math.sin(x / R), R * (1 - math.cos(x / R)) + 0.03 * k
    return g + Vector((X, Y * math.cos(t) - zl * math.sin(t), Y * math.sin(t) + zl * math.cos(t)))


def elbow(S, F, l1, l2, bend):
    """Two-bone reach from S to F, the elbow pushed toward bend."""
    v = F - S; d = v.length; u = v / d
    if d >= l1 + l2: return S + u * d * l1 / (l1 + l2)
    a = (l1 * l1 - l2 * l2 + d * d) / (2 * d); h = math.sqrt(max(l1 * l1 - a * a, 0))
    b = (bend - u * bend.dot(u)).normalized()
    return S + u * a + b * h


def t1big(sc=1.0, k=K, gy=-1.02, claw=1.35, **kw):
    root = begin('t1big', sc, **kw)
    G, W, L, Mg, A = m(GUN), m(WHITE), m(LGREY), m(MGREY), m(AMBER)
    # ---- unchanged from round 3: legs, hips, torso, pauldrons, head
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
    sbox('chest', (1.02, 0.80, 0.76), (0, 0.0, 1.06), G, r=0.20, levels=1, rot=(14, 0, 0), taper=(1.1, 1.0))
    shell('backplate', (0.56, 0.46, 0.50), (0, 0.08, 1.08), W, lon=(122, 238), lat=(-46, 70), thick=0.06, seg=(10, 6), rot=(14, 0, 0))
    sweep('spine', [(0, 0.40, 0.80), (0, 0.55, 1.02), (0, 0.52, 1.30), (0, 0.36, 1.50)], [(0.05, 0.03), (0.06, 0.06), (0.06, 0.06), (0.04, 0.03)],
          L, seg=8)
    for s in (-1, 1):
        pauldron(f'pd{s}', (s * 0.62, 0.02, 1.36), 0.33, W, L, side=s)
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
    # ---- left arm: round 3's hand and claws moved with the shield's left edge, the arm re-posed to reach it
    g = pivot(k, gy)
    d = edge(k, g) - edge(1.0, G0)
    H0 = Vector((-0.64, -0.62, 0.80))
    H, F, S = H0 + d, Vector((-0.66, -0.58, 0.80)) + d, Vector((-0.66, -0.06, 1.18))
    E = elbow(S, F, 0.50, 0.44, Vector((-1.0, 0.25, -0.35)))
    tube('uarmL', tuple(S), tuple(E), 0.14, G, segs=12)
    ball('elbowL', 0.15, tuple(E), Mg, seg=(12, 8))
    limb('farmL', tuple(E), tuple(F), 0.26, 0.26, W, r=0.07, taper=(0.85, 0.85))
    sbox('handL', (0.18, 0.20, 0.22), tuple(H), G, r=0.06, levels=1)
    for i, z in enumerate((0.72, 0.80, 0.88)):      # longer in plan (x, y) to wrap the thicker rim, same spacing up the edge
        pts = [(-0.62, -0.66, z), (-0.60, -0.75, z + 0.01), (-0.52, -0.81, z), (-0.45, -0.80, z - 0.02)]
        pts = [H + Vector(((x - H0.x) * claw, (y - H0.y) * claw, zz - H0.z)) for x, y, zz in pts]
        sweep(f'claw{i}', [tuple(p) for p in pts], [0.048, 0.045, 0.034, 0.009], L, seg=8)
    # ---- the tower shield, k times round 3's, about its own pivot
    group('shield', tuple(g), (TILT, 0, 0))
    T.SC = sc * k
    B = 1.5
    bentprism('rim', [(x * 1.07, z * 1.06) for x, z in OUT], 0.08, (0, 0.03, 0), A, bend=B, r=0.025, cuts=8, seg=1)
    bentprism('face', OUT, 0.06, (0, -0.02, 0), W, bend=B, r=0.02, cuts=8, seg=1)
    bentprism('panel', [(x * 0.80, z * 0.82) for x, z in OUT], 0.03, (0, -0.055, 0), W, bend=B - 0.03, r=0.012, cuts=6, seg=1)
    for x in (-0.36, 0.36):
        bentprism(f'strap{x}', [(x - 0.055, -0.56), (x + 0.055, -0.56), (x + 0.055, 0.48), (x - 0.055, 0.48)], 0.03,
                  (0, -0.075, 0), L, bend=B - 0.06, r=0.01, cuts=2)
        rivets(f'srv{x}', [(x, -0.09 + bent_y(x, B), z) for z in (-0.44, -0.12, 0.20, 0.42)])
    tube('bossring', (0, -0.06, -0.06), (0, -0.13, -0.06), 0.22, Mg, bevel=0.02, segs=20)
    ball('boss', 0.16, (0, -0.12, -0.06), G, scale=(1, 0.75, 1), seg=(16, 10))
    sweep('bossspike', [(0, -0.22, -0.06), (0, -0.36, -0.06)], [0.065, 0.008], L, seg=10)
    rivets('bstud', [(math.cos(math.radians(a)) * 0.19, -0.135, -0.06 + math.sin(math.radians(a)) * 0.19) for a in range(0, 360, 45)])
    T.SC = sc
    ungroup()
    blob('blob', 1.05, 1.15, loc=(0, -0.15, 0.003))
    return root
