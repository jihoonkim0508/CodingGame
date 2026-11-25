# Seven stage-2 fast-runner drafts and the real melee robot (FBX + albedo) for scale.
# Design units: the robot stands 1.55 tall. Every model faces -y (its travel direction), long axis along y.
import bpy, math
from mathutils import Vector, Matrix
import toon as T
from toon import rbox, ball, tube, prism, wedge, blob, toon, flat, begin
from palette import WHITE, LGREY, MGREY, CHAR, ACCENT

ASSETS = r'C:\Dev\CodingGame\Assets\!_Project\Resources\Models\CharacterAnimation'   # read in place, never copied
W = LG = MG = CH = A = None                              # the current design's materials, set by mats()


def mats(key):
    global W, LG, MG, CH, A
    W, LG, MG, CH, A = toon(WHITE), toon(LGREY), toon(MGREY), toon(CHAR), toon(ACCENT[key])
    return flat(ACCENT[key])


def frame(origin, pitch):
    """Local offsets on a part pitched by `pitch` degrees (positive = nose down) to model positions."""
    R = Matrix.Rotation(math.radians(pitch), 3, 'X'); o = Vector(origin)
    return lambda p: tuple(o + R @ Vector(p))


def visor(name, size, at, pitch, eye, er):
    """A charcoal visor band (slightly wider than the head, so it shows from the side) with two lit eye dots on its front corners."""
    w, d, h = size
    rbox(name, size, at((0, 0, 0)), CH, r=min(h, d) * 0.45, rot=(pitch, 0, 0), outline=False)
    for s in (-1, 1):
        ball(f'{name}_eye{s}', er, at((s * (w / 2 - er * 0.75), -d / 2 + er * 0.35, 0)), eye, scale=(0.85, 0.85, 1.25), rot=(pitch, 0, 0), outline=False)


def swept(name, pts, depth, loc, mat, lean=0.0, r=0.03):
    """A side-profile blade: pts are (back, up) offsets, so the shape lies in the y/z plane and points backward."""
    return prism(name, pts, depth, loc, mat, r=r, rot=(lean, 0, 90))


def plate(name, pts, depth, loc, mat, roll=0.0, r=0.03):
    """A horizontal blade: pts are (side, back) offsets, the shape lies flat in the x/y plane."""
    return prism(name, pts, depth, loc, mat, r=r, rot=(-90, roll, 0))


def leg(name, pts, radii, joint=None):
    """A limb through pts (mid-grey segments, charcoal joints at the bends)."""
    for i in range(len(pts) - 1):
        tube(f'{name}{i}', pts[i], pts[i + 1], radii[i], MG, r1=radii[i + 1])
    for i, p in enumerate(pts[1:-1], 1):
        ball(f'{name}j{i}', radii[i] * 1.2, p, joint or CH)


def pitch_of(a, b):
    """Euler x angle that lays a part's long (y) axis from a toward b."""
    t = math.degrees(math.atan2(b[2] - a[2], b[1] - a[1]))
    return t - 180 if t > 90 else t + 180 if t < -90 else t


# ------------------------------------------------------------------ 1 돌진 사냥개

def hound(sc=0.86, **kw):
    begin('hound', sc, **kw); eye = mats('hound')
    p = 9                                                    # the whole body leans nose-down
    tf = frame((0, -0.02, 0.58), p)
    wedge('torso', (0.56, 0.98, 0.40), tf((0, 0, 0)), W, back=(0.8, 0.78), r=0.12, rot=(p, 0, 0))
    wedge('back', (0.50, 0.84, 0.10), tf((0, 0.06, 0.22)), A, front=(0.92, 1), back=(0.72, 1), r=0.045, rot=(p, 0, 0))
    ball('neck', 0.16, (0, -0.58, 0.52), MG)
    hd = frame((0, -0.86, 0.47), 16)
    wedge('head', (0.50, 0.52, 0.36), hd((0, 0, 0)), W, front=(0.72, 0.62), drop=0.04, r=0.09, rot=(16, 0, 0))
    wedge('helmet', (0.44, 0.54, 0.09), hd((0, -0.02, 0.20)), A, front=(0.62, 1), drop=0.03, r=0.04, rot=(16, 0, 0))
    visor('visor', (0.40, 0.14, 0.13), lambda q: hd((q[0], -0.17 + q[1], -0.04 + q[2])), 16, eye, 0.045)
    for s in (-1, 1):
        swept(f'ear{s}', [(0, 0), (0.12, 0), (0.38, 0.17), (0.31, 0.21)], 0.06, hd((s * 0.17, 0.16, 0.16)), W, lean=s * 32)
    # gallop: near front leg reaching, far one pulled back; near hind leg pushing off, far one tucked
    R = [0.10, 0.09, 0.08]
    leg('fl', [(-0.27, -0.42, 0.50), (-0.29, -0.70, 0.28), (-0.29, -0.96, 0.09)], R)
    rbox('flp', (0.20, 0.26, 0.10), (-0.29, -1.03, 0.05), LG, r=0.045)
    leg('fr', [(0.27, -0.42, 0.50), (0.29, -0.32, 0.26), (0.29, -0.10, 0.17)], R)
    rbox('frp', (0.20, 0.26, 0.10), (0.29, -0.03, 0.15), LG, r=0.045, rot=(-25, 0, 0))
    for s, knee, paw in ((-1, (-0.29, 0.62, 0.36), (-0.29, 0.96, 0.10)), (1, (0.29, 0.22, 0.32), (0.29, 0.18, 0.13))):
        ball(f'thigh{s}', 0.18, (s * 0.26, 0.36, 0.54), W, scale=(0.8, 1.25, 1.0), rot=(30 * s, 0, 0))
        leg(f'h{s}', [(s * 0.28, 0.40, 0.48), knee, paw], R)
        rbox(f'hp{s}', (0.20, 0.26, 0.10), (paw[0], paw[1] + 0.05, paw[2] - 0.04), LG, r=0.045, rot=(-35 if s < 0 else 15, 0, 0))
    tube('tail0', (0, 0.46, 0.62), (0, 0.84, 0.74), 0.10, MG, r1=0.08)
    tube('tail1', (0, 0.84, 0.74), (0, 1.12, 0.82), 0.08, MG, r1=0.06)
    ball('tailj', 0.095, (0, 0.84, 0.74), CH)
    plate('tailtip', [(0, -0.08), (0.15, 0.10), (0, 0.46), (-0.15, 0.10)], 0.07, (0, 1.08, 0.82), A)
    blob('blob', 0.62, 1.25)


# ------------------------------------------------------------------ 2 탄환 호버

def bullet(sc=1.0, **kw):
    begin('bullet', sc, **kw); eye = mats('bullet')
    ball('body', 0.31, (0, 0.02, 0.46), W, scale=(1.0, 2.2, 0.80), rot=(4, 0, 0))
    ball('nose', 0.255, (0, -0.42, 0.44), A, scale=(1.0, 1.15, 0.80), rot=(4, 0, 0))
    ball('visor', 0.20, (0, -0.65, 0.50), CH, scale=(1.0, 0.42, 0.40), outline=False)
    for s in (-1, 1):
        ball(f'eye{s}', 0.045, (s * 0.085, -0.71, 0.52), eye, scale=(0.9, 0.8, 1.25), outline=False)
        plate(f'wing{s}', [(0, 0), (s * 0.40, 0.40), (s * 0.38, 0.54), (0, 0.36)], 0.06, (s * 0.22, 0.0, 0.40), A, roll=s * 6)
    swept('fin', [(0, 0), (0.36, 0), (0.54, 0.30), (0.42, 0.31)], 0.06, (0, 0.14, 0.66), A)
    tube('nozzle', (0, 0.62, 0.47), (0, 0.80, 0.48), 0.14, LG, r1=0.16)
    T.disc('glow', 0.10, (0, 0.805, 0.48), flat((255, 214, 150)), facing=(0, 1, 0), depth=0.01)
    for y in (-0.22, 0.30):
        ball(f'pad{y}', 0.12, (0, y, 0.19), MG, scale=(1.4, 1.2, 0.45))
    blob('blob', 0.55, 0.95, loc=(0, 0.02, 0.003))


# ------------------------------------------------------------------ 3 바퀴 레이서

def racer(sc=0.95, **kw):
    begin('racer', sc, **kw); eye = mats('racer')
    wedge('body', (0.52, 1.50, 0.30), (0, -0.02, 0.32), W, front=(0.72, 0.42), back=(1.0, 1.15), drop=0.08, r=0.09)
    wedge('hood', (0.42, 0.96, 0.08), (0, -0.12, 0.42), A, front=(0.62, 1), r=0.035, rot=(8, 0, 0))
    visor('visor', (0.40, 0.14, 0.10), frame((0, -0.70, 0.31), 25), 25, eye, 0.04)
    for s in (-1, 1):
        tube(f'rw{s}', (s * 0.28, 0.48, 0.20), (s * 0.43, 0.48, 0.20), 0.20, CH)
        tube(f'rh{s}', (s * 0.42, 0.48, 0.20), (s * 0.44, 0.48, 0.20), 0.10, LG, outline=False)
        tube(f'fw{s}', (s * 0.24, -0.54, 0.12), (s * 0.35, -0.54, 0.12), 0.12, CH)
        tube(f'fh{s}', (s * 0.34, -0.54, 0.12), (s * 0.355, -0.54, 0.12), 0.055, LG, outline=False)
        swept(f'ear{s}', [(0, 0), (0.14, 0), (0.44, 0.17), (0.38, 0.21)], 0.05, (s * 0.15, -0.50, 0.40), A, lean=s * 35)
        tube(f'strut{s}', (s * 0.16, 0.56, 0.48), (s * 0.16, 0.68, 0.66), 0.04, MG)
        tube(f'pipe{s}', (s * 0.13, 0.66, 0.28), (s * 0.14, 0.82, 0.30), 0.055, LG)
    plate('spoiler', [(-0.36, 0), (0.36, 0), (0.32, 0.22), (-0.32, 0.22)], 0.06, (0, 0.62, 0.68), A)
    blob('blob', 0.58, 1.0)


# ------------------------------------------------------------------ 4 도마뱀 스프린터

def gecko(sc=1.05, **kw):
    begin('gecko', sc, **kw); eye = mats('gecko')
    ball('torso', 0.25, (0, 0.0, 0.29), W, scale=(1.05, 2.0, 0.58))
    ball('stripe', 0.24, (0, 0.0, 0.33), A, scale=(1.0, 2.0, 0.55))
    ball('head', 0.22, (0, -0.60, 0.31), W, scale=(1.1, 1.3, 0.66))
    ball('cap', 0.21, (0, -0.54, 0.38), A, scale=(1.05, 1.1, 0.42))
    for s in (-1, 1):
        ball(f'eyeball{s}', 0.085, (s * 0.16, -0.68, 0.41), W)
        ball(f'eye{s}', 0.042, (s * 0.18, -0.745, 0.43), CH, scale=(0.9, 0.8, 1.2), outline=False)
    tail = [(0, 0.42, 0.29), (0.11, 0.74, 0.26), (0.04, 1.04, 0.24), (-0.13, 1.30, 0.22)]
    rad = [0.13, 0.095, 0.06, 0.035]
    for i in range(3):
        tube(f'tail{i}', tail[i], tail[i + 1], rad[i], W, r1=rad[i + 1])
        if i: ball(f'tailj{i}', rad[i], tail[i], W, outline=False)
    ball('tailtip', 0.08, tail[-1], A, scale=(1, 1.6, 0.7), rot=(0, 0, 30))
    # diagonal stride, elbows splayed out to the side
    for name, sh, el, ft in (('fl', (-0.20, -0.30, 0.27), (-0.38, -0.46, 0.30), (-0.42, -0.66, 0.05)),
                             ('fr', (0.20, -0.30, 0.27), (0.38, -0.24, 0.30), (0.42, -0.08, 0.05)),
                             ('hl', (-0.20, 0.30, 0.27), (-0.38, 0.48, 0.30), (-0.44, 0.70, 0.05)),
                             ('hr', (0.20, 0.30, 0.27), (0.38, 0.22, 0.30), (0.44, 0.08, 0.05))):
        leg(name, [sh, el, ft], [0.085, 0.075, 0.065])
        ball(name + 'f', 0.10, ft, LG, scale=(1.2, 1.2, 0.45))
    blob('blob', 0.75, 1.15, loc=(0, 0.1, 0.003))


# ------------------------------------------------------------------ 5 랩터 러너

def raptor(sc=0.92, **kw):
    begin('raptor', sc, **kw); eye = mats('raptor')
    ball('body', 0.25, (0, 0.02, 0.72), W, scale=(1.0, 1.75, 0.92), rot=(8, 0, 0))
    ball('back', 0.25, (0, 0.06, 0.78), A, scale=(1.02, 1.72, 0.80), rot=(8, 0, 0))
    hd = frame((0, -0.56, 0.70), 10)
    wedge('head', (0.34, 0.46, 0.28), hd((0, 0, 0)), W, front=(0.78, 0.66), drop=0.03, r=0.08, rot=(10, 0, 0))
    visor('visor', (0.32, 0.13, 0.11), lambda q: hd((q[0], -0.17 + q[1], 0.0 + q[2])), 10, eye, 0.04)
    swept('crest', [(0, 0), (0.30, 0), (0.70, 0.17), (0.58, 0.22)], 0.08, hd((0, -0.12, 0.13)), A)
    tube('tail0', (0, 0.36, 0.78), (0, 0.80, 0.86), 0.15, W, r1=0.085)
    tube('tail1', (0, 0.80, 0.86), (0, 1.16, 0.92), 0.085, W, r1=0.05)
    ball('tailj', 0.085, (0, 0.80, 0.86), W, outline=False)
    plate('tailfin', [(0, -0.05), (0.15, 0.08), (0, 0.40), (-0.15, 0.08)], 0.06, (0, 1.10, 0.92), A)
    for s in (-1, 1):
        tube(f'arm{s}', (s * 0.14, -0.26, 0.62), (s * 0.17, -0.42, 0.52), 0.05, MG)
    # stride: near leg reaching forward, far leg pushing off behind
    for s, knee, ankle, toe in ((-1, (-0.22, -0.20, 0.42), (-0.22, -0.06, 0.15), (-0.22, -0.28, 0.05)),
                                (1, (0.22, 0.24, 0.42), (0.22, 0.50, 0.24), (0.22, 0.68, 0.06))):
        ball(f'thigh{s}', 0.17, (s * 0.20, 0.06, 0.60), W, scale=(0.75, 1.15, 1.2), rot=(-20 * s, 0, 0))
        leg(f'leg{s}', [(s * 0.21, 0.06, 0.55), knee, ankle], [0.09, 0.08, 0.07])
        rbox(f'foot{s}', (0.17, 0.30, 0.09), tuple((Vector(ankle) + Vector(toe)) / 2), LG, r=0.04, rot=(pitch_of(ankle, toe), 0, 0))
    blob('blob', 0.5, 1.1, loc=(0, 0.15, 0.003))


# ------------------------------------------------------------------ 6 화살 가오리

def manta(sc=1.0, **kw):
    begin('manta', sc, **kw); eye = mats('manta')
    ray = [(0, -0.56), (0.26, -0.42), (0.58, -0.12), (0.86, 0.26), (0.62, 0.24), (0.34, 0.26), (0.14, 0.40),
           (-0.14, 0.40), (-0.34, 0.26), (-0.62, 0.24), (-0.86, 0.26), (-0.58, -0.12), (-0.26, -0.42)]
    plate('wing', ray, 0.10, (0, 0, 0.32), W, r=0.04)
    plate('stripe', [(x * 0.74, y * 0.74 - 0.01) for x, y in ray], 0.04, (0, -0.02, 0.38), A, r=0.015)
    ball('body', 0.22, (0, -0.08, 0.38), W, scale=(1.0, 1.7, 0.55))
    visor('visor', (0.30, 0.14, 0.08), frame((0, -0.38, 0.41), 14), 14, eye, 0.035)
    tube('tail', (0, 0.36, 0.36), (0, 0.84, 0.42), 0.05, MG, r1=0.03)
    plate('tailtip', [(0, -0.06), (0.11, 0.06), (0, 0.24), (-0.11, 0.06)], 0.05, (0, 0.82, 0.42), A)
    for s in (-1, 1):
        ball(f'pod{s}', 0.10, (s * 0.30, 0.0, 0.24), MG, scale=(1.0, 1.6, 0.5))
    blob('blob', 0.8, 0.8, loc=(0, 0.1, 0.003))


# ------------------------------------------------------------------ 7 토끼 점프봇

def bunny(sc=0.95, **kw):
    begin('bunny', sc, **kw); eye = mats('bunny')
    ball('body', 0.27, (0, 0.08, 0.46), W, scale=(1.0, 1.75, 0.80), rot=(8, 0, 0))
    ball('head', 0.24, (0, -0.44, 0.54), W, scale=(1.0, 1.08, 0.92))
    visor('visor', (0.40, 0.14, 0.12), frame((0, -0.64, 0.54), 6), 6, eye, 0.045)
    for s in (-1, 1):
        ball(f'ear{s}', 0.11, (s * 0.12, -0.02, 0.86), A, scale=(0.8, 3.8, 0.34), rot=(12, 0, -s * 8))
        ball(f'thigh{s}', 0.18, (s * 0.20, 0.34, 0.40), W, scale=(0.75, 1.3, 1.0), rot=(-25, 0, 0))
        leg(f'shin{s}', [(s * 0.22, 0.42, 0.34), (s * 0.23, 0.70, 0.15)], [0.085, 0.075])
        rbox(f'foot{s}', (0.17, 0.42, 0.09), (s * 0.23, 0.88, 0.09), LG, r=0.04, rot=(-12, 0, 0))
        leg(f'arm{s}', [(s * 0.15, -0.26, 0.38), (s * 0.17, -0.56, 0.18)], [0.07, 0.065])
        ball(f'paw{s}', 0.08, (s * 0.17, -0.58, 0.16), LG, scale=(1, 1.3, 0.8))
    ball('tail', 0.09, (0, 0.56, 0.60), LG)
    blob('blob', 0.55, 1.05, loc=(0, 0.15, 0.003))


# ------------------------------------------------------------------ the real melee robot

def robot(height=1.55, **kw):
    """AttackRobot.fbx in its idle pose with MeleeRobot_Albedo, baked to a static mesh so it takes the same cel shading and hull."""
    root = begin('robot', 1.0, **kw)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=ASSETS + r'\FBX\AttackRobot.fbx')
    new = [o for o in bpy.data.objects if o not in before]
    rig = next(o for o in new if o.type == 'ARMATURE'); src = next(o for o in new if o.type == 'MESH')
    idle = next((a for a in bpy.data.actions if a.name.endswith('Melee_Idle')), None)
    if idle and rig.animation_data:
        rig.animation_data.action = idle
    bpy.context.scene.frame_set(1)
    me = bpy.data.meshes.new_from_object(src.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    me.transform(src.matrix_world)
    for o in new: bpy.data.objects.remove(o)
    zs = [v.co.z for v in me.vertices]
    me.transform(Matrix.Scale(height / (max(zs) - min(zs)), 4) @ Matrix.Translation((0, 0, -min(zs))))
    me.materials.clear(); me.materials.append(T.toon_img(ASSETS + r'\Textures\MeleeRobot_Albedo.tga'))
    ob = bpy.data.objects.new('robot_mesh', me); bpy.context.scene.collection.objects.link(ob); ob.parent = root
    T._hull(ob)
    blob('blob', 0.62, 0.62)
    return root


ALL = {'hound': hound, 'bullet': bullet, 'racer': racer, 'gecko': gecko, 'raptor': raptor, 'manta': manta, 'bunny': bunny,
       'robot': robot}


def tris(root):
    """Triangles of the design itself (no outline hulls, no ground blob)."""
    n = 0
    for o in root.children_recursive:
        if o.type == 'MESH' and not o.name.endswith('_hull') and 'blob' not in o.name:
            n += sum(len(p.vertices) - 2 for p in o.data.polygons)
    return n
