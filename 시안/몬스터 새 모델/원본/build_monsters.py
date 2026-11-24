"""Builds the eight new monster model options (시안1 버그 군단, 시안2 글리치 오류 코드) and renders them.

    blender -b --factory-startup -P build_monsters.py -- [--only b_normal,g_boss] [--work DIR] [--samples N]
        [--no-render | --preview | --battle-only] [--parts]

Every model is made from code (bmesh primitives, tubes along splines, text glyphs), saved as <name>.blend and exported
as <name>.fbx next to this script (Forward -Z, Up Y, transforms applied: the front, Blender -Y, becomes Unity +Z).
Each FBX has one root empty (Monster_*) at the feet centre with Body plus separate Leg_*/Arm_*/Wing_*/Flame_*/Loop/Fire
objects pivoting at their joints, ready for animation. Raw renders go to DIR (default %TEMP%/monster_mockups): a
3/4-front hero shot and a battle-camera shot (orthographic, 47.5 deg down like the game camera), each with an
emission-only twin that compose.py blurs into the glow. --preview renders two quick 640 px looks instead, --battle-only
skips the hero shots, --parts prints triangle counts per part.
"""
import bpy, bmesh, math, os, sys, json, tempfile
from mathutils import Vector, Matrix, Euler

HERE = os.path.dirname(os.path.abspath(__file__))
ARGS = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []


def arg(name, default=None):
    return ARGS[ARGS.index(name) + 1] if name in ARGS else default


WORK = arg('--work', os.path.join(tempfile.gettempdir(), 'monster_mockups'))
SAMPLES = int(arg('--samples', '96'))
BATTLE_PPU = 168          # px per unit of the battle render; compose.py shrinks it 4x to the 1080p screenshot's scale
BATTLE_PITCH = 47.5       # the game's battle camera looks 47.5 deg down (TankerDustEffects.cs)
BATTLE_YAW = {'b_fast': 75, 'g_fast': 75}  # deg; walkers come toward the camera and right, flyers side-on so the flames show
HERO_AZ = {'b_fast': 40, 'g_fast': 40}      # deg from the front; 30 unless listed
HOVER = {'b_fast': 0.4, 'g_fast': 0.4}       # units the flyers hover above the road in the battle shot (the game lifts them)
FONT_PATH = r"C:\Windows\Fonts\consolab.ttf"


# ---------------------------------------------------------------- helpers

def lin(h):
    h = h.lstrip('#')
    c = [int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)]
    return tuple(x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4 for x in c) + (1.0,)


def P(name, hexc, rough=0.5, metal=0.0, emit=0.0, ehex=None, alpha=1.0, coat=0.0):
    """A flat Principled BSDF colour, optionally emissive (eyes, glowing code)."""
    m = bpy.data.materials.new(name)
    try:
        m.use_nodes = True
    except Exception:
        pass
    nt = m.node_tree
    b = next((n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED'), None)
    if b is None:
        nt.nodes.clear()
        b = nt.nodes.new('ShaderNodeBsdfPrincipled')
        nt.links.new(b.outputs[0], nt.nodes.new('ShaderNodeOutputMaterial').inputs[0])
    # glowing parts get a dark base so the light doesn't wash their colour out to pastel
    b.inputs['Base Color'].default_value = tuple(x * 0.15 for x in lin(hexc)[:3]) + (1,) if emit >= 1 else lin(hexc)
    b.inputs['Roughness'].default_value = rough
    b.inputs['Metallic'].default_value = metal
    if coat:
        b.inputs['Coat Weight'].default_value = coat
    if emit:
        b.inputs['Emission Color'].default_value = lin(ehex or hexc)
        b.inputs['Emission Strength'].default_value = emit
    if alpha < 1:
        b.inputs['Alpha'].default_value = alpha
        try:
            m.surface_render_method = 'BLENDED'
        except Exception:
            pass
    m.diffuse_color = lin(hexc)
    return m


def Mx(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    return (Matrix.Translation(Vector(loc)) @ Euler([math.radians(a) for a in rot], 'XYZ').to_matrix().to_4x4()
            @ Matrix.Diagonal((*scale, 1)))


def link(ob):
    bpy.context.scene.collection.objects.link(ob)
    return ob


def mesh_obj(name, bm, m, smooth=180):
    """smooth = angle (deg) above which edges stay sharp; 0 = flat."""
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(m)
    ob = link(bpy.data.objects.new(name, me))
    if smooth:
        me.shade_smooth()
        if smooth < 180:
            me.set_sharp_from_angle(angle=math.radians(smooth))
    else:
        me.shade_flat()
    return ob


def bake(ob):
    """Apply the object's modifier stack into its mesh."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), preserve_all_data_layers=True, depsgraph=dg)
    old = ob.data
    ob.modifiers.clear()
    ob.data = me
    bpy.data.meshes.remove(old)
    return ob


def bevel(ob, width, seg=3, angle=None):
    md = ob.modifiers.new('bevel', 'BEVEL')
    md.width, md.segments = width, seg
    md.limit_method = 'ANGLE' if angle else 'NONE'
    if angle:
        md.angle_limit = math.radians(angle)
    md.harden_normals = True
    ob.data.shade_smooth()
    return bake(ob)


def box(name, size, loc=(0, 0, 0), rot=(0, 0, 0), m=None, bev=0.04, seg=3):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=Vector(size), verts=bm.verts)
    ob = mesh_obj(name, bm, m, smooth=0)
    if bev:
        bevel(ob, min(bev, min(size) * 0.45), seg)
    ob.data.transform(Mx(loc, rot))
    return ob


def ell(name, r, loc, m, seg=16, rings=10, rot=(0, 0, 0)):
    """Ellipsoid with radii r."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    bmesh.ops.transform(bm, matrix=Mx(loc, rot, r), verts=bm.verts)
    return mesh_obj(name, bm, m)


def shell(name, r, loc, m, cuts, thick=0.05, seg=20, rings=12, rot=(0, 0, 0), sharp=50, taper=None):
    """Part of an ellipsoid surface (cut by planes in its local frame, kept on the planes' normal side), thickened."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=1.0)
    bmesh.ops.scale(bm, vec=Vector(r), verts=bm.verts)
    if taper:
        taper(bm)
    for co, no in cuts:
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=co, plane_no=no,
                               clear_inner=True)
    bmesh.ops.transform(bm, matrix=Mx(loc, rot), verts=bm.verts)
    ob = mesh_obj(name, bm, m)
    if thick:
        md = ob.modifiers.new('solid', 'SOLIDIFY')
        md.thickness, md.offset, md.use_even_offset = thick, -1, True
        bake(ob)
        ob.data.shade_smooth()
        ob.data.set_sharp_from_angle(angle=math.radians(sharp))
    return ob


def cone_between(name, p0, p1, r0, r1, m, seg=10, bev=0.0, smooth=40):
    p0, p1 = Vector(p0), Vector(p1)
    d = p1 - p0
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r0, radius2=r1, depth=d.length)
    bmesh.ops.transform(bm, matrix=Matrix.Translation((p0 + p1) / 2) @ Vector((0, 0, 1)).rotation_difference(
        d.normalized()).to_matrix().to_4x4(), verts=bm.verts)
    ob = mesh_obj(name, bm, m, smooth=smooth)
    if bev:
        bevel(ob, bev, 2, angle=40)
    return ob


def spline(ctrl, n=5, closed=False):
    """Catmull-Rom through the control points."""
    Pp = [Vector(c) for c in ctrl]
    N = len(Pp)
    out = []
    for i in range(N if closed else N - 1):
        p0 = Pp[(i - 1) % N] if closed else Pp[max(i - 1, 0)]
        p1, p2 = Pp[i % N], Pp[(i + 1) % N]
        p3 = Pp[(i + 2) % N] if closed else Pp[min(i + 2, N - 1)]
        for k in range(n):
            t = k / n
            out.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t
                              + (3 * p1 - p0 - 3 * p2 + p3) * t * t * t))
    if not closed:
        out.append(Pp[-1])
    return out


def tube(name, pts, radii, m, seg=8, closed=False, up=(0, 0, 1), cap=True):
    """A tube swept along a polyline; radii is a number, an (r_start, r_end) pair or one radius per point."""
    Pp = [Vector(p) for p in pts]
    n = len(Pp)
    if isinstance(radii, (int, float)):
        R = [radii] * n
    elif len(radii) == 2 and n != 2:
        R = [radii[0] + (radii[1] - radii[0]) * i / (n - 1) for i in range(n)]
    else:
        R = list(radii)
    U = Vector(up).normalized()
    bm = bmesh.new()
    rings, Nprev = [], None
    for i in range(n):
        if closed:
            T = (Pp[(i + 1) % n] - Pp[i - 1]).normalized()
        else:
            T = (Pp[min(i + 1, n - 1)] - Pp[max(i - 1, 0)]).normalized()
        Nv = U - T * U.dot(T)
        Nv = Nv.normalized() if Nv.length > 1e-4 else Nprev
        Nprev = Nv
        B = T.cross(Nv)
        rings.append([bm.verts.new(Pp[i] + (Nv * math.cos(a) + B * math.sin(a)) * R[i])
                      for a in (2 * math.pi * k / seg for k in range(seg))])
    for i in range(n if closed else n - 1):
        r0, r1 = rings[i], rings[(i + 1) % n]
        for k in range(seg):
            bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k]))
    if cap and not closed:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return mesh_obj(name, bm, m)


def flame_lick(name, base, height, r, m, sway=0.05, phase=0.0, lean=(0, 0, 0)):
    """A teardrop flame tongue: fat at the bottom, swaying to a point."""
    base, lean = Vector(base), Vector(lean)
    pts = [base + Vector((sway * math.sin(phase + 2.4 * t), 0, height * t)) + lean * t * t for t in
           (0, 0.22, 0.45, 0.68, 0.86, 1.0)]
    return tube(name, pts, [r * 0.8, r, r * 0.85, r * 0.55, r * 0.25, r * 0.03], m, seg=7, up=(0, 1, 0))


def thruster(S, groups, pivots, side, s, noz0, d, length=0.22, r=(0.075, 0.095), flame=0.44, seg=10,
             metal='metal', ring=None):
    """A jet nozzle low on one side, with its flame as a separate object (Flame_L/R) pivoting at the exit."""
    noz0, d = Vector(noz0), Vector(d).normalized()
    noz1 = noz0 + d * length
    parts = [cone_between('nozzle', noz0, noz1, r[0], r[1], S[metal], seg=seg, bev=0.012),
             cone_between('nozglow', noz1 - d * 0.01, noz1 + d * 0.012, r[1] * 0.82, r[1] * 0.82, S['nozzle'], seg=seg)]
    if ring:
        parts.append(cone_between('nozring', noz1 - d * 0.045, noz1 - d * 0.005, r[1] * 1.12, r[1] * 1.12, S[ring],
                                  seg=seg))
    nm = f'Flame_{side}'
    prof = (0.75, 1.05, 0.95, 0.7, 0.42, 0.18, 0.02)
    jet = lambda k, rr: tube(nm + k, [noz1 + d * flame * rr * i / 6 for i in range(7)],
                             [r[1] * p * (0.6 if k else 1) for p in prof], S['core_flame' if k else 'flame'], seg=8,
                             up=(0, 0, 1))
    groups[nm] = [jet('', 1.0), jet('c', 0.55)]
    pivots[nm] = noz1
    return parts


def flat_poly(name, pts2d, thick, m, matrix, bev=0.0):
    """A 2D outline (x, y) extruded by thick along z, then placed by matrix."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, -thick / 2)) for x, y in pts2d]
    f = bm.faces.new(vs)
    bmesh.ops.recalc_face_normals(bm, faces=[f])
    if f.normal.z > 0:
        bmesh.ops.reverse_faces(bm, faces=[f])
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0, thick), verts=[e for e in ext['geom'] if isinstance(e, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    ob = mesh_obj(name, bm, m, smooth=0)
    if bev:
        bevel(ob, bev, 2)
    ob.data.transform(matrix)
    return ob


FONT = None


def glyph(name, txt, size, m, matrix, depth=0.02, wrap=None, offset=0.004):
    """Text as a mesh: built in the XY plane facing +Z, placed by matrix. With wrap, it is laid onto that object's
    surface (shrinkwrap) and thickened outward."""
    cu = bpy.data.curves.new(name, 'FONT')
    cu.body, cu.font, cu.size = txt, FONT, size
    cu.align_x, cu.align_y, cu.resolution_u = 'CENTER', 'CENTER', 2
    cu.extrude = 0 if wrap else depth
    tmp = link(bpy.data.objects.new(name, cu))
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(tmp.evaluated_get(dg))
    bpy.data.objects.remove(tmp)
    bpy.data.curves.remove(cu)
    me.materials.clear()
    me.materials.append(m)
    ob = link(bpy.data.objects.new(name, me))
    if wrap:
        bm = bmesh.new()
        bm.from_mesh(me)
        bm.normal_update()
        for f in bm.faces:
            if f.normal.z < 0:
                f.normal_flip()
        bm.to_mesh(me)
        bm.free()
    me.transform(matrix)
    if wrap:
        sw = ob.modifiers.new('wrap', 'SHRINKWRAP')
        sw.target, sw.wrap_method, sw.offset = wrap, 'NEAREST_SURFACEPOINT', offset
        so = ob.modifiers.new('solid', 'SOLIDIFY')
        so.thickness, so.offset = depth, 1
        bake(ob)
    me = ob.data
    me.shade_flat()
    return ob


def surf(c, r, d, upref=(0, 1, 0)):
    """Point on the ellipsoid (centre c, radii r) in direction d, and a frame (x right, y up, z outward normal)."""
    c, r, d = Vector(c), Vector(r), Vector(d).normalized()
    p = c + Vector((r.x * d.x, r.y * d.y, r.z * d.z))
    n = Vector((d.x / r.x, d.y / r.y, d.z / r.z)).normalized()
    up = Vector(upref) - n * Vector(upref).dot(n)
    up.normalize()
    right = up.cross(n)
    return p, n, Matrix((right, up, n)).transposed()


def front(loc, rot_y=0.0, spin=0.0):
    """Matrix placing an XY-plane item so it faces -Y (the model's front), tilted by rot_y about the depth axis."""
    return Mx(loc, (90, 0, 0)) @ Mx((0, 0, 0), (0, 0, -rot_y + spin))


def join(objs, name):
    bm = bmesh.new()
    mats = []
    for ob in objs:
        start = len(bm.faces)
        me = ob.data.copy()
        me.transform(ob.matrix_world)
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        remap = []
        for mm in ob.data.materials:
            if mm not in mats:
                mats.append(mm)
            remap.append(mats.index(mm))
        for f in bm.faces[start:]:
            f.material_index = remap[f.material_index] if remap else 0
        bpy.data.meshes.remove(me)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for mm in mats:
        me.materials.append(mm)
    for ob in objs:
        old = ob.data
        bpy.data.objects.remove(ob)
        if old.users == 0:
            bpy.data.meshes.remove(old)
    me.name = name
    return link(bpy.data.objects.new(name, me))


def set_origin(ob, p):
    p = Vector(p)
    ob.data.transform(Matrix.Translation(-p))
    ob.location = p


def mirror_pair(fn):
    """fn(s, side) for s = +1 (model's left, +X) and -1 (right)."""
    return [fn(1, 'L'), fn(-1, 'R')]


# ---------------------------------------------------------------- 시안1 버그 군단

def bug_mats():
    return dict(
        shell=P('Shell', '#4a2068', 0.32, coat=0.7),
        shell2=P('ShellDark', '#2c1a3c', 0.4, coat=0.4),
        belly=P('Belly', '#1c1524', 0.6),
        leg=P('Leg', '#2a2034', 0.45),
        claw=P('Claw', '#e0354a', 0.4),
        eye=P('Eye', '#ff1428', emit=2.6),
        shine=P('EyeShine', '#ffffff', emit=3),
        code=P('CodeGlow', '#ff40c8', emit=1.8),
        core=P('CoreGlow', '#e02ab8', emit=1.3),
        wing=P('Wing', '#b78cff', 0.15, emit=0.8, alpha=0.42),
        vein=P('WingVein', '#d483ff', emit=2),
        metal=P('Metal', '#555a6c', 0.35, metal=0.7),
        nozzle=P('NozzleGlow', '#ff7a2a', emit=2.5),
        flame=P('Flame', '#ff5a14', emit=2.6, alpha=0.85),
        core_flame=P('FlameCore', '#ffd66a', emit=2.8),
        gold=P('Gold', '#f0b43c', 0.3, metal=0.85),
        gem=P('Gem', '#ff3fd2', emit=2),
        armor=P('Armor', '#4c4a68', 0.3, metal=0.35, coat=0.5),
    )


def bug_leg(name, s, hip, knee, foot, r, S, knee_r=None, foot_r=None, seg=6, foot_mat='claw'):
    hip, knee, foot = Vector(hip), Vector(knee), Vector(foot)
    parts = [tube(name, spline([hip, knee, foot], 3), r, S['leg'], seg=seg, up=(0, 1, 0)),
             ell(name + 'k', [knee_r or r[0] * 1.15] * 3, knee, S['leg'], 8, 5)]
    fr = foot_r or r[1] * 1.4
    parts.append(ell(name + 'f', (fr, fr * 1.25, fr * 0.8), foot + Vector((0, 0, fr * 0.7)), S[foot_mat], 8, 5))
    return parts


def wing(name, s, root, length, width, sweep, dihedral, S, veins=True):
    """A teardrop wing membrane with a glowing rim, root at root, spanning outward and back."""
    pts = []
    k = 12
    for i in range(k + 1):
        u = i / k
        pts.append((u * length, width * math.sin(math.pi * u) ** 0.7 * (0.6 + 0.4 * u)))
    for i in range(k - 1, 0, -1):
        u = i / k
        pts.append((u * length, -width * 0.55 * math.sin(math.pi * u) ** 0.8))
    a, dh = math.radians(sweep), math.radians(dihedral)
    udir = Vector((s * math.cos(a) * math.cos(dh), math.sin(a) * math.cos(dh), math.sin(dh))).normalized()
    vdir = Vector((0, 0, 1)).cross(udir).normalized() * s
    vdir = -vdir  # wide side forward (leading edge)
    ndir = udir.cross(vdir)
    mat = Matrix.Translation(Vector(root)) @ Matrix((udir, vdir, ndir)).transposed().to_4x4()
    parts = [flat_poly(name, pts, 0.012, S['wing'], mat)]
    if veins:
        rim = [mat @ Vector((x, y, 0)) for x, y in pts]
        parts.append(tube(name + 'v', rim, 0.011, S['vein'], seg=4, closed=True, up=tuple(ndir)))
        mid = [mat @ Vector((u * length, width * 0.15 * math.sin(math.pi * u), 0)) for u in (0.05, 0.35, 0.65, 0.9)]
        parts.append(tube(name + 'm', mid, 0.009, S['vein'], seg=4, up=tuple(ndir)))
    return parts


def bug_eyes(S, c, r, spread, z, ey, size, slant=14, brow=True):
    out = []
    for s in (1, -1):
        p = Vector((s * spread, ey, z))
        out.append(ell('eye', size, p, S['eye'], 12, 8, rot=(0, -s * slant, 0)))
        out.append(ell('shine', [size[0] * 0.28] * 3, p + Vector((-size[0] * 0.35, -size[1] * 0.85, size[2] * 0.4)),
                       S['shine'], 8, 6))
        if brow:
            out.append(box('brow', (size[0] * 2.1, size[1] * 0.9, size[2] * 0.42),
                           p + Vector((s * size[0] * 0.15, -size[1] * 0.1, size[2] * 1.15)), (0, -s * 22, 0),
                           S['shell2'], bev=size[2] * 0.15, seg=2))
    return out


def bug_normal():
    S = bug_mats()
    body = []
    c, r = Vector((0, 0.08, 0.5)), Vector((0.40, 0.47, 0.38))
    halves = {}
    for s in (1, -1):
        h = shell('shell', r, c + Vector((s * 0.03, 0, 0)), S['shell'],
                  [((0, 0, 0), (s, 0, 0)), ((0, 0, -0.17), (0, 0, 1))], thick=0.05, seg=18, rings=10)
        halves[s] = h
        body.append(h)
    for s, t in ((-1, '{'), (1, '}')):
        p, n, B = surf(c, r, (s * 0.5, 0.05, 0.86))
        body.append(glyph('brace', t, 0.5, S['code'], Matrix.Translation(p + n * 0.02) @ B.to_4x4(), depth=0.035,
                          wrap=halves[s]))
    seam = [c + Vector((0, r.y * math.sin(math.radians(a)), r.z * math.cos(math.radians(a)) + 0.004))
            for a in range(-50, 100, 15)]
    body.append(tube('seam', seam, 0.022, S['code'], seg=6, up=(1, 0, 0)))
    body.append(ell('belly', (0.34, 0.44, 0.17), (0, 0.06, 0.36), S['belly'], 14, 8))
    hc, hr = (0, -0.36, 0.47), (0.27, 0.23, 0.24)
    body.append(ell('head', hr, hc, S['shell2'], 16, 10))
    body += bug_eyes(S, hc, hr, 0.12, 0.5, -0.545, (0.085, 0.06, 0.1))
    for s in (1, -1):
        body.append(tube('mand', spline([(s * 0.07, -0.53, 0.35), (s * 0.12, -0.64, 0.32), (s * 0.03, -0.7, 0.3)], 3),
                         (0.032, 0.012), S['claw'], seg=6, up=(0, 0, 1)))
        ant = spline([(s * 0.07, -0.47, 0.64), (s * 0.12, -0.57, 0.81), (s * 0.23, -0.63, 0.93)], 3)
        body.append(tube('ant', ant, (0.024, 0.016), S['leg'], seg=6, up=(1, 0, 0)))
        body.append(ell('antip', [0.048] * 3, ant[-1], S['code'], 10, 7))
    groups = {'Body': body}
    pivots = {}
    for s, side in ((1, 'L'), (-1, 'R')):
        for i, (y, dy) in enumerate(((-0.18, -0.1), (0.06, 0.0), (0.3, 0.1))):
            hip = (s * 0.26, y, 0.32)
            nm = f'Leg_{side}{i + 1}'
            groups[nm] = bug_leg(nm, s, hip, (s * 0.45, y + dy, 0.33), (s * 0.5, y + dy * 1.6, 0.0),
                                 (0.065, 0.05), S, knee_r=0.07, foot_r=0.065)
            pivots[nm] = hip
    return groups, pivots


def bug_fast():
    S = bug_mats()
    body = [ell('thorax', (0.2, 0.22, 0.18), (0, 0, 0.3), S['shell'], 16, 10),
            ell('head', (0.16, 0.14, 0.15), (0, -0.26, 0.33), S['shell2'], 14, 9)]
    for s in (1, -1):
        p = Vector((s * 0.1, -0.34, 0.37))
        body.append(ell('eye', (0.1, 0.09, 0.115), p, S['eye'], 14, 9))
        body.append(ell('shine', [0.03] * 3, p + Vector((-0.035, -0.075, 0.05)), S['shine'], 8, 6))
        body.append(box('brow', (0.16, 0.06, 0.04), p + Vector((s * 0.01, -0.02, 0.115)), (0, -s * 24, 0),
                        S['shell2'], bev=0.012, seg=2))
    body.append(tube('needle', spline([(0, -0.39, 0.28), (0, -0.52, 0.24), (0, -0.66, 0.21)], 3), (0.032, 0.006),
                     S['claw'], seg=6, up=(1, 0, 0)))
    for cc, rr in (((0, 0.27, 0.34), (0.14, 0.15, 0.13)), ((0, 0.46, 0.39), (0.1, 0.12, 0.09))):
        body.append(ell('abd', rr, cc, S['shell'], 12, 8))
    body.append(ell('band', (0.125, 0.03, 0.115), (0, 0.37, 0.36), S['code'], 12, 6))
    body.append(ell('band', (0.08, 0.02, 0.07), (0, 0.56, 0.42), S['code'], 10, 6))
    body.append(cone_between('sting', (0, 0.56, 0.42), (0, 0.7, 0.5), 0.06, 0.004, S['claw'], seg=6))
    groups, pivots = {'Body': body}, {}
    for s, side in ((1, 'L'), (-1, 'R')):
        body.append(box('pylon', (0.16, 0.12, 0.07), (s * 0.19, 0.04, 0.2), (0, -s * 15, 0), S['metal'], bev=0.02))
        body += thruster(S, groups, pivots, side, s, (s * 0.28, -0.02, 0.17), (s * 0.3, 0.8, -0.5))
        for i, (root, ln, wd, sw, dh) in enumerate((((s * 0.1, -0.04, 0.44), 0.52, 0.16, 24, 28),
                                                     ((s * 0.09, 0.08, 0.43), 0.42, 0.12, 50, 18))):
            nm = f'Wing_{side}{i + 1}'
            groups[nm] = wing(nm, s, root, ln, wd, sw, dh, S)
            pivots[nm] = root
    return groups, pivots


def bug_tank():
    S = bug_mats()
    body = []
    c, r = Vector((0, 0.05, 0.56)), Vector((0.72, 0.84, 0.6))
    body.append(ell('core', r * 0.97, c, S['core'], 16, 10))
    edges = [-0.86, -0.5, -0.17, 0.16, 0.48, 0.86]
    for i in range(5):
        y0, y1 = edges[i] + 0.02, edges[i + 1] - 0.02
        f = 1.08 if i % 2 == 0 else 1.045
        rr = r * f
        body.append(shell('plate', rr, c, S['armor'], [((0, y0 * rr.y / r.y, 0), (0, 1, 0)),
                                                       ((0, y1 * rr.y / r.y, 0), (0, -1, 0)),
                                                       ((0, 0, -0.32), (0, 0, 1))], thick=0.08, seg=16, rings=10,
                          sharp=40))
        ym = (y0 + y1) / 2
        top = c + Vector((0, ym, rr.z * math.sqrt(max(0.0, 1 - (ym / rr.y) ** 2))))
        if 0 < i < 5:
            body.append(cone_between('spike', top - Vector((0, 0, 0.04)), top + Vector((0, 0.06, 0.24)),
                                     0.1, 0.01, S['claw'], seg=7))
        for s in (1, -1):
            side = c + Vector((s * rr.x * math.sqrt(max(0.0, 1 - (ym / rr.y) ** 2 - (0.1 / rr.z) ** 2)), ym, 0.1))
            body.append(ell('stud', [0.055] * 3, side, S['code'], 6, 4))
    hc, hr = Vector((0, -0.8, 0.42)), Vector((0.46, 0.27, 0.3))
    body.append(ell('head', hr, hc, S['shell2'], 14, 8))
    body.append(shell('helm', hr * 1.08, hc, S['shell'], [((0, 0, 0.05), (0, 0, 1)), ((0, 0.02, 0), (0, -1, 0))],
                      thick=0.06, seg=16, rings=10, rot=(-12, 0, 0)))
    for s in (1, -1):
        p = Vector((s * 0.19, -1.02, 0.43))
        body.append(ell('eye', (0.095, 0.05, 0.06), p, S['eye'], 10, 6, rot=(0, -s * 16, 0)))
        body.append(ell('shine', [0.022] * 3, p + Vector((-0.03, -0.045, 0.025)), S['shine'], 6, 5))
        body.append(tube('tusk', spline([(s * 0.26, -0.98, 0.3), (s * 0.27, -1.14, 0.27), (s * 0.17, -1.22, 0.3)], 3),
                         (0.05, 0.012), S['claw'], seg=6))
    horn = spline([(0, -0.98, 0.56), (0, -1.12, 0.72), (0, -1.16, 0.96)], 4)
    body.append(tube('horn', horn, (0.12, 0.012), S['claw'], seg=8, up=(1, 0, 0)))
    groups, pivots = {'Body': body}, {}
    for s, side in ((1, 'L'), (-1, 'R')):
        for i, (y, dy) in enumerate(((-0.45, -0.1), (0.05, 0.0), (0.52, 0.1))):
            hip = (s * 0.6, y, 0.36)
            nm = f'Leg_{side}{i + 1}'
            groups[nm] = bug_leg(nm, s, hip, (s * 0.86, y + dy, 0.38), (s * 0.93, y + dy * 1.5, 0.0), (0.13, 0.1),
                                 S, knee_r=0.14, foot_r=0.12, seg=7)
            pivots[nm] = hip
    return groups, pivots


def bug_boss():
    S = bug_mats()
    body = []
    ac, ar, tilt = Vector((0, 0.98, 0.76)), Vector((0.84, 1.02, 0.74)), (-14, 0, 0)
    abd = ell('abdomen', ar, ac, S['shell'], 22, 14, rot=tilt)
    body.append(abd)
    for y in (-0.42, 0.06, 0.5):
        body.append(shell('stripe', ar * 1.018, ac, S['code'],
                          [((0, y, 0), (0, 1, 0)), ((0, y + 0.08, 0), (0, -1, 0))], thick=0.02, seg=22, rings=14,
                          rot=tilt))
    p, n, B = surf(ac, ar, (0, 0.18, 1))
    body.append(glyph('tag', '</>', 0.62, S['code'], Matrix.Translation(ac) @ Matrix.Rotation(
        math.radians(tilt[0]), 4, 'X') @ Matrix.Translation(p - ac + n * 0.03) @ B.to_4x4(), depth=0.04, wrap=abd))
    body.append(ell('thorax', (0.56, 0.52, 0.56), (0, 0.02, 0.82), S['shell2'], 18, 12))
    hc, hr = Vector((0, -0.48, 1.36)), Vector((0.52, 0.44, 0.46))
    body.append(ell('head', hr, hc, S['shell'], 18, 12))
    for s in (1, -1):
        pe = Vector((s * 0.21, -0.84, 1.4))
        body.append(ell('eye', (0.145, 0.08, 0.165), pe, S['eye'], 14, 9, rot=(0, -s * 12, 0)))
        body.append(ell('shine', [0.04] * 3, pe + Vector((-0.05, -0.07, 0.06)), S['shine'], 8, 6))
        body.append(ell('eye2', (0.06, 0.04, 0.06), (s * 0.13, -0.83, 1.66), S['eye'], 10, 7))
        body.append(box('brow', (0.3, 0.1, 0.07), (s * 0.23, -0.86, 1.6), (0, -s * 24, 0), S['shell2'], bev=0.025))
        body.append(tube('mand', spline([(s * 0.17, -0.8, 1.1), (s * 0.38, -1.02, 1.04), (s * 0.12, -1.2, 0.98)], 4),
                         (0.08, 0.02), S['gold'], seg=8))
        ant = spline([(s * 0.26, -0.62, 1.68), (s * 0.52, -0.52, 1.92), (s * 0.74, -0.2, 1.96)], 4)
        body.append(tube('ant', ant, (0.035, 0.02), S['leg'], seg=6, up=(0, 1, 0)))
        body.append(ell('antip', [0.065] * 3, ant[-1], S['code'], 10, 7))
    cc, crr = Vector((0, -0.48, 1.74)), 0.36
    ring = [cc + Vector((crr * math.cos(t), 0.85 * crr * math.sin(t), 0)) for t in
            (2 * math.pi * k / 20 for k in range(20))]
    body.append(tube('crownband', ring, 0.065, S['gold'], seg=6, closed=True, up=(0, 0, 1)))
    for k, (ang, hgt) in enumerate(((-90, 0.5), (-30, 0.38), (-150, 0.38), (30, 0.28), (150, 0.28))):
        t = math.radians(ang)
        b = cc + Vector((crr * math.cos(t), 0.85 * crr * math.sin(t), 0.02))
        tip = b + Vector((0.25 * (b - cc).x, 0.25 * (b - cc).y, hgt))
        body.append(cone_between('crownspike', b, tip, 0.1, 0.014, S['gold'], seg=6))
        body.append(ell('crowngem', [0.06 if k else 0.075] * 3, tip, S['gem'], 8, 6))
    body.append(ell('frontgem', (0.09, 0.045, 0.09), cc + Vector((0, -0.85 * crr - 0.035, 0.03)), S['gem'], 10, 7))
    groups, pivots = {'Body': body}, {}
    for s, side in ((1, 'L'), (-1, 'R')):
        for i, (y, dy) in enumerate(((-0.25, -0.25), (0.08, 0.02), (0.4, 0.28))):
            hip = (s * 0.44, y, 0.66)
            nm = f'Leg_{side}{i + 1}'
            groups[nm] = bug_leg(nm, s, hip, (s * 0.88, y + dy, 0.8), (s * 1.08, y + dy * 1.5, 0.0), (0.17, 0.12),
                                 S, knee_r=0.18, foot_r=0.16, seg=8)
            pivots[nm] = hip
        root = (s * 0.16, 0.12, 1.26)
        nm = f'Wing_{side}'
        groups[nm] = wing(nm, s, root, 1.15, 0.28, 52, 22, S)
        pivots[nm] = root
    return groups, pivots


# ---------------------------------------------------------------- 시안2 글리치 오류 코드

def glitch_mats():
    return dict(
        frame=P('Frame', '#2b2f3e', 0.45),
        frame2=P('FrameDark', '#1a1d28', 0.5),
        err=P('ErrorRed', '#e02f40', 0.4, emit=0.35),
        err2=P('ErrorRedDark', '#9e1b2a', 0.45),
        screen=P('Screen', '#0e1119', 0.18),
        eye=P('EyeRed', '#ff1428', emit=2.6),
        white=P('WhiteGlow', '#fff3f3', emit=3),
        cyan=P('GlitchCyan', '#2ef2ff', emit=2),
        mag=P('GlitchMagenta', '#ff3fd2', emit=2),
        amber=P('LoopAmber', '#ffc21a', emit=1.6),
        brick=P('Brick', '#8e3034', 0.6),
        brick2=P('BrickDark', '#6c2428', 0.6),
        mortar=P('Mortar', '#ff5a14', emit=2),
        hot=P('HotEye', '#fff2a8', emit=3),
        gold=P('Gold', '#f0b43c', 0.3, metal=0.85),
        bsod=P('BlueScreen', '#2457ff', 0.3, emit=1.2),
        violet=P('Violet', '#b44bff', emit=1.6),
        key=P('Keycap', '#363a4c', 0.45),
        metal=P('Metal', '#555a6c', 0.35, metal=0.7),
        nozzle=P('NozzleGlow', '#ffb02a', emit=2.5),
        flame=P('Flame', '#ff5a14', emit=2.6, alpha=0.85),
        core_flame=P('FlameCore', '#ffd66a', emit=2.8),
    )


def pixels(S, spots):
    return [box('px', (sz, sz, sz), loc, (0, 0, 0), S[m], bev=sz * 0.12, seg=1) for loc, sz, m in spots]


def angry_eyes(S, x, y, z, w, h, slant=18, mat='eye'):
    return [box('eye', (w, 0.03, h), (s * x, y, z), (0, -s * slant, 0), S[mat], bev=h * 0.3, seg=2) for s in (1, -1)]


def glitch_normal():
    S = glitch_mats()
    body = [box('case', (0.84, 0.64, 0.64), (0, 0, 0.57), m=S['frame'], bev=0.07),
            box('titlebar', (0.87, 0.67, 0.15), (0, 0, 0.925), m=S['err'], bev=0.05),
            box('screen', (0.7, 0.04, 0.5), (0, -0.315, 0.55), m=S['screen'], bev=0.03)]
    body.append(box('close', (0.15, 0.05, 0.1), (0.31, -0.34, 0.925), m=S['err2'], bev=0.02, seg=2))
    body.append(glyph('x', 'x', 0.15, S['white'], front((0.31, -0.37, 0.918)), depth=0.012))
    for x in (0.13, -0.03):
        body.append(box('btn', (0.1, 0.04, 0.07), (x + 0.03, -0.34, 0.925), m=S['err2'], bev=0.015, seg=2))
    body.append(box('min', (0.05, 0.01, 0.012), (0.16, -0.362, 0.912), m=S['white'], bev=0))
    body.append(box('max', (0.04, 0.01, 0.035), (0.0, -0.362, 0.925), m=S['white'], bev=0))
    body += angry_eyes(S, 0.15, -0.34, 0.63, 0.17, 0.085)
    mouth = [(-0.15, -0.34, 0.43), (-0.09, -0.34, 0.47), (-0.03, -0.34, 0.43), (0.03, -0.34, 0.47), (0.09, -0.34, 0.43),
             (0.15, -0.34, 0.47)]
    body.append(tube('mouth', mouth, 0.016, S['eye'], seg=6, up=(0, 1, 0)))
    # glitch: a slice of the case shifted sideways, with the RGB split showing at its ends
    body.append(box('slice', (0.84, 0.645, 0.08), (0.075, 0, 0.75), m=S['frame'], bev=0.015, seg=1))
    body.append(box('rgbL', (0.035, 0.645, 0.08), (-0.37, 0, 0.75), m=S['cyan'], bev=0.008, seg=1))
    body.append(box('rgbR', (0.035, 0.645, 0.08), (0.505, 0, 0.75), m=S['mag'], bev=0.008, seg=1))
    body += pixels(S, [((-0.47, -0.12, 0.42), 0.09, 'mag'), ((-0.5, 0.02, 0.34), 0.06, 'cyan'),
                       ((0.46, 0.2, 0.36), 0.07, 'cyan'), ((0.36, 0.22, 1.04), 0.06, 'mag')])
    groups, pivots = {'Body': body}, {}
    for s, side in ((1, 'L'), (-1, 'R')):
        nm = f'Leg_{side}'
        hip = (s * 0.2, 0, 0.27)
        groups[nm] = [box(nm, (0.15, 0.17, 0.2), (s * 0.2, 0, 0.17), m=S['frame2'], bev=0.04),
                      box(nm + 'f', (0.22, 0.3, 0.1), (s * 0.2, -0.04, 0.05), m=S['err'], bev=0.04)]
        pivots[nm] = hip
    return groups, pivots


def glitch_fast():
    S = glitch_mats()

    def taper(bm):
        for v in bm.verts:
            if v.co.y > 0:
                k = 1 - 0.55 * (v.co.y / 0.4) ** 1.5
                v.co.x *= k
                v.co.z *= k
    body = [shell('pod', (0.22, 0.4, 0.2), (0, 0, 0.3), S['frame'], [], thick=0, seg=18, rings=12, taper=taper)]
    body.append(shell('visor', (0.185, 0.15, 0.13), (0, -0.27, 0.32), S['screen'], [((0, -0.02, 0), (0, -1, 0))],
                      thick=0.03, seg=16, rings=10))
    for s in (1, -1):
        p = Vector((s * 0.075, -0.405, 0.33))
        body.append(ell('eye', (0.05, 0.025, 0.06), p, S['eye'], 10, 7, rot=(0, -s * 25, 0)))
        body.append(ell('shine', [0.017] * 3, p + Vector((-0.02, -0.022, 0.025)), S['white'], 6, 5))
    bolt = [(0.05, 0.42), (0.2, 0.42), (0.12, 0.24), (0.22, 0.24), (0.0, -0.02), (0.07, 0.17), (-0.03, 0.17)]
    body.append(flat_poly('bolt', [(x * 0.95, y * 0.9) for x, y in bolt], 0.05, S['amber'],
                          Mx((0, 0.02, 0.44), (90, 0, 90)), bev=0.008))
    for s in (1, -1):
        body.append(box('fin', (0.03, 0.16, 0.12), (s * 0.08, 0.36, 0.38), (0, s * 35, 0), S['frame2'], bev=0.012))
    body.append(ell('taillight', (0.05, 0.03, 0.035), (0, 0.41, 0.3), S['eye'], 8, 6))
    body += pixels(S, [((0.0, 0.52, 0.3), 0.06, 'mag'), ((0.06, 0.62, 0.34), 0.045, 'cyan'),
                       ((-0.04, 0.7, 0.29), 0.03, 'mag')])
    groups, pivots = {'Body': body}, {}
    a = 0.62
    loop = []
    for k in range(40):
        t = 2 * math.pi * k / 40
        den = 1 + math.sin(t) ** 2
        x, y = a * math.cos(t) / den, a * math.sin(t) * math.cos(t) / den
        loop.append((x, 0.02 + y * 1.1, 0.34 + 0.18 * (abs(x) / a) ** 1.5))
    groups['Loop'] = [tube('Loop', loop, 0.038, S['amber'], seg=7, closed=True, up=(0, 0, 1))]
    pivots['Loop'] = (0, 0.02, 0.34)
    body.append(shell('stripe', (0.226, 0.41, 0.206), (0, 0, 0.3), S['err'], [((0, -0.02, 0), (0, 1, 0)),
                                                                            ((0, 0.07, 0), (0, -1, 0))],
                      thick=0.015, seg=18, rings=12, taper=taper))
    for s, side in ((1, 'L'), (-1, 'R')):
        body.append(box('pylon', (0.18, 0.1, 0.07), (s * 0.18, 0.04, 0.21), (0, -s * 15, 0), S['frame2'], bev=0.02))
        body += thruster(S, groups, pivots, side, s, (s * 0.29, -0.03, 0.17), (s * 0.3, 0.8, -0.5), seg=6,
                         r=(0.075, 0.095), metal='frame2', ring='err')
    return groups, pivots


def glitch_tank():
    S = glitch_mats()
    W, D, z0, rows, rh = 1.4, 0.9, 0.36, 4, 0.2
    body = [box('mortar', (W - 0.04, D - 0.04, rows * rh - 0.02), (0, 0, z0 + rows * rh / 2), m=S['mortar'], bev=0.02,
                seg=1)]
    k = 0
    for r in range(rows):
        bw = W / 4
        xs = [(-W / 2 + bw * (i + 0.5), bw) for i in range(4)] if r % 2 == 0 else \
            [(-W / 2 + bw / 4, bw / 2)] + [(-W / 2 + bw / 2 + bw * (i + 0.5), bw) for i in range(3)] + [(W / 2 - bw / 4, bw / 2)]
        for x, w in xs:
            k += 1
            body.append(box('brick', (w - 0.035, D, rh - 0.035), (x, 0, z0 + rh * (r + 0.5)),
                            m=S['brick' if (k * 7) % 3 else 'brick2'], bev=0.03, seg=1))
    top = z0 + rows * rh
    body.append(box('visor', (0.86, 0.06, 0.15), (0, -D / 2 - 0.005, z0 + rh * 2.5 + 0.01), m=S['screen'], bev=0.02,
                    seg=2))
    body += angry_eyes(S, 0.19, -D / 2 - 0.035, z0 + rh * 2.5 + 0.01, 0.2, 0.07, slant=15, mat='hot')
    lock_c = Vector((0, -D / 2 - 0.05, z0 + rh * 1.15))
    body.append(box('lock', (0.28, 0.08, 0.22), lock_c, m=S['gold'], bev=0.035))
    arc = [lock_c + Vector((0.085 * math.cos(t), 0.0, 0.11 + 0.1 * math.sin(t))) for t in
           (math.pi * i / 10 for i in range(11))]
    body.append(tube('shackle', arc, 0.028, S['gold'], seg=6, up=(0, 1, 0)))
    body.append(cone_between('keyhole', lock_c + Vector((0, -0.03, 0.025)), lock_c + Vector((0, -0.05, 0.025)),
                             0.03, 0.03, S['mortar'], seg=8))
    body.append(box('keyslot', (0.025, 0.02, 0.07), lock_c + Vector((0, -0.042, -0.02)), m=S['mortar'], bev=0))
    body += pixels(S, [((-0.62, -0.2, top + 0.04), 0.08, 'cyan'), ((0.74, 0.1, 0.66), 0.07, 'mag'),
                       ((-0.76, 0.18, 0.5), 0.06, 'mag')])
    groups, pivots = {'Body': body}, {}
    fire = []
    for i, (x, h) in enumerate(((-0.54, 0.24), (-0.27, 0.36), (0.0, 0.44), (0.27, 0.34), (0.54, 0.26))):
        y = 0.08 if i % 2 else -0.06
        fire.append(flame_lick('flame', (x, y, top - 0.04), h, 0.13, S['flame'], sway=0.05, phase=i * 1.7))
        fire.append(flame_lick('flamec', (x + 0.01, y - 0.07, top - 0.04), h * 0.55, 0.075, S['core_flame'],
                               sway=0.03, phase=i * 1.7))
    groups['Fire'] = fire  # 'Fire', not 'Flame_*': the firewall's flames count toward its height
    pivots['Fire'] = (0, 0, top)
    for s, side in ((1, 'L'), (-1, 'R')):
        nm = f'Arm_{side}'
        sh = (s * (W / 2 + 0.08), 0, 0.98)
        groups[nm] = [box(nm, (0.22, 0.4, 0.34), (s * (W / 2 + 0.12), 0, 0.88), m=S['brick2'], bev=0.04),
                      box(nm + 'f', (0.36, 0.5, 0.34), (s * (W / 2 + 0.2), -0.05, 0.5), m=S['brick'], bev=0.05),
                      box(nm + 'k', (0.37, 0.05, 0.035), (s * (W / 2 + 0.2), -0.3, 0.55), m=S['mortar'], bev=0.01,
                          seg=1)]
        pivots[nm] = sh
        nm = f'Leg_{side}'
        hip = (s * 0.38, 0, 0.36)
        groups[nm] = [box(nm, (0.3, 0.44, 0.3), (s * 0.38, 0, 0.2), m=S['frame2'], bev=0.05),
                      box(nm + 'f', (0.36, 0.56, 0.1), (s * 0.38, -0.05, 0.05), m=S['frame'], bev=0.035)]
        pivots[nm] = hip
    return groups, pivots


def glitch_boss():
    S = glitch_mats()
    body = []
    tc = Vector((0, 0.05, 1.06))
    body.append(box('tower', (1.2, 0.82, 0.84), tc, m=S['frame'], bev=0.08))
    fy = tc.y - 0.41
    for s in (1, -1):
        body.append(box('rgb', (0.05, 0.03, 0.66), (s * 0.5, fy - 0.01, tc.z), m=S['violet'], bev=0.012, seg=1))
    for i in range(3):
        body.append(box('vent', (0.6, 0.03, 0.05), (0, fy - 0.005, tc.z - 0.2 + i * 0.11), m=S['mag'], bev=0.012,
                        seg=1))
    pc = Vector((0, fy - 0.02, tc.z + 0.24))
    body.append(cone_between('power', pc + Vector((0, 0.02, 0)), pc - Vector((0, 0.005, 0)), 0.08, 0.08, S['screen'],
                             seg=12))
    body.append(tube('pwr', [pc + Vector((0.045 * math.sin(t), -0.01, 0.045 * math.cos(t))) for t in
                             (math.radians(a) for a in range(35, 330, 25))], 0.011, S['violet'], seg=5, up=(0, 1, 0)))
    body.append(box('pwrbar', (0.02, 0.02, 0.05), pc + Vector((0, -0.01, 0.035)), m=S['violet'], bev=0))
    for s in (1, -1):
        body.append(box('shoulder', (0.36, 0.52, 0.34), (s * 0.74, 0.05, 1.3), m=S['frame2'], bev=0.07))
        body.append(box('shoulderled', (0.37, 0.05, 0.05), (s * 0.74, -0.215, 1.3), m=S['violet'], bev=0.01, seg=1))
    body.append(cone_between('neck', (0, 0.1, 1.46), (0, 0.1, 1.58), 0.16, 0.13, S['frame2'], seg=10))
    mc = Vector((0, 0.0, 2.02))
    body.append(box('monitor', (1.62, 0.6, 1.04), mc, m=S['frame'], bev=0.08))
    body.append(box('crt', (1.16, 0.42, 0.74), mc + Vector((0, 0.44, -0.02)), m=S['frame2'], bev=0.1))
    sy = mc.y - 0.3
    body.append(box('screen', (1.38, 0.04, 0.8), (0, sy, mc.z + 0.02), m=S['bsod'], bev=0.03, seg=2))
    body.append(glyph('face', ':(', 0.6, S['white'], front((0.0, sy - 0.025, mc.z + 0.06), spin=-90), depth=0.012))
    for s in (1, -1):
        body.append(box('brow', (0.24, 0.02, 0.055), (s * 0.18, sy - 0.03, mc.z + 0.33), (0, -s * 20, 0),
                        S['white'], bev=0.012, seg=1))
    body.append(glyph('404', '404', 0.17, S['white'], front((0.44, sy - 0.025, mc.z - 0.27)), depth=0.01))
    for i, w in enumerate((0.42, 0.3)):
        body.append(box('line', (w, 0.01, 0.03), (-0.62 + w / 2, sy - 0.025, mc.z - 0.22 - i * 0.07), m=S['white'],
                        bev=0))
    body.append(box('led', (1.0, 0.04, 0.04), (0, mc.y - 0.31, mc.z - 0.48), m=S['violet'], bev=0.01, seg=1))
    # glitch: a strip of the blue screen slipped out of the bezel, and stray pixels
    body.append(box('slip', (0.5, 0.05, 0.1), (0.78, sy + 0.01, mc.z - 0.05), m=S['bsod'], bev=0.01, seg=1))
    body.append(box('slipc', (0.04, 0.05, 0.1), (1.05, sy + 0.01, mc.z - 0.05), m=S['cyan'], bev=0.008, seg=1))
    body += pixels(S, [((-0.9, -0.05, 2.5), 0.1, 'mag'), ((-1.0, 0.1, 2.36), 0.07, 'cyan'),
                       ((0.92, 0.1, 2.58), 0.08, 'cyan'), ((0.98, -0.1, 1.62), 0.07, 'mag')])
    for s in (1, -1):
        ant = spline([(s * 0.3, 0.2, 2.52), (s * 0.48, 0.28, 2.75), (s * 0.62, 0.36, 2.92)], 3)
        body.append(tube('ant', ant, (0.03, 0.02), S['metal'], seg=6, up=(0, 1, 0)))
        body.append(ell('antip', [0.07] * 3, ant[-1], S['mag'], 10, 7))
    body.append(box('antbase', (0.4, 0.22, 0.08), (0, 0.22, 2.56), m=S['frame2'], bev=0.03))
    groups, pivots = {'Body': body}, {}
    for s, side in ((1, 'L'), (-1, 'R')):
        nm = f'Arm_{side}'
        sh = Vector((s * 0.8, 0.05, 1.24))
        cab = spline([sh, (s * 1.02, 0.02, 0.95), (s * 1.02, -0.12, 0.68)], 4)
        groups[nm] = [tube(nm, cab, 0.11, S['frame2'], seg=8, up=(0, 1, 0)),
                      tube(nm + 'b', [cab[3], cab[4]], 0.125, S['violet'], seg=8, up=(0, 1, 0)),
                      box(nm + 'k', (0.4, 0.4, 0.34), (s * 1.02, -0.16, 0.48), m=S['key'], bev=0.07),
                      box(nm + 't', (0.32, 0.32, 0.06), (s * 1.02, -0.18, 0.66), m=S['key'], bev=0.03),
                      glyph(nm + 'g', 'Del' if s > 0 else 'Esc', 0.13, S['white'], front((s * 1.02, -0.365, 0.5)),
                            depth=0.01)]
        pivots[nm] = sh
        nm = f'Leg_{side}'
        hip = (s * 0.36, 0.05, 0.66)
        groups[nm] = [box(nm, (0.34, 0.4, 0.56), (s * 0.36, 0.05, 0.4), m=S['frame2'], bev=0.07),
                      box(nm + 'p', (0.36, 0.06, 0.12), (s * 0.36, -0.16, 0.42), m=S['violet'], bev=0.02, seg=1),
                      box(nm + 'f', (0.46, 0.62, 0.16), (s * 0.36, -0.05, 0.08), m=S['frame'], bev=0.06)]
        pivots[nm] = hip
    return groups, pivots


MODELS = {
    'b_normal': ('시안1_버그군단_일반', bug_normal, 1.0, 'Monster_BugNormal'),
    'b_fast': ('시안1_버그군단_빠른적', bug_fast, 0.8, 'Monster_BugFast'),
    'b_tank': ('시안1_버그군단_탱커병', bug_tank, 1.3, 'Monster_BugTank'),
    'b_boss': ('시안1_버그군단_보스', bug_boss, 2.5, 'Monster_BugBoss'),
    'g_normal': ('시안2_글리치_일반', glitch_normal, 1.0, 'Monster_GlitchNormal'),
    'g_fast': ('시안2_글리치_빠른적', glitch_fast, 0.8, 'Monster_GlitchFast'),
    'g_tank': ('시안2_글리치_탱커병', glitch_tank, 1.3, 'Monster_GlitchTank'),
    'g_boss': ('시안2_글리치_보스', glitch_boss, 2.5, 'Monster_GlitchBoss'),
}


# ---------------------------------------------------------------- assembly, export, render

def assemble(builder, height, root_name):
    groups, pivots = builder()
    objs = []
    for name, parts in groups.items():
        if '--parts' in ARGS:
            print(name, sorted(((tri_count([p]), p.name) for p in parts), reverse=True)[:8])
        ob = join(parts, name)
        if name in pivots:
            set_origin(ob, pivots[name])
        objs.append(ob)
    bpy.context.view_layer.update()
    solid = [o for o in objs if not o.name.startswith('Flame')]
    pts = [o.matrix_world @ v.co for o in solid for v in o.data.vertices]
    zmin, zmax = min(p.z for p in pts), max(p.z for p in pts)
    low = [p for p in pts if p.z < zmin + 0.05]
    cx = (min(p.x for p in low) + max(p.x for p in low)) / 2
    cy = (min(p.y for p in low) + max(p.y for p in low)) / 2
    k = height / (zmax - zmin)
    for o in objs:  # feet centre to the origin, then scale to the role's height
        o.location = (o.location - Vector((cx, cy, zmin))) * k
        o.data.transform(Matrix.Scale(k, 4))
    root = link(bpy.data.objects.new(root_name, None))
    for o in objs:
        o.parent = root
    bpy.context.view_layer.update()
    return root, objs


def tri_count(objs):
    n = 0
    for o in objs:
        o.data.calc_loop_triangles()
        n += len(o.data.loop_triangles)
    return n


def export_fbx(path, root, objs):
    for o in bpy.context.scene.objects:
        o.select_set(o is root or o in objs)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'EMPTY', 'MESH'},
                             apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, mesh_smooth_type='OFF', add_leaf_bones=False, bake_anim=False)


def use_gpu():
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        for t in ('OPTIX', 'CUDA', 'HIP', 'ONEAPI', 'METAL'):
            try:
                prefs.compute_device_type = t
            except TypeError:
                continue
            prefs.get_devices()
            devs = [d for d in prefs.devices if d.type == t]
            if devs:
                for d in prefs.devices:
                    d.use = d.type == t
                return t
    except Exception as e:
        print('GPU setup failed:', e)
    return None


def sun(name, direction_from, color, energy, angle):
    li = bpy.data.lights.new(name, 'SUN')
    li.color, li.energy, li.angle = lin(color)[:3], energy, math.radians(angle)
    ob = link(bpy.data.objects.new(name, li))
    ob.rotation_euler = Vector(direction_from).to_track_quat('Z', 'Y').to_euler()
    return ob


def setup_scene():
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    dev = use_gpu()
    sc.cycles.device = 'GPU' if dev else 'CPU'
    sc.cycles.use_denoising = True
    sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'
    sc.render.image_settings.color_mode = 'RGBA'
    try:
        sc.view_settings.view_transform = 'Standard'
    except TypeError:
        pass
    w = bpy.data.worlds.new('World')
    sc.world = w
    try:
        w.use_nodes = True
    except Exception:
        pass
    bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND')
    bg.inputs['Color'].default_value = lin('#6f84b0')
    bg.inputs['Strength'].default_value = 0.55
    lights = [sun('Key', (-1.0, -1.25, 1.7), '#ffe2b8', 3.6, 6), sun('Rim', (1.3, 1.5, 0.9), '#c2b0ff', 2.6, 4)]
    bm = bmesh.new()
    bmesh.ops.create_grid(bm, x_segments=1, y_segments=1, size=30)
    floor = mesh_obj('ShadowCatcher', bm, P('Floor', '#808080'), smooth=0)
    floor.is_shadow_catcher = True
    cam = link(bpy.data.objects.new('Camera', bpy.data.cameras.new('Camera')))
    sc.camera = cam
    print('render device:', dev or 'CPU', 'view:', sc.view_settings.view_transform)
    return dict(world=bg, lights=lights, floor=floor, cam=cam)


def model_points(objs):
    return [o.matrix_world @ v.co for o in objs for v in o.data.vertices]


def hero_camera(cam, objs, az=30, el=22, lens=70, margin=0.86):
    cam.data.type, cam.data.lens, cam.data.sensor_width = 'PERSP', lens, 36
    pts = model_points(objs)
    lo = Vector([min(p[i] for p in pts) for i in range(3)])
    hi = Vector([max(p[i] for p in pts) for i in range(3)])
    center = (lo + hi) / 2
    a, e = math.radians(az), math.radians(el)
    d = Vector((math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    rot = (-d).to_track_quat('-Z', 'Y')
    right, up = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0))
    t = math.tan(cam.data.angle / 2) * margin
    for _ in range(3):
        D = max(max(abs((p - center).dot(right)), abs((p - center).dot(up))) / t + (p - center).dot(d) for p in pts)
        xs = [(p - center).dot(right) / (D - (p - center).dot(d)) for p in pts]
        ys = [(p - center).dot(up) / (D - (p - center).dot(d)) for p in pts]
        center += right * (max(xs) + min(xs)) / 2 * D + up * (max(ys) + min(ys)) / 2 * D
    D = max(max(abs((p - center).dot(right)), abs((p - center).dot(up))) / t + (p - center).dot(d) for p in pts)
    cam.location = center + d * D
    cam.rotation_euler = rot.to_euler()


def battle_camera(cam, objs, pad=0.45):
    """Orthographic, like the game's camera (47.5 deg down, FOV 19 at 33 units is nearly parallel)."""
    sc = bpy.context.scene
    cam.data.type = 'ORTHO'
    rot = Euler((math.radians(90 - BATTLE_PITCH), 0, 0)).to_quaternion()
    right, up, fwd = rot @ Vector((1, 0, 0)), rot @ Vector((0, 1, 0)), rot @ Vector((0, 0, -1))
    pts = model_points(objs)
    pts += [Vector((p.x, p.y, 0)) for p in pts[::25]]  # room for the shadow under a hovering flyer
    xs, ys = [p.dot(right) for p in pts], [p.dot(up) for p in pts]
    x0, x1, y0, y1 = min(xs) - pad, max(xs) + pad, min(ys) - pad * 0.6, max(ys) + pad * 0.4
    size = max(x1 - x0, y1 - y0)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    cam.data.ortho_scale = size
    cam.location = right * cx + up * cy - fwd * 40
    cam.rotation_euler = rot.to_euler()
    cam.data.clip_end = 100
    res = int(math.ceil(size * BATTLE_PPU))
    sc.render.resolution_x = sc.render.resolution_y = res
    # pixel of the feet origin, so compose.py can stand the sprite on the road
    return {'res': res, 'anchor': [(0 - cx) / size * res + res / 2, res / 2 - (0 - cy) / size * res],
            'ppu': BATTLE_PPU}


def render(path, samples):
    sc = bpy.context.scene
    sc.cycles.samples = samples
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)


KEEP = {}


def emission_only(rig, on):
    """Black out everything but the emission, for the glow pass."""
    keys = ('Base Color', 'Specular IOR Level', 'Coat Weight', 'Metallic')
    for m in bpy.data.materials:
        b = next((n for n in m.node_tree.nodes if n.type == 'BSDF_PRINCIPLED'), None) if m.node_tree else None
        if b is None:
            continue
        if on:
            KEEP[m.name] = [tuple(b.inputs[k].default_value) if k == 'Base Color' else b.inputs[k].default_value
                            for k in keys]
            for k, v in zip(keys, ((0, 0, 0, 1), 0, 0, 0)):
                b.inputs[k].default_value = v
        elif m.name in KEEP:
            for k, v in zip(keys, KEEP.pop(m.name)):
                b.inputs[k].default_value = v
    rig['world'].inputs['Strength'].default_value = 0 if on else 0.55
    for o in rig['lights'] + [rig['floor']]:
        o.hide_render = on


def build(mid):
    stem, builder, height, root_name = MODELS[mid]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    global FONT
    FONT = bpy.data.fonts.load(FONT_PATH)
    root, objs = assemble(builder, height, root_name)
    tris = tri_count(objs)
    pts = model_points(objs)
    dims = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 3) for i in range(3)]
    print(f'[{mid}] tris={tris} size(x,y,z)={dims} parts=' + ', '.join(f'{o.name}:{tri_count([o])}' for o in objs))
    export_fbx(os.path.join(HERE, stem + '.fbx'), root, objs)
    rig = setup_scene()
    bpy.context.preferences.filepaths.save_version = 0  # no .blend1 backups next to the mockups
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(HERE, stem + '.blend'))
    info = {'id': mid, 'stem': stem, 'tris': tris, 'size': dims, 'objects': sorted(o.name for o in objs)}
    if '--preview' in ARGS:  # quick look while modelling: hero and battle angle side by side
        os.makedirs(WORK, exist_ok=True)
        sc = bpy.context.scene
        sc.render.resolution_x = sc.render.resolution_y = 640
        hero_camera(rig['cam'], objs, az=HERO_AZ.get(mid, 30))
        render(os.path.join(WORK, mid + '_preview.png'), 24)
        root.rotation_euler.z = math.radians(BATTLE_YAW.get(mid, 35))
        root.location.z = HOVER.get(mid, 0)
        bpy.context.view_layer.update()
        battle_camera(rig['cam'], objs)
        sc.render.resolution_x = sc.render.resolution_y = 640
        render(os.path.join(WORK, mid + '_preview_battle.png'), 24)
    elif '--no-render' not in ARGS:
        heroes = '--battle-only' not in ARGS  # re-render just the battle shots
        os.makedirs(WORK, exist_ok=True)
        sc = bpy.context.scene
        sc.render.resolution_x = sc.render.resolution_y = 1024
        hero_camera(rig['cam'], objs, az=HERO_AZ.get(mid, 30))
        if heroes:
            render(os.path.join(WORK, mid + '_hero.png'), SAMPLES)
        root.rotation_euler.z = math.radians(BATTLE_YAW.get(mid, 35))
        root.location.z = HOVER.get(mid, 0)
        bpy.context.view_layer.update()
        # a high sun for the battle shot, so shadows stay short and under the monster like in the game
        rig['lights'][0].rotation_euler = Vector((-0.55, -0.45, 2.4)).to_track_quat('Z', 'Y').to_euler()
        info['battle'] = battle_camera(rig['cam'], objs)
        render(os.path.join(WORK, mid + '_battle.png'), max(32, SAMPLES // 2))
        emission_only(rig, True)
        render(os.path.join(WORK, mid + '_battle_emit.png'), 16)
        sc.render.resolution_x = sc.render.resolution_y = 1024
        root.rotation_euler.z = 0
        root.location.z = 0
        bpy.context.view_layer.update()
        hero_camera(rig['cam'], objs, az=HERO_AZ.get(mid, 30))
        if heroes:
            render(os.path.join(WORK, mid + '_hero_emit.png'), 24)
        with open(os.path.join(WORK, mid + '.json'), 'w', encoding='utf-8') as f:
            json.dump(info, f, ensure_ascii=False, indent=1)
    return info


if __name__ == '__main__':
    only = arg('--only')
    for mid in (only.split(',') if only else MODELS):
        build(mid)
