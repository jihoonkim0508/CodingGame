# Toon look of the robots' CharacterSurface shader, rebuilt in Blender EEVEE for still drafts:
# three cel bands (shadow / mid / light, tints sampled from the robot icons) and an inverted-hull navy outline.
import bpy, bmesh, math
from mathutils import Vector, Matrix

OUTLINE = (23, 31, 41)                 # the robots' _OutlineColor (0.09, 0.12, 0.16), as it shows on the icons
LIGHT = (0.95, 0.98, 1.00)             # white body on Warrior.png: lit 242,250,255
MID = (0.84, 0.88, 0.94)               # mid 214,225,239
SHADOW = (0.71, 0.76, 0.87)            # shadow 180,195,221

SC = 1.0                               # size multiplier of the design being built
HI = True                              # image drafts: every part built at render resolution (more segments, rounded rims,
                                       # smoothed paths); False builds the plain draft mesh, for its triangle count
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
    if smooth == 'auto': _sharp(me)
    elif smooth is not None:
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
    if HI: seg = max(seg, 4)
    bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=rr, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth='auto', outline=outline)


def ball(name, r, loc, mat, scale=(1, 1, 1), rot=None, outline=True, seg=(20, 12), smooth=True, half=False):
    """A (squashed) sphere; half keeps only the top half: domes, caps (seg[1] even keeps an equator ring)."""
    if HI: seg = (max(seg[0] * 2, 24), max(seg[1] * 2, 16))
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
    if HI: segs = max(segs * 2, 32); bevel = bevel or min(r, r1 or r) * 0.12
    bmesh.ops.create_cone(bm, cap_ends=True, segments=segs, radius1=r * SC, radius2=(r1 if r1 is not None else r) * SC, depth=d.length * SC)
    if bevel:
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and e.link_faces[0].normal.angle(e.link_faces[1].normal) > 0.9]
        bmesh.ops.bevel(bm, geom=rim, offset=bevel * SC, segments=3 if HI else 2, profile=0.5, affect='EDGES', clamp_overlap=True)
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    return _obj(name, bm, mat, (p0 + p1) / 2, quat=q, smooth='auto', outline=outline)


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
    if HI: seg = max(seg, 3)
    if r: bmesh.ops.bevel(bm, geom=bm.verts[:] + bm.edges[:], offset=r * SC, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth='auto', outline=outline)


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


# ---------------------------------------------------------------- round 3: sculpted parts (bevels, shells, rocks, skin)

def _sharp(me, deg=40):
    """Smooth shading with hard edges past deg: bevels read round, plate rims stay crisp."""
    for p in me.polygons: p.use_smooth = True
    me.set_sharp_from_angle(angle=math.radians(deg))


def _evaluated(ob):
    """The object's mesh with its modifiers applied, as a bmesh; the temp object is removed."""
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg), depsgraph=dg)
    src = ob.data; bpy.data.objects.remove(ob); bpy.data.meshes.remove(src)
    bm = bmesh.new(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    return bm


def _subsurf(bm, levels):
    me = bpy.data.meshes.new('tmp'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('tmp', me); bpy.context.scene.collection.objects.link(ob)
    m = ob.modifiers.new('s', 'SUBSURF'); m.levels = m.render_levels = levels
    return _evaluated(ob)


def sbox(name, size, loc, mat, r=0.1, levels=2, rot=None, outline=True, taper=(1, 1), shear=(0, 0)):
    """A soft box: one bevel then Catmull-Clark, so it reads sculpted rather than primitive.
    taper scales the top face (x, y); shear slides the top face (x, y) in units: wedges, slanted plates."""
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    sx, sy, sz = (s * SC for s in size)
    for v in bm.verts:
        top = v.co.z > 0
        t = taper if top else (1, 1)
        v.co = Vector((v.co.x * sx * t[0] + (shear[0] * SC if top else 0), v.co.y * sy * t[1] + (shear[1] * SC if top else 0), v.co.z * sz))
    rr = min(r * SC, min(sx * min(taper[0], 1), sy * min(taper[1], 1), sz) * 0.45)
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=rr, segments=1, profile=0.5, affect='EDGES', clamp_overlap=True)
    bm = _subsurf(bm, max(levels, 2) if HI else levels)
    return _obj(name, bm, mat, loc, rot=rot, smooth='auto', outline=outline)


def shell(name, r, loc, mat, lon=(-60, 60), lat=(-20, 70), thick=0.05, seg=(14, 8), rot=None, outline=True, flare=0.0):
    """A patch of an ellipsoid shell (radii r = (rx, ry, rz)), lon/lat in degrees (lon 0 faces front -y, lat 90 is the top),
    given thickness: pauldrons, carapace segments, helmet shells. flare pushes the lower rim outward."""
    bm = bmesh.new(); rows = []
    nu, nv = (min(seg[0] * 2, 48), min(seg[1] * 2, 24)) if HI else seg
    for j in range(nv + 1):
        la = math.radians(lat[0] + (lat[1] - lat[0]) * j / nv)
        k = 1 + flare * (1 - j / nv)
        row = []
        for i in range(nu + 1):
            lo = math.radians(lon[0] + (lon[1] - lon[0]) * i / nu)
            row.append(bm.verts.new(Vector((r[0] * math.cos(la) * math.sin(lo) * k, -r[1] * math.cos(la) * math.cos(lo) * k,
                                            r[2] * math.sin(la))) * SC))
        rows.append(row)
    for a, b in zip(rows, rows[1:]):
        for i in range(nu): bm.faces.new((a[i], a[i + 1], b[i + 1], b[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.solidify(bm, geom=bm.faces[:], thickness=thick * SC)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if HI:
        rim = [e for e in bm.edges if len(e.link_faces) == 2 and e.link_faces[0].normal.angle(e.link_faces[1].normal) > 1.0]
        bmesh.ops.bevel(bm, geom=rim, offset=thick * SC * 0.3, segments=3, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth='auto', outline=outline)


def rock(name, size, loc, mat, seed=1, cuts=7, depth=0.35, chamfer=0.025, rot=None, outline=True):
    """A chunky faceted stone: a box with corners and edges sliced off at random, then chamfered."""
    import random
    rnd = random.Random(seed)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1)
    sx, sy, sz = (s * SC for s in size)
    for v in bm.verts: v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
    for _ in range(cuts):
        n = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))).normalized()
        h = 0.5 * (abs(n.x) * sx + abs(n.y) * sy + abs(n.z) * sz)       # the box's reach along n
        d = h * (1 - rnd.uniform(0.12, depth))
        res = bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=n * d, plane_no=n, clear_outer=True)
        edges = [e for e in bm.edges if len(e.link_faces) < 2]
        if edges: bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    bmesh.ops.remove_doubles(bm, verts=bm.verts[:], dist=0.004 * SC)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if chamfer:
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=chamfer * SC, segments=3 if HI else 1, profile=0.5, affect='EDGES', clamp_overlap=True)
    return _obj(name, bm, mat, loc, rot=rot, smooth='auto' if HI else False, outline=outline)


def skin(name, pts, edges, radii, mat, levels=2, outline=True):
    """An organic form from a stick skeleton (points, edges, a radius or (rx, ry) per point): skin modifier + subdivision."""
    me = bpy.data.meshes.new(name + '_skel'); me.from_pydata([tuple(Vector(p) * SC) for p in pts], edges, [])
    ob = bpy.data.objects.new(name + '_skel', me); bpy.context.scene.collection.objects.link(ob)
    ob.modifiers.new('skin', 'SKIN')
    sv = me.skin_vertices[0].data
    for i, r in enumerate(radii):
        rx, ry = (r, r) if not isinstance(r, (tuple, list)) else r
        sv[i].radius = (rx * SC, ry * SC)
    sv[0].use_root = True
    m = ob.modifiers.new('sub', 'SUBSURF'); m.levels = m.render_levels = levels + (1 if HI else 0)
    bm = _evaluated(ob)
    return _obj(name, bm, mat, (0, 0, 0), smooth=True, outline=outline)


def _densify(P, radii, k=4):
    """A Catmull-Rom path through the points, k steps per span, radii blended along: bends read round, not stepped."""
    def lerp(a, b, t):
        return tuple(x + (y - x) * t for x, y in zip(a, b)) if isinstance(a, (tuple, list)) else a + (b - a) * t
    n = len(P); outp, outr = [], []
    for i in range(n - 1):
        p0, p1, p2, p3 = P[max(i - 1, 0)], P[i], P[i + 1], P[min(i + 2, n - 1)]
        ra, rb = radii[i], radii[i + 1]
        if isinstance(ra, (tuple, list)) != isinstance(rb, (tuple, list)):
            ra = ra if isinstance(ra, (tuple, list)) else (ra, ra)
            rb = rb if isinstance(rb, (tuple, list)) else (rb, rb)
        for j in range(k):
            t = j / k
            outp.append(0.5 * (2 * p1 + (p2 - p0) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t * t + (3 * p1 - p0 - 3 * p2 + p3) * t ** 3))
            outr.append(lerp(ra, rb, t))
    outp.append(P[-1]); outr.append(radii[-1])
    return outp, outr


def sweep(name, pts, radii, mat, seg=12, outline=True, up=(0, 0, 1)):
    """A tube along a polyline with a radius, or (r across, r along up), per point: horns, claws, tusks, cables."""
    P = [Vector(p) * SC for p in pts]
    if HI:
        seg = max(seg * 2, 20)
        P, radii = _densify(P, radii)
    bm = bmesh.new(); rings = []; a = None
    for i, p in enumerate(P):
        t = (P[min(i + 1, len(P) - 1)] - P[max(i - 1, 0)]).normalized()
        if a is None:
            a = Vector(up).cross(t)
            if a.length < 1e-3: a = Vector((1, 0, 0)).cross(t)
        else:
            a = a - t * a.dot(t)                 # carry the frame along the path: no flip, no pinched twist on tight bends
        a.normalize(); b = t.cross(a)
        ra, rb = (radii[i], radii[i]) if not isinstance(radii[i], (tuple, list)) else radii[i]
        rings.append([bm.verts.new(p + (a * math.cos(k * 2 * math.pi / seg) * ra + b * math.sin(k * 2 * math.pi / seg) * rb) * SC)
                      for k in range(seg)])
    for r0, r1 in zip(rings, rings[1:]):
        for k in range(seg): bm.faces.new((r0[k], r0[(k + 1) % seg], r1[(k + 1) % seg], r1[k]))
    bm.faces.new(rings[0]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, (0, 0, 0), smooth='auto', outline=outline)


def bentprism(name, pts, depth, loc, mat, bend=1.5, r=0.03, rot=None, outline=True, cuts=12, seg=2):
    """prism() bent round a vertical axis behind it (radius bend): shields and blades of any outline, curved like a riot shield."""
    bm = bmesh.new()
    vs = [bm.verts.new((x * SC, -depth * SC / 2, z * SC)) for x, z in pts]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in (g for g in ext['geom'] if isinstance(g, bmesh.types.BMVert)): v.co.y += depth * SC
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    if HI: seg, cuts = max(seg, 3), max(cuts * 2, 16)
    if r: bmesh.ops.bevel(bm, geom=bm.edges[:], offset=r * SC, segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    x0, x1 = min(x for x, _ in pts) * SC, max(x for x, _ in pts) * SC
    for i in range(1, cuts):
        x = x0 + (x1 - x0) * i / cuts
        bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(x, 0, 0), plane_no=(1, 0, 0))
    R = bend * SC
    for v in bm.verts:
        th = v.co.x / R; rad = R - v.co.y
        v.co.x = rad * math.sin(th); v.co.y = R - rad * math.cos(th)
    return _obj(name, bm, mat, loc, rot=rot, smooth='auto', outline=outline)


def torus(name, R, r, loc, mat, rot=None, seg=(20, 8), scale=(1, 1, 1), outline=True, axis=None):
    """A ring (radius R, tube radius r) round the z axis, or round axis; scale z > 1 makes a flat band: bracers, belts, chain links."""
    if axis is not None:
        q = Vector((0, 0, 1)).rotation_difference(Vector(axis).normalized())
    else:
        q = None
    if HI: seg = (max(seg[0] * 2, 32), max(seg[1] * 2, 12))
    bm = bmesh.new(); rings = []
    for i in range(seg[0]):
        a = 2 * math.pi * i / seg[0]
        rings.append([bm.verts.new(Vector(((R + r * math.cos(b)) * math.cos(a) * scale[0], (R + r * math.cos(b)) * math.sin(a) * scale[1],
                                           r * math.sin(b) * scale[2])) * SC)
                      for b in (2 * math.pi * j / seg[1] for j in range(seg[1]))])
    for i in range(seg[0]):
        a, b = rings[i], rings[(i + 1) % seg[0]]
        for j in range(seg[1]): bm.faces.new((a[j], b[j], b[(j + 1) % seg[1]], a[(j + 1) % seg[1]]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return _obj(name, bm, mat, loc, rot=rot, quat=q, smooth='auto', outline=outline)


def bent_y(x, bend):
    """How far back (+y) a bentprism / arcplate surface sits at x: to seat studs and trims on it."""
    return bend - math.sqrt(max(bend * bend - x * x, 0))


def part_tris(root):
    """Triangles per part, largest first: to see where a design's budget goes."""
    d = [(sum(len(p.vertices) - 2 for p in o.data.polygons), o.name) for o in root.children_recursive
         if o.type == 'MESH' and not o.name.endswith('_hull') and 'blob' not in o.name]
    return sorted(d, reverse=True)