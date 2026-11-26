# Blender renders for the colour drafts, built on the round-3 models (시안/2스테이지 몬스터 3차 초안/<folder>/원본).
# blender -b --factory-startup -P render.py -- <imp|raptor> views <out>    each colourway alone: big 3/4 and cmp (one scale),
#                                                                         and the real melee robot at the cmp scale
# blender -b --factory-startup -P render.py -- <imp|raptor> battle <out>   the six beside the real robot, battle camera
# blender -b --factory-startup -P render.py -- tank check <out> [gy]      prints the 1.7x shield's clearances
# blender -b --factory-startup -P render.py -- tank views <out>           t1 (round 3) and t1big: big 3/4, 3/4 back
# blender -b --factory-startup -P render.py -- tank compare <out>         TankRobot, t1, t1big at one scale: 3/4 and side rows
# blender -b --factory-startup -P render.py -- tank battle <out>          both robots, t1 and t1big, battle camera
# blender -b --factory-startup -P render.py -- tank pairs <out>           t1big beside the TankRobot, battle camera (sheet crop)
# blender -b --factory-startup -P render.py -- tank neck <out>            t1neck (neck.py): big 3/4 and beside the TankRobot
#                                                                         through the battle camera; neck.json says if its eyes clear the shield
import bpy, sys, os, math, json, re
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from bpy_extras.object_utils import world_to_camera_view

HERE = os.path.dirname(os.path.abspath(__file__))
args = sys.argv[sys.argv.index('--') + 1:]
who, mode, out = args[:3]
R3 = os.path.join(HERE, '..', '..', '2스테이지 몬스터 3차 초안', {'imp': '일반', 'raptor': '빠른 적', 'tank': '탱커병'}[who], '원본')
sys.path[:0] = [os.path.abspath(R3), HERE]
import toon as T
import models as M
from colours import COLOURWAYS

os.makedirs(out, exist_ok=True)
BATTLE_SUN = (0.287, -0.497, 0.819)        # the battle light, Euler(55, -30, 0), seen from the camera side
K = 1.27 / 1.55                            # the in-game robot stands about 1.27 units tall: design to game units
DIST = 32.9 / K                            # the battle camera's distance, in design units
IMP_SC = 1.25                              # the imp 1.25x round 3 (~78 px in battle -> ~97 px)


def parts(root):
    return [o for o in root.children_recursive if o.type == 'MESH' and 'blob' not in o.name and not o.name.endswith('_hull')]


def bounds(root):
    dg = bpy.context.evaluated_depsgraph_get(); pts = []
    for o in parts(root):
        me = o.evaluated_get(dg).to_mesh()
        pts += [o.matrix_world @ v.co for v in me.vertices]
        o.evaluated_get(dg).to_mesh_clear()
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def screen_box(s, root, w):
    dg = bpy.context.evaluated_depsgraph_get(); xs, ys = [], []
    for o in parts(root):
        me = o.evaluated_get(dg).to_mesh()
        for v in me.vertices:
            p = world_to_camera_view(s, s.camera, o.matrix_world @ v.co)
            xs.append(p.x * w); ys.append((1 - p.y) * 1080)
        o.evaluated_get(dg).to_mesh_clear()
    return [min(xs), min(ys), max(xs), max(ys)]


def battle_cam(s, look, w=1920):
    cam = s.camera; cam.data.type = 'PERSP'; cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(19)
    cam.data.clip_end = 500
    fwd = Vector((0, math.cos(math.radians(47.5)), -math.sin(math.radians(47.5))))
    cam.location = Vector(look) - fwd * DIST
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
    s.render.resolution_x, s.render.resolution_y = w, 1080
    T.set_line(1.8 * DIST * 2 * math.tan(math.radians(9.5)) / 1080)   # the shader's 1.8 px outline at the camera's distance


def desk(x0, x1, roads):
    """Stage 2's wood desk island with cream roads (y centre, width), as in round 3."""
    T.begin('desk', 1.0)
    w, c = x1 - x0, (x0 + x1) / 2
    T.rbox('edge', (w, 10.4, 0.6), (c, 1.0, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
    T.rbox('wood', (w, 10.4, 0.04), (c, 1.0, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
    for y, rw in roads:
        T.rbox(f'road{y}', (w, rw, 0.02), (c, y, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)


# ---------------------------------------------------------------- imp and raptor: one model, six colourways

def build(**kw):
    if who == 'imp': M.imp(sc=IMP_SC, **kw)
    else: M.raptor(**kw)
    return T.ROOT


def originals(root):
    """Each part with the round-3 material key it was built with: ('toon' | 'flat', rgb, ...)."""
    by = {m.name: k for k, m in T._mats.items()}
    return [(o, by.get(o.data.materials[0].name)) for o in parts(root)]


def paint(orig, cw):
    for o, k in orig:
        if not k: continue
        rgb = cw['map'].get(k[1], k[1])
        if k[0] == 'toon':
            rgb = next((c for pat, c in cw.get('parts', ()) if re.match(pat, o.name)), rgb)
            o.data.materials[0] = T.toon(rgb)
        else:
            o.data.materials[0] = T.flat(rgb)


# view: elevation, azimuth, px per design unit, outline px (the same as round 3's sheets)
VIEWS = {'imp': {'big': (25, 35, 470, 2.4), 'cmp': (25, 35, 190, 1.8)},
         'raptor': {'big': (32, 55, 380, 2.2), 'cmp': (32, 55, 118, 1.6)}}
VIEW_SUN = {'imp': (-0.75, -0.45, 0.85), 'raptor': (-0.3, -0.3, 0.9)}
CWS = COLOURWAYS.get(who, [])

if who in ('imp', 'raptor') and mode == 'views':
    info = {}
    for n in ('robot', 'cw'):
        T.reset(); T.set_sun(VIEW_SUN[who])
        root = T.real_robot('melee') if n == 'robot' else build()
        bpy.context.view_layer.update()
        lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.12
        info[n] = {'size': [round(v, 3) for v in hi - lo]}
        orig = originals(root)
        for i, cw in enumerate(CWS if n == 'cw' else [None], 1):
            if cw: paint(orig, cw)
            for view, (elev, azim, zoom, line) in VIEWS[who].items():
                if n == 'robot' and view == 'big': continue
                T.set_line(line / zoom)
                T.ortho(elev, 25 if (n == 'robot' and who == 'raptor') else azim, c, size, (int(size * zoom), int(size * zoom)))
                T.render(os.path.join(out, f'{n}{i if cw else ""}_{view}.png'))
    json.dump(info, open(os.path.join(out, 'info.json'), 'w'), indent=1)

elif who in ('imp', 'raptor') and mode == 'battle':
    s = T.reset(); T.set_sun(BATTLE_SUN)
    spots = {}
    if who == 'imp':
        # back road: walking left along it (side-on, as they move in game); front road: turned toward the camera
        desk(-14.4, 8.6, ((2.6, 2.6), (-1.6, 2.6)))
        for row, y, yaw in (('walk', 2.6, -90), ('face', -1.6, 40)):
            spots[f'robot_{row}'] = T.real_robot('melee', at=(-11.4, y + (1.9 if row == 'walk' else 1.7), 0), yaw=-20, name=f'robot_{row}')
            for i, cw in enumerate(CWS, 1):
                root = build(at=(-8.0 + 2.45 * (i - 1), y, 0), yaw=yaw); paint(originals(root), cw)
                spots[f'{i}_{row}'] = root
        look = (-3.6, 0.9, 0.35)
    else:
        desk(-15.0, 8.0, ((2.6, 2.6), (-1.6, 2.6)))
        for row, y in (('a', 2.6), ('b', -1.6)):
            spots[f'robot_{row}'] = T.real_robot('melee', at=(-10.6, y + 1.9, 0), yaw=-20, name=f'robot_{row}')
            for j in range(3):
                i = j + 1 + (3 if row == 'b' else 0)
                root = build(at=(-6.6 + 4.1 * j, y, 0), yaw=-90); paint(originals(root), CWS[i - 1])   # running left, toward the robots
                spots[f'{i}_{row}'] = root
        look = (-4.0, 0.9, 0.35)
    battle_cam(s, look)
    bpy.context.view_layer.update()
    marks = {k: screen_box(s, r, 1920) for k, r in spots.items()}
    for k, b in marks.items(): print('BATTLE', k, 'h %.0f w %.0f px' % (b[3] - b[1], b[2] - b[0]))
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))

# ---------------------------------------------------------------- tank: the 1.7x shield

if who == 'tank':
    import shield as SH
    # t1mid: only a reference column on the before/after sheet, 1.3x long = 1.7x the area
    ALL = {'t1': M.t1, 't1big': SH.t1big, 't1mid': lambda **kw: SH.t1big(k=1.3, gy=SH.GY_MID, **kw),
           'robot_tank': M.robot_tank, 'robot_melee': M.robot_melee}

if who == 'tank' and mode == 'check':
    gy = float(args[3]) if len(args) > 3 else -1.02
    k = float(args[4]) if len(args) > 4 else SH.K
    for n in ('t1', 't1big'):
        T.reset(); root = ALL[n]() if n == 't1' else SH.t1big(gy=gy, k=k); bpy.context.view_layer.update()
        dg = bpy.context.evaluated_depsgraph_get()
        sh = [o for o in parts(root) if o.parent and o.parent.name.startswith('shield')]
        body = [o for o in parts(root) if o not in sh]
        lo, hi = bounds(root)
        slo = min((o.matrix_world @ v.co).z for o in sh for v in o.evaluated_get(dg).to_mesh().vertices)
        shi = max((o.matrix_world @ v.co).z for o in sh for v in o.evaluated_get(dg).to_mesh().vertices)
        sx = [(o.matrix_world @ v.co).x for o in sh for v in o.evaluated_get(dg).to_mesh().vertices]
        print(f'CHECK {n}: model z {lo.z:.3f}..{hi.z:.3f}  shield z {slo:.3f}..{shi:.3f} (h {shi - slo:.3f}) x {min(sx):.3f}..{max(sx):.3f}')

        def tree(o):
            me = o.evaluated_get(dg).to_mesh(); me.transform(o.matrix_world)
            t = BVHTree.FromPolygons([v.co.copy() for v in me.vertices], [tuple(p.vertices) for p in me.polygons])
            o.evaluated_get(dg).to_mesh_clear(); return t
        st = {o.name: tree(o) for o in sh}
        for b in body:
            bt = tree(b)
            hits = [k for k, t in st.items() if bt.overlap(t)]
            if hits: print('  OVERLAP', b.name, '->', hits)

elif who == 'tank' and mode == 'views':
    info = {}
    for n in ('t1', 't1big'):
        T.HI = False; T.reset(); base = T.tris(ALL[n]()); T.HI = True
        T.reset(); root = ALL[n](); bpy.context.view_layer.update()
        lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.08
        info[n] = {'tris': base, 'render_tris': T.tris(root), 'size': [round(v, 3) for v in hi - lo]}
        for view, elev, azim, zoom, line in (('q34', 25, 35, 400, 2.0), ('back', 25, 145, 190, 1.5)):
            T.set_line(line / zoom)
            T.ortho(elev, azim, c, size, (int(size * zoom), int(size * zoom)))
            T.render(os.path.join(out, f'{n}_{view}.png'))
    json.dump(info, open(os.path.join(out, 'info.json'), 'w'), indent=1)

elif who == 'tank' and mode == 'compare':
    ROW = ['robot_tank', 't1', 't1big', 't1mid']
    PX, width, height = 1800, 12.0, 3.0      # one scale for both rows
    zoom = PX / width
    marks = {}
    for row, yaw, elev in (('q34', 35, 14), ('side', -90, 4)):
        s = T.reset(); roots, x = {}, 0.0
        for k in ROW:
            r = ALL[k](yaw=yaw); bpy.context.view_layer.update()
            lo, hi = bounds(r); r.location.x = x - lo.x; x += (hi.x - lo.x) + 0.7
            roots[k] = r
        bpy.context.view_layer.update()
        w = x - 0.7
        print('COMPARE', row, 'width %.2f of %.2f' % (w, width))
        T.set_line(1.8 / zoom)
        T.ortho(elev, 0, Vector((w / 2, 0, height / 2 - 0.32)), width, (PX, int(height * zoom)))
        bpy.context.view_layer.update()
        for k, r in roots.items():
            a, b = bounds(r)
            top = world_to_camera_view(s, s.camera, Vector(((a.x + b.x) / 2, (a.y + b.y) / 2, b.z)))
            bot = world_to_camera_view(s, s.camera, Vector(((a.x + b.x) / 2, a.y, 0)))
            sz = [(o.matrix_world @ v.co).z for o in parts(r) if o.parent.name.startswith('shield') for v in o.data.vertices]
            marks[f'{k}_{row}'] = {'x': top.x * PX, 'bottom': (1 - bot.y) * int(height * zoom), 'height_units': b.z,
                                   'shield_units': max(sz) - min(sz) if sz else 0}
        T.render(os.path.join(out, f'compare_{row}.png'))
    json.dump({'zoom': zoom, 'marks': marks}, open(os.path.join(out, 'compare.json'), 'w'), indent=1)

elif who == 'tank' and mode == 'battle':
    s = T.reset(); T.set_sun(BATTLE_SUN)
    desk(-14, 10, ((0.7, 3.2),))
    spots = {'robot_melee': ALL['robot_melee'](at=(-8.6, 0.7, 0), yaw=30), 'robot_tank': ALL['robot_tank'](at=(-6.0, 0.7, 0), yaw=30),
             't1': ALL['t1'](at=(-2.4, 0.7, 0), yaw=-30), 't1big': ALL['t1big'](at=(1.3, 0.7, 0), yaw=-30),
             't1mid': ALL['t1mid'](at=(5.0, 0.7, 0), yaw=-30)}
    battle_cam(s, (-1.8, 1.0, 0.6))
    bpy.context.view_layer.update()
    marks = {k: screen_box(s, r, 1920) for k, r in spots.items()}
    for k, b in marks.items(): print('BATTLE', k, 'h %.0f w %.0f px' % (b[3] - b[1], b[2] - b[0]))
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))

elif who == 'tank' and mode == 'neck':
    import neck as NK

    def eyes_seen(root, origin=None, along=None):
        """Per eye slit: whether a ray from the camera (a point, or a direction for orthographic) reaches it first."""
        bpy.context.view_layer.update()
        eyes = [o for o in parts(root) if o.name.split('.')[0] in ('eye-1', 'eye1')]
        verts, faces = [], []
        for o in parts(root):           # the tank's parts carry no modifiers (only their outline hulls do)
            if o in eyes: continue
            me = o.data; n = len(verts)
            verts += [o.matrix_world @ v.co for v in me.vertices]; faces += [tuple(i + n for i in p.vertices) for p in me.polygons]
        tree = BVHTree.FromPolygons(verts, faces)
        seen = []
        for e in eyes:
            c = sum((e.matrix_world @ v.co for v in e.data.vertices), Vector()) / len(e.data.vertices)
            start = origin if origin is not None else c - along * 60
            d = c - start
            hit = tree.ray_cast(start, d.normalized(), d.length - 0.04)   # short of the slit's own depth: it sits in the visor
            seen.append(hit[0] is None)
        return seen

    info = {}
    T.reset(); root = NK.t1neck(); bpy.context.view_layer.update()
    lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.08
    sh = [o for o in parts(root) if o.parent.name.startswith('shield')]
    info['height'] = hi.z
    info['shield_top'] = max((o.matrix_world @ v.co).z for o in sh for v in o.data.vertices)
    info['eye_z'] = max((o.matrix_world @ v.co).z for o in parts(root) if o.name.split('.')[0] in ('eye-1', 'eye1') for v in o.data.vertices)
    T.set_line(2.0 / 400)
    T.ortho(25, 35, c, size, (int(size * 400), int(size * 400)))
    s = bpy.context.scene
    info['eyes_q34'] = eyes_seen(root, along=s.camera.matrix_world.to_3x3() @ Vector((0, 0, -1)))
    def tree(o):
        return BVHTree.FromPolygons([o.matrix_world @ v.co for v in o.data.vertices], [tuple(p.vertices) for p in o.data.polygons])
    st = [tree(o) for o in sh]
    info['clips_shield'] = sorted({b.name for b in parts(root) if b not in sh and any(tree(b).overlap(t) for t in st)})
    T.render(os.path.join(out, 't1neck_q34.png'))
    s = T.reset(); T.set_sun(BATTLE_SUN)
    desk(-6, 6, ((0.7, 3.2),))
    rob = ALL['robot_tank'](at=(-1.35, 0.7, 0), yaw=30)
    mon = NK.t1neck(at=(1.3, 0.7, 0), yaw=-30)
    battle_cam(s, (0.0, 0.7, 0.6))
    bpy.context.view_layer.update()
    info['eyes_battle'] = eyes_seen(mon, origin=s.camera.location.copy())
    mon.rotation_euler.z = 0                 # head-on: walking straight down the screen at the camera
    info['eyes_battle_head_on'] = eyes_seen(mon, origin=s.camera.location.copy())
    mon.rotation_euler.z = math.radians(-30); bpy.context.view_layer.update()
    info['robot'], info['monster'] = screen_box(s, rob, 1920), screen_box(s, mon, 1920)
    print('NECK', json.dumps(info))
    json.dump(info, open(os.path.join(out, 'neck.json'), 'w'), indent=1)
    T.render(os.path.join(out, 't1neck_pair.png'))

elif who == 'tank' and mode == 'pairs':
    s = T.reset(); T.set_sun(BATTLE_SUN)
    desk(-6, 6, ((0.7, 3.2),))
    rob = ALL['robot_tank'](at=(-1.35, 0.7, 0), yaw=30)
    mon = ALL['t1big'](at=(1.3, 0.7, 0), yaw=-30)
    battle_cam(s, (0.0, 0.7, 0.6))
    bpy.context.view_layer.update()
    json.dump({'robot': screen_box(s, rob, 1920), 'monster': screen_box(s, mon, 1920)}, open(os.path.join(out, 'pairs.json'), 'w'), indent=1)
    T.render(os.path.join(out, 't1big_pair.png'))
