# blender -b --factory-startup -P render.py -- views <out_dir> [names...]
#   each design alone: 3/4 front (25° up, 35° to its left), front and side, orthographic, transparent PNGs
# blender -b --factory-startup -P render.py -- battle <out_dir>
#   all eight beside two robots on a wood desk and cream road, through the battle camera (19° FOV, 47.5° down, 1080p)
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import toon as T
import models as M
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

ZOOM = 450          # px per design unit in the view renders (the sheets scale them down together)
LINE = 0.031        # outline width in design units: about 1.9% of the robot's height, between the icons' inner and outer lines
args = sys.argv[sys.argv.index('--') + 1:]
mode, out = args[0], args[1]
os.makedirs(out, exist_ok=True)


def bounds(root):
    pts = [o.matrix_world @ Vector(c) for o in root.children_recursive if o.type in ('MESH', 'FONT') and 'blob' not in o.name
           for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


if mode == 'views':
    names = args[2:] or list(M.ALL)
    for n in names:
        T.reset(); M.ALL[n](); bpy.context.view_layer.update()
        T.set_line(LINE)
        lo, hi = bounds(T.ROOT); c = (lo + hi) / 2; size = (hi - lo).length * 1.15
        px = int(size * ZOOM)
        blobs = [o for o in T.ROOT.children if 'blob' in o.name]
        for view, elev, azim in (('q34', 25, 35), ('front', 0, 0), ('side', 0, 90)):
            for b in blobs: b.hide_render = view != 'q34'
            T.ortho(elev, azim, c, size, (px, px))
            T.render(os.path.join(out, f'{n}_{view}.png'))

elif mode == 'battle':
    K = 1.27 / 1.55                        # the in-game robot stands about 1.27 units tall (~120 px): design to game units
    dist = 32.9 / K                        # the battle camera's distance, in design units
    s = T.reset()
    s.eevee.taa_render_samples = 64
    T.set_sun((0.287, -0.497, 0.819))      # the battle light, Euler(55, -30, 0), seen from the camera side
    # a wood desk island (stage 2's colours, read off the battle screenshot) with two cream roads: A behind, B in front
    T.begin('desk', 1.0)
    T.rbox('edge', (21, 10.4, 0.6), (-2.5, 1.0, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
    T.rbox('wood', (21, 10.4, 0.04), (-2.5, 1.0, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
    for y in (2.6, -1.6):
        T.rbox(f'road{y}', (21, 2.6, 0.02), (-2.5, y, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)
    row = ['normal', 'fast', 'tank', 'boss']
    xs = [-6.6, -3.5, 0.0, 4.4]
    spots = {}
    for opt, y in (('a', 2.6), ('b', -1.6)):
        M.robot(at=(-10.6, y + 1.9, 0), yaw=-20)
        spots[f'robot_{opt}'] = T.ROOT
        for name, x in zip(row, xs):
            M.ALL[f'{name}_{opt}'](at=(x, y, 0), yaw=40)
            spots[f'{name}_{opt}'] = T.ROOT
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
    for k, root in spots.items():
        lo, hi = bounds(root)
        top = world_to_camera_view(s, cam, Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, hi.z)))
        bot = world_to_camera_view(s, cam, Vector(((lo.x + hi.x) / 2, lo.y, 0)))
        marks[k] = {'top': [top.x * 1920, (1 - top.y) * 1080], 'bottom': [bot.x * 1920, (1 - bot.y) * 1080]}
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))
