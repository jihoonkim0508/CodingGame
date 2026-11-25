# blender -b --factory-startup -P render.py -- views <out_dir> [names...]
#   each design alone: 3/4 (32° up, 55° to its left) and side, orthographic, ZOOM px per design unit, transparent PNGs
# blender -b --factory-startup -P render.py -- blend <out_dir>
#   saves fast_runners.blend: the seven and the robot side by side
# blender -b --factory-startup -P render.py -- battle <out_dir>
#   all seven beside the real robot on a wood desk and cream roads, facing along the road (-x),
#   through the battle camera (19° FOV, 47.5° down, 1080p); battle.json holds each one's screen box
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import toon as T
import models as M
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ZOOM = 450          # px per design unit in the view renders (the sheets scale them all by one factor)
LINE = 0.014        # outline width in design units: thin, as the in-game 1.8 px line (1.5% of the robot's height) reads in a close-up
DESIGNS = ['hound', 'bullet', 'racer', 'gecko', 'raptor', 'manta', 'bunny']
args = sys.argv[sys.argv.index('--') + 1:]
mode, out = args[0], args[1]
os.makedirs(out, exist_ok=True)


def parts(root):
    return [o for o in root.children_recursive if o.type in ('MESH', 'FONT') and 'blob' not in o.name and not o.name.endswith('_hull')]


def bounds(root):
    pts = [o.matrix_world @ Vector(c) for o in parts(root) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


if mode == 'views':
    names = args[2:] or DESIGNS + ['robot']
    info = {}
    for n in names:
        T.reset(); M.ALL[n](); root = T.ROOT; bpy.context.view_layer.update()
        T.set_line(LINE)
        lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.15
        info[n] = {'tris': M.tris(root), 'size': [round(v, 3) for v in (hi - lo)]}
        print('INFO', n, info[n])
        px = int(size * ZOOM)
        blobs = [o for o in root.children if 'blob' in o.name]
        # the robot turned to show its lens; the runners turned to show their length
        for view, elev, azim in (('q34', 32, 25 if n == 'robot' else 55), ('side', 0, 90)):
            for b in blobs: b.hide_render = view != 'q34'
            T.ortho(elev, azim, c, size, (px, px))
            T.render(os.path.join(out, f'{n}_{view}.png'))
    path = os.path.join(out, 'info.json')
    old = json.load(open(path)) if os.path.exists(path) else {}
    old.update(info); json.dump(old, open(path, 'w'), indent=1)

elif mode == 'blend':
    # all seven in a row beside the robot, saved for later modelling (the robot's texture stays a path into Assets)
    T.reset()
    M.robot(at=(-2.0, 0, 0))
    for i, n in enumerate(DESIGNS):
        M.ALL[n](at=(i * 1.9, 0, 0))
    T.set_line(LINE)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(out, 'fast_runners.blend'), compress=True)

elif mode == 'battle':
    K = 1.27 / 1.55                        # the in-game robot stands about 1.27 units tall (~118 px): design to game units
    dist = 32.9 / K                        # the battle camera's distance, in design units
    s = T.reset()
    T.set_sun((0.287, -0.497, 0.819))      # the battle light, Euler(55, -30, 0), seen from the camera side
    # a wood desk island (stage 2's colours, read off the battle screenshot) with two cream roads: A behind, B in front
    T.begin('desk', 1.0)
    T.rbox('edge', (21, 10.4, 0.6), (-2.5, 1.0, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
    T.rbox('wood', (21, 10.4, 0.04), (-2.5, 1.0, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
    for y in (2.6, -1.6):
        T.rbox(f'road{y}', (21, 2.6, 0.02), (-2.5, y, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)
    spots = {}
    for y, row in ((2.6, DESIGNS[:4]), (-1.6, DESIGNS[4:])):
        spots[f'robot{y}'] = M.robot(at=(-10.4, y + 1.9, 0), yaw=-20)
        for name, x in zip(row, (-7.0, -3.2, 0.6, 4.4)):
            M.ALL[name](at=(x, y, 0), yaw=-90)     # front (-y) turned to -x: running along the road toward the robots
            spots[name] = T.ROOT
    cam = s.camera; cam.data.type = 'PERSP'; cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(19)
    cam.data.clip_end = 500
    look = Vector((-2.8, 0.9, 0.35))
    fwd = Vector((0, math.cos(math.radians(47.5)), -math.sin(math.radians(47.5))))
    cam.location = look - fwd * dist
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
    s.render.resolution_x, s.render.resolution_y = 1920, 1080
    # the shader's 1.8 px outline, in world units at the camera's distance
    T.set_line(1.8 * dist * 2 * math.tan(math.radians(9.5)) / 1080)
    bpy.context.view_layer.update()
    marks = {}
    dg = bpy.context.evaluated_depsgraph_get()
    for k, root in spots.items():
        xs, ys = [], []
        for o in parts(root):
            me = o.evaluated_get(dg).to_mesh()
            for v in me.vertices:
                p = world_to_camera_view(s, cam, o.matrix_world @ v.co)
                xs.append(p.x * 1920); ys.append((1 - p.y) * 1080)
            o.evaluated_get(dg).to_mesh_clear()
        marks[k] = {'box': [min(xs), min(ys), max(xs), max(ys)]}
        print('BATTLE', k, 'w %.0f h %.0f px' % (max(xs) - min(xs), max(ys) - min(ys)))
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))
