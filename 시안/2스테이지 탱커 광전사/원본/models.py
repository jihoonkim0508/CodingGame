# Three berserker (광전사) tank drafts for stage 2, in the robots' colour scheme: an original hulking brute
# with a small sunk head, huge shoulders and forearms, an oversized two-handed blade, worn plates and chain.
# Design units: the real melee robot stands 1.55. Front is -y; the weapon is on +x, the side the battle camera sees.
import math
from mathutils import Vector, Matrix, Euler
from toon import rbox, ball, tube, prism, sweep, shell, chain, arcplate, blob, toon, flat, begin, group, ungroup
from palette import WHITE, LGREY, MGREY, CHAR, GUN, CRIMSON, SCARLET, GARNET
import round2


def m(rgb):
    return toon(rgb)


def V(p):
    return Vector(p)


def look(fwd, up=(0, 0, 1)):
    """Rotation that turns a part's front (-y) toward fwd and its top (+z) toward up."""
    y = -V(fwd).normalized(); z = V(up); z = (z - z.dot(y) * y).normalized(); x = y.cross(z)
    return Matrix((x, y, z)).transposed().to_quaternion()


def along(z, x=(0, 0, 1)):
    """Rotation that turns a part's +z along z and its +x toward x."""
    z = V(z).normalized(); x = V(x); x = (x - x.dot(z) * z).normalized(); y = z.cross(x)
    return Matrix((x, y, z)).transposed().to_quaternion()


def tilt(roll, yaw):
    """Turn about z by yaw first, then roll about y: a fin set front-to-back on a pauldron that leans outward."""
    return Euler((0, math.radians(roll), 0)).to_quaternion() @ Euler((0, 0, math.radians(yaw))).to_quaternion()


def placer(loc, rot):
    """Where a point given in a group's frame (group(loc, rot)) lands in the design's frame."""
    R = Euler([math.radians(a) for a in rot]).to_matrix()
    return lambda p: V(loc) + R @ V(p)


# ------------------------------------------------------------------ shared body parts

def leg(n, hip, knee, ankle, body, toe=0.0, yaw=0.0, thigh=MGREY, cap=LGREY):
    hip, knee, ankle = V(hip), V(knee), V(ankle)
    tube(f'thigh{n}', hip, knee, 0.16, m(thigh), r1=0.13, segs=18, bev=0.04)
    ball(f'knee{n}', 0.12, knee, m(CHAR), seg=(18, 12), outline=False)
    tube(f'shin{n}', knee, ankle, 0.13, m(body), r1=0.175, segs=18, bev=0.035)        # the greave flares toward the boot
    out = ((knee - hip).normalized() + (knee - ankle).normalized()).normalized()
    rbox(f'kneecap{n}', (0.22, 0.09, 0.22), knee + out * 0.12, m(cap), r=0, bev=0.035, quat=look(out, (0, 0, 1)))
    q = Euler((math.radians(toe), 0, math.radians(yaw))).to_quaternion()
    f = q @ V((0, -1, 0))
    rbox(f'boot{n}', (0.38, 0.50, 0.22), V((ankle.x, ankle.y, 0.12)) + f * 0.07, m(body), r=0.10, sub=2, quat=q)
    rbox(f'toe{n}', (0.36, 0.17, 0.15), V((ankle.x, ankle.y, 0.09)) + f * 0.29, m(cap), r=0.06, sub=2, quat=q)


def arm(n, sh, elb, hand, body, fist_fwd, fist_up=(0, 0, 1), cuff=LGREY, big=1.0, upper=MGREY):
    """Shoulder to elbow to the fist's centre (hand); the gauntlet flares toward the fist."""
    sh, elb, hand = V(sh), V(elb), V(hand)
    d = (hand - elb).normalized()
    wrist = hand - d * 0.13 * big
    out = V((1 if sh.x > 0 else -1, 0, 0))
    tube(f'uarm{n}', sh, elb, 0.14, m(upper), r1=0.125, segs=18, bev=0.04)
    ball(f'elb{n}', 0.13, elb, m(CHAR), seg=(18, 12), outline=False)
    # the gauntlet: a bevelled block that widens to the wrist, a cuff band over its end, then the fist
    g = (wrist - elb).length
    q = along(d, out)
    rbox(f'farm{n}', (0.26 * big, 0.26 * big, g), (elb + wrist) / 2, m(body), r=0, bev=0.05, taper=(1.35, 1.3), quat=q)
    rbox(f'cuff{n}', (0.37 * big, 0.36 * big, 0.07), wrist - d * 0.02, m(cuff), r=0, bev=0.025, quat=q)
    q = look(fist_fwd, fist_up); R = q.to_matrix()
    rbox(f'fist{n}', (0.32 * big, 0.31 * big, 0.30 * big), hand, m(body), r=0.10, sub=2, quat=q)
    rbox(f'knuck{n}', (0.30 * big, 0.08, 0.13 * big), hand + R @ V((0, -0.155 * big, 0.05 * big)), m(cuff), r=0, bev=0.025, quat=q)
    return hand


def eyes(z, y, A, w=0.10, h=0.036, gap=0.075, tilt=16):
    """Two slanted slits, inner ends low: a scowl under the brow (flat colour, no glow past it)."""
    for s in (-1, 1):
        rbox(f'eye{s}', (w, 0.03, h), (s * gap, y, z), flat(A), r=h * 0.45, rot=(0, -s * tilt, 0), outline=False)


def crescent(cx, cz, R, a0, a1, n=14, notch=None):
    """Points of an axe edge: an arc round (cx, cz) from angle a0 to a1 (degrees from -x, up positive)."""
    pts = []
    for i in range(n + 1):
        a = a0 + (a1 - a0) * i / n
        r = R - (0.055 if notch is not None and abs(a - notch) < 1e-6 else 0)
        pts.append((cx - r * math.cos(math.radians(a)), cz + r * math.sin(math.radians(a))))
    return pts


def blade(name, edge, back, depth, body, band=WHITE, side=-1, bandw=0.075):
    """One axe bit in the x/z plane, toward side (-1: -x): a steel body thinning to a bright honed edge band.
    edge: the cutting arc (top to bottom); back: the return to the haft (bottom to top)."""
    sx = lambda pts: [(x * -side, z) for x, z in pts]
    reach = max(abs(x) for x, _ in edge)
    thin = lambda x, z: 1 - 0.6 * min(1, max(0, (abs(x) - 0.05) / (reach - 0.05)))
    prism(name, sx(edge + back), depth, (0, 0, 0), m(body), thin=thin, bev=0.012)
    # the band: the same arc and an inner arc a band-width inside it, a hair thicker than the body there
    inner = []
    for i, (x, z) in enumerate(edge):
        a, b = edge[max(i - 1, 0)], edge[min(i + 1, len(edge) - 1)]
        tx, tz = b[0] - a[0], b[1] - a[1]; L = math.hypot(tx, tz)
        nx, nz = -tz / L, tx / L                       # inward for an arc traced top to bottom on the -x side
        k = 0.35 if i in (0, len(edge) - 1) else 1.0   # taper the band to points at the tips
        inner.append((x + nx * bandw * k, z + nz * bandw * k))
    prism(name + '_edge', sx(edge + inner[::-1]), depth + 0.01, (0, 0, 0), m(band), thin=thin, bev=0.006)


def axe(name, head, hand, blade_dir, A, double=False, tail=0.22, R=0.48, haft_r=0.055):
    """A great axe: the head's centre, the hand that holds it (the haft runs on past it by tail), the way the bit
    points. Steel bit with a bright honed edge and a notch, a gunmetal socket, a red binding under the head."""
    head, hand = V(head), V(hand)
    up = (hand - head).normalized()                  # local +z: up the haft, toward the hand
    group(name, head, quat=along(up, -V(blade_dir)))  # the bit points to local -x
    L = (hand - head).length + tail
    tube('haft', (0, 0, -0.20), (0, 0, L), haft_r, m(MGREY), segs=16, bev=0.02)
    tube('grip', (0, 0, L - 0.50), (0, 0, L - 0.03), haft_r + 0.01, m(CHAR), segs=16, bev=0.015)
    tube('bind', (0, 0, 0.30), (0, 0, 0.40), haft_r + 0.014, m(A), segs=16, bev=0.015)
    ball('pommel', haft_r + 0.03, (0, 0, L + 0.02), m(LGREY), seg=(18, 12))
    rbox('socket', (0.17, 0.16, 0.42), (0, 0, 0.05), m(GUN), r=0, bev=0.035)
    edge = crescent(-0.14, 0.04, R, 64, -72, n=16, notch=64 - 136 * 6 / 16)
    k = R / 0.48
    back = [(-0.25 * k, -0.32 * k), (-0.13, -0.17), (-0.07, -0.13), (-0.07, 0.20), (-0.20 * k, 0.25 * k), (-0.30 * k, 0.34 * k)]
    blade('blade', edge, back, 0.10, LGREY)
    if double:
        blade('blade2', crescent(-0.14, 0.04, R, 64, -72, n=16), back, 0.10, LGREY, side=1)
        tube('spike', (0, 0, -0.18), (0, 0, -0.42), 0.06, m(LGREY), r1=0.01, segs=16, bev=0.01)
    else:
        rbox('poll', (0.17, 0.12, 0.16), (0.13, 0, 0.06), m(GUN), r=0, bev=0.03)
    ungroup()


# ------------------------------------------------------------------ 1 뿔투구 광전사: horned helm, axe dragged behind

def b1(sc=1.04, **kw):
    begin('b1', sc, **kw); A = CRIMSON; Wt = m(WHITE)
    leg('L', (-0.24, 0.02, 0.58), (-0.36, -0.20, 0.36), (-0.38, -0.16, 0.15), WHITE, yaw=-10)
    leg('R', (0.24, 0.08, 0.58), (0.36, 0.22, 0.34), (0.40, 0.40, 0.15), WHITE, toe=-6, yaw=12)
    rbox('pelvis', (0.62, 0.44, 0.26), (0, 0.05, 0.60), m(MGREY), r=0.10, sub=2)
    rbox('belt', (0.72, 0.52, 0.11), (0, 0.04, 0.70), m(CHAR), r=0.04, sub=2)
    rbox('buckle', (0.18, 0.06, 0.14), (0, -0.25, 0.70), m(LGREY), r=0, bev=0.025)
    prism('loin', [(-0.19, 0.0), (0.19, 0.0), (0.23, -0.36), (-0.23, -0.36)], 0.045, (0, -0.27, 0.68), m(A),
          r=0, bev=0.012, rot=(-12, 0, 0))
    prism('loinplate', [(-0.20, 0.0), (0.20, 0.0), (0.17, -0.12), (-0.17, -0.12)], 0.05, (0, -0.30, 0.67), m(LGREY),
          r=0, bev=0.015, rot=(-12, 0, 0))
    for s in (-1, 1):
        rbox(f'tasset{s}', (0.26, 0.36, 0.26), (s * 0.38, 0.02, 0.56), Wt, r=0, bev=0.04, rot=(0, s * 18, 0))

    TL, TR = (0, 0.04, 0.66), (14, 0, 0)
    P = placer(TL, TR)
    group('torso', TL, TR)
    rbox('abs', (0.60, 0.44, 0.30), (0, 0.02, 0.12), m(MGREY), r=0.11, sub=2)
    rbox('chest', (0.92, 0.66, 0.58), (0, 0, 0.42), Wt, r=0, bev=0.13, taper=(1.16, 1.0))
    rbox('sternum', (0.15, 0.08, 0.38), (0, -0.33, 0.42), m(LGREY), r=0, bev=0.03, taper=(1.5, 1))
    rbox('hump', (0.62, 0.36, 0.24), (0, 0.22, 0.62), m(LGREY), r=0, bev=0.08)
    for i, (x, z) in enumerate(((-0.30, 0.48), (-0.22, 0.42))):     # two old scratches across the plate
        rbox(f'scratch{i}', (0.17, 0.02, 0.016), (x, -0.335, z), m(LGREY), r=0.006, rot=(0, 32, 0), outline=False)
    for s in (-1, 1):
        ball(f'shj{s}', 0.15, (s * 0.50, 0.0, 0.52), m(CHAR), seg=(18, 12), outline=False)
        shell(f'lame2{s}', (0.36, 0.39, 0.22), (s * 0.62, 0.02, 0.50), m(LGREY), az=(-180, 180), el=(-40, -6), thick=0.05,
              rot=(0, s * 24, 0), rows=4, cols=28)
        shell(f'lame1{s}', (0.36, 0.39, 0.22), (s * 0.60, 0.02, 0.56), Wt, az=(-180, 180), el=(-28, 0), thick=0.05,
              rot=(0, s * 24, 0), rows=4, cols=28)
        shell(f'pad{s}', (0.35, 0.38, 0.21), (s * 0.58, 0.02, 0.61), m(A), az=(-180, 180), el=(-6, 90), thick=0.06,
              rot=(0, s * 24, 0), rows=8, cols=28)
        ball(f'stud{s}', 0.055, (s * 0.665, 0.02, 0.80), m(LGREY), seg=(16, 10))
    group('head', (0, -0.32, 0.66), (-24, 0, 0))
    rbox('helm', (0.40, 0.40, 0.36), (0, 0, 0.03), Wt, r=0, bev=0.10, taper=(0.9, 0.9))
    rbox('visor', (0.32, 0.06, 0.11), (0, -0.195, 0.0), m(CHAR), r=0.045, sub=2, outline=False)
    eyes(0.004, -0.228, A)
    prism('brow', [(-0.25, 0.02), (0, -0.045), (0.25, 0.02), (0.24, 0.10), (0, 0.055), (-0.24, 0.10)], 0.12,
          (0, -0.18, 0.09), m(LGREY), r=0, bev=0.02)
    rbox('jaw', (0.26, 0.10, 0.10), (0, -0.17, -0.13), m(LGREY), r=0, bev=0.025)
    for s in (-1, 1):
        horn = [(s * 0.15, 0.0, 0.10), (s * 0.30, -0.02, 0.13), (s * 0.44, -0.07, 0.23), (s * 0.51, -0.15, 0.37), (s * 0.50, -0.23, 0.50)]
        sweep(f'horn{s}', horn, [0.10, 0.092, 0.072, 0.048, 0.016], m(LGREY), seg=18, sub=1)
        tube(f'hornring{s}', (s * 0.18, 0.0, 0.10), (s * 0.25, -0.005, 0.112), 0.11, m(A), segs=18, bev=0.015)
    ungroup()
    ungroup()
    # a chain worn as a bandolier, right pauldron to left hip, lying on the chest plate
    chain('band', [P((0.36, -0.33, 0.66)), P((0.12, -0.39, 0.46)), P((-0.14, -0.40, 0.28)), P((-0.34, -0.35, 0.10))], m(MGREY),
          link=0.075, wire=0.02)
    # free fist low and forward; the axe hand at the +x hip, dragging the axe behind and out
    arm('L', P((-0.50, 0.0, 0.52)), (-0.82, -0.10, 0.84), (-0.80, -0.46, 0.50), WHITE, fist_fwd=(0, -0.5, -0.85), fist_up=(0, -0.85, 0.5))
    hand = arm('R', P((0.50, 0.0, 0.52)), (0.84, -0.06, 0.84), (0.92, -0.06, 0.48), WHITE, fist_fwd=(0.3, -0.9, -0.2), fist_up=(0.1, 0.4, 0.9))
    head = V((1.16, 0.92, 0.36))
    d = (head - hand).normalized(); h = V((d.y, -d.x, 0)).normalized()
    axe('axe', head, hand, h * math.cos(math.radians(35)) + V((0, 0, -math.sin(math.radians(35)))), A, R=0.52)
    blob('blob', 1.05, 0.85, loc=(0.20, 0.15, 0.003))


# ------------------------------------------------------------------ 2 쌍날 도끼 광전사: gunmetal, double-bit axe cocked over the shoulder

def b2(sc=1.02, **kw):
    begin('b2', sc, **kw); A = SCARLET; G = m(GUN)
    leg('L', (-0.26, 0.04, 0.58), (-0.42, -0.14, 0.36), (-0.46, -0.10, 0.15), GUN, yaw=-14, cap=MGREY)
    leg('R', (0.26, 0.06, 0.58), (0.42, 0.12, 0.36), (0.46, 0.24, 0.15), GUN, yaw=16, cap=MGREY)
    rbox('pelvis', (0.62, 0.44, 0.26), (0, 0.05, 0.60), m(MGREY), r=0.10, sub=2)
    rbox('sash', (0.76, 0.56, 0.13), (0, 0.04, 0.71), m(A), r=0.05, sub=2)
    for i, (x, rz, ln) in enumerate(((-0.30, 8, 0.40), (-0.20, -6, 0.34))):     # the sash's knotted tails on the left hip
        prism(f'tail{i}', [(-0.06, 0.0), (0.06, 0.0), (0.05, -ln), (0.0, -ln + 0.06), (-0.05, -ln)], 0.035,
              (x, -0.26, 0.68), m(A), r=0, bev=0.01, rot=(-10, 0, rz))
    rbox('knot', (0.16, 0.09, 0.14), (-0.25, -0.29, 0.70), m(A), r=0.05, sub=2)
    for s in (-1, 1):
        rbox(f'tasset{s}', (0.26, 0.36, 0.24), (s * 0.38, 0.02, 0.56), G, r=0, bev=0.04, rot=(0, s * 18, 0))

    TL, TR = (0, 0.04, 0.66), (10, 0, 0)
    P = placer(TL, TR)
    group('torso', TL, TR)
    rbox('abs', (0.60, 0.44, 0.30), (0, 0.02, 0.12), m(MGREY), r=0.11, sub=2)
    rbox('chest', (0.96, 0.68, 0.60), (0, 0, 0.42), G, r=0, bev=0.13, taper=(1.2, 1.0))
    prism('plate', [(-0.34, 0.20), (0.34, 0.20), (0.24, -0.06), (0.0, -0.14), (-0.24, -0.06)], 0.06, (0, -0.335, 0.46),
          m(LGREY), r=0, bev=0.02)
    rbox('stripe', (0.08, 0.02, 0.26), (0, -0.37, 0.44), m(A), r=0.01, outline=False)
    rbox('hump', (0.70, 0.40, 0.30), (0, 0.24, 0.62), m(MGREY), r=0, bev=0.09)
    for s in (-1, 1):
        ball(f'shj{s}', 0.15, (s * 0.50, 0.0, 0.52), m(CHAR), seg=(18, 12), outline=False)
        # one angular block per shoulder, sloping outward, a light trim plate along its outer edge and a spike
        rbox(f'pauld{s}', (0.50, 0.60, 0.34), (s * 0.60, 0.02, 0.62), G, r=0, bev=0.06, taper=(0.62, 0.86), rot=(0, s * 26, 0))
        rbox(f'trim{s}', (0.12, 0.64, 0.22), (s * 0.80, 0.02, 0.50), m(LGREY), r=0, bev=0.035, rot=(0, s * 26, 0))
        tube(f'spike{s}', (s * 0.62, 0.02, 0.76), (s * 0.74, 0.02, 1.00), 0.07, m(LGREY), r1=0.012, segs=16, bev=0.01)
    group('head', (0, -0.32, 0.68), (-22, 0, 0))
    rbox('helm', (0.40, 0.40, 0.36), (0, 0, 0.03), G, r=0, bev=0.10, taper=(0.9, 0.9))
    rbox('mask', (0.30, 0.08, 0.24), (0, -0.18, -0.03), m(LGREY), r=0, bev=0.03, taper=(1.3, 1))
    rbox('visor', (0.28, 0.05, 0.07), (0, -0.215, 0.02), m(CHAR), r=0.03, sub=2, outline=False)
    rbox('nose', (0.06, 0.05, 0.14), (0, -0.215, -0.05), m(CHAR), r=0.025, sub=2, outline=False)   # the T of the visor
    eyes(0.02, -0.242, A, w=0.09, h=0.032, gap=0.072, tilt=14)
    prism('brow', [(-0.23, 0.0), (0, -0.04), (0.23, 0.0), (0.22, 0.07), (0, 0.04), (-0.22, 0.07)], 0.10,
          (0, -0.19, 0.10), G, r=0, bev=0.018)
    prism('crest', [(-0.24, 0.0), (0.20, 0.0), (0.22, 0.10), (0.10, 0.17), (-0.06, 0.21), (-0.22, 0.17)], 0.06,
          (0, 0.0, 0.19), m(A), r=0, bev=0.018, rot=(0, 0, 90))
    ungroup()
    ungroup()
    # both fists on the haft, the double-bit head cocked high over the +x shoulder
    H1, H2 = V((-0.02, -0.64, 0.74)), V((0.30, -0.38, 1.08))
    d = (H2 - H1).normalized()
    arm('R', P((0.50, 0.0, 0.52)), (0.74, -0.24, 0.82), H2, GUN, fist_fwd=(0.3, -0.9, -0.2), fist_up=d, cuff=MGREY)
    arm('L', P((-0.50, 0.0, 0.52)), (-0.62, -0.40, 0.84), H1, GUN, fist_fwd=(0.2, -0.9, -0.3), fist_up=d, cuff=MGREY)
    top = H2 + d * 0.86
    cam = V((0.40, -0.68, 0.58))
    side = d.cross(cam).normalized()                 # the bits spread across the view, flat toward the camera
    axe('axe', top, H1, side, A, double=True, tail=0.26, R=0.44)
    blob('blob', 1.0, 0.8)


# ------------------------------------------------------------------ 3 거대 견갑 광전사: one huge pauldron, a cleaver on a chain

def cleaver(name, hand, fwd, edge, A, L=0.86, H=0.42):
    """A great cleaver in the fist, pointing fwd: the spine runs on from the handle, the blade drops toward edge.
    Returns where its chain hole is."""
    hand = V(hand); z = -V(fwd).normalized()
    x = -V(edge); x = (x - x.dot(z) * z).normalized()  # local x: the spine side; -x: the edge
    G = Matrix((x, z.cross(x), z)).transposed()
    group(name, hand, quat=G.to_quaternion())
    tube('handle', (0, 0, 0.16), (0, 0, -0.30), 0.055, m(CHAR), segs=16, bev=0.015)
    ball('cap', 0.08, (0, 0, 0.17), m(LGREY), seg=(18, 12))
    tube('guard', (0, 0, -0.27), (0, 0, -0.33), 0.085, m(A), segs=16, bev=0.015)
    t = 0.10
    thin = lambda px, pz: 1 - 0.65 * min(1, max(0, -(px - 0.04) / H))
    body = [(0.06, -0.30), (0.06, -0.30 - L), (-H + 0.10, -0.30 - L - 0.06), (-H, -0.30 - L + 0.10), (-H, -0.42), (-0.12, -0.30)]
    prism('blade', body, t, (0, 0, 0), m(LGREY), thin=thin, bev=0.012)
    eb = [(-H + 0.10, -0.30 - L - 0.06), (-H, -0.30 - L + 0.10), (-H, -0.42), (-H + 0.085, -0.45), (-H + 0.085, -0.30 - L + 0.06),
          (-H + 0.16, -0.30 - L - 0.03)]
    prism('edge', eb, t + 0.01, (0, 0, 0), m(WHITE), thin=thin, bev=0.006)
    for i, zz in enumerate((-0.48, -0.68, -0.88)):   # three bolts down the spine
        tube(f'bolt{i}', (-0.02, -0.06, zz), (-0.02, 0.06, zz), 0.028, m(GUN), segs=16, bev=0.01, outline=False)
    hz = -0.30 - L + 0.13
    tube('hole', (-0.10, -0.07, hz), (-0.10, 0.07, hz), 0.06, m(CHAR), segs=20, bev=0.012, outline=False)
    ungroup()
    return hand + G @ V((-0.10, 0, hz))


def b3(sc=0.96, **kw):
    root = begin('b3', sc, **kw); A = GARNET; Wt = m(WHITE)
    root.scale.x = -1      # built with the pauldron on -x, mirrored so it sits on the side the cameras see
    leg('L', (-0.24, 0.06, 0.58), (-0.38, 0.20, 0.36), (-0.42, 0.34, 0.15), WHITE, toe=-6, yaw=-12)
    leg('R', (0.24, 0.02, 0.58), (0.38, -0.20, 0.38), (0.40, -0.18, 0.15), WHITE, yaw=10)
    rbox('pelvis', (0.62, 0.44, 0.26), (0, 0.05, 0.60), m(MGREY), r=0.10, sub=2)
    rbox('belt', (0.72, 0.52, 0.11), (0, 0.04, 0.70), m(CHAR), r=0.04, sub=2)
    rbox('buckle', (0.18, 0.06, 0.14), (0, -0.25, 0.70), m(A), r=0, bev=0.025)
    for s in (-1, 1):
        rbox(f'tasset{s}', (0.26, 0.36, 0.26), (s * 0.38, 0.02, 0.56), m(LGREY), r=0, bev=0.04, rot=(0, s * 18, 0))

    TL, TR = (0, 0.04, 0.66), (16, -5, 0)
    P = placer(TL, TR)
    group('torso', TL, TR)
    rbox('abs', (0.62, 0.46, 0.30), (0, 0.02, 0.12), m(MGREY), r=0.11, sub=2)
    rbox('chest', (0.94, 0.68, 0.58), (0, 0, 0.42), Wt, r=0, bev=0.13, taper=(1.14, 1.0))
    rbox('hump', (0.70, 0.40, 0.30), (0, 0.24, 0.62), m(LGREY), r=0, bev=0.09)
    # a harness strap from under the big pauldron to the right hip
    sweep('strap', [(-0.40, -0.30, 0.66), (-0.12, -0.37, 0.46), (0.16, -0.37, 0.24), (0.36, -0.30, 0.04)],
          [0.05, 0.05, 0.05, 0.05], m(CHAR), seg=12, flat=0.35, up=(0, -1, 0.3))
    rbox('strapbuckle', (0.11, 0.05, 0.11), (0.02, -0.39, 0.36), m(LGREY), r=0, bev=0.02)
    # the -x shoulder: one huge layered pauldron with a raised guard beside the head
    ball('shjL', 0.16, (-0.50, 0.0, 0.52), m(CHAR), seg=(18, 12), outline=False)
    # stepped lames down the upper arm: short flaring bands, each one lower and wider, like a skirt of plates
    ax = V((-math.sin(math.radians(24)), 0, -math.cos(math.radians(24))))
    top = V((-0.66, 0.02, 0.70))
    for k, col in enumerate((WHITE, LGREY, WHITE)):
        a = top + ax * (0.06 + 0.12 * k)
        tube(f'lame{k}', a, a + ax * 0.16, 0.36 + 0.035 * k, m(col), r1=0.44 + 0.035 * k, segs=40, bev=0.02)
    # the cap: a crimson dome over the top of the shoulder, a light rim, a raised boss and rivets
    shell('bigpad', (0.39, 0.45, 0.28), (-0.64, 0.02, 0.70), m(A), az=(-180, 180), el=(-2, 90), thick=0.07, rot=(0, -16, 0), rows=9, cols=30)
    shell('rim', (0.41, 0.47, 0.28), (-0.64, 0.02, 0.70), m(LGREY), az=(-180, 180), el=(-8, 8), thick=0.05, rot=(0, -16, 0), rows=3, cols=32)
    ball('boss', 0.10, (-0.72, 0.02, 0.97), m(LGREY), scale=(1, 1, 0.55), seg=(20, 12))
    for k, yy in enumerate((-0.30, 0.30)):           # rivets front and back on the cap
        ball(f'rivet{k}', 0.04, (-0.66, yy, 0.86), m(LGREY), seg=(14, 8), outline=False)
    # the +x shoulder bare: a joint and a small cap
    ball('shjR', 0.16, (0.50, 0.0, 0.52), m(CHAR), seg=(18, 12), outline=False)
    ball('capR', 0.20, (0.54, 0.02, 0.58), m(MGREY), scale=(1, 1.05, 0.7), half=True, seg=(24, 12), sub=1)
    group('head', (0, -0.30, 0.62), (-26, 0, 0))
    rbox('helm', (0.40, 0.40, 0.38), (0, 0, 0.03), m(MGREY), r=0, bev=0.11, taper=(0.88, 0.9))
    rbox('visor', (0.34, 0.06, 0.09), (0, -0.195, 0.0), m(CHAR), r=0.04, sub=2, outline=False)
    rbox('eye', (0.26, 0.03, 0.035), (0, -0.228, 0.002), flat(A), r=0.015, outline=False)
    prism('brow', [(-0.23, 0.02), (0, -0.03), (0.23, 0.02), (0.22, 0.09), (0, 0.05), (-0.22, 0.09)], 0.11,
          (0, -0.18, 0.07), m(LGREY), r=0, bev=0.018)
    rbox('chin', (0.28, 0.08, 0.14), (0, -0.18, -0.11), m(LGREY), r=0, bev=0.025, taper=(1.2, 1))
    for k, x in enumerate((-0.07, 0.0, 0.07)):       # a grille over the mouth
        rbox(f'grille{k}', (0.035, 0.03, 0.09), (x, -0.225, -0.11), m(CHAR), r=0.012, outline=False)
    ungroup()
    ungroup()
    # the cleaver held low across the front like a guard, edge down, its flat to the camera; chained to the left wrist
    hand = V((0.64, -0.58, 0.64))
    hole = cleaver('cleaver', hand, (-0.97, -0.12, -0.10), (0, 0, -1), A)
    arm('R', P((0.50, 0.0, 0.52)), (0.84, -0.20, 0.80), hand, WHITE, fist_fwd=(0, -1, 0), fist_up=(0, 0, 1))
    lh = arm('L', P((-0.50, 0.0, 0.52)), (-0.90, -0.10, 0.78), (-0.86, -0.36, 0.42), WHITE, fist_fwd=(0, -0.4, -0.9),
             fist_up=(0, -0.9, 0.4), big=1.12)
    cuff = lh + V((0.0, 0.10, 0.18))
    sag = (cuff + hole) / 2 + V((0.0, -0.06, -0.24))
    chain('chain', [hole, sag, cuff], m(MGREY), link=0.07, wire=0.019)
    blob('blob', 1.05, 0.85, loc=(0.1, -0.1, 0.003))


ALL = {'b1': b1, 'b2': b2, 'b3': b3, 't1': round2.t1, 't5': round2.t5, 'robot_tank': round2.robot_tank, 'robot_melee': round2.robot_melee}
