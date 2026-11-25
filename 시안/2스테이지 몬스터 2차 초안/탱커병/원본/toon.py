# Toon look of the robots' CharacterSurface shader, rebuilt in Blender EEVEE for still drafts:
# three cel bands (shadow / mid / light, tints sampled from the robot icons) and an inverted-hull navy outline.
import bpy, bmesh, math
from mathutils import Vector, Matrix

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
    _mats.clear(); _stack.clear(); HULLS = []; ROOT = None; _font = None
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


def toon(rgb, name=None, image=None):
    """rgb: flat albedo (sRGB 0-255); image: an albedo texture path instead (the real robots' *_Albedo.tga)."""
    key = ('toon', rgb, image)
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
    if image:
        tex = N.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(image, check_existing=True)
        L.new(tex.outputs['Color'], alb.inputs[7])
    else:
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
    bm = bmesh.new(); bm.from_mesh(me); bmesh.ops.reverse_faces(bm, faces=bm.faces[:]); bm.to_mesh(me); bm.free()
    h = bpy.data.objects.new(ob.name + '_hull', me); bpy.context.scene.collection.objects.link(h)
    h.parent = ob.parent; h.location = ob.location; h.rotation_mode = ob.rotation_mode
    h.rotation_euler = ob.rotation_euler; h.rotation_quaternion = ob.rotation_quaternion

    d = h.modifiers.new('hull', 'DISPLACE'); d.direction = 'NORMAL'; d.mid_level = 0; d.strength = -0.02
    HULLS.append(d)


ROBOT_DIR = 'C:/Dev/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation/'
ROBOTS = {'melee': ('AttackRobot', 'MeleeRobot'), 'ranged': ('ShotRobot', 'RangedRobot'), 'tank': ('TankRobot', 'TankRobot')}
GAME = 1.55 / 2.156   # the melee robot FBX (2.156 tall) shows as the 1.55-unit robot of the last round (~118 px in battle)


def real_robot(kind='melee', at=(0, 0, 0), yaw=0.0, frame=1):
    """A real robot FBX in its idle pose, frozen to a static mesh, with its albedo texture, cel bands and outline hull."""
    fbx, tex = ROBOTS[kind]
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=ROBOT_DIR + f'FBX/{fbx}.fbx')
    new = [o for o in bpy.data.objects if o not in before]
    bpy.context.scene.frame_set(frame); bpy.context.view_layer.update()
    root = begin(f'robot_{kind}', 1.0, at=at, yaw=yaw)
    dg = bpy.context.evaluated_depsgraph_get()
    for o in new:
        if o.type != 'MESH': continue
        me = bpy.data.meshes.new_from_object(o.evaluated_get(dg), depsgraph=dg)
        me.transform(o.matrix_world); me.transform(Matrix.Scale(GAME, 4))
        me.materials.clear(); me.materials.append(toon((255, 255, 255), image=ROBOT_DIR + f'Textures/{tex}_Albedo.tga'))
        ob = bpy.data.objects.new(o.name + '_static', me); bpy.context.scene.collection.objects.link(ob)
        ob.parent = root
        _hull(ob)
    for o in new: bpy.data.objects.remove(o, do_unlink=True)
    return root


def set_line(world_thickness):
    for d in HULLS: d.strength = -world_thickness


_stack = []


def group(name, loc=(0, 0, 0), rot=(0, 0, 0)):
    """The next parts hang under a moved/tilted empty (a leaning torso, a tilted shield) until ungroup()."""
    global ROOT
    e = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(e)
    e.parent = ROOT; e.location = _v(loc); e.rotation_euler = tuple(math.radians(a) for a in rot)
    _stack.append(ROOT); ROOT = e


def ungroup():
    global ROOT
    ROOT = _stack.pop()


def rbox(name, size, loc, mat, r=0.08, seg=2, rot=None, outline=True, taper=(1, 1)):
    """A rounded box; taper scales its top face (x, y), for frustums like a weight or a wedge."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    sx, sy, sz = (s * SC for s in size)
    for v in bm.verts:
        t = taper if v.co.z > 0 else (1, 1)
        v.co = Vector((v.co.x * sx * t[0], v.co.y * sy * t[1], v.co.z * sz))
    rr = min(r * SC, min(sx * min(taper[0], 1), sy * min(taper[1], 1), sz) * 0.49)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=rr, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth=False, outline=outline)


def ball(name, r, loc, mat, scale=(1, 1, 1), rot=None, outline=True, seg=(20, 12), smooth=True, half=False):
    """A (squashed) sphere; half keeps only the top half: domes, caps (seg[1] even keeps an equator ring)."""
    bm = bmesh.new(); bmesh.ops.create_uvsphere(bm, u_segments=seg[0], v_segments=seg[1], radius=r * SC)
    if half:
        bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z < -1e-5], context='VERTS')
        bmesh.ops.holes_fill(bm, edges=bm.edges[:], sides=0)   # a flat bottom, so the outline hull can't show inside
    for v in bm.verts: v.co = Vector((v.co.x * scale[0], v.co.y * scale[1], v.co.z * scale[2]))
    return _obj(name, bm, mat, loc, rot=rot, smooth=smooth, outline=outline)


def arcplate(name, size, loc, mat, bend=1.2, r=0.05, rot=None, outline=True, cuts=6):
    """A rounded plate (size x, y=thickness, z) bent around a vertical axis behind it (radius bend):
    the middle stands forward (-y), the sides curl back. Riot shields, dozer blades (negative bend: concave front)."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    sx, sy, sz = (s * SC for s in size)
    for v in bm.verts: v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    rr = min(r * SC, min(sx, sy, sz) * 0.49)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=rr, segments=2, profile=0.5, affect='EDGES', clamp_overlap=True)
    for i in range(1, cuts):
        x = -sx / 2 + sx * i / cuts
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(x, 0, 0), plane_no=(1, 0, 0))
    R = bend * SC
    for v in bm.verts:
        th = v.co.x / R; rad = R - v.co.y
        v.co.x = rad * math.sin(th); v.co.y = R - rad * math.cos(th)
    return _obj(name, bm, mat, loc, rot=rot, smooth=False, outline=outline)


def tris(root):
    """Triangle count of a design (outline hulls are the shader's job in game, so they don't count)."""
    return sum(len(p.vertices) - 2 for o in root.children_recursive if o.type == 'MESH' and not o.name.endswith('_hull')
               and 'blob' not in o.name for p in o.data.polygons)


def tube(name, p0, p1, r, mat, r1=None, outline=True, bevel=0.0, segs=16):
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0)
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r * SC, radius2=(r1 if r1 is not None else r) * SC, depth=d.length * SC)
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
