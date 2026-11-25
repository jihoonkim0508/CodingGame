# The seven stage-2 normal-grunt drafts and the real melee robot (AttackRobot.fbx + MeleeRobot_Albedo.tga) for scale.
# Design units: the robot stands 1.55 tall; each grunt is built about 1 tall and scaled by its own SC.
# Faces lean back 10-25° so the battle camera (47.5° down) still sees them.
import bpy, os, math
from mathutils import Vector, Matrix
import toon as T
from toon import rbox, ball, tube, disc, blob, toon, begin
from palette import WHITE, GREY, LGREY, CHAR, ORANGE, CORAL, YELLOW, MAGENTA, PINK, AMBER, CRIMSON

ART = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../Assets/!_Project/Resources/Models/CharacterAnimation'))


def dots(x, y, z, r=0.052, tilt=0):
    """Two plain charcoal eye dots (no glow, no frame)."""
    for s in (-1, 1):
        ball(f'eye{s}', r, (s * x, y, z), toon(CHAR), scale=(1, 0.5, 1.25), rot=(-tilt, 0, 0), outline=False)


# ------------------------------------------------------------------ 1  두건 졸개: the reference's hooded robe minion, shrunk

def hood(sc=1.0, **kw):
    begin('hood', sc, **kw)
    A = toon(ORANGE)
    tube('robe', (0, 0, 0.03), (0, 0, 0.60), 0.33, A, r1=0.19, bevel=0.04)
    ball('head', 0.25, (0, 0.02, 0.74), A)
    tube('tip', (0, 0.10, 0.90), (0, 0.34, 0.96), 0.11, A, r1=0.035)   # the hood's tip flops back
    rbox('mask', (0.32, 0.07, 0.28), (0, -0.215, 0.72), toon(WHITE), r=0.07, rot=(-10, 0, 0))
    dots(0.07, -0.255, 0.735, r=0.05, tilt=10)
    for s in (-1, 1):
        tube(f'sleeve{s}', (s * 0.17, -0.02, 0.50), (s * 0.25, -0.17, 0.35), 0.07, A, r1=0.095)  # sleeves too long
        ball(f'hand{s}', 0.05, (s * 0.26, -0.20, 0.31), toon(CHAR))
        ball(f'foot{s}', 0.075, (s * 0.12, -0.28, 0.035), toon(CHAR), scale=(1, 1.5, 0.55))
    blob('blob', 0.46, 0.42)


# ------------------------------------------------------------------ 2  졸 폰: a chess pawn, the weakest piece

def pawn(sc=1.0, **kw):
    begin('pawn', sc, **kw)
    A = toon(CORAL)
    tube('base', (0, 0, 0.0), (0, 0, 0.10), 0.30, toon(LGREY), r1=0.27, bevel=0.03)
    tube('body', (0, 0, 0.10), (0, 0, 0.48), 0.24, A, r1=0.12)
    tube('collar', (0, 0, 0.46), (0, 0, 0.53), 0.19, toon(WHITE), bevel=0.025)
    ball('head', 0.22, (0, 0, 0.71), A)
    ball('face', 0.155, (0, -0.165, 0.75), toon(WHITE), scale=(1, 0.42, 0.85), rot=(-25, 0, 0), outline=False)
    dots(0.06, -0.226, 0.775, r=0.045, tilt=25)
    for s in (-1, 1):
        ball(f'hand{s}', 0.055, (s * 0.19, -0.04, 0.33), toon(WHITE))
    blob('blob', 0.40, 0.40)


# ------------------------------------------------------------------ 3  깡통 졸병: a tin can on stubby legs, lid flapping open

def can(sc=1.0, **kw):
    begin('can', sc, **kw)
    tube('can', (0, 0, 0.20), (0, 0, 0.66), 0.25, toon(YELLOW), bevel=0.03)
    for z in (0.19, 0.60):      # the top band sits just under the lip, so the can's top stays yellow
        tube(f'rim{z}', (0, 0, z), (0, 0, z + 0.05), 0.268, toon(LGREY), bevel=0.02)
    th = math.radians(62)        # lid hinged at the back, open 62°
    n = Vector((0, math.sin(th), math.cos(th)))
    c = Vector((0, 0.25, 0.69)) + 0.24 * Vector((0, -math.cos(th), math.sin(th)))
    disc('lid', 0.245, tuple(c), toon(WHITE), facing=tuple(n), depth=0.04, outline=True)
    dots(0.09, -0.25, 0.53, r=0.062)
    for s in (-1, 1):
        tube(f'leg{s}', (s * 0.11, 0, 0.20), (s * 0.11, -0.01, 0.06), 0.05, toon(CHAR))
        rbox(f'foot{s}', (0.13, 0.19, 0.06), (s * 0.11, -0.04, 0.03), toon(GREY), r=0.025)
        ball(f'hand{s}', 0.055, (s * 0.28, -0.02, 0.40), toon(WHITE))
    blob('blob', 0.40, 0.38)


# ------------------------------------------------------------------ 4  양동이 투구: a helmet far too big for him

def bucket(sc=1.0, **kw):
    begin('bucket', sc, **kw)
    ball('body', 0.25, (0, 0, 0.36), toon(WHITE), scale=(1, 0.92, 0.9))
    dots(0.08, -0.225, 0.37, r=0.05)
    # far too big, slipped back and to one side
    p0, p1 = Vector((0.0, 0.08, 0.50)), Vector((0.05, 0.15, 0.86))
    d = (p1 - p0).normalized()
    tube('bucket', p0, p1, 0.29, toon(MAGENTA), r1=0.22, bevel=0.03)
    tube('brim', p0 - 0.02 * d, p0 + 0.045 * d, 0.305, toon(WHITE), bevel=0.02)
    for s in (-1, 1):
        tube(f'leg{s}', (s * 0.10, 0, 0.18), (s * 0.11, -0.01, 0.06), 0.05, toon(CHAR))
        ball(f'foot{s}', 0.07, (s * 0.11, -0.04, 0.035), toon(GREY), scale=(1, 1.4, 0.6))
        ball(f'hand{s}', 0.06, (s * 0.26, -0.03, 0.30), toon(WHITE))
    blob('blob', 0.42, 0.40)


# ------------------------------------------------------------------ 5  콩알: a jelly bean with one sprout

def bean(sc=1.0, **kw):
    begin('bean', sc, **kw)
    ball('body', 0.30, (0, 0, 0.44), toon(PINK), scale=(0.95, 0.85, 1.3), rot=(8, 0, 0))
    ball('belly', 0.21, (0, -0.19, 0.50), toon(WHITE), scale=(0.9, 0.42, 1.15), rot=(-5, 0, 0), outline=False)
    dots(0.075, -0.268, 0.62, r=0.05, tilt=12)
    tube('stem', (0, 0.0, 0.80), (0.03, 0.01, 0.93), 0.03, toon(GREY))
    ball('leaf', 0.075, (0.09, 0.01, 0.945), toon(WHITE), scale=(1.5, 0.5, 0.5), rot=(0, -25, 0))
    for s in (-1, 1):
        ball(f'foot{s}', 0.07, (s * 0.11, -0.05, 0.04), toon(CHAR), scale=(1, 1.3, 0.6))
        ball(f'hand{s}', 0.05, (s * 0.29, -0.04, 0.40), toon(WHITE))
    blob('blob', 0.38, 0.36)


# ------------------------------------------------------------------ 6  외바퀴: a ball wobbling on one wheel

def wheel(sc=1.0, **kw):
    root = begin('wheel', sc, **kw)
    tube('tyre', (-0.06, 0, 0.16), (0.06, 0, 0.16), 0.16, toon(CHAR), bevel=0.035)
    tube('hub', (-0.075, 0, 0.16), (0.075, 0, 0.16), 0.06, toon(LGREY))
    rbox('fork', (0.18, 0.10, 0.14), (0, 0, 0.33), toon(GREY), r=0.04)
    ball('body', 0.25, (0, 0, 0.57), toon(AMBER))
    rbox('visor', (0.30, 0.08, 0.18), (0, -0.215, 0.60), toon(WHITE), r=0.07, rot=(-12, 0, 0), outline=False)
    dots(0.07, -0.255, 0.605, r=0.045, tilt=12)
    for s in (-1, 1):   # arms flung out for balance
        tube(f'arm{s}', (s * 0.22, 0, 0.58), (s * 0.38, -0.02, 0.70), 0.045, toon(CHAR))
        ball(f'hand{s}', 0.065, (s * 0.39, -0.02, 0.71), toon(WHITE))
    root.rotation_euler.y = math.radians(9)
    blob('blob', 0.34, 0.30)


# ------------------------------------------------------------------ 7  납작 벌레: a flat little six-legged bug

def bug(sc=1.0, **kw):
    begin('bug', sc, **kw)
    ball('shell', 0.36, (0, 0.06, 0.24), toon(CRIMSON), scale=(0.85, 1.0, 0.55))
    ball('head', 0.17, (0, -0.30, 0.24), toon(WHITE), scale=(1.15, 0.9, 0.9))
    dots(0.07, -0.443, 0.27, r=0.048, tilt=10)
    for s in (-1, 1):
        tube(f'ant{s}', (s * 0.06, -0.36, 0.36), (s * 0.13, -0.42, 0.48), 0.028, toon(GREY))
        ball(f'antip{s}', 0.05, (s * 0.13, -0.42, 0.49), toon(WHITE))
        for i, y in enumerate((-0.14, 0.06, 0.26)):
            tube(f'leg{s}{i}', (s * 0.25, y, 0.15), (s * 0.34, y - 0.02, 0.03), 0.036, toon(CHAR))
    blob('blob', 0.46, 0.50)


# ------------------------------------------------------------------ the real melee robot

def robot(sc=1.0, **kw):
    """AttackRobot.fbx in its idle pose's first frame, baked to a static mesh, with MeleeRobot_Albedo.tga."""
    root = begin('robot', sc, **kw)
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ART, 'FBX', 'AttackRobot.fbx'))
    new = [o for o in bpy.data.objects if o not in before]
    rig = next(o for o in new if o.type == 'ARMATURE'); src = next(o for o in new if o.type == 'MESH')
    idle = next(a for a in bpy.data.actions if a.name.endswith('Idle'))
    rig.animation_data.action = idle
    if hasattr(rig.animation_data, 'action_slot') and idle.slots: rig.animation_data.action_slot = idle.slots[0]
    bpy.context.scene.frame_set(1); bpy.context.view_layer.update()
    me = bpy.data.meshes.new_from_object(src.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    me.transform(src.matrix_world)
    for o in new: bpy.data.objects.remove(o)
    zs = [v.co.z for v in me.vertices]
    me.transform(Matrix.Scale(1.55 * T.SC / (max(zs) - min(zs)), 4))
    for p in me.polygons: p.use_smooth = True
    me.materials.clear(); me.materials.append(toon(os.path.join(ART, 'Textures', 'MeleeRobot_Albedo.tga')))
    ob = bpy.data.objects.new('robot_mesh', me); bpy.context.scene.collection.objects.link(ob); ob.parent = root
    T._hull(ob)
    blob('blob', 0.62, 0.62)
    return root


# each grunt's size: about 82 px tall through the battle camera when the robot is about 123 px (the flat bug about 66)
SC = {'1': 1.09, '2': 1.14, '3': 1.06, '4': 1.2, '5': 1.2, '6': 1.22, '7': 1.1}
ALL = {'1': hood, '2': pawn, '3': can, '4': bucket, '5': bean, '6': wheel, '7': bug}
