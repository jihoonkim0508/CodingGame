# blender -b --factory-startup -P render.py -- views <out_dir> [ids...]
#   each boss alone, orthographic, transparent: a big 3/4 view (25° up, 35° round) and a small front view, rendered at
#   their sheet size so the outline stays a fixed pixel width; tris.json with the triangle counts
# blender -b --factory-startup -P render.py -- compare <out_dir>
#   the real melee robot, a grunt-sized block and the five bosses in one row, one scale, 3/4 view
# blender -b --factory-startup -P render.py -- battle <out_dir>
#   the five beside real robots on a wood desk and cream road, through the battle camera (19° FOV, 47.5° down, 1080p)
import bpy, sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import toon as T
import models as M
from mathutils import Vector
from bpy_extras.object_utils import world_to_camera_view

BIG, BIG_LINE = 225, 2.4         # px per design unit and outline px of the big 3/4 views
SMALL, SMALL_LINE = 80, 1.6      # the small front views
CMP_LINE = 1.8                   # the comparison row: the game's own 1.8 px
args = sys.argv[sys.argv.index('--') + 1:]
mode, out = args[0], args[1]
os.makedirs(out, exist_ok=True)


def bounds(root):
    pts = [o.matrix_world @ Vector(c) for o in root.children_recursive if o.type in ('MESH', 'FONT') and 'blob' not in o.name
           for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def build(k, **kw):
    if k == 'robot': return T.real_robot('melee', **kw)
    if k == 'grunt': M.grunt(**kw); return T.ROOT
    M.BOSSES[int(k)](**kw); return T.ROOT


if mode == 'views':
    ids = args[2:] or [str(i) for i in M.BOSSES]
    counts = {}
    for k in ids:
        T.reset(); root = build(k); bpy.context.view_layer.update()
        counts[k] = T.tris(root)
        lo, hi = bounds(root); c = (lo + hi) / 2; size = (hi - lo).length * 1.12
        blobs = [o for o in root.children if 'blob' in o.name]
        for view, elev, azim, zoom, line in (('q34', 25, 35, BIG, BIG_LINE), ('front', 8, 0, SMALL, SMALL_LINE)):
            for b in blobs: b.hide_render = view != 'q34'
            T.set_line(line / zoom)
            T.ortho(elev, azim, c, size, (int(size * zoom), int(size * zoom)))
            T.render(os.path.join(out, f'{k}_{view}.png'))
    json.dump(counts, open(os.path.join(out, 'tris.json'), 'w'), indent=1)

elif mode == 'compare':
    s = T.reset()
    row = ['robot', 'grunt'] + [str(i) for i in M.BOSSES]
    roots, x = {}, 0.0
    for k in row:
        r = build(k, yaw=35); bpy.context.view_layer.update()
        lo, hi = bounds(r)
        r.location.x = x - lo.x; x += (hi.x - lo.x) + 0.55
        roots[k] = r
    bpy.context.view_layer.update()
    pts = [bounds(r) for r in roots.values()]
    lo = Vector((min(p[0].x for p in pts), min(p[0].y for p in pts), 0)); hi = Vector((max(p[1].x for p in pts), max(p[1].y for p in pts), max(p[1].z for p in pts)))
    width = hi.x - lo.x + 0.6
    zoom = 1840 / width
    T.set_line(CMP_LINE / zoom)
    h = (hi.z + 1.0) * zoom
    T.ortho(14, 0, Vector(((lo.x + hi.x) / 2, 0, hi.z / 2 + 0.1)), width, (1840, int(h)))
    bpy.context.view_layer.update()
    cam = s.camera; marks = {}
    for k, r in roots.items():
        a, b = bounds(r)
        top = world_to_camera_view(s, cam, Vector(((a.x + b.x) / 2, (a.y + b.y) / 2, b.z)))
        bot = world_to_camera_view(s, cam, Vector(((a.x + b.x) / 2, a.y, 0)))
        marks[k] = {'x': top.x * 1840, 'top': (1 - top.y) * int(h), 'bottom': (1 - bot.y) * int(h), 'height_units': b.z}
    json.dump({'zoom': zoom, 'marks': marks}, open(os.path.join(out, 'compare.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'compare.png'))

elif mode == 'battle':
    BW = 2400                              # wider than 1080p only sideways: same vertical FOV, so the same px per unit
    K = 1.27 / 1.55                        # the in-game robot stands about 1.27 units tall (~118 px): design to game units
    dist = 32.9 / K                        # the battle camera's distance, in design units
    s = T.reset()
    T.set_sun((0.287, -0.497, 0.819))      # the battle light, Euler(55, -30, 0), seen from the camera side
    # a wood desk island (stage 2's colours) with one cream road the bosses walk along, toward the robots on the left
    T.begin('desk', 1.0)
    T.rbox('edge', (32, 10.4, 0.6), (0.0, 0.9, -0.32), T.flat((196, 136, 80)), r=0.05, seg=1, outline=False)
    T.rbox('wood', (32, 10.4, 0.04), (0.0, 0.9, -0.02), T.flat((226, 166, 100)), r=0.01, seg=1, outline=False)
    T.rbox('road', (32, 3.2, 0.02), (0.0, 0.7, 0.01), T.flat((250, 236, 200)), r=0.005, seg=1, outline=False)
    spots = {}
    spots['robot_a'] = T.real_robot('melee', at=(-13.0, 0.7, 0), yaw=50, name='robot_a')
    spots['robot_b'] = T.real_robot('melee', at=(-1.8, 3.4, 0), yaw=40, name='robot_b')
    for i, x in zip(M.BOSSES, (-8.6, -4.1, 0.6, 5.5, 10.6)):
        spots[str(i)] = build(str(i), at=(x, 0.7, 0), yaw=-40)      # walking left along the road, turned a little to the camera
    cam = s.camera; cam.data.type = 'PERSP'; cam.data.sensor_fit = 'VERTICAL'; cam.data.angle_y = math.radians(19)
    cam.data.clip_end = 500
    look = Vector((-0.6, 1.0, 0.9))
    fwd = Vector((0, math.cos(math.radians(47.5)), -math.sin(math.radians(47.5))))
    cam.location = look - fwd * dist
    cam.rotation_mode = 'QUATERNION'; cam.rotation_quaternion = fwd.to_track_quat('-Z', 'Y')
    s.render.resolution_x, s.render.resolution_y = BW, 1080
    T.set_line(1.8 * dist * 2 * math.tan(math.radians(9.5)) / 1080)   # the shader's 1.8 px outline at the camera's distance
    bpy.context.view_layer.update()
    marks = {}
    for k, root in spots.items():
        lo, hi = bounds(root)
        pts = [world_to_camera_view(s, cam, Vector((x, y, z))) for x in (lo.x, hi.x) for y in (lo.y, hi.y) for z in (lo.z, hi.z)]
        bot = world_to_camera_view(s, cam, Vector(((lo.x + hi.x) / 2, lo.y, 0)))
        # silhouette height on screen: highest to lowest projected vertex of the model itself
        ys = [world_to_camera_view(s, cam, o.matrix_world @ v.co).y for o in root.children_recursive
              if o.type == 'MESH' and 'blob' not in o.name and not o.name.endswith('_hull') for v in o.data.vertices]
        h = (max(ys) - min(ys)) * 1080
        marks[k] = {'box': [min(p.x for p in pts) * BW, (1 - max(p.y for p in pts)) * 1080,
                            max(p.x for p in pts) * BW, (1 - min(p.y for p in pts)) * 1080],
                    'bottom': [bot.x * BW, (1 - bot.y) * 1080], 'height_px': h}
    json.dump(marks, open(os.path.join(out, 'battle.json'), 'w'), indent=1)
    T.render(os.path.join(out, 'battle.png'))
