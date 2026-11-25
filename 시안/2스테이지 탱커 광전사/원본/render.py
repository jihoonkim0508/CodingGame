# blender -b --factory-startup -P render.py -- views <out_dir> [names...]
#   each design alone, orthographic, transparent, rendered at its sheet size so the outline keeps a fixed pixel width:
#   q34 (big 3/4 front, 25° up, 35° round to the weapon side), side, back (3/4 back), cmp (the line-up scale)
# blender -b --factory-startup -P render.py -- battle <out_dir>
#   the three beside the real melee and tank robots on a desk and road, through the battle camera (19° FOV, 47.5° down,
#   1080p, 1.8 px outline); battle.json holds each one's screen box. Then pair_<n>.png: the tank robot and one
#   berserker side by side through the same camera, for the battle-size crop on each sheet.
# blender -b --factory-startup -P render.py -- blend <file.blend>
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import toon as T
import models as M
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

VIEWS = {  # view: elevation, azimuth (from the front toward -x), px per design unit, outline px
    'q34': (25, -35, 330, 2.4), 'side': (6, -90, 112, 1.5), 'back': (25, -145, 112, 1.5), 'cmp': (25, -35, 175, 1.8)}
OWN_SIDE = {'robot_tank': 35, 't1': 35, 't5': 35}   # the round-2 designs keep the side their own sheets showed
BERSERKERS = ['b1', 'b2', 'b3']
args = sys.argv[sys.argv.index('--') + 1:]
mode, out = args[0], args[1]


def parts(root):
    return [o for o in root.children_recursive if o.type in ('MESH', 'FONT') and 'blob' not in o.name]


def bounds(root):
    pts = [o.matrix_world @ Vector(c) for o in parts(root) for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def build(n, **kw):
    M.ALL[n](**kw)
    root = T.ROOT
    while root.parent: root = root.parent
    return root


if mode == 'blend':
    T.reset()
    for i, n in enumerate(BERSERKERS): build(n, at=(i * 2.6, 0, 0))
    T.set_line(0.012)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(out), compress=True)
    sys.exit(0)
os.makedirs(out, exist_ok=True)

if mode == 'views':
    names = args[2:] or BERSERKERS + ['t1', 't5', 'robot_tank']
    for n in names:
        T.reset(); root = build(n); bpy.context.view_layer.update()
        lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.1
        blobs = [o for o in root.children_recursive if 'blob' in o.name]
        views = ('q34', 'side', 'back', 'cmp') if n in BERSERKERS else ('cmp',)
        for v in views:
            elev, azim, zoom, line = VIEWS[v]
            if v == 'cmp' and n in OWN_SIDE: azim = OWN_SIDE[n]
            for b in blobs: b.hide_render = v == 'side'
            T.set_line(line / zoom)
            T.ortho(elev, azim, c, size, (int(size * zoom), int(size * zoom)))
            T.render(os.path.join(out, f'{n}_{v}.png'))
        print('SIZE', n, [round(x, 3) for x in (hi - lo)])

elif mode == 'battle':
    K = 1.27 / 1.55                        # the in-game robot stands about 1.27 units tall (~118 px): design to game units
    dist = 32.9 / K                        # the battle camera's distance, in design units

    def stage(spots_at, look, res):
        s = T.reset()
        T.set_sun((0.287, -0.497, 0.819))  # the battle light, Euler(55, -30, 0), seen from the camera side
        T.begin('desk', 1.0)
        T.rbox('edge', (40, 9.0, 0.6), (look.x, 1.2, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
        T.rbox('wood', (40, 9.0, 0.04), (look.x, 1.2, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
        T.rbox('road', (40, 2.9, 0.02), (look.x, 0.9, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)
        spots = {n: build(n, at=at, yaw=yaw) for n, (at, yaw) in spots_at.items()}
        cam = s.camera; cam.data.type = 'PERSP'; cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(19)
        cam.data.clip_end = 500
        fwd = Vector((0, math.cos(math.radians(47.5)), -math.sin(math.radians(47.5))))
        cam.location = look - fwd * dist
        cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
        s.render.resolution_x, s.render.resolution_y = res
        T.set_line(1.8 * dist * 2 * math.tan(math.radians(9.5)) / 1080)   # the shader's 1.8 px outline, in world units
        bpy.context.view_layer.update()
        marks = {}
        for k, root in spots.items():
            sx, sy = [], []
            dg = bpy.context.evaluated_depsgraph_get()
            for o in parts(root):
                if o.name.endswith('_hull'): continue
                ev = o.evaluated_get(dg)
                for v in ev.data.vertices:
                    p = world_to_camera_view(s, cam, o.matrix_world @ v.co)
                    sx.append(p.x * res[0]); sy.append((1 - p.y) * res[1])
            marks[k] = {'box': [min(sx), min(sy), max(sx), max(sy)], 'h': max(sy) - min(sy)}
            print('SCREEN', k, round(max(sy) - min(sy), 1), 'px tall,', round(max(sx) - min(sx), 1), 'px wide')
        return marks

    row = ['robot_melee', 'robot_tank'] + BERSERKERS
    xs = [-8.4, -5.6, -1.6, 2.9, 7.4]
    at = {n: ((x, 1.0 if n.startswith('robot') else 0.9, 0), 30 if n.startswith('robot') else -25) for n, x in zip(row, xs)}
    marks = stage(at, Vector((-0.5, 0.9, 0.35)), (1920, 1080))
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))
    pairs = {}
    for n in BERSERKERS:
        pairs[n] = stage({'robot_tank': ((-1.1, 1.0, 0), 30), n: ((1.0, 0.9, 0), -25)}, Vector((0.0, 0.9, 0.35)), (1920, 1080))
        T.render(os.path.join(out, f'pair_{n}.png'))
    json.dump(pairs, open(os.path.join(out, 'pairs.json'), 'w'), indent=1)
