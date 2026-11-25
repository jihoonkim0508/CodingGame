# Toon look of the robots' CharacterSurface shader, rebuilt in Blender EEVEE for still drafts:
# three cel bands (shadow / mid / light, tints sampled from the robot icons) and an inverted-hull navy outline.
# Round 3 adds modelled forms: lofted bodies through cross-sections, armour shells that follow them, faceted blades.
import bpy, bmesh, math
from mathutils import Vector

OUTLINE = (23, 31, 41)                 # the robots' _OutlineColor (0.09, 0.12, 0.16), as it shows on the icons
LIGHT = (0.95, 0.98, 1.00)             # white body on Warrior.png: lit 242,250,255
MID = (0.84, 0.88, 0.94)               # mid 214,225,239
SHADOW = (0.71, 0.76, 0.87)            # shadow 180,195,221

SC = 1.0                               # size multiplier of the design being built
ROOT = None                            # empty the parts hang under
HULLS = []                             # displace modifiers of all outline hulls (strength set per render)
_mats = {}


def lin(c):
    c = c / 255
    return c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4


def lin3(rgb):
    return tuple(lin(c) for c in rgb)


def reset():
    global ROOT, HULLS, _font
    bpy.ops.wm.read_factory_settings(use_empty=True)
    _mats.clear(); HULLS = []; ROOT = None; _font = None
    s = bpy.context.scene
    s.render.engine = 'BLENDER_EEVEE'
    s.view_settings.view_transform = 'Standard'
    s.view_settings.look = 'None'
    s.render.film_transparent = True
    s.render.image_settings.file_format = 'PNG'
    s.render.image_settings.color_mode = 'RGBA'
    s.eevee.taa_render_samples = 64
    w = bpy.data.worlds.new('w'); s.world = w; w.use_nodes = True
    bg = next(n for n in w.node_tree.nodes if n.type == 'BACKGROUND'); bg.inputs[1].default_value = 0
    cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); s.collection.objects.link(cam); s.camera = cam
    set_sun((-0.75, -0.45, 0.85))
    return s


SUN_DIR = Vector((0, 0, 1))


def set_sun(toward_light):
    """The key light's direction (toward the light). Call before making materials: toon() bakes it in."""
    global SUN_DIR
    SUN_DIR = Vector(toward_light).normalized()


def toon(rgb, name=None, tex=None):
    """tex: an albedo image path (the real robots), multiplied in place of the flat colour."""
    key = ('toon', rgb, tex)
    if key in _mats: return _mats[key]
    m = bpy.data.materials.new(name or f'toon{rgb}'); m.use_nodes = True
    nt = m.node_tree; N = nt.nodes; L = nt.links; N.clear()
    out = N.new('ShaderNodeOutputMaterial')
    # bands on N.L in [-1, 1] mapped to [0, 1], at the shader's thresholds: shadow below -0.12, light above 0.48.
    # (No shadow maps: their self-shadowing broke lit faces into blocks; the ground gets a soft blob instead.)
    geo = N.new('ShaderNodeNewGeometry')
    dot = N.new('ShaderNodeVectorMath'); dot.operation = 'DOT_PRODUCT'; dot.inputs[1].default_value = SUN_DIR
    L.new(geo.outputs['Normal'], dot.inputs[0])
    norm = N.new('ShaderNodeMapRange'); norm.inputs['From Min'].default_value = -1
    L.new(dot.outputs['Value'], norm.inputs['Value'])
    norm = norm.outputs['Result']
    cel = N.new('ShaderNodeValToRGB'); r = cel.color_ramp; r.interpolation = 'LINEAR'
    stops = [(0.0, SHADOW), (0.425, SHADOW), (0.455, MID), (0.725, MID), (0.755, LIGHT), (1.0, LIGHT)]
    while len(r.elements) < len(stops): r.elements.new(0.5)
    for e, (p, c) in zip(r.elements, stops): e.position = p; e.color = (*lin3(tuple(v * 255 for v in c)), 1)
    L.new(norm, cel.inputs[0])
    soft = N.new('ShaderNodeValToRGB'); r2 = soft.color_ramp
    r2.elements[0].color = (*lin3(tuple(v * 255 for v in SHADOW)), 1); r2.elements[1].color = (*lin3(tuple(v * 255 for v in LIGHT)), 1)
    L.new(norm, soft.inputs[0])
    mix = N.new('ShaderNodeMix'); mix.data_type = 'RGBA'; mix.inputs[0].default_value = 0.22  # a little soft light, as _CelStrength blends
    L.new(cel.outputs[0], mix.inputs[6]); L.new(soft.outputs[0], mix.inputs[7])
    alb = N.new('ShaderNodeMix'); alb.data_type = 'RGBA'; alb.blend_type = 'MULTIPLY'; alb.inputs[0].default_value = 1
    alb.inputs[7].default_value = (*lin3(rgb), 1)
    if tex:
        im = N.new('ShaderNodeTexImage'); im.image = bpy.data.images.load(tex, check_existing=True)
        L.new(im.outputs['Color'], alb.inputs[7])
    L.new(mix.outputs[2], alb.inputs[6])
    em = N.new('ShaderNodeEmission'); L.new(alb.outputs[2], em.inputs[0])
    L.new(em.outputs[0], out.inputs[0])
    _mats[key] = m
    return m


def flat(rgb, strength=1.0):
    """Unlit colour: eye slits, a small core or thruster glow (a gentle self-lit look, nothing glows past its colour)."""
    key = ('flat', rgb, strength)
    if key in _mats: return _mats[key]
    m = bpy.data.materials.new(f'flat{rgb}'); m.use_nodes = True
    nt = m.node_tree; nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial'); em = nt.nodes.new('ShaderNodeEmission')
    em.inputs[0].default_value = (*lin3(rgb), 1); em.inputs[1].default_value = strength
    nt.links.new(em.outputs[0], out.inputs[0])
    _mats[key] = m
    return m


def hull_mat():
    m = flat(OUTLINE)
    m.use_backface_culling = True
    return m


def blob_mat():
    if 'blob' in _mats: return _mats['blob']
    m = bpy.data.materials.new('blob'); m.use_nodes = True; m.surface_render_method = 'BLENDED'
    nt = m.node_tree; N = nt.nodes; N.clear()
    out = N.new('ShaderNodeOutputMaterial')
    tc = N.new('ShaderNodeTexCoord'); g = N.new('ShaderNodeTexGradient'); g.gradient_type = 'SPHERICAL'
    nt.links.new(tc.outputs['Object'], g.inputs[0])
    ramp = N.new('ShaderNodeMath'); ramp.operation = 'MULTIPLY'; ramp.inputs[1].default_value = 0.28
    nt.links.new(g.outputs[1], ramp.inputs[0])
    tr = N.new('ShaderNodeBsdfTransparent'); em = N.new('ShaderNodeEmission'); em.inputs[0].default_value = (*lin3((60, 70, 92)), 1)
    mx = N.new('ShaderNodeMixShader')
    nt.links.new(ramp.outputs[0], mx.inputs[0]); nt.links.new(tr.outputs[0], mx.inputs[1]); nt.links.new(em.outputs[0], mx.inputs[2])
    nt.links.new(mx.outputs[0], out.inputs[0])
    _mats['blob'] = m
    return m


# ---------------------------------------------------------------- objects

def begin(name, sc=1.0, at=(0, 0, 0), yaw=0.0):
    global ROOT, SC
    SC = sc
    ROOT = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(ROOT)
    ROOT.location = at; ROOT.rotation_euler = (0, 0, math.radians(yaw))
    return ROOT


def _v(p):
    return Vector(p) * SC


# Render-time smoothing (the drafts are images, so they may look high-poly): organic parts get a Subdivision Surface
# (render level 2); hard parts keep their planes, with a small 3-segment Bevel on edges past 30 degrees and face-area
# Weighted Normals, so edges read crisp but rounded. The outline hull carries the same modifiers before its push-out.
BEVEL = 0.007                          # hard-edge rounding in design units


def _obj(name, bm, mat, loc, rot=None, quat=None, smooth=None, outline=True, sharp=None, kind='hard'):
    """kind: 'organic' (subdivided) or 'hard' (bevelled, weighted normals); every face shades smooth."""
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    ob.parent = ROOT; ob.location = _v(loc)
    if quat is not None: ob.rotation_mode = 'QUATERNION'; ob.rotation_quaternion = quat
    elif rot is not None: ob.rotation_euler = tuple(math.radians(a) for a in rot)
    if kind == 'organic':
        m = ob.modifiers.new('subd', 'SUBSURF'); m.levels = 1; m.render_levels = 2
    else:
        b = ob.modifiers.new('bevel', 'BEVEL'); b.width = BEVEL * SC; b.segments = 3; b.limit_method = 'ANGLE'
        b.angle_limit = math.radians(30); b.use_clamp_overlap = True
        w = ob.modifiers.new('wn', 'WEIGHTED_NORMAL'); w.mode = 'FACE_AREA'; w.keep_sharp = True
    if outline: _hull(ob)
    return ob


def _hull(ob):
    me = ob.data.copy(); me.materials.clear(); me.materials.append(hull_mat())
    bm = bmesh.new(); bm.from_mesh(me)
    if not ob.modifiers:               # imported meshes split verts at seams: weld so the hull doesn't crack
        bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    h = bpy.data.objects.new(ob.name + '_hull', me); bpy.context.scene.collection.objects.link(h)
    h.parent = ob.parent; h.location = ob.location; h.rotation_mode = ob.rotation_mode
    h.rotation_euler = ob.rotation_euler; h.rotation_quaternion = ob.rotation_quaternion
    for m in ob.modifiers:             # the same smoothing as the part, so the line follows the smoothed surface
        if m.type == 'SUBSURF':
            n = h.modifiers.new(m.name, 'SUBSURF'); n.levels = m.levels; n.render_levels = m.render_levels
        elif m.type == 'BEVEL':
            n = h.modifiers.new(m.name, 'BEVEL'); n.width = m.width; n.segments = m.segments; n.limit_method = m.limit_method
            n.angle_limit = m.angle_limit; n.use_clamp_overlap = m.use_clamp_overlap
    d = h.modifiers.new('hull', 'DISPLACE'); d.direction = 'NORMAL'; d.mid_level = 0; d.strength = -0.02
    HULLS.append(d)


def set_line(world_thickness):
    for d in HULLS: d.strength = -world_thickness


def rbox(name, size, loc, mat, r=0.08, seg=2, rot=None, outline=True):
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    sx, sy, sz = (s * SC for s in size)
    for v in bm.verts: v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    rr = min(r * SC, min(sx, sy, sz) * 0.49)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=rr, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth=False, outline=outline)


def ball(name, r, loc, mat, scale=(1, 1, 1), rot=None, outline=True, u=None):
    u = u or (8 if r * SC < 0.06 else 10 if r * SC < 0.1 else 14 if r * SC < 0.3 else 18)
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=max(4, u * 2 // 3), radius=r * SC)
    for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    return _obj(name, bm, mat, loc, rot=rot, outline=outline, kind='organic')


def tube(name, p0, p1, r, mat, r1=None, outline=True, bevel=0.0, seg=None):
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0)
    bm = bmesh.new()
    seg = max(seg or 0, 32)
    bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r * SC, radius2=(r1 if r1 is not None else r) * SC, depth=d.length * SC)
    if bevel:
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and e.link_faces[0].normal.angle(e.link_faces[1].normal) > 0.9]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel * SC, segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return _obj(name, bm, mat, (p0 + p1) / 2, quat=q, smooth=lambda p: abs(p.normal.z) < 0.95, outline=outline)


def prism(name, pts, depth, loc, mat, r=0.05, rot=None, outline=True, seg=2):
    """A 2D outline in the x/z plane, given thickness along y (front faces -y): diamonds, triangles, fins."""
    bm = bmesh.new()
    vs = [bm.verts.new((x * SC, -depth * SC / 2, z * SC)) for x, z in pts]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in (g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)): v.co.y += depth * SC
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if r: bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=r * SC, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth=False, outline=outline)


def sweep(name, pts, radii, mat, seg=14, flat=1.0, outline=True):
    """A tube along a polyline with a radius per point: horns, tails, cables, claws. flat < 1 squashes the section."""
    pts, radii = _smooth_path(pts, [(r,) for r in radii]); radii = [r[0] for r in radii]; seg = max(seg, 10)
    P = [Vector(p) * SC for p in pts]
    bm = bmesh.new(); rings = []
    for i, p in enumerate(P):
        t = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
        a = Vector((0, 0, 1)).cross(t)
        if a.length < 1e-3: a = Vector((1, 0, 0)).cross(t)
        a.normalize(); b = t.cross(a); r = radii[i] * SC
        rings.append([bm.verts.new(p + (a * math.cos(k * 2 * math.pi / seg) * flat + b * math.sin(k * 2 * math.pi / seg)) * r)
                      for k in range(seg)])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(seg): bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k]))
    bm.faces.new(rings[0]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, (0, 0, 0), outline=outline, kind='organic')


def _smooth_path(pts, vals, per=3):
    """Catmull-Rom through control points (per steps a span), values per point interpolated linearly: round curves."""
    P = [Vector(p) for p in pts]; n = len(P); op, ov = [], []
    for i in range(n - 1):
        p0, p1, p2, p3 = P[max(i - 1, 0)], P[i], P[i + 1], P[min(i + 2, n - 1)]
        for k in range(per):
            f = k / per
            op.append(0.5 * (2 * p1 + (p2 - p0) * f + (2 * p0 - 5 * p1 + 4 * p2 - p3) * f * f + (3 * p1 - p0 - 3 * p2 + p3) * f ** 3))
            ov.append(tuple(a + (b - a) * f for a, b in zip(vals[i], vals[i + 1])))
    op.append(P[-1]); ov.append(tuple(vals[-1]))
    return op, ov


# ---------------------------------------------------------------- modelled forms (round 3)
# A key section is (pos, w, top, bottom, n): a superellipse of half-width w, half-heights top / bottom above and below
# the spine, exponent n (1 = diamond with a ridge, 2 = ellipse, 3+ = rounded box). Sections stand across the spine;
# `side` is the section's width axis (x for anything drawn in the side plane), `up` says which way is its top.

def _at(keys, u):
    """Catmull-Rom between key sections at parameter u (0 .. len(keys) - 1)."""
    m = len(keys) - 1
    j = min(int(u), m - 1); f = u - j
    q = [keys[max(0, min(m, i))] for i in (j - 1, j, j + 1, j + 2)]

    def cr(a, b, c, d):
        return 0.5 * (2 * b + (c - a) * f + (2 * a - 5 * b + 4 * c - d) * f * f + (3 * b - a - 3 * c + d) * f * f * f)
    pos = cr(*(Vector(k[0]) for k in q))
    vals = [max(0.0, cr(*(k[i] for k in q))) for i in range(1, 5)]
    vals[3] = max(0.8, vals[3])
    return (pos, *vals)


def path(keys, rings, u0=0.0, u1=1.0):
    m = len(keys) - 1
    return [_at(keys, (u0 + (u1 - u0) * i / (rings - 1)) * m) for i in range(rings)]


def _frames(T, side, up):
    """Section axes (width a, height b) per ring: the first ring's top faces `up`, later rings follow it without flips."""
    out = []
    for t in T:
        a = side - t * side.dot(t)
        if a.length < 1e-4: a = up.cross(t)
        a.normalize(); b = t.cross(a)
        if (out and a.dot(out[-1][0]) < 0) or (not out and b.dot(up) < 0): a, b = -a, -b
        out.append((a, b))
    return out


def _ring(p, ab, w, top, bot, n, angles):
    a, b = ab
    out = []
    for th in angles:
        c, s = math.cos(th), math.sin(th)
        x = w * math.copysign(abs(c) ** (2 / n), c)
        y = (top if s > 0 else bot) * math.copysign(abs(s) ** (2 / n), s)
        out.append(p + a * x + b * y)
    return out


def _tangents(S):
    P = [s[0] for s in S]
    return [(P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized() for i in range(len(P))]


def loft(name, keys, mat, rings=12, seg=16, side=(1, 0, 0), up=(0, 0, 1), outline=True, sharp=50):
    """A closed body through key sections (smoothly interpolated to `rings` rings). A section of zero size is a point."""
    S = path(keys, rings) if rings else [(Vector(k[0]), *k[1:]) for k in keys]
    F = _frames(_tangents(S), Vector(side), Vector(up))
    angles = [2 * math.pi * k / seg for k in range(seg)]
    bm = bmesh.new(); R = []
    for (p, w, top, bot, n), ab in zip(S, F):
        if max(w, top, bot) < 1e-4: R.append([bm.verts.new(p * SC)])
        else: R.append([bm.verts.new(q * SC) for q in _ring(p, ab, w, top, bot, n, angles)])
    for r0, r1 in zip(R, R[1:]):
        if len(r0) == 1 and len(r1) == 1: continue
        for k in range(seg):
            k1 = (k + 1) % seg
            if len(r0) == 1: bm.faces.new((r0[0], r1[k1], r1[k]))
            elif len(r1) == 1: bm.faces.new((r0[k], r0[k1], r1[0]))
            else: bm.faces.new((r0[k], r0[k1], r1[k1], r1[k]))
    for r in (R[0], R[-1]):
        if len(r) > 1: bm.faces.new(r)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, (0, 0, 0), outline=outline, kind='organic')


def shell(name, keys, mat, u=(0.0, 1.0), arc=(15, 165), lift=0.015, thick=0.035, flare=0.0, rings=5, seg=10,
          side=(1, 0, 0), up=(0, 0, 1), outline=True, sharp=40, chev=0.0):
    """An armour plate over part of a lofted body: the arc (degrees from the side, 90 = top, 270 = belly) of the
    sections between u[0] and u[1] (0 .. 1 along the keys), `lift` off the surface and `thick` thick.
    arc = ((a0, a1) at u[0], (a0, a1) at u[1]) tapers it; flare lifts the u[1] edge further, so plates in a row overlap
    like shingles; chev pushes the top of the u[1] edge further along (by chev in u), a V-shaped swept edge."""
    if isinstance(arc[0], (tuple, list)): (fa0, fa1), (ba0, ba1) = arc
    else: fa0, fa1 = ba0, ba1 = arc
    rings, seg = rings * 2 + 1, seg * 3            # finely sampled: the bevel keeps the rims crisp, the arcs stay round
    side, up = Vector(side), Vector(up); m = len(keys) - 1
    bm = bmesh.new(); IN, OUT = [], []
    for i in range(rings):
        f = i / (rings - 1)
        a0 = math.radians(fa0 + (ba0 - fa0) * f); a1 = math.radians(fa1 + (ba1 - fa1) * f)
        g = lift + flare * f
        ri, ro = [], []
        for k in range(seg + 1):
            th = a0 + (a1 - a0) * k / seg
            v = max(0.0, math.sin(th)) ** 2                         # the swept point sits on the top line
            uu = u[0] + (u[1] + chev * v - u[0]) * f
            p, w, top, bot, n = _at(keys, min(1.0, max(0.0, uu)) * m)
            t = (_at(keys, min(1.0, uu + 0.01) * m)[0] - _at(keys, max(0.0, uu - 0.01) * m)[0]).normalized()
            ab = _frames([t], side, up)[0]
            ri.append(bm.verts.new(_ring(p, ab, w + g, top + g, bot + g, n, [th])[0] * SC))
            ro.append(bm.verts.new(_ring(p, ab, w + g + thick, top + g + thick, bot + g + thick, n, [th])[0] * SC))
        IN.append(ri); OUT.append(ro)
    for i in range(rings - 1):
        for k in range(seg):
            bm.faces.new((OUT[i][k], OUT[i][k + 1], OUT[i + 1][k + 1], OUT[i + 1][k]))
            bm.faces.new((IN[i][k], IN[i + 1][k], IN[i + 1][k + 1], IN[i][k + 1]))
        for k in (0, seg):
            bm.faces.new((IN[i][k], OUT[i][k], OUT[i + 1][k], IN[i + 1][k]))
    for i in (0, rings - 1):
        for k in range(seg):
            bm.faces.new((IN[i][k], IN[i][k + 1], OUT[i][k + 1], OUT[i][k]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, (0, 0, 0), outline=outline, sharp=sharp)


def blade(name, pts, depth, mat, frame, outline=True, ridge=0.45):
    """A faceted blade: a 2D outline (u, v) bevelled to a sharp edge from a raised centre facet, `depth` thick at the
    centre. frame(u, v, w) maps the blade's plane (u, v) and its thickness axis (w) to model positions."""
    cu = sum(p[0] for p in pts) / len(pts); cv = sum(p[1] for p in pts) / len(pts)
    bm = bmesh.new()
    edge_t = depth * 0.12
    E = [[bm.verts.new(Vector(frame(u, v, s * edge_t)) * SC) for u, v in pts] for s in (-1, 1)]
    C = [[bm.verts.new(Vector(frame(cu + (u - cu) * ridge, cv + (v - cv) * ridge, s * depth / 2)) * SC) for u, v in pts] for s in (-1, 1)]
    n = len(pts)
    for k in range(n):
        k1 = (k + 1) % n
        bm.faces.new((E[0][k], E[1][k], E[1][k1], E[0][k1]))
        for s in (0, 1): bm.faces.new((E[s][k], E[s][k1], C[s][k1], C[s][k]))
    bm.faces.new(C[0]); bm.faces.new(C[1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, (0, 0, 0), outline=outline, sharp=25)


def surface(keys, u, elev, s, off=0.0):
    """A point on a lofted body: u along the keys (0 .. 1), elev degrees above the side line (0 = flank, 90 = top,
    negative = below) on side s (-1 left, +1 right), lifted `off` outward. Returns (point, spine tangent, outward)."""
    m = len(keys) - 1
    p0, p1 = _at(keys, max(0.0, u - 0.01) * m)[0], _at(keys, min(1.0, u + 0.01) * m)[0]
    t = (p1 - p0).normalized()
    p, w, top, bot, n = _at(keys, u * m)
    a, b = _frames([t], Vector((1, 0, 0)), Vector((0, 0, 1)))[0]
    th = math.radians(elev if a.x * s > 0 else 180 - elev)
    q = _ring(p, (a, b), w + off, top + off, bot + off, n, [th])[0]
    return q, t, (q - p).normalized()


def slit_on(name, keys, u, elev, s, length, height, tilt, mat, off=0.004):
    """A glowing eye slit lying on a lofted head: long along the head, its front end dipped `tilt` degrees (a scowl)."""
    q, t, o = surface(keys, u, elev, s, off)
    fwd = Vector((0, -1, 0)); fwd = (fwd - o * fwd.dot(o)).normalized()
    upv = Vector((0, 0, 1)); upv = (upv - o * upv.dot(o) - fwd * upv.dot(fwd)).normalized()
    k = math.radians(tilt)
    l = (fwd * math.cos(k) - upv * math.sin(k)).normalized()
    hgt = o.cross(l).normalized()
    from mathutils import Matrix
    rot = Matrix((o, l, hgt)).transposed().to_quaternion()
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    for v in bm.verts: v.co = Vector((v.co.x * height * 0.8 * SC, v.co.y * length * SC, v.co.z * height * SC))
    for v in bm.verts:          # taper the back end of the slit to a point-ish sliver
        if v.co.y < 0: v.co.z *= 0.45
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=height * 0.2 * SC, segments=1, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, q, quat=rot, smooth=False, outline=False)


def sickle(name, pts, widths, depth, normal, mat, outline=True):
    """A curved faceted blade along a centre line: half-width per point (0 at a tip), a diamond section `depth` thick
    at its widest, lying in the plane whose normal is `normal`. Scythes, curved leg blades, hooked claws."""
    P, widths = _smooth_path(pts, [(w,) for w in widths], per=4); widths = [w[0] for w in widths]
    nrm = Vector(normal).normalized(); wmax = max(widths)
    bm = bmesh.new(); rows = []
    for i, p in enumerate(P):
        t = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
        q = nrm.cross(t).normalized(); w = widths[i]; d = depth * 0.5 * (0.35 + 0.65 * w / wmax)
        if w < 1e-4: rows.append([bm.verts.new(p * SC)]); continue
        rows.append([bm.verts.new(v * SC) for v in (p + q * w, p + nrm * d, p - q * w, p - nrm * d)])
    for a, b in zip(rows, rows[1:]):
        for k in range(4):
            k1 = (k + 1) % 4
            if len(b) == 1: bm.faces.new((a[k], a[k1], b[0]))
            elif len(a) == 1: bm.faces.new((a[0], b[k1], b[k]))
            else: bm.faces.new((a[k], a[k1], b[k1], b[k]))
    for r in (rows[0], rows[-1]):
        if len(r) == 4: bm.faces.new(r)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, (0, 0, 0), outline=outline, sharp=25)


def plane_frame(origin, u_axis, v_axis):
    """frame() for blade(): u along u_axis, v along v_axis, thickness along their cross product."""
    o, U, V = Vector(origin), Vector(u_axis).normalized(), Vector(v_axis).normalized()
    Wd = U.cross(V).normalized()
    return lambda u, v, w: tuple(o + U * u + V * v + Wd * w)


def spike(name, base, tip, r, mat, seg=6, outline=True, bend=(0, 0, 0)):
    """A tapered point from base to tip (claws, teeth, horns); bend pushes its middle sideways for a hook."""
    b, t, k = Vector(base), Vector(tip), Vector(bend)
    pts = [b, b.lerp(t, 0.5) + k, t]
    return sweep(name, pts, [r, r * 0.6, r * 0.04], mat, seg=seg, outline=outline)


# The real robots, posed at frame 1 of their idle and toon-shaded with their own albedo.
# ponytail: the worktree holds LFS pointers only, so the FBX and textures are read from the main checkout.
ASSETS = 'C:/Dev/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation/'
ROBOTS = {'melee': ('AttackRobot', 'MeleeRobot'), 'ranged': ('ShotRobot', 'RangedRobot'), 'tank': ('TankRobot', 'TankRobot')}


def real_robot(kind='melee', height=1.55, at=(0, 0, 0), yaw=0.0, name='robot'):
    fbx, tex = ROBOTS[kind]
    root = begin(name, 1.0, at=at, yaw=yaw)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=ASSETS + f'FBX/{fbx}.fbx')
    new = [o for o in bpy.data.objects if o not in before]
    bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
    src = next(o for o in new if o.type == 'MESH')
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(dg), depsgraph=dg)
    me.transform(src.matrix_world)
    for o in new: bpy.data.objects.remove(o)
    xs = [v.co for v in me.vertices]
    lo = Vector((min(c.x for c in xs), min(c.y for c in xs), min(c.z for c in xs)))
    hi = Vector((max(c.x for c in xs), max(c.y for c in xs), max(c.z for c in xs)))
    k = height / (hi.z - lo.z)
    for v in me.vertices: v.co = Vector(((v.co.x - (lo.x + hi.x) / 2) * k, (v.co.y - (lo.y + hi.y) / 2) * k, (v.co.z - lo.z) * k))
    me.materials.clear(); me.materials.append(toon((255, 255, 255), tex=ASSETS + f'Textures/{tex}_Albedo.tga'))
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new(name + '_mesh', me); bpy.context.scene.collection.objects.link(ob)
    ob.parent = root
    _hull(ob)
    blob('blob', 0.62, 0.62)
    return root


def tris(root):
    """Triangles of the design itself (no outline hulls, no ground blob)."""
    return sum(len(p.vertices) - 2 for o in root.children_recursive
               if o.type == 'MESH' and not o.name.endswith('_hull') and 'blob' not in o.name for p in o.data.polygons)


_font = None


def blob(name, rx, ry, loc=(0, 0, 0.003)):
    bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, segments=48, radius=1.0)
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free(); me.materials.append(blob_mat())
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    ob.parent = ROOT; ob.location = _v(loc); ob.scale = (rx * SC, ry * SC, 1)
    return ob


# ---------------------------------------------------------------- cameras

def ortho(elev, azim, target, scale, res):
    """azim from the front (-y) toward the model's left (-x), degrees."""
    s = bpy.context.scene; cam = s.camera
    e, a = math.radians(elev), math.radians(azim)
    d = Vector((-math.sin(a) * math.cos(e), -math.cos(a) * math.cos(e), math.sin(e)))
    cam.data.type = 'ORTHO'; cam.data.ortho_scale = scale; cam.data.clip_end = 200
    cam.location = Vector(target) + d * 40
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = (-d).to_track_quat('-Z', 'Y')
    s.render.resolution_x, s.render.resolution_y = res; s.render.resolution_percentage = 100


def render(path):
    s = bpy.context.scene; s.render.filepath = path
    bpy.ops.render.render(write_still=True)
