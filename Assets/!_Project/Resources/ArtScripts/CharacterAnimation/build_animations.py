"""Rigid mechanical rigs and in-place clips. Source meshes/UVs are retained."""
import bpy, math, json, sys
from pathlib import Path
from mathutils import Vector, Matrix, Euler, Quaternion
sys.dont_write_bytecode=True
sys.path.insert(0,str(Path(__file__).resolve().parent))
from melee_sword_frame import sword_frame

ROOT=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
FPS=30

def components(mesh):
    parents=list(range(len(mesh.vertices)))
    def find(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for e in mesh.edges:
        a,b=map(find,e.vertices);parents[b]=a
    groups={}
    for i in range(len(parents)):groups.setdefault(find(i),[]).append(i)
    return list(groups.values())

def center(ob,indices):
    pts=[ob.data.vertices[i].co for i in indices]
    return Vector([(min(p[k] for p in pts)+max(p[k] for p in pts))/2 for k in range(3)])

def transform(pivot,angles=(0,0,0),translation=(0,0,0)):
    return Matrix.Translation(Vector(translation))@Matrix.Translation(pivot)@Euler(tuple(math.radians(a) for a in angles),'XYZ').to_matrix().to_4x4()@Matrix.Translation(-pivot)

def bone(arm,name,head,tail,parent=None):
    b=arm.data.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=arm.data.edit_bones[parent]
    b.use_deform=True
    return name

def set_delta(rig,name,delta):
    pb=rig.pose.bones[name];pb.matrix=delta@rig.data.bones[name].matrix_local
    bpy.context.view_layer.update()

def solve_leg(rig,leg,body_delta,foot):
    hip=body_delta@leg['hip'];rest_knee=body_delta@leg['knee'];rest_foot=body_delta@leg['foot']
    length_a=(leg['knee']-leg['hip']).length;length_b=(leg['foot']-leg['knee']).length
    offset=foot-hip;distance=min(max(offset.length,abs(length_b-length_a)+1e-5),length_a+length_b-1e-5)
    direction=offset.normalized();a=(length_a**2-length_b**2+distance**2)/(2*distance)
    perpendicular=(rest_knee-hip)-direction*(rest_knee-hip).dot(direction)
    if perpendicular.length<1e-6:perpendicular=Vector((0,0,1))-direction*direction.z
    knee=hip+direction*a+perpendicular.normalized()*math.sqrt(max(0,length_a**2-a*a))
    solved_foot=hip+direction*distance
    for name,start,end in [(leg['upper'],hip,knee),(leg['lower'],knee,solved_foot)]:
        b=rig.data.bones[name];q=(b.tail_local-b.head_local).rotation_difference(end-start)@b.matrix_local.to_quaternion()
        rig.pose.bones[name].matrix=Matrix.LocRotScale(start,q,Vector((1,1,1)));bpy.context.view_layer.update()

def floor_height(ob):
    dg=bpy.context.evaluated_depsgraph_get();evaluated=ob.evaluated_get(dg);mesh=evaluated.to_mesh()
    z=min((evaluated.matrix_world@v.co).z for v in mesh.vertices);evaluated.to_mesh_clear();return z

def smooth(a,b,t):
    s=max(0,min(1,(t-a)/(b-a)));return s*s*(3-2*s)

def sample_keys(points,t):
    if t<=points[0][0]:return points[0][1]
    for (a,x),(b,y) in zip(points,points[1:]):
        if t<=b:
            s=smooth(a,b,t);return x+(y-x)*s
    return points[-1][1]

def material(name):
    mat=bpy.data.materials.new(name+'_Albedo');mat.use_nodes=True
    p=mat.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.7
    tex=mat.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/'Textures'/f'{name}_Albedo.tga'))
    mat.node_tree.links.new(tex.outputs['Color'],p.inputs['Base Color']);return mat

def create(name):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Blender~'/f'{name}_Imported.blend'))
    prefs=bpy.context.preferences.filepaths
    prefs.save_version=0
    if hasattr(prefs,'use_save_preview_images'):prefs.use_save_preview_images=False
    meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1
    ob=meshes[0];ob.name=name+'_Mesh';original=[ob.matrix_world@v.co for v in ob.data.vertices]
    ob.data.transform(ob.matrix_world);ob.matrix_world=Matrix.Identity(4)
    ob.data.materials.clear();ob.data.materials.append(material(name))
    parts=components(ob.data);centers=[center(ob,p) for p in parts]
    bpy.ops.object.select_all(action='DESELECT')
    data=bpy.data.armatures.new(name+'_Skeleton');rig=bpy.data.objects.new(name+'_Rig',data);bpy.context.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    bone(rig,'Root',(0,0,0),(0,0,.25));weights={};legs=[]
    if name=='Monster':
        bone(rig,'Body',(0,0,.74),(0,0,1.1),'Root');weights[0]='Body'
        for sx in [-1,1]:
            for sy in [-1,1]:
                def pick(count):return next(i for i,p in enumerate(parts) if len(p)==count and centers[i].x*sx>0 and centers[i].y*sy>0)
                shin,joint,hip_index=pick(84),pick(121),pick(108)
                hip=centers[hip_index];knee=centers[joint]
                pts=[ob.data.vertices[i].co for i in parts[shin]];low=min(p.z for p in pts)
                sole=[p for p in pts if p.z<low+.065];foot=sum(sole,Vector())/len(sole)
                suffix=('L' if sx<0 else 'R')+('F' if sy<0 else 'B')
                upper=bone(rig,'Leg_'+suffix+'_Upper',hip,knee,'Body');lower=bone(rig,'Leg_'+suffix+'_Lower',knee,foot,upper)
                weights[hip_index]='Body';weights[joint]=upper;weights[shin]=lower
                legs.append(dict(hip=hip.copy(),knee=knee.copy(),foot=foot.copy(),upper=upper,lower=lower,sx=sx,sy=sy))
    else:
        z=.81 if name=='MeleeRobot' else .63
        bone(rig,'TurretYaw',(0,0,z),(0,0,z+.16),'Root')
        bone(rig,'Body',(0,0,z+.1),(0,0,z+.32),'TurretYaw')
        head_z=1.56 if name=='MeleeRobot' else 1.31
        bone(rig,'Head',(0,0,head_z),(0,0,head_z+.35),'Body')
        if name=='MeleeRobot':
            bone(rig,'Arm_L',(-.25,-.02,.77),(-.65,-.02,.77),'Body')
            bone(rig,'Arm_R',(.25,-.02,.77),(.665,-.029,.707),'Body')
            bone(rig,'Sword',(.665,-.029,.707),(.765,.2,1.30),'Arm_R')
            weights={0:'Head',1:'Head',2:'Body',3:'Root',4:'Sword',5:'Sword',6:'Sword',7:'Sword',8:'TurretYaw',9:'Arm_L',10:'Arm_R'}
        else:
            bone(rig,'GunPitch',(.0069,-.036,1.845),(.0069,-.036,2.134),'Head')
            bone(rig,'Cannon',(.0069,.069,2.134),(.0069,-.333,2.134),'GunPitch')
            weights={0:'Head',1:'Head',2:'Cannon',3:'Cannon',4:'GunPitch',5:'Head',6:'Head',7:'Body',8:'Root'}
    bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;data.display_type='OCTAHEDRAL'
    for pb in rig.pose.bones:pb.rotation_mode='QUATERNION'
    assert len(weights)==len(parts)
    for i,indices in enumerate(parts):
        group=ob.vertex_groups.get(weights[i]) or ob.vertex_groups.new(name=weights[i]);group.add(indices,1,'REPLACE')
    modifier=ob.modifiers.new('Mechanical rigid skin','ARMATURE');modifier.object=rig;ob.parent=rig
    bpy.context.view_layer.update()
    evaluated=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());rest=evaluated.to_mesh()
    error=max((original[i]-evaluated.matrix_world@v.co).length for i,v in enumerate(rest.vertices));evaluated.to_mesh_clear()
    assert error<1e-5,('rest pose changed',error)
    scene=bpy.context.scene;scene.render.fps=FPS
    clips=[('Monster_Walk',33,True)] if name=='Monster' else [(('Melee' if name=='MeleeRobot' else 'Ranged')+'_Idle',61,True),(('Melee' if name=='MeleeRobot' else 'Ranged')+'_Attack',31 if name=='MeleeRobot' else 25,False)]
    animation_reports=[]
    for clip,frames,loop in clips:
        rig.animation_data_create();action=bpy.data.actions.new(clip);action.use_fake_user=True;rig.animation_data.action=action
        scene.frame_start=1;scene.frame_end=frames;last_q={};poses=[];min_floor=10
        for f in range(1,frames+1):
            scene.frame_set(f);t=(f-1)/(frames-1)
            for pb in rig.pose.bones:pb.matrix_basis=Matrix.Identity(4)
            bpy.context.view_layer.update()
            if name=='Monster':
                phase=2*math.pi*t
                body_delta=transform(Vector((0,0,.74)),(1.4*math.sin(phase),1.6*math.sin(phase),1.3*math.sin(phase)),(0,0,-.018+.013*math.cos(phase*2)))
                set_delta(rig,'Body',body_delta)
                for leg in legs:
                    u=(t+(0 if leg['sx']*leg['sy']>0 else .5))%1
                    if u<.6:y=-.095+.19*u/.6;lift=0
                    else:s=(u-.6)/.4;y=.095-.19*s;lift=.14*math.sin(math.pi*s)
                    target=leg['foot']+Vector((0,y,lift+.003))
                    solve_leg(rig,leg,body_delta,target)
                floor=floor_height(ob)
                if floor<0:
                    matrix=rig.pose.bones['Body'].matrix.copy();matrix.translation.z-=floor;rig.pose.bones['Body'].matrix=matrix;bpy.context.view_layer.update()
                min_floor=min(min_floor,floor_height(ob))
            else:
                z=.81 if name=='MeleeRobot' else .63;phase=2*math.pi*t
                yaw=3.5*math.sin(phase) if loop else sample_keys([(0,0),(.25,28),(.32,28),(.52,-100),(.64,-115),(.96,0),(1,0)],t) if name=='MeleeRobot' else .8*math.sin(math.pi*t)
                turret=transform(Vector((0,0,z)),(0,0,yaw));set_delta(rig,'TurretYaw',turret)
                lift=.014*math.sin(phase) if loop else -.025*math.sin(math.pi*t)**2
                lean=1*math.sin(phase) if loop else sample_keys([(0,0),(.2,-4),(.45,7),(.8,0),(1,0)],t) if name=='MeleeRobot' else -1.8*math.sin(math.pi*t)**2
                body=turret@transform(Vector((0,0,z+.1)),(lean,0,0),(0,0,lift));set_delta(rig,'Body',body)
                head=body@transform(Vector((0,0,1.56 if name=='MeleeRobot' else 1.31)),(1.4*math.sin(phase) if loop else -lean*.3,0,1.5*math.sin(phase) if loop else 0));set_delta(rig,'Head',head)
                if name=='MeleeRobot':
                    shoulder=Vector((.25,-.02,.77));grip=Vector((.665,-.029,.707))
                    if loop:
                        swing=2.5*math.sin(phase)
                        arm=body@transform(shoulder,(swing,0,0));set_delta(rig,'Arm_R',arm)
                        set_delta(rig,'Sword',arm@transform(grip,(1.5*math.sin(phase),0,0)))
                        set_delta(rig,'Arm_L',body@transform(Vector((-.25,-.02,.77)),(-swing*.14,0,0)))
                    else:
                        # Hold a horizontal blade outside the chassis and sweep
                        # across the front by rotating the torso.
                        body=turret;set_delta(rig,'Body',body);set_delta(rig,'Head',body)
                        ready=sample_keys([(0,0),(.25,-30),(.64,-30),(.96,0),(1,0)],t)
                        arm=body@transform(shoulder,(0,ready,0));set_delta(rig,'Arm_R',arm)
                        rest_direction=Vector((.1,.229,.593)).normalized()
                        horizontal=Vector((1,.02,.02)).normalized()
                        directions=[(0,rest_direction),(.25,horizontal),(.64,horizontal),(.96,rest_direction),(1,rest_direction)]
                        blade=Vector([sample_keys([(time,direction[k]) for time,direction in directions],t) for k in range(3)]).normalized()
                        finish=Euler((0,0,math.radians(-115)),'XYZ').to_quaternion()
                        flat_twist,approved_twist,_=sword_frame(ob,rest_direction,horizontal,finish)
                        target_twist=flat_twist.slerp(approved_twist,sample_keys([(0,0),(.40,0),(.52,1),(1,1)],t))
                        local_rotation=rest_direction.rotation_difference(blade)@Quaternion((1,0,0,0)).slerp(target_twist,-ready/30)
                        rotation=(body.to_quaternion()@local_rotation).to_matrix().to_4x4()
                        sword_delta=Matrix.Translation(arm@grip)@rotation@Matrix.Translation(-grip)
                        set_delta(rig,'Sword',sword_delta)
                        set_delta(rig,'Arm_L',body)
                else:
                    kick=1.2*math.sin(phase) if loop else sample_keys([(0,0),(.20,0),(.28,-7),(.40,-4),(.72,0),(1,0)],t)
                    gun=head@transform(Vector((.0069,-.036,1.845)),(kick,0,0));set_delta(rig,'GunPitch',gun)
                    recoil=0 if loop else sample_keys([(0,0),(.20,0),(.28,.14),(.40,.09),(.7,0),(1,0)],t)
                    set_delta(rig,'Cannon',gun@Matrix.Translation(Vector((0,recoil,0))))
            for pb in rig.pose.bones:
                q=pb.rotation_quaternion.copy()
                if pb.name in last_q and q.dot(last_q[pb.name])<0:pb.rotation_quaternion.negate()
                last_q[pb.name]=pb.rotation_quaternion.copy()
                for channel in ['location','rotation_quaternion','scale']:pb.keyframe_insert(data_path=channel,frame=f,group=pb.name)
            poses.append({pb.name:list(pb.matrix.translation)+list(pb.matrix.to_quaternion()) for pb in rig.pose.bones})
        for layer in action.layers:
            for strip in layer.strips:
                for bag in strip.channelbags:
                    for curve in bag.fcurves:
                        for key in curve.keyframe_points:key.interpolation='LINEAR'
        seam=max(abs(a-b) for bone_name in poses[0] for a,b in zip(poses[0][bone_name],poses[-1][bone_name]))
        if loop:assert seam<1e-4,(clip,'loop seam',seam)
        animation_reports.append(dict(clip=clip,frames=frames,fps=FPS,duration=(frames-1)/FPS,loop=loop,loop_seam=seam if loop else None,min_ground_z=min_floor if name=='Monster' else None))
    rig.animation_data.action=bpy.data.actions[clips[0][0]];scene.frame_start=1;scene.frame_end=clips[0][1];scene.frame_set(1)
    bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);ob.select_set(True);bpy.context.view_layer.objects.active=rig
    bpy.ops.file.make_paths_relative()
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Blender~'/f'{name}_Animated.blend'))
    bpy.ops.export_scene.fbx(filepath=str(ROOT/'FBX'/f'{name}_Animated.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',add_leaf_bones=False,armature_nodetype='NULL',use_armature_deform_only=True,bake_anim=True,bake_anim_use_all_bones=True,bake_anim_use_nla_strips=False,bake_anim_use_all_actions=True,bake_anim_force_startend_keying=True,bake_anim_step=1,bake_anim_simplify_factor=0,path_mode='RELATIVE',mesh_smooth_type='OFF')
    return dict(model=name,vertices=len(ob.data.vertices),faces=len(ob.data.polygons),bones=[b.name for b in rig.data.bones],rest_position_error=error,rigid_weights=True,clips=animation_reports)

names=['Monster','MeleeRobot','RangedRobot']
only=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else names
assert only and all(name in names for name in only),'Unknown model name'
report_path=ROOT/'Reports~'/'AnimationBuild.json'
previous={item['model']:item for item in json.loads(report_path.read_text())} if report_path.exists() else {}
for name in only:previous[name]=create(name)
reports=[previous[name] for name in names if name in previous]
(ROOT/'Reports~'/'AnimationBuild.json').write_text(json.dumps(reports,indent=2),encoding='utf8')
print('ANIMATION_BUILD_COMPLETE')

