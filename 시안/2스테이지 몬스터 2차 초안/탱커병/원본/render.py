# blender -b --factory-startup -P render.py -- views <out_dir> [names...]
#   each design alone at one fixed px per unit: 3/4 front (25° up, 35° to its left), front, side and 3/4 back, transparent PNGs
# blender -b --factory-startup -P render.py -- battle <out_dir>
#   all seven beside the real melee and tank robots on a desk and road, through the battle camera (19° FOV, 47.5° down, 1080p)
# blender -b --factory-startup -P render.py -- blend <file.blend>
#   the seven in a row, saved as one .blend to model from
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import toon as T
import models as M
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ZOOM = 420          # px per design unit in the view renders (the sheets scale them down together)
LINE = 0.031        # outline width in design units: about 1.9% of the robot's height, as last round
args = sys.argv[sys.argv.index('--') + 1:]
mode, out = args[0], args[1]
if mode == 'blend':
    T.reset()
    for i in range(7): M.ALL[f't{i + 1}'](at=(i * 2.2, 0, 0))
    T.set_line(LINE)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out), compress=True)
    sys.exit(0)
os.makedirs(out, exist_ok=True)


def parts(root):
    return [o for o in root.children_recursive if o.type in ('MESH', 'FONT') and 'blob' not in o.name]


def bounds(root):
    pts = [o.matrix_world @ Vector(c) for o in parts(root) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


if mode == 'views':
    names = args[2:] or list(M.ALL)
    info = {}
    for n in names:
        T.reset(); M.ALL[n](); bpy.context.view_layer.update()
        T.set_line(LINE)
        lo, hi = bounds(T.ROOT); c = (lo + hi) / 2; size = (hi - lo).length * 1.15
        px = int(size * ZOOM)
        info[n] = {'tris': T.tris(T.ROOT), 'size': list(hi - lo)}
        print('INFO', n, info[n])
        blobs = [o for o in T.ROOT.children if 'blob' in o.name]
        for view, elev, azim in (('q34', 25, 35), ('front', 0, 0), ('side', 0, 90), ('back', 25, 145)):
            for b in blobs: b.hide_render = view not in ('q34', 'back')
            T.ortho(elev, azim, c, size, (px, px))
            T.render(os.path.join(out, f'{n}_{view}.png'))
    path = os.path.join(out, 'info.json')
    old = json.load(open(path)) if os.path.exists(path) else {}
    old.update(info); json.dump(old, open(path, 'w'), indent=1)

elif mode == 'battle':
    K = 1.27 / 1.55                        # the in-game robot stands about 1.27 units tall (~118 px): design to game units
    dist = 32.9 / K                        # the battle camera's distance, in design units
    s = T.reset()
    T.set_sun((0.287, -0.497, 0.819))      # the battle light, Euler(55, -30, 0), seen from the camera side
    # a wood desk island (stage 2's colours, from the battle screenshot) with one cream road the row walks along
    T.begin('desk', 1.0)
    T.rbox('edge', (25, 9.0, 0.6), (-2.6, 1.2, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
    T.rbox('wood', (25, 9.0, 0.04), (-2.6, 1.2, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
    T.rbox('road', (25, 2.9, 0.02), (-2.6, 0.9, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)
    spots = {}
    M.ALL['robot_melee'](at=(-12.0, 1.0, 0), yaw=30); spots['robot_melee'] = T.ROOT
    M.ALL['robot_tank'](at=(-9.6, 1.0, 0), yaw=30); spots['robot_tank'] = T.ROOT
    for i in range(7):
        n = f't{i + 1}'
        M.ALL[n](at=(-6.6 + i * 2.45, 0.9, 0), yaw=-25); spots[n] = T.ROOT
    cam = s.camera; cam.data.type = 'PERSP'; cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(19)
    cam.data.clip_end = 500
    look = Vector((-2.6, 0.9, 0.35))
    fwd = Vector((0, math.cos(math.radians(47.5)), -math.sin(math.radians(47.5))))
    cam.location = look - fwd * dist
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
    s.render.resolution_x, s.render.resolution_y = 1920, 1080
    T.set_line(1.8 * dist * 2 * math.tan(math.radians(9.5)) / 1080)   # the shader's 1.8 px outline, in world units
    bpy.context.view_layer.update()
    marks = {}
    for k, root in spots.items():
        sx, sy = [], []
        for o in parts(root):
            if o.name.endswith('_hull'): continue
            for v in o.data.vertices:
                p = world_to_camera_view(s, cam, o.matrix_world @ v.co)
                sx.append(p.x * 1920); sy.append((1 - p.y) * 1080)
        marks[k] = {'box': [min(sx), min(sy), max(sx), max(sy)], 'h': max(sy) - min(sy)}
        print('SCREEN', k, round(max(sy) - min(sy), 1), 'px tall,', round(max(sx) - min(sx), 1), 'px wide')
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))
