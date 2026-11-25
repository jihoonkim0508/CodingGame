# Seven stage-2 fast-runner drafts (round 3) and the real melee robot for scale.
# Design units: the robot stands 1.55 tall. Every model faces -y (its travel direction), long axis along y, posed
# mid-dash. Bodies are lofted through cross-sections (toon.loft), armoured with shells that follow them (toon.shell),
# with faceted blades, multi-segment legs, claws, jaws and slit eyes under a brow.
# Spine keys run front to back, so a shell's flare lifts its rear edge and an arc pair narrows it toward the back:
# plates overlap like swept-back chevrons.
import math
from mathutils import Vector
import toon as T
from toon import loft, shell, blade, sickle, plane_frame, spike, sweep, ball, rbox, tube, blob, toon, flat, begin, surface, slit_on
from palette import WHITE, LGREY, MGREY, CHAR, GUN, RED, GLOW

W = L = M = C = G = R = E = None


def mats():
    global W, L, M, C, G, R, E
    W, L, M, C, G, R = (toon(c) for c in (WHITE, LGREY, MGREY, CHAR, GUN, RED))
    E = flat(GLOW)


def V(*a):
    return Vector(a)


def seg_keys(a, b, w0, d0, w1, d1, n=2.0):
    """Key sections of a straight limb segment from a to b (half-width w, half-depth d at each end)."""
    a, b = V(*a), V(*b)
    return [(a, w0 * 0.75, d0 * 0.75, d0 * 0.75, n), (a.lerp(b, 0.28), w0, d0 * 1.08, d0, n),
            (a.lerp(b, 0.85), w1 * 1.02, d1, d1, n), (b, w1 * 0.72, d1 * 0.72, d1 * 0.72, n)]


def leg(name, pts, sizes, segmats, joint=None, n=2.0, seg=8, rings=5, side=(1, 0, 0), up=(0, -1, 0)):
    """A limb through joint points; sizes[i] = (half-width, half-depth) at joint i. Each segment swells just below its
    upper joint (muscle / actuator) and narrows into the next; the bends get a knuckle."""
    for i in range(len(pts) - 1):
        (w0, d0), (w1, d1) = sizes[i], sizes[i + 1]
        loft(f'{name}{i}', seg_keys(pts[i], pts[i + 1], w0, d0, w1, d1, n), segmats[i], rings=rings, seg=seg, side=side, up=up)
    for i, p in enumerate(pts[1:-1], 1):
        ball(f'{name}k{i}', min(sizes[i]) * 0.95, p, joint or C, u=8)


def guard(name, a, b, w0, d0, w1, d1, mat, arc, lift=0.012, thick=0.025, flare=0.0, seg=8):
    """An armour plate over a limb segment; arc in the segment's own frame (0 = +x side, 90 = its front)."""
    shell(name, seg_keys(a, b, w0, d0, w1, d1)[:3], mat, arc=arc, lift=lift, thick=thick, flare=flare, rings=4, seg=seg, up=(0, -1, 0))


def outer(s):
    return 180 if s < 0 else 0


def paw(name, at, d, length, w, mat, claw=None, claws=3, cl=0.075, cr=0.017):
    """A foot from the ankle at `at` along direction d (in the side plane), toes splayed, claws hooking down."""
    a, d = V(*at), V(*d).normalized()
    up = V(0, d.z, -d.y)
    if up.z < 0: up = -up
    keys = [(a + up * 0.01, w * 0.7, 0.032, 0.022, 2.2), (a + d * length * 0.45, w, 0.036, 0.022, 2.6),
            (a + d * length, w * 0.9, 0.024, 0.016, 2.2)]
    loft(name, keys, mat, rings=4, seg=8)
    for k in range(claws):
        dx = (k - (claws - 1) / 2) * w * 0.62
        tip = a + d * length
        spike(f'{name}c{k}', tip + V(dx, 0, 0) - d * 0.02, tip + V(dx * 1.15, 0, 0) + d * cl - up * cl * 0.55, cr,
              claw or L, bend=up * 0.012, seg=5)


def eyes(keys, u, elev, length, height, tilt=16, off=0.004):
    for s in (-1, 1):
        slit_on(f'eye{s}', keys, u, elev, s, length, height, tilt, E, off=off)


def split_shell(name, keys, mat, arc, gap=12, **kw):
    """A plate in two halves either side of the spine line (90°), leaving a gap for a ridge down the back."""
    (f0, f1), (b0, b1) = arc if isinstance(arc[0], (tuple, list)) else (arc, arc)
    shell(name + 'R', keys, mat, arc=((f0, 90 - gap), (b0, 90 - gap)), **kw)
    shell(name + 'L', keys, mat, arc=((90 + gap, f1), (90 + gap, b1)), **kw)


def side_blade(name, origin, back, rise, pts, depth, mat, splay=0.0):
    """A blade in the side plane: u runs along (0, back, rise) (back = 1 rakes it backward), v up (splay tilts it outward)."""
    return blade(name, pts, depth, mat, plane_frame(origin, (0, back, rise), (splay, -rise * 0.5, 1)))


def radial_blade(name, keys, u, elev, s, length, width, mat, back=1.0, rise=0.25, out=0.55, depth=0.04, sink=0.01):
    """A blade standing off a lofted body (manes, spines): rooted on the surface, raked back and outward, its flat face
    turned around the body so it reads as a fin from the side."""
    q, t, o = surface(keys, u, elev, s, -sink)
    d = (V(0, back, rise) + o * out).normalized()
    v = (o - d * o.dot(d)).normalized()
    blade(name, [(-0.02, -width * 0.5), (length, 0.0), (length * 0.72, width * 0.55), (0.0, width * 0.6)], depth, mat, plane_frame(q, d, v))


def blade_leg(name, knee, foot, width, mat):
    """A lower leg as a faceted blade from the knee to a point on the ground (insect legs)."""
    k, f = V(*knee), V(*foot)
    U = (f - k); ln = U.length; U.normalize()
    Vv = U.cross(V(0, 0, 1))
    if Vv.length < 1e-3: Vv = V(1, 0, 0)
    blade(name, [(0, -width * 0.5), (ln * 0.25, -width * 0.6), (ln, 0.0), (ln * 0.2, width * 0.45), (0, width * 0.4)], width * 0.45, mat,
          plane_frame(k, U, Vv.normalized()))


# ------------------------------------------------------------------ 1 강철 사냥개: armoured hound, full stretch

def hound(sc=1.0, **kw):
    begin('hound', sc, **kw); mats()
    body = [((0, -0.60, 0.84), 0.14, 0.13, 0.13, 2.0),
            ((0, -0.40, 0.76), 0.22, 0.18, 0.21, 2.2),
            ((0, -0.12, 0.71), 0.23, 0.17, 0.26, 2.2),
            ((0, 0.22, 0.72), 0.15, 0.11, 0.12, 2.0),
            ((0, 0.50, 0.76), 0.19, 0.14, 0.16, 2.2),
            ((0, 0.70, 0.78), 0.10, 0.08, 0.08, 2.0)]
    loft('body', body, G, rings=11, seg=14)
    split_shell('shoulder', body, W, u=(0.04, 0.34), arc=((-30, 210), (-5, 185)), lift=0.03, thick=0.03, flare=0.03, rings=5, seg=8, chev=0.06)
    split_shell('ribs', body, W, u=(0.28, 0.54), arc=((-15, 195), (5, 175)), lift=0.014, thick=0.028, flare=0.025, rings=4, seg=7, chev=0.06)
    split_shell('back', body, W, u=(0.48, 0.68), arc=((10, 170), (25, 155)), lift=0.014, thick=0.025, flare=0.025, rings=3, seg=6, chev=0.06)
    split_shell('haunch', body, W, u=(0.64, 0.96), arc=((-40, 220), (-10, 190)), lift=0.014, thick=0.03, flare=0.02, rings=5, seg=8, chev=0.04)
    shell('spine', body, C, u=(0.02, 0.97), arc=(78, 102), lift=0.01, thick=0.065, rings=8, seg=2)
    for i, (y, z, h) in enumerate(((-0.42, 1.0, 0.15), (-0.24, 0.96, 0.12), (-0.06, 0.93, 0.09))):
        side_blade(f'fin{i}', (0, y, z), 1, 0.2, [(0, 0), (0.18, 0), (0.28, h), (0.12, h * 0.75)], 0.04, R)
    neck = [((0, -0.86, 0.92), 0.10, 0.09, 0.09, 2.0), ((0, -0.70, 0.88), 0.12, 0.11, 0.11, 2.0), ((0, -0.52, 0.82), 0.15, 0.13, 0.13, 2.0)]
    loft('neck', neck, G, rings=5, seg=12)
    shell('gorget', neck, W, u=(0.1, 0.8), arc=((-10, 190), (10, 170)), lift=0.012, thick=0.025, flare=0.02, rings=4, seg=12, chev=0.15)
    skull = [((0, -1.38, 0.83), 0.038, 0.035, 0.02, 1.5),
             ((0, -1.24, 0.86), 0.07, 0.055, 0.035, 1.7),
             ((0, -1.06, 0.91), 0.115, 0.08, 0.05, 2.2),
             ((0, -0.90, 0.94), 0.14, 0.10, 0.07, 2.6),
             ((0, -0.76, 0.93), 0.10, 0.08, 0.07, 2.2)]
    loft('skull', skull, W, rings=9, seg=14)
    shell('helm', skull, G, u=(0.40, 1.0), arc=((40, 140), (12, 168)), lift=0.03, thick=0.022, flare=-0.026, rings=5, seg=10)
    eyes(skull, 0.56, 22, 0.12, 0.034)
    jaw = [((0, -1.26, 0.73), 0.04, 0.018, 0.025, 1.6), ((0, -1.05, 0.78), 0.065, 0.025, 0.035, 2.0), ((0, -0.86, 0.84), 0.075, 0.03, 0.04, 2.2)]
    loft('jaw', jaw, G, rings=5, seg=10)
    loft('mouth', [((0, -1.20, 0.79), 0.04, 0.03, 0.03, 2), ((0, -0.92, 0.85), 0.06, 0.04, 0.03, 2)], C, rings=3, seg=8, outline=False)
    for s in (-1, 1):
        blade(f'ear{s}', [(0, 0), (0.36, 0.03), (0.42, 0.09), (0.06, 0.10)], 0.03, R,
              plane_frame((s * 0.07, -0.86, 1.00), (s * 0.12, 1, 0.42), (s * 0.55, -0.1, 1)))
        for k, y in enumerate((-1.25, -1.17, -1.09)):
            x = s * (0.035 if k == 0 else 0.05 + 0.01 * k)
            spike(f'tooth{s}{k}', (x, y, 0.835 + 0.016 * k), (x, y - 0.006, 0.775 + 0.016 * k), 0.016 if k == 0 else 0.012, W, seg=5)
        spike(f'ltooth{s}', (s * 0.03, -1.21, 0.735), (s * 0.03, -1.215, 0.78), 0.011, W, seg=5)
    F = [(0.10, 0.14), (0.065, 0.085), (0.05, 0.055), (0.045, 0.05)]
    H = [(0.11, 0.17), (0.07, 0.09), (0.05, 0.055), (0.045, 0.05)]
    fore = {-1: [(-0.19, -0.38, 0.72), (-0.21, -0.28, 0.44), (-0.21, -0.66, 0.17), (-0.21, -0.76, 0.09)],
            1: [(0.19, -0.38, 0.72), (0.21, -0.24, 0.46), (0.21, -0.52, 0.30), (0.21, -0.62, 0.22)]}
    hind = {-1: [(-0.19, 0.54, 0.74), (-0.21, 0.34, 0.46), (-0.21, 0.72, 0.22), (-0.21, 0.84, 0.07)],
            1: [(0.19, 0.54, 0.74), (0.21, 0.26, 0.50), (0.21, 0.52, 0.31), (0.21, 0.42, 0.19)]}
    for s in (-1, 1):
        f, h = fore[s], hind[s]
        leg(f'fl{s}', f, F, [G, G, C])
        guard(f'fg{s}', f[0], f[1], *F[0], *F[1], W, arc=(outer(s) - 80, outer(s) + 80), flare=0.015)
        guard(f'fa{s}', f[1], f[2], *F[1], *F[2], W, arc=(20, 160))
        paw(f'fp{s}', f[3], (0, -1, -0.35) if s < 0 else (0, -1, -0.8), 0.13, 0.06, G)
        leg(f'hl{s}', h, H, [G, G, C])
        guard(f'hg{s}', h[0], h[1], *H[0], *H[1], W, arc=(outer(s) - 85, outer(s) + 85), flare=0.015)
        spike(f'hspur{s}', V(*h[2]) + V(0, 0.02, 0.02), V(*h[2]) + V(0, 0.14, 0.07), 0.028, W, seg=6)
        paw(f'hp{s}', h[3], (0, -0.6, -0.2) if s < 0 else (0, -1, -0.6), 0.13, 0.06, G)
    tail = [((0, 0.68, 0.79), 0.075, 0.07, 0.065, 2.2), ((0, 0.90, 0.85), 0.055, 0.05, 0.045, 2.0), ((0, 1.08, 0.92), 0.035, 0.035, 0.03, 2.0)]
    loft('tail', tail, G, rings=5, seg=10)
    shell('tailplate', tail, W, u=(0.05, 0.75), arc=((10, 170), (35, 145)), lift=0.01, thick=0.022, flare=0.015, rings=3, seg=8, chev=0.15)
    blade('tailblade', [(0, -0.045), (0.34, 0), (0, 0.045), (-0.03, 0)], 0.04, R, plane_frame((0, 1.06, 0.915), (0, 1, 0.32), (1, 0, 0)))
    blob('blob', 0.55, 1.15, loc=(0, -0.05, 0.003))


# ------------------------------------------------------------------ 2 흑철 표범: heavy machine panther, pouncing

def panther(sc=1.0, **kw):
    begin('panther', sc, **kw); mats()
    body = [((0, -0.58, 0.72), 0.18, 0.16, 0.15, 2.2),
            ((0, -0.40, 0.76), 0.27, 0.21, 0.22, 2.4),
            ((0, -0.12, 0.72), 0.27, 0.19, 0.24, 2.4),
            ((0, 0.20, 0.65), 0.19, 0.13, 0.14, 2.2),
            ((0, 0.48, 0.65), 0.22, 0.15, 0.17, 2.4),
            ((0, 0.70, 0.64), 0.11, 0.09, 0.09, 2.0)]
    loft('body', body, C, rings=10, seg=14)
    shell('pauldron', body, G, u=(0.10, 0.38), arc=((-40, 220), (-10, 190)), lift=0.03, thick=0.035, flare=0.03, rings=5, seg=14, chev=0.05)
    shell('ribs', body, G, u=(0.36, 0.60), arc=((-15, 195), (5, 175)), lift=0.015, thick=0.03, flare=0.025, rings=4, seg=12, chev=0.05)
    shell('back', body, G, u=(0.55, 0.72), arc=((20, 160), (35, 145)), lift=0.015, thick=0.028, flare=0.025, rings=3, seg=10, chev=0.05)
    shell('haunch', body, G, u=(0.68, 0.96), arc=((-50, 230), (-15, 195)), lift=0.015, thick=0.03, flare=0.02, rings=5, seg=14, chev=0.04)
    neck = [((0, -0.86, 0.63), 0.13, 0.12, 0.11, 2.2), ((0, -0.72, 0.67), 0.16, 0.14, 0.13, 2.2), ((0, -0.52, 0.73), 0.19, 0.16, 0.15, 2.2)]
    loft('neck', neck, C, rings=4, seg=12)
    # blade mane: three staggered rows of red blades raked back off the neck and shoulders
    for row, (keys, u, elevs, ln) in enumerate(((neck, 0.45, (12, 50, 86), 0.30), (body, 0.04, (-10, 28, 64), 0.40),
                                                (body, 0.15, (8, 46, 84), 0.36))):
        for s in (-1, 1):
            for k, e in enumerate(elevs):
                radial_blade(f'mane{row}{s}{k}', keys, u, e, s, ln * (0.75 + 0.25 * max(0, e) / 90), 0.10, R, rise=0.3, out=0.7)
    skull = [((0, -1.18, 0.52), 0.075, 0.055, 0.045, 2.0),
             ((0, -1.06, 0.56), 0.12, 0.08, 0.065, 2.2),
             ((0, -0.92, 0.61), 0.17, 0.11, 0.085, 2.6),
             ((0, -0.78, 0.65), 0.14, 0.11, 0.10, 2.2)]
    loft('skull', skull, M, rings=8, seg=14)
    shell('visor', skull, C, u=(0.32, 1.0), arc=((40, 140), (12, 168)), lift=0.03, thick=0.024, flare=-0.024, rings=5, seg=10)
    eyes(skull, 0.58, 24, 0.12, 0.034)
    jaw = [((0, -1.12, 0.43), 0.065, 0.02, 0.03, 1.8), ((0, -0.98, 0.47), 0.11, 0.03, 0.04, 2.2), ((0, -0.82, 0.53), 0.12, 0.035, 0.045, 2.2)]
    loft('jaw', jaw, G, rings=5, seg=12)
    loft('mouth', [((0, -1.08, 0.48), 0.05, 0.03, 0.03, 2), ((0, -0.86, 0.55), 0.08, 0.04, 0.03, 2)], C, rings=3, seg=8, outline=False)
    for s in (-1, 1):
        spike(f'fang{s}', (s * 0.065, -1.08, 0.50), (s * 0.072, -1.13, 0.33), 0.026, W, seg=6, bend=(0, -0.015, 0))
        spike(f'lfang{s}', (s * 0.05, -1.06, 0.44), (s * 0.05, -1.07, 0.50), 0.013, W, seg=5)
        blade(f'ear{s}', [(0, 0), (0.24, 0.02), (0.28, 0.08), (0.04, 0.08)], 0.03, G,
              plane_frame((s * 0.10, -0.82, 0.73), (s * 0.25, 1, 0.5), (s * 0.6, -0.2, 1)))
    F = [(0.12, 0.16), (0.09, 0.11), (0.075, 0.085), (0.07, 0.075)]
    H = [(0.12, 0.18), (0.08, 0.10), (0.06, 0.065), (0.055, 0.06)]
    fore = {-1: [(-0.23, -0.38, 0.68), (-0.26, -0.30, 0.38), (-0.26, -0.74, 0.14), (-0.26, -0.84, 0.08)],
            1: [(0.23, -0.38, 0.68), (0.26, -0.42, 0.40), (0.26, -0.72, 0.36), (0.26, -0.84, 0.31)]}
    hind = {-1: [(-0.21, 0.48, 0.66), (-0.24, 0.26, 0.40), (-0.24, 0.62, 0.20), (-0.24, 0.68, 0.07)],
            1: [(0.21, 0.48, 0.66), (0.24, 0.20, 0.42), (0.24, 0.48, 0.26), (0.24, 0.40, 0.15)]}
    for s in (-1, 1):
        f, h = fore[s], hind[s]
        leg(f'fl{s}', f, F, [C, C, C], joint=M)
        guard(f'fg{s}', f[0], f[1], *F[0], *F[1], G, arc=(outer(s) - 85, outer(s) + 85), flare=0.015)
        guard(f'fa{s}', f[1], f[2], *F[1], *F[2], L, arc=(30, 240) if s < 0 else (-60, 150), thick=0.03)
        spike(f'elbow{s}', V(*f[1]) + V(s * 0.03, 0.04, 0.02), V(*f[1]) + V(s * 0.06, 0.20, 0.08), 0.035, W, seg=6)
        paw(f'fp{s}', f[3], (0, -1, -0.2) if s < 0 else (0, -1, -0.9), 0.15, 0.09, G, claw=W, cl=0.11, cr=0.026)
        leg(f'hl{s}', h, H, [C, C, C], joint=M)
        guard(f'hg{s}', h[0], h[1], *H[0], *H[1], G, arc=(outer(s) - 85, outer(s) + 85), flare=0.015)
        guard(f'hs{s}', h[1], h[2], *H[1], *H[2], L, arc=(outer(s) - 70, outer(s) + 70))
        paw(f'hp{s}', h[3], (0, -0.7, -0.25) if s < 0 else (0, -1, -0.6), 0.13, 0.075, G, claw=W, cl=0.08, cr=0.02)
    tail = [(0, 0.70, 0.66), (0, 0.92, 0.74), (0, 1.12, 0.90), (0, 1.30, 1.04), (0, 1.46, 1.08)]
    sweep('tail', tail, [0.07, 0.06, 0.05, 0.04, 0.012], C, seg=8)
    for i, k in enumerate((1, 2, 3)):
        p = V(*tail[k])
        side_blade(f'tspike{i}', p + V(0, 0, 0.03), 1, 0.4, [(0, 0), (0.10, 0), (0.16, 0.10 - i * 0.015), (0.05, 0.07)], 0.03, L)
    blob('blob', 0.65, 1.15, loc=(0, -0.05, 0.003))


# ------------------------------------------------------------------ 3 추격 랩터: two-legged raptor, body level

def raptor(sc=1.0, **kw):
    begin('raptor', sc, **kw); mats()
    body = [((0, -0.74, 0.98), 0.075, 0.07, 0.075, 2.0),
            ((0, -0.56, 0.92), 0.09, 0.08, 0.09, 2.0),
            ((0, -0.34, 0.86), 0.15, 0.13, 0.17, 2.2),
            ((0, -0.04, 0.84), 0.19, 0.15, 0.21, 2.2),
            ((0, 0.28, 0.85), 0.16, 0.13, 0.15, 2.2),
            ((0, 0.66, 0.88), 0.09, 0.08, 0.08, 2.0),
            ((0, 1.05, 0.92), 0.05, 0.045, 0.045, 2.0),
            ((0, 1.42, 0.96), 0.0, 0.0, 0.0, 2.0)]
    loft('body', body, L, rings=18, seg=14)
    plates = ((0.02, 0.18, (5, 175), (15, 165)), (0.13, 0.38, (-25, 205), (-5, 185)), (0.33, 0.50, (-5, 185), (10, 170)),
              (0.46, 0.62, (5, 175), (20, 160)), (0.58, 0.74, (15, 165), (30, 150)), (0.70, 0.84, (25, 155), (45, 135)))
    for i, (u0, u1, a0, a1) in enumerate(plates):
        split_shell(f'plate{i}', body, W, u=(u0, u1), arc=(a0, a1), lift=0.012, thick=0.026, flare=0.024, rings=4, seg=6, chev=0.05)
    shell('spine', body, C, u=(0.03, 0.84), arc=(78, 102), lift=0.01, thick=0.06, rings=10, seg=2)
    shell('belly', body, M, u=(0.22, 0.48), arc=(235, 305), lift=0.01, thick=0.02, rings=4, seg=6)
    blade('tailblade', [(0, -0.07), (0.36, 0), (0, 0.07), (-0.04, 0)], 0.04, R, plane_frame((0, 1.30, 0.95), (0, 1, 0.1), (1, 0, 0)))
    side_blade('tailfin', (0, 1.16, 0.95), 1, 0.15, [(0, 0), (0.16, 0), (0.30, 0.10), (0.14, 0.09)], 0.035, R)
    skull = [((0, -1.30, 0.94), 0.035, 0.03, 0.02, 1.5),
             ((0, -1.16, 0.97), 0.06, 0.05, 0.03, 1.7),
             ((0, -0.98, 1.01), 0.10, 0.075, 0.045, 2.2),
             ((0, -0.84, 1.02), 0.115, 0.085, 0.06, 2.6),
             ((0, -0.72, 1.00), 0.08, 0.07, 0.06, 2.2)]
    loft('skull', skull, W, rings=9, seg=14)
    shell('helm', skull, M, u=(0.40, 1.0), arc=((40, 140), (15, 165)), lift=0.028, thick=0.02, flare=-0.024, rings=5, seg=10)
    eyes(skull, 0.58, 22, 0.11, 0.032)
    jaw = [((0, -1.20, 0.86), 0.035, 0.016, 0.02, 1.6), ((0, -1.00, 0.90), 0.06, 0.022, 0.03, 2.0), ((0, -0.80, 0.95), 0.07, 0.03, 0.035, 2.2)]
    loft('jaw', jaw, L, rings=5, seg=10)
    loft('mouth', [((0, -1.16, 0.92), 0.035, 0.03, 0.03, 2), ((0, -0.86, 0.97), 0.055, 0.035, 0.03, 2)], C, rings=3, seg=8, outline=False)
    for s in (-1, 1):
        for k, y in enumerate((-1.23, -1.15, -1.07, -0.99)):
            x = s * (0.03 + 0.012 * k)
            spike(f'tooth{s}{k}', (x, y, 0.935 + 0.012 * k), (x, y - 0.005, 0.89 + 0.012 * k), 0.011, W, seg=5)
    for i, (y, z, l, h) in enumerate(((-0.96, 1.08, 0.36, 0.10), (-0.86, 1.10, 0.40, 0.13), (-0.76, 1.07, 0.30, 0.09))):
        side_blade(f'crest{i}', (0, y, z), 1, 0.35, [(0, 0), (0.12, 0), (l, h), (l * 0.8, h * 1.1)], 0.035, R)
    for s in (-1, 1):
        leg(f'arm{s}', [(s * 0.13, -0.40, 0.80), (s * 0.17, -0.30, 0.64), (s * 0.15, -0.48, 0.58)], [(0.045, 0.05), (0.035, 0.035), (0.03, 0.03)], [M, M])
        for k in (-1, 1):
            spike(f'aclaw{s}{k}', (s * 0.15 + k * 0.015, -0.50, 0.58), (s * 0.15 + k * 0.02, -0.57, 0.52), 0.014, W, seg=5)
    S = [(0.12, 0.18), (0.075, 0.09), (0.05, 0.055), (0.045, 0.05)]
    legs = {-1: [(-0.17, 0.06, 0.80), (-0.21, -0.26, 0.56), (-0.21, -0.06, 0.27), (-0.21, -0.20, 0.10)],
            1: [(0.17, 0.06, 0.80), (0.21, -0.08, 0.50), (0.21, 0.38, 0.32), (0.21, 0.50, 0.08)]}
    for s in (-1, 1):
        p = legs[s]
        leg(f'leg{s}', p, S, [L, M, C])
        guard(f'thigh{s}', p[0], p[1], *S[0], *S[1], W, arc=(outer(s) - 90, outer(s) + 90), flare=0.02, thick=0.028)
        guard(f'shin{s}', p[1], p[2], *S[1], *S[2], W, arc=(25, 155))
        d = (0, -1, -0.45) if s < 0 else (0, -0.45, -1)
        paw(f'foot{s}', p[3], d, 0.14, 0.06, M, claw=W, claws=3, cl=0.07)
        a = V(*p[3]) + V(-s * 0.04, 0, 0.03)
        sweep(f'sickle{s}', [a, a + V(0, -0.06, 0.08), a + V(0, -0.16, 0.06), a + V(0, -0.20, -0.02)], [0.028, 0.026, 0.016, 0.003], W, seg=6)
    blob('blob', 0.5, 1.2, loc=(0, 0.1, 0.003))


# ------------------------------------------------------------------ 4 낫 사마귀: mantis striker, four legs, scythes swept back

def mantis(sc=1.0, **kw):
    begin('mantis', sc, **kw); mats()
    abd = [((0, 0.04, 0.52), 0.11, 0.08, 0.08, 2.0),
           ((0, 0.32, 0.54), 0.18, 0.11, 0.10, 2.2),
           ((0, 0.64, 0.58), 0.15, 0.09, 0.08, 2.2),
           ((0, 0.90, 0.65), 0.07, 0.05, 0.045, 2.0),
           ((0, 1.04, 0.70), 0.0, 0.0, 0.0, 2.0)]
    loft('abdomen', abd, G, rings=12, seg=14)
    for i, (u0, u1) in enumerate(((0.02, 0.30), (0.24, 0.52), (0.46, 0.74), (0.68, 0.92))):
        shell(f'seg{i}', abd, M, u=(u0, u1), arc=((0, 180), (15, 165)), lift=0.012, thick=0.026, flare=0.03, rings=3, seg=10, chev=0.07)
    thx = [((0, -0.64, 0.72), 0.07, 0.06, 0.06, 2.0),
           ((0, -0.38, 0.62), 0.085, 0.065, 0.065, 2.0),
           ((0, -0.10, 0.54), 0.14, 0.10, 0.10, 2.2),
           ((0, 0.10, 0.52), 0.12, 0.09, 0.09, 2.2)]
    loft('thorax', thx, M, rings=10, seg=12)
    shell('pronotum', thx, W, u=(0.02, 0.60), arc=((0, 180), (20, 160)), lift=0.012, thick=0.024, flare=0.02, rings=5, seg=10, chev=0.06)
    shell('scutum', thx, W, u=(0.55, 0.95), arc=((-15, 195), (5, 175)), lift=0.014, thick=0.026, flare=0.02, rings=4, seg=12, chev=0.05)
    head = [((0, -0.92, 0.64), 0.03, 0.03, 0.03, 1.6),
            ((0, -0.84, 0.70), 0.09, 0.055, 0.05, 1.8),
            ((0, -0.74, 0.77), 0.17, 0.075, 0.06, 2.4),
            ((0, -0.65, 0.77), 0.11, 0.06, 0.05, 2.2)]
    loft('head', head, W, rings=7, seg=14)
    shell('crown', head, G, u=(0.45, 1.0), arc=((30, 150), (15, 165)), lift=0.022, thick=0.02, flare=-0.016, rings=4, seg=10)
    for s in (-1, 1):
        ce = [((s * 0.165, -0.82, 0.79), 0.0, 0.0, 0.0, 2), ((s * 0.17, -0.74, 0.80), 0.06, 0.06, 0.055, 2.2), ((s * 0.165, -0.66, 0.79), 0.0, 0.0, 0.0, 2)]
        loft(f'ceye{s}', ce, G, rings=6, seg=10)
        slit_on(f'eye{s}', ce, 0.5, 8, s, 0.10, 0.03, 12, E, off=0.003)
        spike(f'mand{s}', (s * 0.05, -0.86, 0.63), (s * 0.015, -0.98, 0.57), 0.024, L, seg=6, bend=(s * 0.03, 0, 0))
        sweep(f'antenna{s}', [(s * 0.05, -0.76, 0.82), (s * 0.12, -0.74, 0.96), (s * 0.20, -0.60, 1.04), (s * 0.28, -0.42, 1.06)],
              [0.013, 0.011, 0.008, 0.004], C, seg=5)
        # raptorial arm: a plated femur raised high, the red hooked blade folded down in front of the face, serrated inside
        sh, el = (s * 0.08, -0.52, 0.66), (s * 0.21, -0.62, 1.04)
        leg(f'sfem{s}', [sh, el], [(0.06, 0.07), (0.05, 0.055)], [W])
        for k, t in enumerate((0.35, 0.6, 0.82)):
            bp = V(*sh).lerp(V(*el), t)
            spike(f'fspine{s}{k}', bp + V(-s * 0.03, -0.03, 0), bp + V(-s * 0.06, -0.10, -0.02), 0.016, L, seg=5)
        ball(f'selb{s}', 0.065, el, C, u=8)
        U, Vv = V(s * 0.12, -1, 0).normalized(), V(0, 0, 1)
        fr = plane_frame(el, U, Vv)
        cl = [(0, 0), (0.15, 0.03), (0.32, -0.02), (0.45, -0.15), (0.50, -0.31), (0.45, -0.45)]
        sickle(f'scythe{s}', [fr(u, v, 0) for u, v in cl], [0.055, 0.075, 0.08, 0.065, 0.04, 0.0], 0.06, U.cross(Vv), R)
        for k, (u, v) in enumerate(cl[1:4]):
            bp = V(*fr(u - 0.05, v - 0.06, 0)); tip = V(*fr(u - 0.13, v - 0.11, 0))
            spike(f'serr{s}{k}', bp, tip, 0.02, W, seg=5)
    legs = {('m', -1): [(-0.11, -0.14, 0.53), (-0.40, -0.30, 0.64), (-0.56, -0.62, 0.0)],
            ('m', 1): [(0.11, -0.14, 0.53), (0.42, -0.06, 0.62), (0.60, 0.14, 0.0)],
            ('h', -1): [(-0.11, 0.08, 0.52), (-0.42, 0.26, 0.62), (-0.60, 0.70, 0.02)],
            ('h', 1): [(0.11, 0.08, 0.52), (0.42, 0.16, 0.64), (0.58, 0.40, 0.06)]}
    for (k, s), p in legs.items():
        loft(f'fem{k}{s}', seg_keys(p[0], p[1], 0.06, 0.06, 0.045, 0.045), M, rings=5, seg=8, side=(0, 1, 0))
        shell(f'fgd{k}{s}', seg_keys(p[0], p[1], 0.06, 0.06, 0.045, 0.045)[:3], L, arc=(25, 155), lift=0.01, thick=0.022, rings=4, seg=8,
              side=(0, 1, 0))
        ball(f'knee{k}{s}', 0.05, p[1], C, u=8)
        blade_leg(f'tib{k}{s}', p[1], p[2], 0.11, L)
    blob('blob', 0.8, 1.1, loc=(0, 0.1, 0.003))


# ------------------------------------------------------------------ 5 강철 매: machine bird of prey skimming the road

def hawk(sc=1.0, **kw):
    begin('hawk', sc, **kw); mats()
    body = [((0, -0.50, 0.70), 0.10, 0.09, 0.08, 2.0),
            ((0, -0.30, 0.67), 0.19, 0.15, 0.17, 2.2),
            ((0, 0.05, 0.67), 0.17, 0.12, 0.13, 2.2),
            ((0, 0.40, 0.69), 0.10, 0.07, 0.07, 2.0),
            ((0, 0.58, 0.70), 0.05, 0.04, 0.04, 2.0)]
    loft('body', body, C, rings=14, seg=16)
    shell('breast', body, W, u=(0.05, 0.50), arc=((195, 345), (220, 320)), lift=0.012, thick=0.026, flare=0.02, rings=5, seg=10)
    shell('mantle', body, G, u=(0.10, 0.60), arc=((20, 160), (40, 140)), lift=0.014, thick=0.026, flare=0.025, rings=5, seg=10, chev=0.08)
    skull = [((0, -0.96, 0.60), 0.012, 0.012, 0.012, 1.6),
             ((0, -0.92, 0.68), 0.04, 0.04, 0.03, 1.6),
             ((0, -0.81, 0.74), 0.08, 0.065, 0.05, 2.0),
             ((0, -0.66, 0.76), 0.115, 0.09, 0.075, 2.4),
             ((0, -0.54, 0.73), 0.09, 0.07, 0.07, 2.2)]
    loft('skull', skull, W, rings=9, seg=14)
    shell('mask', skull, G, u=(0.45, 1.0), arc=((30, 150), (5, 175)), lift=0.026, thick=0.02, flare=-0.022, rings=4, seg=10)
    eyes(skull, 0.62, 18, 0.11, 0.032, off=0.03)
    loft('lowbeak', [((0, -0.87, 0.63), 0.02, 0.015, 0.015, 1.6), ((0, -0.74, 0.665), 0.05, 0.025, 0.025, 2.0)], L, rings=3, seg=8)
    for i, (y, z, l, h) in enumerate(((-0.70, 0.84, 0.30, 0.07), (-0.62, 0.84, 0.36, 0.10))):
        side_blade(f'crest{i}', (0, y, z), 1, 0.25, [(0, 0), (0.10, 0), (l, h), (l * 0.75, h * 1.1)], 0.03, R)
    for s in (-1, 1):
        fr = plane_frame((s * 0.12, -0.30, 0.74), (s * 1, 0.10, 0.16), (0, 1, 0))
        blade(f'wing{s}', [(0, -0.02), (0.38, 0.10), (0.80, 0.42), (0.74, 0.52), (0.36, 0.40), (0, 0.42)], 0.07, W, fr)
        for k in range(4):
            base = V(*fr(0.78 - 0.15 * k, 0.44 + 0.01 * k, 0.0))
            dirn = V(s * (0.48 - k * 0.13), 1, 0.03).normalized()
            fr2 = plane_frame(base, dirn, V(s * 1, -dirn.x * s * 0.3, 0.1))
            ln = 0.56 - 0.07 * k
            blade(f'prim{s}{k}', [(0, -0.05), (ln, -0.01), (ln * 1.03, 0.03), (0.1, 0.06)], 0.035, R if k < 2 else G, fr2)
        for k in range(3):
            base = V(*fr(0.26 - 0.11 * k, 0.40, -0.01))
            dirn = V(s * (0.10 - 0.03 * k), 1, 0.0).normalized()
            fr2 = plane_frame(base, dirn, V(s * 1, 0, 0.1))
            ln = 0.34 - 0.05 * k
            blade(f'sec{s}{k}', [(0, -0.05), (ln, -0.01), (ln * 1.04, 0.03), (0.08, 0.05)], 0.03, G, fr2)
        shell(f'scap{s}', body, W, u=(0.12, 0.42), arc=((10, 60), (25, 70)) if s > 0 else ((120, 170), (110, 155)), lift=0.04, thick=0.03,
              flare=0.03, rings=4, seg=6, chev=0.05)
        hip, knee, ank = (s * 0.09, -0.12, 0.56), (s * 0.11, -0.26, 0.40), (s * 0.11, -0.50, 0.30)
        leg(f'leg{s}', [hip, knee, ank], [(0.06, 0.07), (0.04, 0.045), (0.035, 0.035)], [G, L])
        a = V(*ank)
        for k, dx in enumerate((-0.05, 0.0, 0.05)):
            sweep(f'talon{s}{k}', [a, a + V(dx, -0.08, 0.0), a + V(dx * 1.4, -0.16, -0.06), a + V(dx * 1.4, -0.15, -0.12)],
                  [0.022, 0.02, 0.012, 0.002], W, seg=5)
        sweep(f'hallux{s}', [a, a + V(0, 0.05, -0.05), a + V(0, 0.02, -0.12)], [0.02, 0.014, 0.002], W, seg=5)
    for k, a in enumerate((-22, 0, 22)):
        d = V(math.sin(math.radians(a)), 1, 0.05).normalized()
        blade(f'tail{k}', [(0, -0.06), (0.42, -0.07), (0.50, 0.0), (0.42, 0.07), (0, 0.06)], 0.035, G if k != 1 else W,
              plane_frame((0, 0.42, 0.71 + 0.01 * (k == 1)), d, (math.cos(math.radians(a)), -math.sin(math.radians(a)), 0)))
    tube('nozzle', (0, 0.40, 0.62), (0, 0.56, 0.62), 0.055, M, r1=0.065, seg=12)
    ball('jet', 0.045, (0, 0.565, 0.62), E, scale=(1, 0.4, 1), outline=False)
    blob('blob', 0.75, 1.0, loc=(0, -0.1, 0.003))


# ------------------------------------------------------------------ 6 칼날 상어: hover shark, no legs

def shark(sc=1.0, **kw):
    begin('shark', sc, **kw); mats()
    body = [((0, -1.10, 0.53), 0.03, 0.03, 0.02, 1.6),
            ((0, -0.94, 0.56), 0.13, 0.10, 0.08, 2.0),
            ((0, -0.66, 0.58), 0.26, 0.20, 0.17, 2.2),
            ((0, -0.30, 0.58), 0.30, 0.23, 0.20, 2.2),
            ((0, 0.15, 0.59), 0.22, 0.17, 0.14, 2.2),
            ((0, 0.55, 0.61), 0.10, 0.08, 0.07, 2.0),
            ((0, 0.80, 0.63), 0.05, 0.05, 0.04, 2.0)]
    loft('body', body, W, rings=16, seg=18)
    for i, (u0, u1) in enumerate(((0.0, 0.36), (0.30, 0.64), (0.58, 1.0))):
        shell(f'back{i}', body, G, u=(u0, u1), arc=((8, 172), (14, 166)), lift=0.004 + 0.006 * i, thick=0.024, flare=0.02, rings=5, seg=14, chev=0.05)
    eyes(body, 0.22, 16, 0.13, 0.036, tilt=14, off=0.034)
    loft('mouth', [((0, -0.97, 0.45), 0.05, 0.04, 0.04, 2.0), ((0, -0.80, 0.42), 0.15, 0.05, 0.05, 2.2), ((0, -0.56, 0.43), 0.19, 0.05, 0.05, 2.2)],
         C, rings=4, seg=12, outline=False)
    jaw = [((0, -0.94, 0.38), 0.05, 0.02, 0.03, 1.8), ((0, -0.80, 0.34), 0.15, 0.03, 0.05, 2.2), ((0, -0.60, 0.37), 0.21, 0.04, 0.06, 2.2),
           ((0, -0.44, 0.42), 0.22, 0.03, 0.04, 2.2)]
    loft('jaw', jaw, W, rings=6, seg=12)
    for s in (-1, 1):
        for k, t in enumerate((0.12, 0.32, 0.52, 0.72)):
            y = -0.95 + 0.32 * t; x = s * (0.04 + 0.15 * t)
            spike(f'ut{s}{k}', (x, y, 0.475 - 0.02 * t), (x, y - 0.005, 0.40 - 0.02 * t), 0.017, W, seg=5)
            spike(f'lt{s}{k}', (x * 0.95, y + 0.02, 0.36 - 0.03 * t), (x * 0.95, y + 0.015, 0.42 - 0.03 * t), 0.014, W, seg=5)
        for k in range(3):
            rbox(f'gill{s}{k}', (0.02, 0.022, 0.13), (s * 0.29, -0.50 + k * 0.075, 0.55), C, r=0.008, rot=(-12, 0, s * -8), outline=False)
        blade(f'pec{s}', [(0, 0), (0.30, 0.10), (0.60, 0.42), (0.50, 0.48), (0.18, 0.34), (0, 0.26)], 0.05, W,
              plane_frame((s * 0.24, -0.42, 0.46), (s * 1, 0.25, -0.22), (0, 1, 0)))
        blade(f'pelv{s}', [(0, 0), (0.18, 0.10), (0.22, 0.24), (0, 0.16)], 0.035, G, plane_frame((s * 0.15, 0.32, 0.50), (s * 1, 0.3, -0.35), (0, 1, 0)))
        tube(f'jet{s}', (s * 0.14, -0.20, 0.40), (s * 0.14, -0.20, 0.32), 0.06, M, r1=0.07, seg=12)
        ball(f'glow{s}', 0.05, (s * 0.14, -0.20, 0.315), E, scale=(1, 1, 0.35), outline=False)
    side_blade('dorsal', (0, -0.30, 0.78), 1, 0.15, [(0, 0), (0.42, 0), (0.58, 0.44), (0.36, 0.38)], 0.05, R)
    side_blade('dorsal2', (0, 0.42, 0.66), 1, 0.15, [(0, 0), (0.14, 0), (0.22, 0.14), (0.12, 0.12)], 0.03, G)
    side_blade('tailup', (0, 0.72, 0.64), 1, 0.0, [(0, -0.04), (0.10, 0), (0.42, 0.42), (0.30, 0.44), (0, 0.10)], 0.045, R)
    side_blade('taillow', (0, 0.74, 0.60), 1, 0.0, [(0, 0.04), (0.10, 0), (0.30, -0.24), (0.20, -0.25), (0, -0.06)], 0.04, G)
    blob('blob', 0.7, 1.2, loc=(0, -0.1, 0.003))


# ------------------------------------------------------------------ 7 강철 지네: machine centipede, S-curving along the road

def centipede(sc=1.0, **kw):
    begin('centipede', sc, **kw); mats()
    N, z = 7, 0.32
    cx = lambda i: 0.08 * math.sin(i * 0.9)
    keys = []
    for i in range(N):
        y = -0.66 + 0.27 * i; t = 1 - 0.06 * max(0, i - 3)
        keys.append(((cx(i - 0.5), y - 0.135, z), 0.13 * t, 0.055, 0.045, 1.8))
        keys.append(((cx(i), y, z), 0.21 * t, 0.085, 0.06, 1.6))
    keys.append(((cx(N - 0.5), -0.66 + 0.27 * N - 0.135, z), 0.09, 0.045, 0.04, 1.8))
    m = len(keys) - 1
    loft('body', keys, C, rings=36, seg=12)
    for i in range(N):
        um, ur = (2 * i + 1) / m, (2 * i + 1.95) / m
        shell(f'plate{i}', keys, L, u=((2 * i + 0.4) / m, ur), arc=((-35, 215), (-15, 195)), lift=0.014, thick=0.03, flare=0.035, rings=4,
              seg=12, chev=0.45 / m)
        for s in (-1, 1):
            radial_blade(f'spine{i}{s}', keys, ur - 0.25 / m, -5, s, 0.17, 0.06, R, rise=0.05, out=1.2, depth=0.03, sink=-0.03)
            hip, _, _ = surface(keys, um, -20, s)
            ph = 0.07 * (1 if (i + (s > 0)) % 2 else -1)
            knee = hip + V(s * 0.20, ph + 0.02, 0.08); foot = hip + V(s * 0.44, ph * 2.0 + 0.16, 0.01 - hip.z)
            loft(f'fem{i}{s}', seg_keys(hip, knee, 0.038, 0.038, 0.03, 0.03), M, rings=4, seg=6, side=(0, 1, 0))
            ball(f'knee{i}{s}', 0.032, knee, C, u=8)
            U = (foot - knee).normalized(); Vv = U.cross(V(0, 0, 1)).normalized()
            sickle(f'tib{i}{s}', [knee, knee.lerp(foot, 0.5) + V(s * 0.03, 0, 0.04), foot], [0.045, 0.035, 0.0], 0.035, U.cross(Vv), M)
    head = [((0, -1.06, 0.31), 0.10, 0.04, 0.035, 2.0), ((0, -0.95, 0.33), 0.19, 0.07, 0.05, 2.4), ((0, -0.82, 0.33), 0.15, 0.065, 0.05, 2.2)]
    loft('head', head, C, rings=5, seg=14)
    shell('headplate', head, L, arc=((10, 170), (-10, 190)), lift=0.016, thick=0.026, rings=4, seg=12)
    shell('brow', head, G, u=(0.2, 0.62), arc=(28, 152), lift=0.05, thick=0.02, flare=-0.03, rings=3, seg=8)
    eyes(head, 0.40, 20, 0.10, 0.03, tilt=10, off=0.045)
    for s in (-1, 1):
        sweep(f'pincer{s}', [(s * 0.12, -0.86, 0.29), (s * 0.21, -1.00, 0.27), (s * 0.17, -1.16, 0.26), (s * 0.05, -1.25, 0.26)],
              [0.045, 0.04, 0.026, 0.004], R, seg=8, flat=0.7)
        sweep(f'antenna{s}', [(s * 0.06, -0.98, 0.37), (s * 0.14, -0.96, 0.52), (s * 0.26, -0.80, 0.64), (s * 0.36, -0.58, 0.66)],
              [0.016, 0.013, 0.009, 0.004], G, seg=5)
    tail = V(*keys[-1][0])
    for s in (-1, 1):
        sweep(f'cercus{s}', [tail, tail + V(s * 0.08, 0.20, 0.03), tail + V(s * 0.16, 0.42, 0.10)], [0.03, 0.022, 0.003], R, seg=6)
    blob('blob', 0.65, 1.5, loc=(0, 0.2, 0.003))


# ------------------------------------------------------------------ the real melee robot

def robot(**kw):
    return T.real_robot('melee', **kw)


ALL = {'hound': hound, 'panther': panther, 'raptor': raptor, 'mantis': mantis, 'hawk': hawk, 'shark': shark,
       'centipede': centipede, 'robot': robot}
