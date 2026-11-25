# Toon look of the robots' CharacterSurface shader, rebuilt in Blender EEVEE for still drafts:
# three cel bands (shadow / mid / light, tints sampled from the robot icons) and an inverted-hull navy outline.
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
    global ROOT, HULLS
    bpy.ops.wm.read_factory_settings(use_empty=True)
    global _font
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


def toon_img(path):
    """toon() with the albedo read from a texture: the real robots' *_Albedo.tga."""
    m = toon((255, 255, 255)).copy(); m.name = 'toon_' + path.rsplit('\\', 1)[-1]
    N = m.node_tree.nodes
    tex = N.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(path)
    alb = next(n for n in N if n.type == 'MIX' and n.blend_type == 'MULTIPLY')
    m.node_tree.links.new(tex.outputs['Color'], alb.inputs[7])
    return m


def toon(rgb, name=None):
    key = ('toon', rgb)
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
    L.new(mix.outputs[2], alb.inputs[6])
    em = N.new('ShaderNodeEmission'); L.new(alb.outputs[2], em.inputs[0])
    L.new(em.outputs[0], out.inputs[0])
    _mats[key] = m
    return m


def flat(rgb, strength=1.0):
    """Unlit colour: eye dots and indicator lights (a gentle self-lit look, nothing glows past its colour)."""
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


def _obj(name, bm, mat, loc, rot=None, quat=None, smooth=None, outline=True):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    if smooth is not None:
        for p in me.polygons: p.use_smooth = smooth(p) if callable(smooth) else smooth
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(ob)
    ob.parent = ROOT; ob.location = _v(loc)
    if quat is not None: ob.rotation_mode = 'QUATERNION'; ob.rotation_quaternion = quat
    elif rot is not None: ob.rotation_euler = tuple(math.radians(a) for a in rot)
    if outline: _hull(ob)
    return ob


def _hull(ob):
    me = ob.data.copy(); me.materials.clear(); me.materials.append(hull_mat())
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=1e-4)   # imported meshes split verts at seams: weld so the hull doesn't crack
    bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    h = bpy.data.objects.new(ob.name + '_hull', me); bpy.context.scene.collection.objects.link(h)
    h.parent = ob.parent; h.location = ob.location; h.rotation_mode = ob.rotation_mode
    h.rotation_euler = ob.rotation_euler; h.rotation_quaternion = ob.rotation_quaternion

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


def wedge(name, size, loc, mat, front=(1, 1), back=(1, 1), drop=0.0, r=0.08, seg=2, rot=None, outline=True):
    """rbox whose front (-y) and back (+y) faces are scaled (x, z) and whose front face drops by `drop`: noses, tapered torsos."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    sx, sy, sz = (s * SC for s in size)
    for v in bm.verts:
        fx, fz = front if v.co.y < 0 else back
        v.co = Vector((v.co.x * sx * fx, v.co.y * sy, v.co.z * sz * fz - (drop * SC if v.co.y < 0 else 0)))
    rr = min(r * SC, min(sx * min(front[0], back[0]), sy, sz * min(front[1], back[1])) * 0.49)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=rr, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth=False, outline=outline)


def ball(name, r, loc, mat, scale=(1, 1, 1), rot=None, outline=True):
    u = max(8, min(18, round(r * SC * max(scale) * 34)))          # fewer segments on small parts: keeps the triangle count low
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=max(5, u // 2 + 1), radius=r * SC)
    for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    return _obj(name, bm, mat, loc, rot=rot, smooth=True, outline=outline)


def tube(name, p0, p1, r, mat, r1=None, outline=True, bevel=0.0):
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=max(8, min(16, round(max(r, r1 or 0) * SC * 70))), radius1=r * SC, radius2=(r1 if r1 is not None else r) * SC, depth=d.length * SC)
    if bevel:
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and e.link_faces[0].normal.angle(e.link_faces[1].normal) > 0.9]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel * SC, segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return _obj(name, bm, mat, (p0 + p1) / 2, quat=q, smooth=lambda p: abs(p.normal.z) < 0.95, outline=outline)


def disc(name, r, loc, mat, facing=(0, -1, 0), depth=0.02, scale=(1, 1), outline=False):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=28, radius1=r * SC, radius2=r * SC, depth=depth * SC)
    for v in bm.verts: v.co.x *= scale[0]; v.co.y *= scale[1]
    q = Vector((0, 0, 1)).rotation_difference(Vector(facing).normalized())
    return _obj(name, bm, mat, loc, quat=q, smooth=False, outline=outline)


def prism(name, pts, depth, loc, mat, r=0.05, rot=None, outline=True, seg=3):
    """A 2D outline in the x/z plane, given thickness along y (front faces -y): diamonds, triangles, fins."""
    bm = bmesh.new()
    vs = [bm.verts.new((x * SC, -depth * SC / 2, z * SC)) for x, z in pts]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in (g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)): v.co.y += depth * SC
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if r: bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=r * SC, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth=False, outline=outline)


_font = None


def text(name, body, size, loc, mat, rot=(90, 0, 0), depth=0.012):
    global _font
    if _font is None: _font = bpy.data.fonts.load('C:/Windows/Fonts/ARLRDBD.TTF')
    cu = bpy.data.curves.new(name, 'FONT'); cu.body = body; cu.font = _font
    cu.size = size * SC; cu.extrude = depth * SC; cu.align_x = 'CENTER'; cu.align_y = 'CENTER'
    ob = bpy.data.objects.new(name, cu); bpy.context.scene.collection.objects.link(ob)
    ob.data.materials.append(mat)
    ob.parent = ROOT; ob.location = _v(loc); ob.rotation_euler = tuple(math.radians(a) for a in rot)
    return ob


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
