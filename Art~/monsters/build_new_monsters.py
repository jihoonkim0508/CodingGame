"""The 2-4 / 3-4 support monsters (healer, shielder, queue and stack centipedes) and the reflector, as game models.

    blender -b --factory-startup -P build_new_monsters.py -- --game "<Unity Models/Monsters folder>" [--only healer,...] [--preview]

Same pipeline as the 시안 monsters (monster_kit.py = build_monsters.py of the model branch): parts built from bmesh
primitives in the 시안1 bug-bot style, joined into one skinned mesh (one bone per part), coloured from a palette, with a
looping "Walk" take. Parts the game moves on its own: Cross (healer), Dome (shielder), Crystal (reflector) pulse on a cast.
Also writes MonsterMarker.fbx (the static down-arrow over a centipede's hittable segment).
"""
import bpy, math, os, sys, importlib.util
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location('monster_kit', os.path.join(HERE, 'monster_kit.py'))
K = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(K)
P, ell, shell, tube, spline, cone_between, box, surf, glyph, flat_poly = (
    K.P, K.ell, K.shell, K.tube, K.spline, K.cone_between, K.box, K.surf, K.glyph, K.flat_poly)


def mats(shell_hex, dark_hex, belly_hex, glow_hex):
    S = K.bug_mats()
    S.update(shell=P('Shell', shell_hex, 0.32, coat=0.7), shell2=P('ShellDark', dark_hex, 0.4, coat=0.4),
             belly=P('Belly', belly_hex, 0.55), code=P('Glow', glow_hex, emit=2.2),
             leg=P('Leg', '#33303e', 0.45), claw=P('Claw', glow_hex, 0.4))
    return S


def round_body(S, c, r, groups, glyph_txt=None):
    """Two shell halves over a light belly, a glowing seam down the back, optional glyphs on the halves."""
    body = []
    halves = {}
    for s in (1, -1):
        h = shell('shell', r, c + Vector((s * 0.02, 0, 0)), S['shell'],
                  [((0, 0, 0), (s, 0, 0)), ((0, 0, -r.z * 0.4), (0, 0, 1))], thick=0.05, seg=18, rings=10)
        halves[s] = h
        body.append(h)
    if glyph_txt:
        for s in (1, -1):
            p, n, B = surf(c, r, (s * 0.55, 0.1, 0.83))
            body.append(glyph('mark', glyph_txt, r.x * 1.1, S['code'], Matrix.Translation(p + n * 0.02) @ B.to_4x4(),
                              depth=0.035, wrap=halves[s]))
    seam = [c + Vector((0, r.y * 0.95 * math.sin(math.radians(a)), r.z * math.cos(math.radians(a)) + 0.004))
            for a in range(-60, 75, 15)]
    body.append(tube('seam', seam, 0.02, S['code'], seg=6, up=(1, 0, 0)))
    body.append(ell('belly', (r.x * 0.86, r.y * 0.92, r.z * 0.42), c + Vector((0, 0, -r.z * 0.35)), S['belly'], 14, 8))
    groups['Body'] = body
    return body


def face(S, body, hc, hr, eye=0.085, mand=True, antennae=0.0):
    body.append(ell('head', hr, hc, S['shell2'], 16, 10))
    hc = Vector(hc)
    body += K.bug_eyes(S, hc, hr, hr[0] * 0.45, hc.z + hr[2] * 0.15, hc.y - hr[1] * 0.85,
                       (eye, eye * 0.7, eye * 1.15))
    for s in (1, -1):
        if mand:
            m0 = hc + Vector((s * hr[0] * 0.3, -hr[1] * 0.8, -hr[2] * 0.5))
            body.append(tube('mand', K.spline([m0, m0 + Vector((s * 0.05, -0.1, -0.02)), m0 + Vector((-s * 0.04, -0.16, -0.03))], 3),
                             (0.03, 0.011), S['claw'], seg=6, up=(0, 0, 1)))
        if antennae:
            a0 = hc + Vector((s * hr[0] * 0.3, -hr[1] * 0.4, hr[2] * 0.8))
            ant = K.spline([a0, a0 + Vector((s * 0.05, -0.1, antennae * 0.6)), a0 + Vector((s * 0.16, -0.16, antennae))], 3)
            body.append(tube('ant', ant, (0.022, 0.014), S['leg'], seg=6, up=(1, 0, 0)))
            body.append(ell('antip', [0.045] * 3, ant[-1], S['code'], 10, 7))


def legs(S, groups, pivots, rows, hip_x, hip_z, reach_x, r=(0.06, 0.045)):
    for s, side in ((1, 'L'), (-1, 'R')):
        for i, (y, dy) in enumerate(rows):
            hip = (s * hip_x, y, hip_z)
            nm = f'Leg_{side}{i + 1}'
            groups[nm] = K.bug_leg(nm, s, hip, (s * (hip_x + reach_x * 0.75), y + dy, hip_z + 0.02),
                                   (s * (hip_x + reach_x), y + dy * 1.6, 0.0), r, S, knee_r=r[0] * 1.1, foot_r=r[1] * 1.3)
            pivots[nm] = hip


def healer():
    """A mint beetle with a big glowing white-green cross on a stalk over its back (the Cross pulses on each heal)."""
    S = mats('#5fdca0', '#2d7a5e', '#f4fff8', '#59ff9c')
    S['cross'] = P('Cross', '#ffffff', 0.3, emit=0.9, ehex='#7dffb2')
    groups, pivots = {}, {}
    c, r = Vector((0, 0.06, 0.42)), Vector((0.36, 0.42, 0.32))
    body = round_body(S, c, r, groups, '+')
    face(S, body, (0, -0.34, 0.42), (0.25, 0.21, 0.22), antennae=0.0)
    top = c + Vector((0, 0.08, r.z))
    body.append(cone_between('stalk', top - Vector((0, 0, 0.05)), top + Vector((0, 0, 0.34)), 0.035, 0.03, S['leg'], seg=8))
    body.append(ell('stalkbase', (0.09, 0.09, 0.05), top, S['belly'], 12, 6))
    cc = top + Vector((0, 0, 0.5))
    groups['Cross'] = [box('crossx', (0.38, 0.12, 0.12), cc, m=S['cross'], bev=0.035),
                       box('crossz', (0.12, 0.12, 0.38), cc, m=S['cross'], bev=0.035),
                       box('crossy', (0.12, 0.38, 0.12), cc, m=S['cross'], bev=0.035),
                       ell('crosscore', [0.085] * 3, cc, S['code'], 10, 7)]
    pivots['Cross'] = cc
    legs(S, groups, pivots, ((-0.16, -0.08), (0.06, 0.0), (0.28, 0.08)), 0.24, 0.3, 0.24)
    return groups, pivots


def shielder():
    """A steel-blue beetle with a glass dome emitter on its back and a round-cornered shield plate held in front."""
    S = mats('#5b8dff', '#2c3f7a', '#e3ecff', '#72d6ff')
    S['dome'] = P('Dome', '#a8ecff', 0.1, emit=0.6, ehex='#8fe6ff', alpha=0.5)
    S['plate'] = P('Plate', '#d6eaff', 0.3, coat=0.6)
    groups, pivots = {}, {}
    c, r = Vector((0, 0.08, 0.4)), Vector((0.38, 0.42, 0.3))
    body = round_body(S, c, r, groups)
    face(S, body, (0, -0.32, 0.4), (0.25, 0.21, 0.22), antennae=0.18)
    top = c + Vector((0, 0.06, r.z - 0.02))
    dome = shell('dome', Vector((0.27, 0.27, 0.27)), top, S['dome'], [((0, 0, 0), (0, 0, 1))], thick=0.02, seg=20, rings=12)
    ring = tube('domering', [top + Vector((0.28 * math.cos(a), 0.28 * math.sin(a), 0.01))
                             for a in (2 * math.pi * k / 20 for k in range(20))], 0.035, S['code'], seg=6, closed=True)
    groups['Dome'] = [dome, ring, ell('domecore', [0.08] * 3, top + Vector((0, 0, 0.1)), S['code'], 10, 7)]
    pivots['Dome'] = top
    # the shield plate, tilted back so the battle camera sees its face, with a glowing rim
    pc = Vector((0, -0.64, 0.36))
    tilt = Matrix.Translation(pc) @ K.Rxyz(-22, 0, 0)
    w, h = 0.36, 0.28
    outline = [(w * math.copysign(abs(math.cos(a)) ** 0.5, math.cos(a)), h * math.copysign(abs(math.sin(a)) ** 0.5, math.sin(a)))
               for a in (2 * math.pi * k / 28 for k in range(28))]
    plate = flat_poly('plate', outline, 0.06, S['plate'], tilt @ K.Rxyz(90, 0, 0), bev=0.015)
    rim = tube('platerim', [tilt @ K.Rxyz(90, 0, 0) @ Vector((x, y, -0.035)) for x, y in outline], 0.026, S['code'], seg=6,
               closed=True)
    boss = ell('plateboss', (0.09, 0.04, 0.09), tilt @ Vector((0, -0.05, 0)), S['code'], 10, 7)
    arms = [tube('arm', [(s * 0.17, -0.42, 0.34), (s * 0.2, -0.52, 0.34), (s * 0.16, -0.6, 0.36)], 0.035, S['leg'], seg=6)
            for s in (1, -1)]
    groups['Shield'] = [plate, rim, boss] + arms
    pivots['Shield'] = (0, -0.45, 0.34)
    legs(S, groups, pivots, ((-0.14, -0.08), (0.08, 0.0), (0.3, 0.08)), 0.25, 0.28, 0.25)
    return groups, pivots


def centipede(kind, part):
    """One segment of a centipede: 'head' (eyes, mandibles, antennae), 'body' or 'tail' (two glowing cerci). Every
    segment has two pairs of legs and a ball joint at the back toward the next one (SegmentGap .8 apart in the game)."""
    shell_hex, dark_hex, glow = {'queue': ('#ff9d3f', '#8a4416', '#ffe27a'), 'stack': ('#a878ff', '#4b2d8f', '#ff8ff0')}[kind]
    S = mats(shell_hex, dark_hex, '#fff3e3' if kind == 'queue' else '#f1eaff', glow)
    groups, pivots = {}, {}
    c, r = Vector((0, 0.0, 0.34)), Vector((0.3, 0.3, 0.26))
    body = round_body(S, c, r, groups)
    if part != 'tail':
        body.append(ell('joint', (0.12, 0.12, 0.11), c + Vector((0, 0.33, -0.04)), S['shell2'], 10, 7))
    if part == 'head':
        face(S, body, (0, -0.33, 0.36), (0.23, 0.2, 0.2), eye=0.09, antennae=0.34)
    else:
        body.append(ell('front', (0.17, 0.12, 0.14), c + Vector((0, -0.27, -0.02)), S['shell2'], 10, 7))
    if part == 'tail':
        for s in (1, -1):
            cer = K.spline([c + Vector((s * 0.08, 0.24, 0.0)), c + Vector((s * 0.16, 0.42, 0.06)),
                            c + Vector((s * 0.22, 0.56, 0.2))], 3)
            body.append(tube('cercus', cer, (0.05, 0.016), S['shell2'], seg=7, up=(1, 0, 0)))
            body.append(ell('cerctip', [0.045] * 3, cer[-1], S['code'], 10, 7))
    legs(S, groups, pivots, ((-0.12, -0.04), (0.14, 0.04)), 0.2, 0.24, 0.22, r=(0.05, 0.038))
    return groups, pivots


BOOK_COVERS = (('#ff9fb8', '#ffe27a'), ('#8fd3ff', '#ffffff'), ('#b9a3ff', '#ffd2f2'), ('#9be8b0', '#fff3a8'))


def book(variant, walker):
    """One book of the stack monster's pile (a stack is a pile of books, the bottom one walking): a pastel hardcover with
    cream pages, a spine band and a glowing title strip; the bottom book (walker) has stubby legs and angry eyes on its
    spine (the front, -Y). Width x 0.8, depth y 0.56, 0.17 thick."""
    cover, accent = BOOK_COVERS[variant]
    S = mats(cover, '#4b3a6a', '#fff6e0', accent)
    S['pages'] = P('Pages', '#fff4dc', 0.7)
    S['title'] = P('Title', accent, 0.3, emit=0.8)
    groups, pivots = {}, {}
    w, d, t, lid = 0.8, 0.56, 0.17, 0.03
    z0 = 0.16 if walker else 0.0
    body = [box('lower', (w, d, lid), (0, 0, z0 + lid / 2), m=S['shell'], bev=0.012),
            box('upper', (w, d, lid), (0, 0, z0 + t - lid / 2), m=S['shell'], bev=0.012),
            box('spine', (w, lid * 1.3, t), (0, -d / 2 + lid * 0.4, z0 + t / 2), m=S['shell'], bev=0.015),
            box('pages', (w - 0.05, d - 0.06, t - 2 * lid + 0.004), (0, 0.02, z0 + t / 2), m=S['pages'], bev=0.006),
            box('band1', (0.05, lid * 1.45, t * 1.02), (-w * 0.34, -d / 2 + lid * 0.35, z0 + t / 2), m=S['shell2'], bev=0.006),
            box('band2', (0.05, lid * 1.45, t * 1.02), (w * 0.34, -d / 2 + lid * 0.35, z0 + t / 2), m=S['shell2'], bev=0.006),
            box('title', (w * 0.5, 0.1, 0.012), (0, 0.04, z0 + t + 0.004), m=S['title'], bev=0.0)]
    groups['Body'] = body
    if walker:
        for s in (1, -1):
            p = Vector((s * 0.15, -d / 2 - 0.005, z0 + t * 0.55))
            body.append(ell('eye', (0.07, 0.03, 0.06), p, S['eye'], 12, 8))
            body.append(ell('shine', [0.02] * 3, p + Vector((-0.02, -0.03, 0.02)), S['shine'], 8, 6))
            body.append(box('brow', (0.13, 0.03, 0.03), p + Vector((s * 0.01, -0.01, 0.065)), (0, -s * 20, 0), S['shell2'], bev=0.008))
        for s, side in ((1, 'L'), (-1, 'R')):
            hip = Vector((s * 0.22, 0.0, z0 + 0.02))
            nm = f'Leg_{side}1'
            groups[nm] = [cone_between(nm, hip, hip - Vector((0, 0, z0 - 0.05)), 0.055, 0.05, S['leg'], seg=8),
                          ell(nm + 'f', (0.08, 0.11, 0.05), (s * 0.22, -0.03, 0.045), S['claw'], 10, 6)]
            pivots[nm] = hip
    return groups, pivots


def walk_book(t, Pp):
    """A book in the pile wobbles (each book in the game starts at its own phase, so the pile sways); the bottom book
    also stomps along on its two stubby legs, dipping at each step."""
    w = 2 * math.pi * t / Pp['cycle']
    out = {}
    for side, off in (('L', 0.0), ('R', 0.5)):
        n = 'Leg_' + side + '1'
        if n in Pp:
            u = (t / Pp['cycle'] + off) % 1
            dy, dz = K.step(u, 0.5, 0.08, 0.07)
            out[n] = Matrix.Translation((0, dy, dz))
    bob = -0.015 * math.cos(2 * w) if 'Leg_L1' in Pp else 0.0
    out['Body'] = Matrix.Translation((0, 0, bob)) @ K.about(Pp['Body'], K.Rxyz(2.5 * math.sin(w), 3.5 * math.sin(w + 0.8), 3 * math.sin(w)))
    return out


def reflector():
    """A pale crystal-mirror beetle: hexagonal mirror tiles on its shell, a crown of glassy crystal spikes (Crystal) and
    a round mirror held in front (Mirror)."""
    S = mats('#ece8ff', '#6c63a8', '#ffffff', '#8ff6ff')
    S['mirror'] = P('MirrorFace', '#c9fbff', 0.05, emit=0.7, ehex='#bff8ff')
    S['crystal'] = P('Crystal', '#b9f3ff', 0.05, emit=1.6, ehex='#9feeff', alpha=0.55)
    S['pink'] = P('CrystalPink', '#ffc6f4', 0.05, emit=1.4, ehex='#ff9be9', alpha=0.55)
    groups, pivots = {}, {}
    c, r = Vector((0, 0.06, 0.4)), Vector((0.37, 0.42, 0.31))
    body = round_body(S, c, r, groups)
    face(S, body, (0, -0.34, 0.4), (0.25, 0.21, 0.22), antennae=0.0)
    hexagon = [(0.1 * math.cos(math.radians(60 * k + 30)), 0.1 * math.sin(math.radians(60 * k + 30))) for k in range(6)]
    for d in ((0.55, -0.2, 0.8), (-0.55, -0.2, 0.8), (0.6, 0.35, 0.72), (-0.6, 0.35, 0.72), (0.0, 0.62, 0.78)):
        p, n, B = surf(c, r, d)
        body.append(flat_poly('tile', hexagon, 0.03, S['mirror'], Matrix.Translation(p + n * 0.02) @ B.to_4x4(), bev=0.008))
    top = c + Vector((0, 0.08, r.z - 0.03))
    spikes = []
    for i, (dx, dy, h, lean) in enumerate(((0, 0, 0.42, 0), (0.13, 0.05, 0.3, 18), (-0.13, 0.05, 0.3, -18),
                                           (0.06, 0.16, 0.26, 10), (-0.07, -0.12, 0.24, -8))):
        b = top + Vector((dx, dy, 0))
        tip = b + Vector((math.sin(math.radians(lean)) * h, 0.02, h))
        spikes.append(cone_between('spike', b, tip, 0.075 if i == 0 else 0.06, 0.0, S['crystal' if i % 2 == 0 else 'pink'], seg=5,
                                   smooth=0))
    groups['Crystal'] = spikes
    pivots['Crystal'] = top
    mc = Vector((0, -0.62, 0.38))
    tilt = Matrix.Translation(mc) @ K.Rxyz(-25, 0, 0) @ K.Rxyz(90, 0, 0)
    disc = [(0.24 * math.cos(a), 0.24 * math.sin(a)) for a in (2 * math.pi * k / 24 for k in range(24))]
    groups['Mirror'] = [flat_poly('mirror', disc, 0.05, S['mirror'], tilt, bev=0.012),
                        tube('mirrorrim', [tilt @ Vector((x * 1.04, y * 1.04, 0)) for x, y in disc], 0.03, S['shell2'], seg=6,
                             closed=True),
                        tube('mirrorshine', [tilt @ Vector((0.13 * math.cos(a) - 0.04, 0.13 * math.sin(a) + 0.05, -0.03))
                                             for a in (math.radians(x) for x in range(100, 171, 14))], 0.016,
                             P('Shine', '#ffffff', emit=3), seg=5)] + \
        [tube('arm', [(s * 0.17, -0.42, 0.34), (s * 0.18, -0.52, 0.35), (s * 0.12, -0.58, 0.37)], 0.035, S['leg'], seg=6)
         for s in (1, -1)]
    pivots['Mirror'] = (0, -0.45, 0.34)
    legs(S, groups, pivots, ((-0.16, -0.08), (0.06, 0.0), (0.28, 0.08)), 0.24, 0.29, 0.24)
    return groups, pivots


def walk_bugs(reach, wiggle=0.0):
    """Tripod gait (or alternating pairs for the centipede): each planted foot swept back about its hip, lifted legs
    brought forward; the body dips at each touchdown; extra parts sway with the body. wiggle: the body's yaw (deg) for
    the centipede's travelling wave (the game starts each segment at its own phase)."""
    def fn(t, Pp):
        Pq = dict(Pp, reach=reach)
        out = {}
        for name in [n for n in Pq if n.startswith('Leg_')]:
            side, i = name[4], int(name[5])
            off = 0.0 if (side == 'L') == (i != 2) else 0.5
            u = (t / Pq['cycle'] + off) % 1
            dy, _ = K.step(u, 0.55, reach, 0)
            hip, foot = Pq[name], Pq['foot_' + name]
            fx, fy = foot.x - hip.x, foot.y - hip.y
            rr = math.hypot(fx, fy)
            y = max(-0.98 * rr, min(0.98 * rr, fy + dy))
            a0, a1 = math.atan2(fy, fx), math.asin(y / rr)
            if fx < 0:
                a1 = math.pi - a1
            yaw = (math.degrees(a1 - a0) + 180) % 360 - 180
            lift = 0.0 if u < 0.55 else 18 * math.sin(math.pi * (u - 0.55) / 0.45)
            axis = Vector((-math.sin(a0), math.cos(a0), 0))
            out[name] = K.about(hip, K.Rxyz(0, 0, yaw) @ Matrix.Rotation(math.radians(-lift), 4, axis))
        w = 2 * math.pi * t / Pq['cycle']
        out['Body'] = Matrix.Translation((0, 0, -0.025 * math.cos(2 * w))) @ K.about(
            Pq['Body'], K.Rxyz(-Pq['lean'] + 1.5 * math.cos(2 * w), 3 * math.sin(w), 2 * math.sin(w + 0.6) + wiggle * math.sin(w)))
        for part, amp in (('Cross', 7), ('Dome', 2), ('Shield', 4), ('Crystal', 3), ('Mirror', 4)):
            if part in Pq:
                out[part] = K.about(Pq[part], K.Rxyz(amp * math.sin(2 * w + 1), amp * 0.6 * math.sin(w), 0))
        return out
    return fn


NEW = {
    'healer': ('MonsterHealer', healer, 1.25, walk_bugs(0.09), 0.7),
    'shielder': ('MonsterShielder', shielder, 1.1, walk_bugs(0.09), 0.7),
    'reflector': ('MonsterReflector', reflector, 1.25, walk_bugs(0.09), 0.7),
}
for part in ('head', 'body', 'tail'):
    NEW[f'queue_{part}'] = (f'MonsterQueue{part.title()}', (lambda p=part: centipede('queue', p)),
                            0.72 if part != 'head' else 0.82, walk_bugs(0.07, wiggle=9), 0.5)
for v in range(len(BOOK_COVERS)):
    NEW[f'book_{v}'] = (f'MonsterBook{v}', (lambda v=v: book(v, False)), 0.17, walk_book, 1.2)
NEW['book_walker'] = ('MonsterBookWalker', lambda: book(0, True), 0.33, walk_book, 0.5)
for mid, (name, builder, height, walk, cycle) in NEW.items():
    K.MODELS[mid] = (name, builder, height, 'Monster_' + name)
    K.GAME[mid] = name
    K.WALKS[mid] = (walk, cycle, 4)


def marker(out_dir):
    """A fat down-arrow (cone tip, round shaft) for the hittable segment; one material, coloured in Unity."""
    bpy.ops.wm.read_factory_settings(use_empty=True)
    m = P('Marker', '#ffe14a')
    parts = [cone_between('tip', (0, 0, 0.42), (0, 0, 0.0), 0.22, 0.0, m, seg=4),
             cone_between('shaft', (0, 0, 0.8), (0, 0, 0.41), 0.08, 0.08, m, seg=4)]
    ob = K.join(parts, 'Arrow')
    root = K.link(bpy.data.objects.new('MonsterMarker', None))
    ob.parent = root
    K.export_fbx(os.path.join(out_dir, 'MonsterMarker.fbx'), root, [ob])


if __name__ == '__main__':
    out = os.path.abspath(K.arg('--game'))
    only = K.arg('--only')
    for mid in (only.split(',') if only else NEW):
        if mid != 'marker':
            K.build_game(mid, out)
    if not only or 'marker' in only:
        marker(out)
