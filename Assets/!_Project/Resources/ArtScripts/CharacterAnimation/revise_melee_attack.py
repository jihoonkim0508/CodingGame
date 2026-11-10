"""Revise only the attack action on the user's currently named AttackRobot FBX."""
import bpy, json, math, sys
from pathlib import Path
from mathutils import Vector, Matrix, Euler, Quaternion
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from melee_sword_frame import sword_frame

ROOT=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.context.preferences.filepaths.save_version=0
bpy.ops.import_scene.fbx(filepath=str(ROOT/'FBX/AttackRobot.fbx'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
assert len(rig.data.bones)==7 and len(mesh.data.vertices)==697
for action in list(bpy.data.actions):
    if action.name.endswith('Melee_Attack'):bpy.data.actions.remove(action)
    elif action.name.endswith('Melee_Idle'):action.name='Melee_Idle';action.use_fake_user=True
    else:raise RuntimeError('Unexpected action: '+action.name)
action=bpy.data.actions.new('Melee_Attack');action.use_fake_user=True
rig.animation_data.action=action
for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'

def keys(points,t):
    if t<=points[0][0]:return points[0][1]
    for (a,x),(b,y) in zip(points,points[1:]):
        if t<=b:
            u=max(0,min(1,(t-a)/(b-a)));u=u*u*(3-2*u)
            return x+(y-x)*u
    return points[-1][1]
def transform(pivot,angles=(0,0,0),translation=(0,0,0)):
    return Matrix.Translation(Vector(translation))@Matrix.Translation(pivot)@Euler(tuple(math.radians(v) for v in angles),'XYZ').to_matrix().to_4x4()@Matrix.Translation(-pivot)
def pose(name,delta):
    rig.pose.bones[name].matrix=delta@rig.data.bones[name].matrix_local
    bpy.context.view_layer.update()

scene=bpy.context.scene;scene.render.fps=30;scene.frame_start=1;scene.frame_end=31
last={};report=[];samples=[]
rest_direction=Vector((.1,.229,.593)).normalized()
horizontal=Vector((1,.02,.02)).normalized()
# Match the approved follow-through orientation in torso space. A shortest-arc
# rotation solved in world space every frame caused the guard/blade to roll
# while the torso turned. Keep this local roll fixed throughout the slash.
finish=Euler((0,0,math.radians(-115)),'XYZ').to_quaternion()
flat_twist,approved_twist,blade_normal=sword_frame(mesh,rest_direction,horizontal,finish)
grip=rig.data.bones['Sword'].head_local.copy()
arm_pivot=rig.data.bones['Arm_R'].head_local.copy()
sword_group=mesh.vertex_groups['Sword'].index
sword_indices=[v.index for v in mesh.data.vertices if any(g.group==sword_group for g in v.groups)]
for f in range(1,32):
    scene.frame_set(f);t=(f-1)/30
    for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
    bpy.context.view_layer.update()
    # A level, outward-facing blade sweeps the front through torso rotation.
    yaw=keys([(0,0),(.25,28),(.32,28),(.52,-100),(.64,-115),(.96,0),(1,0)],t)
    turret=transform(Vector((0,0,.81)),(0,0,yaw));pose('TurretYaw',turret)
    body=turret;pose('Body',body);pose('Head',body)
    ready=keys([(0,0),(.25,-30),(.64,-30),(.96,0),(1,0)],t)
    arm=body@transform(arm_pivot,(0,ready,0));pose('Arm_R',arm)
    directions=[(0,rest_direction),(.25,horizontal),(.64,horizontal),(.96,rest_direction),(1,rest_direction)]
    blade=Vector([keys([(time,d[k]) for time,d in directions],t) for k in range(3)]).normalized()
    final_blend=keys([(0,0),(.40,0),(.52,1),(1,1)],t)
    target_twist=flat_twist.slerp(approved_twist,final_blend)
    twist=Quaternion((1,0,0,0)).slerp(target_twist,-ready/30)
    local_rotation=rest_direction.rotation_difference(blade)@twist
    rotation=(body.to_quaternion()@local_rotation).to_matrix().to_4x4()
    pose('Sword',Matrix.Translation(arm@grip)@rotation@Matrix.Translation(-grip))
    pose('Arm_L',body)
    for pb in rig.pose.bones:
        q=pb.rotation_quaternion.copy()
        if pb.name in last and q.dot(last[pb.name])<0:pb.rotation_quaternion.negate()
        last[pb.name]=pb.rotation_quaternion.copy()
        for channel in ['location','rotation_quaternion','scale']:pb.keyframe_insert(data_path=channel,frame=f,group=pb.name)
    grip_error=(rig.pose.bones['Sword'].head-arm@grip).length
    assert grip_error<1e-5,('detached sword',f,grip_error)
    evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());baked=evaluated.to_mesh()
    points=[evaluated.matrix_world@v.co for v in baked.vertices];samples.append(points)
    center=sum((points[i] for i in sword_indices),Vector())/len(sword_indices)
    report.append({'frame':f,'sword_center':list(center),'blade_direction':list(blade),'blade_face_normal':list(body.to_quaternion()@local_rotation@blade_normal),'local_rotation':list(local_rotation),'grip_error':grip_error})
    evaluated.to_mesh_clear()
for layer in action.layers:
    for strip in layer.strips:
        for bag in strip.channelbags:
            for curve in bag.fcurves:
                for key in curve.keyframe_points:key.interpolation='LINEAR'
assert max((a-b).length for a,b in zip(samples[0],samples[-1]))<1e-4,'Attack does not return to rest'
for mat in mesh.data.materials:
    if not mat or not mat.use_nodes:continue
    for node in mat.node_tree.nodes:
        if node.type=='TEX_IMAGE':node.image=bpy.data.images.load(str(ROOT/'Textures/MeleeRobot_Albedo.tga'),check_existing=True)
rig.animation_data.action=action;scene.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);mesh.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.file.make_paths_relative()
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Blender~/AttackRobot.blend'))
bpy.ops.export_scene.fbx(filepath=str(ROOT/'FBX/AttackRobot.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,armature_nodetype='NULL',use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='RELATIVE',mesh_smooth_type='OFF')
(ROOT/'Reports~/MeleeSwingMotion.json').write_text(json.dumps(report,indent=2))
print('MELEE_SWING_BUILD_PASS')
