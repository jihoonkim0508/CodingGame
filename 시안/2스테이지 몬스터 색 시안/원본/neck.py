# 탱커병 1 철방패 거병 with the 1.7x shield (shield.t1big) and its neck raised: at 1.7x the shield (2.29 high) hid the
# head, so the torso and shoulder plates stand TORSO higher (a gunmetal waist fills the gap above the hips) and the head
# HEAD higher still on a short bull neck with a collar ring: the horned head and slit eyes clear the shield's top from the
# battle camera and a front 3/4 view, the brute still hunched between its shoulders. Shield, grip and legs as t1big; the
# left arm reaches the same grip from the raised shoulder.
import bpy
from mathutils import Vector
import toon as T
from toon import sbox, ball, tube, torus
from models import m, limb
from palette import GUN, WHITE, LGREY, MGREY
import shield as SH

TORSO = 0.45               # chest, back plate, spine ridge and shoulder plates up by this
HEAD = 1.05                # the head up by this (the neck is the difference)
LEAN = -0.08               # and forward over the shield's top edge, peering over it (hunched, not a giraffe neck)
HUMP = 0.35                # the back plate and spine ridge higher still, the shoulder plates a little: a hunched back that
                           # hides the neck from behind
TORSO_PARTS = ('chest', 'backplate', 'spine', 'pd-1cap', 'pd-1lame0', 'pd-1lame1', 'pd1cap', 'pd1lame0', 'pd1lame1')
HUMP_PARTS = ('backplate', 'spine')
SHOULDER_PARTS = ('pd-1cap', 'pd-1lame0', 'pd-1lame1', 'pd1cap', 'pd1lame0', 'pd1lame1')
HEAD_PARTS = ('helm', 'face', 'visor', 'eye-1', 'eye1', 'brow-1', 'brow1', 'horn-1', 'horn1', 'jaw', 'tusk-1', 'tusk1')
SHOULDER = Vector((-0.66, -0.06, 1.18))          # shield.t1big's left shoulder


def own(root, names):
    """The root's own parts (and their outline hulls) by name, whatever suffix Blender gave them; the shield's parts hang
    under its group, so its 'face' is not the head's."""
    return [o for o in root.children if o.name.replace('_hull', '').split('.')[0] in names]


def lift(root, names, dz, dy=0.0):
    for o in own(root, names): o.location.z += dz * T.SC; o.location.y += dy * T.SC


def t1neck(sc=1.0, k=SH.K, gy=-1.02, **kw):
    root = SH.t1big(sc=sc, k=k, gy=gy, **kw)
    T.ROOT = root; T.SC = sc
    lift(root, TORSO_PARTS, TORSO)
    lift(root, HUMP_PARTS, HUMP)
    lift(root, SHOULDER_PARTS, HUMP * 0.4)
    lift(root, HEAD_PARTS, HEAD, LEAN)
    G, W, L, Mg = m(GUN), m(WHITE), m(LGREY), m(MGREY)
    sbox('waist', (0.86, 0.60, 0.42 + TORSO * 0.3), (0, 0.06, 0.70 + TORSO * 0.75), G, r=0.12, levels=1, taper=(1.05, 1.0))
    neck0, neck1 = Vector((0, -0.14, 1.62 + TORSO * 0.3)), Vector((0, -0.36 + LEAN, 1.32 + HEAD))
    tube('neck', tuple(neck0), tuple(neck1), 0.33, G, r1=0.21, segs=12)     # a bull neck: wide where it meets the shoulders
    torus('collar', 0.27, 0.06, tuple(neck0.lerp(neck1, 0.45)), L, axis=neck1 - neck0, scale=(1, 1, 1.6), seg=(16, 6))
    # the left arm from the raised shoulder to t1big's wrist on the shield's edge
    for o in own(root, ('uarmL', 'elbowL', 'farmL')): bpy.data.objects.remove(o)
    d = SH.edge(k, SH.pivot(k, gy)) - SH.edge(1.0, SH.G0)
    F, S = Vector((-0.66, -0.58, 0.80)) + d, SHOULDER + Vector((0, 0, TORSO))
    E = SH.elbow(S, F, 0.58, 0.46, Vector((-1.0, 0.25, -0.35)))
    tube('uarmL', tuple(S), tuple(E), 0.14, G, segs=12)
    ball('elbowL', 0.15, tuple(E), Mg, seg=(12, 8))
    limb('farmL', tuple(E), tuple(F), 0.26, 0.26, W, r=0.07, taper=(0.85, 0.85))
    return root
