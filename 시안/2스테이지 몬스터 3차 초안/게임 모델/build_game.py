# The three chosen round-3 stage-2 monsters as game models: 일반 4 뿔 마귀 (imp), 빠른 적 3 추격 랩터 (raptor) and
# 탱커병 1 철방패 거병 (brute), built by the drafts' own code (../<role>/원본/models.py) at game resolution, rigged and given
# a looping walk, for Unity (MonsterModels.Apply fits them on the enemy prefabs):
#
#   blender -b --factory-startup -P build_game.py -- <imp|raptor|brute> <Unity Models/Monsters folder> [--preview <dir>]
#
# Writes <Model>.fbx (one skinned mesh on an armature, a looping "Walk" take at 60 fps) and <Model>.json (its height and
# how fast its planted feet go back, in design units, so the game plays the walk at the enemy's own speed). The draft
# outline shells and ground blob are left out (the game shader draws its own outline); render-time bevels and
# subdivision are dropped or kept at their low level. Every colour region is one cell of a palette texture
# (REGIONS below; UVs sit on the cell centres): MonsterModels holds the colours and writes the texture, so a colourway
# is changed there, not here. --preview renders the walk from the battle camera's angle into <dir> (Workbench).
#
# The user's changes to the drafts: the brute is 색 시안/원본/neck.py's t1neck (shield.py's 1.7x shield with its grip and
# arm, the neck raised so the face clears the shield), its legs re-posed neutral for the walk; the imp is scaled up in the
# game (MonsterModels).
import bpy, bmesh, sys, os, math, json, re
from mathutils import Vector, Matrix

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index('--') + 1:]
KIND, OUT = ARGS[0], os.path.abspath(ARGS[1])
PREVIEW = ARGS[ARGS.index('--preview') + 1] if '--preview' in ARGS else None
SRC = {'imp': '일반', 'raptor': '빠른 적', 'brute': '탱커병'}[KIND]
NAME = {'imp': 'MonsterImp', 'raptor': 'MonsterRaptor', 'brute': 'MonsterShieldBrute'}[KIND]
sys.path[:0] = [os.path.join(HERE, '..', SRC, '원본'), os.path.join(HERE, '..', '..', '2스테이지 몬스터 색 시안', '원본')]
import toon as T     # noqa: E402  (the role's own helpers)
import models as M   # noqa: E402
import palette as P  # noqa: E402

FPS = 60
# Palette cells, in this order on a 4x4 grid (MonsterModels.Regions keeps the same order).
REGIONS = ['white', 'lightgrey', 'midgrey', 'charcoal', 'gunmetal', 'accent', 'eye']
COLS = 4


def region_of(rgb):
    for name, attrs in (('white', ['WHITE']), ('lightgrey', ['LGREY']), ('midgrey', ['MGREY']), ('charcoal', ['CHAR']),
                        ('gunmetal', ['GUN', 'SLATE']), ('accent', ['ORANGE', 'RED', 'AMBER']), ('eye', ['EYE', 'GLOW'])):
        if any(getattr(P, a, None) == tuple(rgb) for a in attrs):
            return name
    raise ValueError(f'colour {rgb} is not in {SRC}/palette.py')


# ---------------------------------------------------------------- game resolution (patches over the drafts' helpers)

def imp_patches():
    """The imp's skin bodies at their own subdivision (the draft forced level 2), its sphere and bent-plate grids coarser."""
    import bmesh as B

    def skin(name, verts, edges, radii, mat, subdiv=2, outline=True):
        me = bpy.data.meshes.new(name + '_src')
        me.from_pydata([tuple(Vector(v) * T.SC) for v in verts], edges, [])
        src = bpy.data.objects.new(name + '_src', me); bpy.context.scene.collection.objects.link(src)
        src.modifiers.new('skin', 'SKIN')
        for sv, r in zip(me.skin_vertices[0].data, radii):
            sv.radius = (r[0] * T.SC, r[1] * T.SC) if isinstance(r, (tuple, list)) else (r * T.SC, r * T.SC)
        me.skin_vertices[0].data[0].use_root = True
        s = src.modifiers.new('sub', 'SUBSURF'); s.levels = s.render_levels = subdiv
        dg = bpy.context.evaluated_depsgraph_get()
        em = bpy.data.meshes.new_from_object(src.evaluated_get(dg), depsgraph=dg)
        bpy.data.objects.remove(src); bpy.data.meshes.remove(me)
        bm = B.new(); bm.from_mesh(em); bpy.data.meshes.remove(em)
        return T._obj(name, bm, mat, (0, 0, 0), kind='smooth', outline=outline)

    def ball(name, r, loc, mat, scale=(1, 1, 1), rot=None, outline=True):
        u = 12 if r * T.SC < 0.05 else 16 if r * T.SC < 0.2 else 20
        bm = B.new(); B.ops.create_uvsphere(bm, u_segments=u, v_segments=u // 2, radius=r * T.SC)
        for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
        return T._obj(name, bm, mat, loc, rot=rot, kind='smooth', outline=outline)

    def prism(name, pts, depth, loc, mat, r=0.05, rot=None, outline=True, seg=2, bend=0.0, bendz=0.0):
        bm = B.new()
        vs = [bm.verts.new((x * T.SC, -depth * T.SC / 2, z * T.SC)) for x, z in pts]
        f = bm.faces.new(vs)
        ext = B.ops.extrude_face_region(bm, geom=[f])
        for v in (g for g in ext['geom'] if isinstance(g, B.types.BMVert)): v.co.y += depth * T.SC
        B.ops.recalc_face_normals(bm, faces=bm.faces[:])
        if r: B.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=r * T.SC, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
        if bend or bendz:
            xs = [v.co.x for v in bm.verts]; zs = [v.co.z for v in bm.verts]
            for axis, lo, hi in ((0, min(xs), max(xs)), (2, min(zs), max(zs))):
                for i in range(1, 5):
                    co = [0, 0, 0]; co[axis] = lo + (hi - lo) * i / 5; no = [0, 0, 0]; no[axis] = 1
                    B.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no)
            for v in bm.verts: v.co.y += (bend * v.co.x ** 2 + bendz * v.co.z ** 2) / T.SC
            B.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
        return T._obj(name, bm, mat, loc, rot=rot, kind='wn' if (bend or bendz) else 'hard', outline=outline)

    M.skin, M.ball, M.prism = skin, ball, prism


def raptor_patches():
    """The raptor's armour plates sampled half as finely (the draft sampled them for close-up stills)."""
    full = T.shell

    def shell(name, keys, mat, rings=5, seg=10, **kw):
        return full(name, keys, mat, rings=max(2, (rings + 1) // 2), seg=max(3, (seg + 1) // 2), **kw)
    M.shell = shell


# ---------------------------------------------------------------- the three builds

def build_imp():
    imp_patches(); T.reset()
    return M.imp()


# The raptor's legs stand in one neutral pose (the draft posed them mid-stride, of two lengths): hip, knee, heel, ball.
RAPTOR_LEG = [(0.17, 0.06, 0.80), (0.20, -0.20, 0.55), (0.21, 0.04, 0.27), (0.21, -0.08, 0.10)]


def build_raptor():
    raptor_patches(); T.reset()
    M.raptor()
    root = T.ROOT
    for o in [o for o in bpy.data.objects if re.match(r'(leg|thigh|shin|foot|sickle)-?1', o.name)]:
        bpy.data.objects.remove(o)
    T.ROOT = root
    S = [(0.12, 0.18), (0.075, 0.09), (0.05, 0.055), (0.045, 0.05)]
    for s in (-1, 1):
        p = [(s * x, y, z) for x, y, z in RAPTOR_LEG]
        M.leg(f'leg{s}', p, S, [M.L, M.M, M.C])
        M.guard(f'thigh{s}', p[0], p[1], *S[0], *S[1], M.W, arc=(M.outer(s) - 90, M.outer(s) + 90), flare=0.02, thick=0.028)
        M.guard(f'shin{s}', p[1], p[2], *S[1], *S[2], M.W, arc=(25, 155))
        M.paw(f'foot{s}', p[3], (0, -1, -0.3), 0.14, 0.06, M.M, claw=M.W, claws=3, cl=0.07)
        a = M.V(*p[3]) + M.V(-s * 0.04, 0, 0.03)
        M.sweep(f'sickle{s}', [a, a + M.V(0, -0.06, 0.08), a + M.V(0, -0.16, 0.06), a + M.V(0, -0.20, -0.02)],
                [0.028, 0.026, 0.016, 0.003], M.W, seg=6)
    return root


# The brute's legs in one neutral walking pose (the draft braced one forward, one back): hip, knee, ankle, sole.
BRUTE_LEG = [(0.32, 0.12, 0.64), (0.42, -0.08, 0.38), (0.46, 0.04, 0.14), (0.46, 0.02, 0.0)]


def build_brute():
    import neck as NK
    T.HI = False; T.reset()
    root = NK.t1neck()
    remove_render_only()
    G, W, Mg = M.m(P.GUN), M.m(P.WHITE), M.m(P.MGREY)
    # neutral legs
    for o in [o for o in bpy.data.objects if o.name.rstrip('-1') in ('thigh', 'greave', 'knee', 'kspike', 'sabaton', 'toe')]:
        bpy.data.objects.remove(o)
    T.ROOT = root
    for s in (-1, 1):
        hip, knee, ankle, foot = [(s * x, y, z) for x, y, z in BRUTE_LEG]
        T.tube(f'thigh{s}', hip, knee, 0.18, G, r1=0.15, segs=12)
        M.limb(f'greave{s}', ankle, knee, 0.24, 0.26, W, r=0.07, taper=(1.15, 1.1))
        T.sbox(f'knee{s}', (0.24, 0.16, 0.22), (knee[0], knee[1] - 0.13, knee[2] + 0.04), M.m(P.LGREY), r=0.06, levels=1, rot=(-12, 0, 0), taper=(0.8, 1))
        T.sweep(f'kspike{s}', [(knee[0], knee[1] - 0.20, knee[2] + 0.06), (knee[0], knee[1] - 0.30, knee[2] + 0.13)], [0.045, 0.008], M.m(P.LGREY), seg=8)
        T.sbox(f'sabaton{s}', (0.32, 0.48, 0.18), (foot[0], foot[1] - 0.08, 0.09), G, r=0.06, levels=1, taper=(0.85, 0.7))
        T.rbox(f'toe{s}', (0.28, 0.16, 0.12), (foot[0], foot[1] - 0.32, 0.07), Mg, r=0.04)
    remove_render_only()
    return root


# ---------------------------------------------------------------- mesh: one skinned mesh coloured from the palette

def remove_render_only():
    for o in [o for o in bpy.data.objects if o.name.endswith('_hull') or 'blob' in o.name or o.name.startswith('tmp')]:
        bpy.data.objects.remove(o)


# Parts that keep the draft's (viewport level) subdivision: the raptor's lofted body and head; its claws, teeth and
# limbs read the same at battle size without it.
SUBDIVIDED = ('body', 'skull', 'jaw')


def bake_parts():
    """Each part's mesh at game resolution, modifiers applied: render-only bevels dropped (hard edges kept crisp by
    angle instead), subdivision kept only on SUBDIVIDED at its viewport level, weighted normals kept."""
    out = []
    for o in [o for o in bpy.data.objects if o.type == 'MESH']:
        hard = any(m.type == 'BEVEL' for m in o.modifiers)
        for m in [m for m in o.modifiers if m.type == 'BEVEL' or (m.type == 'SUBSURF' and o.name not in SUBDIVIDED)]:
            o.modifiers.remove(m)
        if hard:
            o.data.set_sharp_from_angle(angle=math.radians(40))
        if o.modifiers:
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg)
            o.modifiers.clear(); old = o.data; o.data = me; bpy.data.meshes.remove(old)
        out.append(o)
    return out


def colour(o):
    key = next(k for k, m in T._mats.items() if m == o.data.materials[0])
    return key[1]


def paint(objs, material):
    """UVs on each part's palette cell, one material for all."""
    for o in objs:
        me = o.data
        cell = REGIONS.index(region_of(colour(o)))
        while me.uv_layers: me.uv_layers.remove(me.uv_layers[0])
        uv = me.uv_layers.new(name='UV')
        c = ((cell % COLS + 0.5) / COLS, 1 - (cell // COLS + 0.5) / COLS)
        for loop in uv.data: loop.uv = c
        me.materials.clear(); me.materials.append(material)


def seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (a + ab * t - p).length


def smooth_weights(o, segs, power=5, keep=3):
    """Weights on the bones whose segments are nearest, falling off with distance: organic bodies bend at the joints."""
    mw = o.matrix_world
    groups = {}
    for v in o.data.vertices:
        p = mw @ v.co
        near = {}
        for bone, a, b in segs:
            near[bone] = min(near.get(bone, 1e9), seg_dist(p, Vector(a), Vector(b)))
        best = sorted(near.items(), key=lambda kv: kv[1])[:keep]
        ws = [(n, 1 / max(d, 1e-4) ** power) for n, d in best]
        total = sum(w for _, w in ws)
        for n, w in ws:
            if w / total < 0.03: continue
            g = groups.get(n) or o.vertex_groups.new(name=n); groups[n] = g
            g.add([v.index], w / total, 'REPLACE')


def rigid(o, bone):
    g = o.vertex_groups.new(name=bone)
    g.add(list(range(len(o.data.vertices))), 1.0, 'REPLACE')


# ---------------------------------------------------------------- motion helpers

def V(*a):
    return Vector(a[0] if len(a) == 1 else a)


def Rx(d): return Matrix.Rotation(math.radians(d), 4, 'X')
def Ry(d): return Matrix.Rotation(math.radians(d), 4, 'Y')
def Rz(d): return Matrix.Rotation(math.radians(d), 4, 'Z')


def about(p, m):
    p = Vector(p)
    return Matrix.Translation(p) @ m @ Matrix.Translation(-p)


def seg_delta(a, b, a2, b2):
    """The rigid motion taking the segment a-b to a2-b2, turning it the shortest way (no twist)."""
    q = (Vector(b) - Vector(a)).rotation_difference(Vector(b2) - Vector(a2))
    return Matrix.Translation(a2) @ q.to_matrix().to_4x4() @ Matrix.Translation(-Vector(a))


SHORT = [0.0]   # the most any foot fell short of its target (a leg out of reach slides its foot)


def ik2(h, a, l1, l2, pole):
    """Knee of a two-bone chain from h reaching for a, bending toward pole; a is pulled in if out of reach."""
    d = a - h; L = d.length; u = d / L
    SHORT[0] = max(SHORT[0], L - (l1 + l2))
    L = min(max(L, abs(l1 - l2) + 1e-4), l1 + l2 - 1e-4)
    ca = (l1 * l1 + L * L - l2 * l2) / (2 * l1 * L); sa = math.sqrt(max(0.0, 1 - ca * ca))
    n = (pole - u * pole.dot(u)).normalized()
    return h + (u * ca + n * sa) * l1, h + u * L


def smooth(s):
    return s * s * (3 - 2 * s)


def step(u, duty, reach, lift, hard=False):
    """A foot along the walk (forward is -y) and its height at phase u: planted it goes back from -reach to +reach at a
    constant speed (still on the ground as the body moves on), lifted it swings forward and comes down. hard: up fast,
    held high, down fast (a stomp)."""
    if u < duty:
        return -reach + 2 * reach * u / duty, 0.0, 0.0
    s = (u - duty) / (1 - duty)
    h = math.sin(math.pi * s) ** 0.5 if hard else math.sin(math.pi * s ** 0.85)
    return reach - 2 * reach * smooth(s), lift * h, s


class Leg:
    """A leg as joints in rest: hip, knee, ankle (heel) and the foot's ground point; with a tarsus (digitigrade, imp and
    raptor) the foot point is the ball and the ankle-ball segment is a bone of its own, else the foot is rigid under the
    ankle (brute). Bones: Thigh_X, Shin_X, [Tarsus_X], Foot_X."""

    def __init__(self, side, hip, knee, ankle, foot, tarsus):
        self.x = 'L' if side < 0 else 'R'
        self.H, self.K, self.A, self.F = (Vector(p) for p in (hip, knee, ankle, foot))
        self.tarsus = tarsus
        self.l1, self.l2 = (self.K - self.H).length, (self.A - self.K).length
        u = (self.A - self.H).normalized()
        self.pole = ((self.K - self.H) - u * (self.K - self.H).dot(u)).normalized()

    def bones(self, parent):
        x = self.x
        out = [(f'Thigh_{x}', self.H, self.K, parent), (f'Shin_{x}', self.K, self.A, f'Thigh_{x}')]
        if self.tarsus:
            out += [(f'Tarsus_{x}', self.A, self.F, f'Shin_{x}'), (f'Foot_{x}', self.F, self.F + V(0, -0.08, 0), f'Tarsus_{x}')]
        else:
            out += [(f'Foot_{x}', self.F, self.F + V(0, -0.12, 0), f'Shin_{x}')]
        return out

    def pose(self, hips, dy, dz, tarsus_pitch=0.0, foot_pitch=0.0):
        """Bone motions (armature space) with the foot moved by dy, dz from rest, the hip carried by the hips' motion."""
        x = self.x
        H2 = hips @ self.H
        F2 = self.F + V(0, dy, dz)
        if self.tarsus:
            A2 = F2 + Rx(tarsus_pitch).to_3x3() @ (self.A - self.F)
        else:
            foot = Matrix.Translation(F2 - self.F) @ about(self.F, Rx(foot_pitch))
            A2 = foot @ self.A
        K2, A2c = ik2(H2, A2, self.l1, self.l2, hips.to_3x3() @ self.pole)
        out = {f'Thigh_{x}': seg_delta(self.H, self.K, H2, K2), f'Shin_{x}': seg_delta(self.K, self.A, K2, A2c)}
        if self.tarsus:
            F2c = A2c + Rx(tarsus_pitch).to_3x3() @ (self.F - self.A)
            out[f'Tarsus_{x}'] = seg_delta(self.A, self.F, A2c, F2c)
            out[f'Foot_{x}'] = Matrix.Translation(F2c - self.F) @ about(self.F, Rx(foot_pitch))
        else:
            out[f'Foot_{x}'] = Matrix.Translation(A2c - A2) @ foot
        return out


# ---------------------------------------------------------------- rigs and walks

def imp_rig():
    # the body's skin graph (models.imp), front -y
    Vs = [V(p) for p in [(0, 0.06, 0.36), (0, 0.02, 0.46), (0, -0.06, 0.56), (0, -0.15, 0.63),
                         (-0.15, -0.06, 0.60), (-0.25, -0.05, 0.43), (-0.27, -0.18, 0.28),
                         (0.15, -0.06, 0.60), (0.27, -0.09, 0.47), (0.24, -0.24, 0.50),
                         (-0.09, 0.06, 0.34), (-0.14, -0.09, 0.25), (-0.13, 0.07, 0.11), (-0.14, -0.03, 0.03),
                         (0.09, 0.06, 0.34), (0.14, -0.06, 0.24), (0.13, 0.10, 0.11), (0.14, 0.01, 0.03),
                         (0, 0.14, 0.36), (-0.04, 0.27, 0.27), (-0.13, 0.36, 0.22), (-0.22, 0.40, 0.27)]]
    legs = [Leg(-1, Vs[10], Vs[11], Vs[12], Vs[13], True), Leg(1, Vs[14], Vs[15], Vs[16], Vs[17], True)]
    palm = lambda el, wr: wr + (wr - el).normalized() * 0.08
    bones = [('Root', V(0, 0, 0), V(0, 0, 0.1), None), ('Hips', Vs[0], Vs[1], 'Root'), ('Spine', Vs[1], Vs[2], 'Hips'),
             ('Neck', Vs[2], Vs[3], 'Spine'), ('Head', Vs[3], V(0, -0.30, 0.74), 'Neck'),
             ('UpperArm_L', Vs[4], Vs[5], 'Spine'), ('LowerArm_L', Vs[5], Vs[6], 'UpperArm_L'), ('Hand_L', Vs[6], palm(Vs[5], Vs[6]), 'LowerArm_L'),
             ('UpperArm_R', Vs[7], Vs[8], 'Spine'), ('LowerArm_R', Vs[8], Vs[9], 'UpperArm_R'), ('Hand_R', Vs[9], palm(Vs[8], Vs[9]), 'LowerArm_R'),
             ('Tail1', Vs[18], Vs[19], 'Hips'), ('Tail2', Vs[19], Vs[20], 'Tail1'), ('Tail3', Vs[20], Vs[21], 'Tail2')]
    for leg in legs: bones += leg.bones('Hips')
    S = lambda a, b: (Vs[a], Vs[b])
    segs = [('Hips', *S(0, 1)), ('Hips', *S(0, 10)), ('Hips', *S(0, 14)), ('Hips', *S(0, 18)),
            ('Spine', *S(1, 2)), ('Spine', *S(2, 4)), ('Spine', *S(2, 7)), ('Neck', *S(2, 3)),
            ('UpperArm_L', *S(4, 5)), ('LowerArm_L', *S(5, 6)), ('UpperArm_R', *S(7, 8)), ('LowerArm_R', *S(8, 9)),
            ('Thigh_L', *S(10, 11)), ('Shin_L', *S(11, 12)), ('Tarsus_L', *S(12, 13)),
            ('Thigh_R', *S(14, 15)), ('Shin_R', *S(15, 16)), ('Tarsus_R', *S(16, 17)),
            ('Tail1', *S(18, 19)), ('Tail2', *S(19, 20)), ('Tail3', *S(20, 21))]
    smooth_parts = {'body': segs}
    exact = {'hand-1': 'Hand_L', 'hand1': 'Hand_R', 'wrap-1': 'LowerArm_L', 'wrap1': 'LowerArm_R', 'tailtip': 'Tail3',
             'cord': 'Hips', 'cloth': 'Hips', 'cloth2': 'Hips', 'strap': 'Spine',
             'spine0': 'Spine', 'spine1': 'Spine', 'spine2': 'Hips', 'spine3': 'Hips'}   # the back's spikes
    prefix = [('claw-1', 'Hand_L'), ('claw1', 'Hand_R'), ('toe-1', 'Foot_L'), ('tclaw-1', 'Foot_L'), ('toe1', 'Foot_R'),
              ('tclaw1', 'Foot_R'), ('paul', 'Spine'), ('priv', 'Spine'), ('shaft', 'Hand_R'), ('spearhead', 'Hand_R'),
              ('socket', 'Hand_R'), ('rag', 'Hand_R')] + [(n, 'Head') for n in ('skull', 'eye', 'brow', 'snout', 'mouth', 'jaw', 'fang', 'ufang', 'horn', 'ear')]

    # A hunched scurrying shuffle: quick short steps low to the ground, the body rocking over the planted foot, the long
    # left arm swinging, the spear arm steady, the tail lashing a beat behind.
    # strides sized so the game's ordinary speeds (about 2 world units a second) play it near its own pace
    T_, D, reach, lift = 0.40, 0.6, 0.15, 0.05

    def walk(t):
        w = 2 * math.pi * t / T_
        hips = Matrix.Translation((0.012 * math.sin(w), 0, -0.012 * math.cos(2 * w))) @ about(Vs[0], Rz(6 * math.sin(w)) @ Ry(-4 * math.sin(w)) @ Rx(2 * math.cos(2 * w)))
        out = {'Hips': hips}
        spine = hips @ about(Vs[1], Rz(-3 * math.sin(w)) @ Rx(-2 * math.cos(2 * w)))
        out['Spine'] = spine
        out['Neck'] = spine @ about(Vs[2], Rz(-2 * math.sin(w)))
        out['Head'] = out['Neck'] @ about(Vs[3], Rx(3 * math.cos(2 * w)) @ Rz(-2 * math.sin(w)))
        ua = spine @ about(Vs[4], Rx(22 * math.cos(w)) @ Ry(4 * math.sin(w)))
        out['UpperArm_L'] = ua; out['LowerArm_L'] = ua @ about(Vs[5], Rx(-12 + 12 * math.cos(w + 0.6)))
        out['Hand_L'] = out['LowerArm_L'] @ about(Vs[6], Rx(10 * math.cos(w + 1.2)))
        ur = spine @ about(Vs[7], Rx(-5 * math.cos(w)))
        out['UpperArm_R'] = ur; out['LowerArm_R'] = ur @ about(Vs[8], Rx(3 * math.cos(2 * w))); out['Hand_R'] = out['LowerArm_R']
        t1 = hips @ about(Vs[18], Rz(12 * math.sin(w - 0.6)) @ Rx(4 * math.cos(2 * w)))
        t2 = t1 @ about(Vs[19], Rz(14 * math.sin(w - 1.2))); t3 = t2 @ about(Vs[20], Rz(18 * math.sin(w - 1.8)))
        out.update(Tail1=t1, Tail2=t2, Tail3=t3)
        for leg, off in zip(legs, (0.0, 0.5)):
            dy, dz, s = step((t / T_ + off) % 1, D, reach, lift)
            out.update(leg.pose(hips, dy, dz, tarsus_pitch=18 * math.sin(math.pi * s), foot_pitch=20 * math.sin(math.pi * s)))
        return out
    return dict(bones=bones, smooth=smooth_parts, exact=exact, prefix=prefix, walk=walk, cycle=T_, speed=2 * reach / (D * T_))


def raptor_rig():
    legs = [Leg(s, *[(s * x, y, z) for x, y, z in RAPTOR_LEG], True) for s in (-1, 1)]
    hip, chest, neck = V(0, 0.06, 0.84), V(0, -0.34, 0.86), V(0, -0.74, 0.98)
    t1, t2, t3, tip = V(0, 0.28, 0.85), V(0, 0.66, 0.88), V(0, 1.05, 0.92), V(0, 1.42, 0.96)
    jaw = V(0, -0.80, 0.95)
    bones = [('Root', V(0, 0, 0), V(0, 0, 0.1), None), ('Body', hip, chest, 'Root'), ('Chest', chest, neck, 'Body'),
             ('Head', neck, V(0, -1.20, 0.97), 'Chest'), ('Jaw', jaw, V(0, -1.20, 0.86), 'Head'),
             ('Tail1', t1, t2, 'Body'), ('Tail2', t2, t3, 'Tail1'), ('Tail3', t3, tip, 'Tail2')]
    for s, x in ((-1, 'L'), (1, 'R')):
        bones.append((f'Arm_{x}', V(s * 0.13, -0.40, 0.80), V(s * 0.15, -0.48, 0.58), 'Chest'))
    for leg in legs: bones += leg.bones('Body')
    chain = [('Chest', neck, chest), ('Body', chest, t1), ('Tail1', t1, t2), ('Tail2', t2, t3), ('Tail3', t3, tip)]
    # the body, its spine ridge and armour plates bend together along the chain
    smooth_parts = {'body': chain, 'spine': chain, 'plate': chain, 'belly': chain}
    exact = {'tailblade': 'Tail3', 'tailfin': 'Tail3', 'jaw': 'Jaw'}
    for s, x in ((-1, 'L'), (1, 'R')):
        exact.update({f'leg{s}0': f'Thigh_{x}', f'leg{s}k1': f'Thigh_{x}', f'thigh{s}': f'Thigh_{x}', f'leg{s}1': f'Shin_{x}',
                      f'leg{s}k2': f'Shin_{x}', f'shin{s}': f'Shin_{x}', f'leg{s}2': f'Tarsus_{x}', f'sickle{s}': f'Foot_{x}'})
    prefix = [('foot-1', 'Foot_L'), ('foot1', 'Foot_R'), ('arm-1', 'Arm_L'), ('aclaw-1', 'Arm_L'), ('arm1', 'Arm_R'),
              ('aclaw1', 'Arm_R')] + [(n, 'Head') for n in ('skull', 'helm', 'eye', 'crest', 'mouth', 'tooth')]

    # A sprint with the body level: long strides with a flight phase, each leg folding up under the body as it swings
    # forward, the body bobbing up between footfalls, the tail swinging against the strides, the head held steady.
    T_, D, reach, lift = 0.55, 0.36, 0.15, 0.15     # about 2.4 world units a second at its own pace

    def walk(t):
        w = 2 * math.pi * t / T_
        body = Matrix.Translation((0, 0, 0.03 * math.cos(2 * w))) @ about(hip, Rx(2.5 * math.sin(2 * w)) @ Ry(3 * math.sin(w)) @ Rz(2 * math.sin(w)))
        out = {'Body': body}
        out['Chest'] = body @ about(chest, Rx(-1.5 * math.sin(2 * w)) @ Rz(-1.5 * math.sin(w)))
        out['Head'] = out['Chest'] @ about(neck, Rx(-1.5 * math.sin(2 * w)) @ Rz(-1 * math.sin(w)) @ Ry(-3 * math.sin(w)))
        out['Jaw'] = out['Head'] @ about(jaw, Rx(5 + 5 * math.sin(2 * w + 1)))
        a = body @ about(t1, Rx(-3 * math.sin(2 * w + 0.6)) @ Rz(-5 * math.sin(w - 0.5)))
        b = a @ about(t2, Rx(-3 * math.sin(2 * w + 1.2)) @ Rz(-7 * math.sin(w - 1.0)))
        out.update(Tail1=a, Tail2=b, Tail3=b @ about(t3, Rz(-9 * math.sin(w - 1.5))))
        for s, x in ((-1, 'L'), (1, 'R')):
            out[f'Arm_{x}'] = out['Chest'] @ about(V(s * 0.13, -0.40, 0.80), Rx(-10 * s * math.sin(w)))
        for leg, off in zip(legs, (0.0, 0.5)):
            u = (t / T_ + off) % 1
            dy, dz, s = step(u, D, reach, lift)
            push = smooth(max(0.0, (u - 0.6 * D) / (0.4 * D))) if u < D else 0.0     # the heel lifting as it pushes off
            out.update(leg.pose(body, dy, dz, tarsus_pitch=20 * push + 45 * math.sin(math.pi * s), foot_pitch=35 * math.sin(math.pi * s)))
        return out
    return dict(bones=bones, smooth=smooth_parts, exact=exact, prefix=prefix, walk=walk, cycle=T_, speed=2 * reach / (D * T_))


def brute_rig():
    import neck as NK
    legs = [Leg(s, *[(s * x, y, z) for x, y, z in BRUTE_LEG], False) for s in (-1, 1)]
    # the head turns on the top of its raised neck (neck.t1neck)
    hip, neck = V(0, 0.12, 0.68), V(0, -0.36 + NK.LEAN, 1.32 + NK.HEAD)
    bones = [('Root', V(0, 0, 0), V(0, 0, 0.1), None), ('Body', hip, V(0, 0.0, 1.06), 'Root'),
             ('Head', neck, neck + V(0, -0.25, 0.10), 'Body')]
    for leg in legs: bones += leg.bones('Body')
    exact = {'face': 'Head', 'helm': 'Head', 'visor': 'Head', 'jaw': 'Head'}
    exact.update({n: 'Body' for n in ('hips', 'waist', 'chest', 'backplate', 'spine', 'neck', 'collar', 'uarmL', 'elbowL', 'farmL', 'handL')})
    for s, x in ((-1, 'L'), (1, 'R')):
        exact.update({f'thigh{s}': f'Thigh_{x}', f'greave{s}': f'Shin_{x}', f'knee{s}': f'Shin_{x}', f'kspike{s}': f'Shin_{x}',
                      f'sabaton{s}': f'Foot_{x}', f'toe{s}': f'Foot_{x}'})
    prefix = [(n, 'Head') for n in ('eye', 'brow', 'horn', 'tusk')] + [(n, 'Body') for n in ('tasset', 'pd', 'claw')]

    # A heavy stomp behind the shield: each foot lifted high and brought down hard, the whole body (and the shield in its
    # grip) dropping onto it and rolling over it, the head nodding with the jolt.
    T_, D, reach, lift = 0.75, 0.58, 0.17, 0.14     # about 1.25 world units a second at its own pace

    def walk(t):
        w = 2 * math.pi * t / T_
        k = ((t / T_) % 0.5) / 0.5
        k = k if k < 0.5 else k - 1                       # from the nearest footfall, in half cycles
        drop = -0.045 * math.exp(-((k - 0.06) / 0.11) ** 2) + 0.01 * math.cos(2 * w)
        body = Matrix.Translation((0.02 * math.sin(w), 0, drop)) @ about(hip, Ry(-3.5 * math.sin(w)) @ Rz(3 * math.sin(w)) @ Rx(1.2 * math.cos(2 * w)))
        out = {'Body': body, 'Head': body @ about(neck, Rx(-4 * math.exp(-((k - 0.12) / 0.12) ** 2)) @ Rz(-2 * math.sin(w)))}
        for leg, off in zip(legs, (0.0, 0.5)):
            dy, dz, s = step((t / T_ + off) % 1, D, reach, lift, hard=True)
            out.update(leg.pose(body, dy, dz, foot_pitch=-12 * math.sin(math.pi * s)))
        return out
    return dict(bones=bones, smooth={}, exact=exact, prefix=prefix, walk=walk, cycle=T_, speed=2 * reach / (D * T_),
                shield='Body')


# ---------------------------------------------------------------- assembly and export

def nearest_bone(o, bones):
    pts = [o.matrix_world @ v.co for v in o.data.vertices]
    c = sum(pts, Vector()) / len(pts)
    return min((b for b in bones if b[0] != 'Root'), key=lambda b: seg_dist(c, Vector(b[1]), Vector(b[2])))[0]


def assemble(parts, rig, material):
    names = {}
    for o in parts:
        base = o.name
        segs = next((s for k, s in rig['smooth'].items() if base == k or (k == 'plate' and base.startswith(k))), None)
        if segs:
            smooth_weights(o, segs); names[base] = 'smooth'; continue
        if o.parent and o.parent.name == 'shield':
            bone = rig['shield']
        elif base in rig['exact']:
            bone = rig['exact'][base]
        else:
            bone = next((b for p, b in rig['prefix'] if base.startswith(p)), None) or nearest_bone(o, rig['bones'])
        rigid(o, bone); names[base] = bone
    print('[parts]', ' '.join(f'{k}:{v}' for k, v in sorted(names.items())))
    paint(parts, material)

    arm = bpy.data.armatures.new(NAME)
    ob = bpy.data.objects.new(NAME, arm); bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    for o in bpy.context.scene.objects: o.select_set(o is ob)
    bpy.ops.object.mode_set(mode='EDIT')
    for n, head, tail, parent in rig['bones']:
        eb = arm.edit_bones.new(n)
        eb.head, eb.tail = Vector(head), Vector(tail) if (Vector(tail) - Vector(head)).length > 1e-3 else Vector(head) + V(0, 0, 0.05)
        eb.parent = arm.edit_bones[parent] if parent else None
    bpy.ops.object.mode_set(mode='OBJECT')

    for o in bpy.context.scene.objects: o.select_set(o in parts)
    mesh = parts[0]
    bpy.context.view_layer.objects.active = mesh
    bpy.ops.object.join()
    mesh.name = mesh.data.name = NAME + '_Mesh'
    mw = mesh.matrix_world.copy(); mesh.parent = ob; mesh.matrix_world = mw
    mesh.modifiers.new('Armature', 'ARMATURE').object = ob
    for e in [o for o in bpy.data.objects if o.type == 'EMPTY']: bpy.data.objects.remove(e)
    bpy.context.view_layer.update()
    return ob, mesh


def animate(ob, rig):
    frames = int(round(rig['cycle'] * FPS))
    sc = bpy.context.scene
    sc.render.fps = FPS; sc.frame_start, sc.frame_end = 0, frames
    parents = {n: p for n, _, _, p in rig['bones']}
    rest = {b.name: b.matrix_local.copy() for b in ob.data.bones}
    ob.animation_data_create(); ob.animation_data.action = bpy.data.actions.new('Walk')
    for pb in ob.pose.bones: pb.rotation_mode = 'QUATERNION'
    I = Matrix.Identity(4)
    for f in range(frames + 1):
        D = rig['walk']((f % frames) / frames * rig['cycle'])
        for pb in ob.pose.bones:
            n = pb.name; p = parents[n]
            local = (D.get(p, I).inverted() if p else I) @ D.get(n, D.get(p, I) if p else I)
            pb.matrix_basis = rest[n].inverted() @ local @ rest[n]
            for path in ('location', 'rotation_quaternion', 'scale'): pb.keyframe_insert(path, frame=f)
    return frames


def game_material():
    """The preview's material: the palette (the drafts' colours) on a texture node, nearest-neighbour."""
    img = bpy.data.images.new(NAME + '_Palette', COLS * 8, COLS * 8)
    px = [0.0] * (COLS * 8 * COLS * 8 * 4)
    found = {}
    for key, _ in T._mats.items():
        if key[0] in ('toon', 'flat') and isinstance(key[1], tuple):
            try: found[region_of(key[1])] = key[1]
            except ValueError: pass
    for i, r in enumerate(REGIONS):
        rgb = found.get(r, (255, 0, 255))
        for y in range(8):
            for x in range(8):
                X, Y = (i % COLS) * 8 + x, (COLS * 8 - 1) - ((i // COLS) * 8 + y)
                k = (Y * COLS * 8 + X) * 4
                px[k:k + 4] = [c / 255 for c in rgb] + [1.0]
    img.pixels = px
    m = bpy.data.materials.new(NAME)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    tex = m.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = img; tex.interpolation = 'Closest'
    m.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    m.node_tree.nodes.active = tex
    return m


def preview(ob, mesh, frames, span, height):
    """The walk seen from the battle camera's angle (47.5 deg down), walking across (left to right), toward the camera
    and away: one strip per view, eight frames a cycle."""
    import numpy as np
    os.makedirs(PREVIEW, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'STUDIO'; sh.color_type = 'TEXTURE'; sh.show_object_outline = True; sh.object_outline_color = (0.09, 0.12, 0.16)
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new('w')
    sc.render.resolution_x = sc.render.resolution_y = 300
    floor = bpy.data.objects.new('floor', bpy.data.meshes.new('floor'))
    bm = bmesh.new(); bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=6); bm.to_mesh(floor.data); bm.free()
    sc.collection.objects.link(floor)
    cam = sc.camera
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = span * 1.2
    rot = Matrix.Rotation(math.radians(90 - 47.5), 4, 'X')
    for name, yaw in (('across', 90), ('toward', 0), ('away', 180)):
        ob.rotation_euler = (0, 0, math.radians(yaw))      # the model's front (-y) turned: 90 = walking +x (screen right)
        tiles = []
        for i in range(8):
            sc.frame_set(int(round(i * frames / 8)))
            cam.matrix_world = Matrix.Translation((0, 0, height * 0.45)) @ rot @ Matrix.Translation((0, 0, 40))
            path = os.path.join(PREVIEW, f'_{KIND}_{name}_{i}.png')
            sc.render.filepath = path; bpy.ops.render.render(write_still=True)
            im = bpy.data.images.load(path)
            tiles.append(np.array(im.pixels[:]).reshape(300, 300, 4)); bpy.data.images.remove(im); os.remove(path)
        strip = np.concatenate(tiles, axis=1)
        out = bpy.data.images.new(f'{KIND}_{name}', strip.shape[1], strip.shape[0])
        out.pixels = strip.ravel().tolist(); out.filepath_raw = os.path.join(PREVIEW, f'{KIND}_{name}.png'); out.file_format = 'PNG'; out.save()
    ob.rotation_euler = (0, 0, 0)


def main():
    {'imp': build_imp, 'raptor': build_raptor, 'brute': build_brute}[KIND]()
    remove_render_only()
    bpy.context.view_layer.update()
    rig = {'imp': imp_rig, 'raptor': raptor_rig, 'brute': brute_rig}[KIND]()
    parts = bake_parts()
    tris = sum(len(p.vertices) - 2 for o in parts for p in o.data.polygons)
    height = max((o.matrix_world @ v.co).z for o in parts for v in o.data.vertices)
    material = game_material()
    ob, mesh = assemble(parts, rig, material)
    frames = animate(ob, rig)
    info = dict(height=round(height, 4), speed=round(rig['speed'], 4), cycle=round(frames / FPS, 4))
    print(f'[game] {NAME}: {tris} triangles, {len(ob.data.bones)} bones, {info}, feet short of reach by at most {SHORT[0]:.3f}')
    os.makedirs(OUT, exist_ok=True)
    for o in bpy.context.scene.objects: o.select_set(o is ob or o is mesh)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, NAME + '.fbx'), use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=False, add_leaf_bones=False,
                             primary_bone_axis='Y', secondary_bone_axis='X', armature_nodetype='NULL', use_armature_deform_only=False,
                             bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False, bake_anim_use_all_actions=False,
                             bake_anim_force_startend_keying=True, bake_anim_step=1.0, bake_anim_simplify_factor=0.0, path_mode='STRIP')
    with open(os.path.join(OUT, NAME + '.json'), 'w', encoding='utf-8') as f:
        json.dump(info, f, indent=1)
    if PREVIEW:
        ys = [(o.matrix_world @ v.co).y for o in [mesh] for v in o.data.vertices]
        preview(ob, mesh, frames, max(height, max(ys) - min(ys)), height)


main()
