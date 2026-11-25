# blender -b --factory-startup -P render.py -- views <out_dir> [names...]
#   each design alone, orthographic, transparent: a big 3/4 front (25° up, 35° to its left) and a smaller 3/4 back,
#   each at a fixed px per unit with a fixed-px outline (thin on the sheets too); info.json with triangles and size
# blender -b --factory-startup -P render.py -- compare <out_dir>
#   the real tank robot and the seven at one scale, two rows (1-4, 5-7), 3/4 front, the game's 1.8 px outline
# blender -b --factory-startup -P render.py -- battle <out_dir>
#   all seven beside the real melee and tank robots on the desk and road, through the battle camera (19° FOV, 47.5° down)
# blender -b --factory-startup -P render.py -- pairs <out_dir> [names...]
#   each design next to the real tank robot through the same battle camera, for the sheets' true-size crop
# blender -b --factory-startup -P render.py -- blend <file.blend>
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import toon as T
import models as M
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

BIG, BIG_LINE = 400, 2.0         # px per unit and outline px of the big 3/4 views (shown 1:1 on the sheets)
BACK, BACK_LINE = 190, 1.5       # the 3/4 back views
CMP_LINE = 1.8
NAMES = [f't{i}' for i in range(1, 8)]
args = sys.argv[sys.argv.index('--') + 1:]
mode, out = args[0], args[1]
K = 1.27 / 1.55                  # the in-game robot stands about 1.27 units tall: design to game units
DIST = 32.9 / K                  # the battle camera's distance, in design units

if mode == 'blend':
    T.reset()
    for i, n in enumerate(NAMES): M.ALL[n](at=(i * 2.6, 0, 0))
    for o in [o for o in bpy.data.objects if o.name.endswith('_hull')]: bpy.data.objects.remove(o)   # outlines are a render effect: keeps the file small
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out), compress=True)
    sys.exit(0)
os.makedirs(out, exist_ok=True)


def parts(root):
    return [o for o in root.children_recursive if o.type in ('MESH', 'FONT') and 'blob' not in o.name]


def bounds(root):
    pts = [o.matrix_world @ v.co for o in parts(root) if not o.name.endswith('_hull') for v in o.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def battle_cam(s, look, width):
    cam = s.camera; cam.data.type = 'PERSP'; cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(19)
    cam.data.clip_end = 500
    fwd = Vector((0, math.cos(math.radians(47.5)), -math.sin(math.radians(47.5))))
    cam.location = Vector(look) - fwd * DIST
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
    s.render.resolution_x, s.render.resolution_y = width, 1080
    T.set_line(1.8 * DIST * 2 * math.tan(math.radians(9.5)) / 1080)   # the shader's 1.8 px outline at the camera's distance


def screen_box(s, root, width):
    xs, ys = [], []
    for o in parts(root):
        if o.name.endswith('_hull'): continue
        for v in o.data.vertices:
            p = world_to_camera_view(s, s.camera, o.matrix_world @ v.co)
            xs.append(p.x * width); ys.append((1 - p.y) * 1080)
    return [min(xs), min(ys), max(xs), max(ys)]


def desk(x0, x1):
    T.begin('desk', 1.0)
    w, c = x1 - x0, (x0 + x1) / 2
    T.rbox('edge', (w, 10.4, 0.6), (c, 0.9, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
    T.rbox('wood', (w, 10.4, 0.04), (c, 0.9, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
    T.rbox('road', (w, 3.2, 0.02), (c, 0.7, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)


if mode == 'views':
    names = args[2:] or NAMES
    path = os.path.join(out, 'info.json')
    info = json.load(open(path)) if os.path.exists(path) else {}
    for n in names:
        if n.startswith('t'):                # the plain draft mesh first, for its triangle count; then the render mesh
            T.HI = False; T.reset(); base = T.tris(M.ALL[n]()); T.HI = True
        T.reset(); root = M.ALL[n](); bpy.context.view_layer.update()
        lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.08
        if n.startswith('t'): info[n] = {'tris': base, 'render_tris': T.tris(root), 'size': [round(v, 3) for v in hi - lo]}
        print('INFO', n, info.get(n), T.part_tris(root)[:12])
        blobs = [o for o in root.children if 'blob' in o.name]
        for view, elev, azim, zoom, line in (('q34', 25, 35, BIG, BIG_LINE), ('back', 25, 145, BACK, BACK_LINE)):
            T.set_line(line / zoom)
            T.ortho(elev, azim, c, size, (int(size * zoom), int(size * zoom)))
            T.render(os.path.join(out, f'{n}_{view}.png'))
    json.dump(info, open(path, 'w'), indent=1)

elif mode == 'compare':
    ROWS = (('a', ['robot_tank'] + NAMES[:4]), ('b', ['robot_tank'] + NAMES[4:]))

    def lay(names):
        roots, x = {}, 0.0
        for k in names:
            r = M.ALL[k](yaw=35); bpy.context.view_layer.update()
            lo, hi = bounds(r)
            r.location.x = x - lo.x; x += (hi.x - lo.x) + 0.45
            roots[k] = r
        bpy.context.view_layer.update()
        return roots, x - 0.45

    widths = []
    for row, names in ROWS:                  # first pass: measure both rows, so they share one scale that fits the wider
        T.reset(); widths.append(lay(names)[1])
    width = max(widths) + 0.5
    zoom = 1840 / width
    marks = {}
    for row, names in ROWS:
        s = T.reset()
        roots, w = lay(names)
        T.set_line(CMP_LINE / zoom)
        height = 2.75
        T.ortho(14, 0, Vector((w / 2, 0, height / 2 - 0.12)), width, (1840, int(height * zoom)))
        bpy.context.view_layer.update()
        for k, r in roots.items():
            a, b = bounds(r)
            top = world_to_camera_view(s, s.camera, Vector(((a.x + b.x) / 2, (a.y + b.y) / 2, b.z)))
            bot = world_to_camera_view(s, s.camera, Vector(((a.x + b.x) / 2, a.y, 0)))
            marks[k + '_' + row] = {'row': row, 'x': top.x * 1840, 'bottom': (1 - bot.y) * int(height * zoom), 'height_units': b.z}
        T.render(os.path.join(out, f'compare_{row}.png'))
    json.dump({'zoom': zoom, 'marks': marks}, open(os.path.join(out, 'compare.json'), 'w'), indent=1)

elif mode == 'battle':
    BW = 2400                              # wider than 1080p sideways only: same vertical FOV, so the same px per unit
    s = T.reset()
    T.set_sun((0.287, -0.497, 0.819))      # the battle light, Euler(55, -30, 0), seen from the camera side
    desk(-17, 17)
    spots = {'robot_melee': M.ALL['robot_melee'](at=(-13.6, 0.7, 0), yaw=30),
             'robot_tank': M.ALL['robot_tank'](at=(-11.0, 0.7, 0), yaw=30)}
    for i, n in enumerate(NAMES):
        spots[n] = M.ALL[n](at=(-7.6 + i * 2.95, 0.7, 0), yaw=-30)   # walking left along the road, turned a little to the camera
    battle_cam(s, (-0.6, 1.0, 0.6), BW)
    bpy.context.view_layer.update()
    marks = {}
    for k, root in spots.items():
        b = screen_box(s, root, BW)
        marks[k] = {'box': b, 'h': b[3] - b[1]}
        print('SCREEN', k, round(b[3] - b[1], 1), 'px tall,', round(b[2] - b[0], 1), 'px wide')
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))

elif mode == 'pairs':
    names = args[2:] or NAMES
    path = os.path.join(out, 'pairs.json')
    marks = json.load(open(path)) if os.path.exists(path) else {}
    for n in names:
        s = T.reset()
        T.set_sun((0.287, -0.497, 0.819))
        desk(-6, 6)
        rob = M.ALL['robot_tank'](at=(-1.25, 0.7, 0), yaw=30)
        mon = M.ALL[n](at=(1.2, 0.7, 0), yaw=-30)
        battle_cam(s, (0.0, 0.7, 0.6), 1920)
        bpy.context.view_layer.update()
        marks[n] = {'robot': screen_box(s, rob, 1920), 'monster': screen_box(s, mon, 1920)}
        T.render(os.path.join(out, f'{n}_pair.png'))
    json.dump(marks, open(path, 'w'), indent=1)
