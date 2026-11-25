# blender -b --factory-startup -P render.py -- views <out_dir> [names...]
#   each grunt and the real robot alone, orthographic, transparent PNGs, at a fixed px per design unit:
#   <n>_big (3/4 front, 25° up, 35° to its left), <n>_back (3/4 back), <n>_cmp (3/4 front, small, for the side-by-side sheet);
#   stats.json with triangle counts, heights and colours
# blender -b --factory-startup -P render.py -- battle <out_dir>
#   all seven beside the robot on a wood desk and cream road, through the battle camera (19° FOV, 47.5° down, 1080p)
# blender -b --factory-startup -P render.py -- blend <out_dir>
#   the seven in a row, saved as 일반 7안.blend
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import toon as T
import models as M
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

BIG, CMP = 560, 210     # px per design unit: the per-option sheets and the side-by-side sheet
LINE_PX = 2.4           # the robots' outline is a fixed 1.8 px on screen at any zoom; a touch more on the big views
args = sys.argv[sys.argv.index('--') + 1:]
mode, out = args[0], args[1]
os.makedirs(out, exist_ok=True)


def parts(root):
    return [o for o in root.children_recursive if o.type in ('MESH', 'FONT') and 'blob' not in o.name]


def bounds(root):
    pts = [o.matrix_world @ Vector(c) for o in parts(root) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def build(n, **kw):
    if n == 'robot': return T.real_robot('melee', **kw)
    M.ALL[n](**kw)
    return T.ROOT


if mode == 'views':
    names = args[2:] or ['robot', *M.ALL]
    stats = {}
    for n in names:
        T.reset(); root = build(n); bpy.context.view_layer.update()
        lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.12
        used = {m for o in parts(root) for m in o.data.materials}
        stats[n] = {'tris': T.tris(root), 'height': hi.z,
                    'colours': [k[1] for k, m in T._mats.items() if k[0] == 'toon' and not k[2] and m in used]}
        for view, elev, azim, zoom in (('big', 25, 35, BIG), ('back', 25, 150, BIG * 0.5), ('cmp', 25, 35, CMP)):
            T.set_line(LINE_PX / zoom if view != 'cmp' else 1.8 / zoom)
            px = int(size * zoom)
            T.ortho(elev, azim, c, size, (px, px))
            T.render(os.path.join(out, f'{n}_{view}.png'))
        print('TRIS', n, stats[n]['tris'], 'H', round(hi.z, 3))
    p = os.path.join(out, 'stats.json')
    old = json.load(open(p)) if os.path.exists(p) else {}
    old.update(stats); json.dump(old, open(p, 'w'), indent=1)

elif mode == 'blend':
    T.reset()
    for i, n in enumerate(M.ALL): build(n, at=(i * 1.6, 0, 0))
    T.set_line(0.012)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, '일반 7안.blend'), compress=True)

elif mode == 'battle':
    K = 1.27 / 1.55                        # the in-game robot stands about 1.27 units tall (~120 px): design to game units
    dist = 32.9 / K                        # the battle camera's distance, in design units
    s = T.reset()
    T.set_sun((0.287, -0.497, 0.819))      # the battle light, Euler(55, -30, 0), seen from the camera side
    # a wood desk island (stage 2's colours, read off the battle screenshot) with two cream roads
    T.begin('desk', 1.0)
    T.rbox('edge', (23, 10.4, 0.6), (-2.9, 1.0, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
    T.rbox('wood', (23, 10.4, 0.04), (-2.9, 1.0, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
    for y in (2.6, -1.6):
        T.rbox(f'road{y}', (23, 2.6, 0.02), (-2.9, y, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)
    xs = [-8.2 + 2.1 * i for i in range(7)]
    spots = {}
    # back road: walking left along the road (side-on, as they move in game); front road: turned toward the camera
    for row, y, yaw in (('walk', 2.6, -90), ('face', -1.6, 40)):
        spots[f'robot_{row}'] = build('robot', at=(-11.6, y + (1.9 if row == 'walk' else 1.7), 0), yaw=-20, name=f'robot_{row}')
        for n, x in zip(M.ALL, xs):
            spots[f'{n}_{row}'] = build(n, at=(x, y, 0), yaw=yaw)
    cam = s.camera; cam.data.type = 'PERSP'; cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(19)
    cam.data.clip_end = 500
    look = Vector((-3.8, 0.9, 0.35))
    fwd = Vector((0, math.cos(math.radians(47.5)), -math.sin(math.radians(47.5))))
    cam.location = look - fwd * dist
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
    s.render.resolution_x, s.render.resolution_y = 1920, 1080
    # the shader's 1.8 px outline, in world units at the camera's distance
    T.set_line(1.8 * dist * 2 * math.tan(math.radians(9.5)) / 1080)
    bpy.context.view_layer.update()
    marks = {}
    for k, root in spots.items():
        ys = []; xs_ = []
        for o in parts(root):
            if o.name.endswith('_hull'): continue
            for v in o.data.vertices:
                p = world_to_camera_view(s, cam, o.matrix_world @ v.co)
                xs_.append(p.x * 1920); ys.append((1 - p.y) * 1080)
        marks[k] = {'top': min(ys), 'bottom': max(ys), 'left': min(xs_), 'right': max(xs_), 'px': max(ys) - min(ys)}
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))
