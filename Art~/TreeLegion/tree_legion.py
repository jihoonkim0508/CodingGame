# The tree legion (군단 트리) monster: two low-poly models in the soft pastel desk-diorama style, built headless:
#   "C:\Program Files\Blender Foundation\Blender 5.3\blender.exe" -b --factory-startup -P Art~/TreeLegion/tree_legion.py
# Writes Assets/!_Project/Defense/Models/TreeLegion/:
#   TreeLegion.fbx      the root (대장): a stubby trunk with angry eyes, a leaf tuft on top and four root legs (Leg0..Leg3,
#                        pivots at the hips, swung by DefenseTreeView so it waddles)
#   TreeLegionNode.fbx  a child node: a leaf-and-fruit cluster with small eyes (its branch is drawn in Unity, parent to child)
#   TreePalette.png     4x4 colour palette; every face's UVs sit on one cell, so one material colours the whole model and the
#                        view's _BaseColor tint (white = as painted) still works.
import bpy, bmesh, math, os

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.normpath(os.path.join(HERE, "..", "..", "Assets", "!_Project", "Defense", "Models", "TreeLegion"))
os.makedirs(OUT, exist_ok=True)

PALETTE = [  # cell index -> colour (linear-ish pastel, sRGB in the PNG)
    (0.66, 0.47, 0.34), (0.47, 0.33, 0.24), (0.56, 0.82, 0.46), (0.74, 0.93, 0.56),
    (1.00, 0.60, 0.74), (1.00, 0.27, 0.38), (1.00, 1.00, 1.00), (0.22, 0.16, 0.16),
    (0.40, 0.66, 0.36), (0.98, 0.84, 0.48), (0.80, 0.62, 0.46), (0.30, 0.52, 0.30),
    (1, 1, 1), (1, 1, 1), (1, 1, 1), (1, 1, 1)]
BARK, BARK_DARK, LEAF, LEAF_LIGHT, FRUIT, EYE, WHITE, BROW, LEAF_DARK, GLOW, BARK_LIGHT, LEAF_DEEP = range(12)

bpy.ops.wm.read_factory_settings(use_empty=True)

img = bpy.data.images.new("TreePalette", 4, 4, alpha=False)
pixels = []
for i in range(16):
    r, g, b = PALETTE[i]
    pixels += [r, g, b, 1.0]
img.pixels = pixels
img.filepath_raw = os.path.join(OUT, "TreePalette.png")
img.file_format = "PNG"
img.save()

mat = bpy.data.materials.new("TreeLegion")
mat.use_nodes = True
bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
tex = mat.node_tree.nodes.new("ShaderNodeTexImage"); tex.image = img; tex.interpolation = "Closest"
mat.node_tree.links.new(tex.outputs[0], bsdf.inputs[0])


def paint(obj, cell):
    """Every face's UVs onto one palette cell."""
    me = obj.data
    if not me.uv_layers: me.uv_layers.new()
    u, v = (cell % 4 + .5) / 4, (cell // 4 + .5) / 4
    for loop in me.uv_layers.active.data: loop.uv = (u, v)
    me.materials.clear(); me.materials.append(mat)


def shade(obj, smooth=False):
    for p in obj.data.polygons: p.use_smooth = smooth


def cylinder(name, r1, r2, depth, loc, cell, verts=8, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r1, radius2=r2, depth=depth, location=loc, rotation=rot)
    o = bpy.context.active_object; o.name = name; paint(o, cell); shade(o); return o


def blob(name, radius, loc, cell, subdiv=1, scale=(1, 1, 1), smooth=False):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=subdiv, radius=radius, location=loc)
    o = bpy.context.active_object; o.name = name; o.scale = scale; paint(o, cell); shade(o, smooth); return o


def join(objs, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join(); o = bpy.context.active_object; o.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    return o


def eyes(y, z, spread, size, cell_white=WHITE, front=-1):
    """Two angry eyes on the -Y (front) side: white, red pupil, slanted dark brows."""
    parts = []
    for side in (-1, 1):
        x = side * spread
        parts.append(blob("EyeWhite", size, (x, y, z), cell_white, 2, (1, .55, 1), True))
        parts.append(blob("Pupil", size * .58, (x * 1.05, y + front * size * .42, z - size * .08), EYE, 2, (1, .5, 1), True))
        brow = cylinder("Brow", size * .28, size * .28, size * 1.6, (x, y + front * size * .3, z + size * 1.0), BROW, 6,
                        (0, math.radians(90 + side * 22), 0))
        parts.append(brow)
    return parts


def export(objs, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, filename), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                             mesh_smooth_type="FACE", add_leaf_bones=False, path_mode="STRIP")


# ---- Root (대장): trunk + crown tuft + face, legs separate --------------------------------------------------------
trunk_parts = [
    cylinder("TrunkBody", .34, .26, .95, (0, 0, .62), BARK, 9),
    cylinder("TrunkRing", .355, .355, .08, (0, 0, .42), BARK_DARK, 9),
    cylinder("TrunkTop", .26, .2, .16, (0, 0, 1.15), BARK_LIGHT, 9),
    blob("Tuft", .3, (0, .04, 1.36), LEAF, 1, (1.1, 1, .8)),
    blob("TuftLeaf", .18, (.22, -.02, 1.46), LEAF_LIGHT, 1),
    blob("TuftLeaf2", .16, (-.2, .1, 1.44), LEAF_DARK, 1),
    blob("Knot", .07, (.2, -.24, .55), BARK_DARK, 1),
] + eyes(-.27, .82, .13, .1)
trunk = join(trunk_parts, "Trunk")
bpy.context.scene.cursor.location = (0, 0, 0); bpy.ops.object.origin_set(type="ORIGIN_CURSOR")  # the model stands on its origin
legs = []
for i in range(4):
    a = math.radians(45 + 90 * i)
    hip = (math.cos(a) * .2, math.sin(a) * .2, .22)
    # a root leg: a bent cone from the hip down and out to a little foot; the object's origin at the hip so it swings there
    leg = cylinder("Leg%d" % i, .1, .045, .32, (0, 0, -.13), BARK_DARK, 6)
    foot = blob("Foot", .07, (0, -.06, -.29), BARK, 1, (1, 1.4, .6))
    leg = join([leg, foot], "Leg%d" % i)
    bpy.context.scene.cursor.location = (0, 0, 0); bpy.ops.object.origin_set(type="ORIGIN_CURSOR")  # pivot at the hip
    leg.location = hip
    leg.rotation_euler = (math.radians(-18), 0, a + math.radians(90))  # foot out, leaning out a little
    bpy.ops.object.select_all(action="DESELECT"); leg.select_set(True); bpy.context.view_layer.objects.active = leg
    legs.append(leg)
for leg in legs: leg.parent = trunk
export([trunk] + legs, "TreeLegion.fbx")

# ---- Child node: a leaf cluster with a fruit and small eyes -------------------------------------------------------
bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete()
node_parts = [
    blob("Leaves", .3, (0, 0, .3), LEAF, 1, (1.1, 1, .95)),
    blob("Leaves2", .2, (.2, .08, .42), LEAF_LIGHT, 1),
    blob("Leaves3", .19, (-.21, .06, .38), LEAF_DARK, 1),
    blob("Leaves4", .17, (.02, .14, .52), LEAF_DEEP, 1),
    blob("Fruit", .085, (.17, -.2, .2), FRUIT, 2, (1, 1, 1), True),
    blob("Fruit2", .07, (-.2, -.17, .17), FRUIT, 2, (1, 1, 1), True),
    cylinder("Stem", .05, .035, .18, (0, 0, .03), BARK, 6),
] + eyes(-.27, .33, .1, .075)
node = join(node_parts, "Crown")
export([node], "TreeLegionNode.fbx")
print("[tree_legion] wrote", OUT)

# Optional look check: blender -b --factory-startup -P tree_legion.py -- <preview.png> renders both models side by side.
import sys
if "--" in sys.argv and len(sys.argv) > sys.argv.index("--") + 1:
    bpy.ops.object.select_all(action="SELECT"); bpy.ops.object.delete()
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, "TreeLegion.fbx"))
    for o in bpy.context.selected_objects:
        if o.parent is None: o.location.x -= .5
    bpy.ops.import_scene.fbx(filepath=os.path.join(OUT, "TreeLegionNode.fbx"))
    for o in bpy.context.selected_objects:
        if o.parent is None: o.location.x += .6; o.location.z += .6
    scene = bpy.context.scene
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
    cam.location = (0.6, -3.6, 1.9); cam.rotation_euler = (math.radians(72), 0, math.radians(8))
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN")); scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(40), math.radians(20), math.radians(30)); sun.data.energy = 3
    scene.world = bpy.data.worlds.new("W"); scene.world.color = (.35, .4, .5)
    scene.render.resolution_x, scene.render.resolution_y = 800, 600
    scene.render.filepath = sys.argv[sys.argv.index("--") + 1]
    bpy.ops.render.render(write_still=True)
