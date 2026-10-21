import bpy,json
from pathlib import Path
root=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
before=Path(__file__).parent/'Verification~/Snapshots/AttackRobotBeforeGripFix.fbx'
def sample(path):
    bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    action=next(a for a in bpy.data.actions if a.name.endswith('Melee_Attack'));rig.animation_data.action=action
    first,last=action.frame_range;result={}
    for label,t in [('ready',0),('middle',8/30),('finish',.64),('return',1)]:
        time=first+(last-first)*t;bpy.context.scene.frame_set(int(time),subframe=time%1)
        evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());baked=evaluated.to_mesh()
        result[label]=[evaluated.matrix_world@v.co for v in baked.vertices];evaluated.to_mesh_clear()
    return result
a=sample(before);b=sample(root/'FBX/AttackRobot.fbx')
errors={name:max((x-y).length for x,y in zip(a[name],b[name])) for name in a}
assert errors['ready']<1e-4 and errors['return']<1e-4 and errors['finish']<.002,errors
assert errors['middle']>.03,errors
motion=json.loads((root/'Reports~/MeleeSwingMotion.json').read_text())
assert min(item['blade_face_normal'][2] for item in motion if 9<=item['frame']<=13)>.99
report={'max_vertex_displacements_from_previous':errors,'middle_blade_face_up':True,'approved_finish_preserved':True,'result':'PASS'}
(root/'Reports~/MeleeGripFixVerification.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report,indent=2))
