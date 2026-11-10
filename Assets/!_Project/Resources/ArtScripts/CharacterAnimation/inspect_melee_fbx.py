import bpy,json
from pathlib import Path
root=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(root/'FBX/AttackRobot.fbx'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
report={'rig':rig.name,'rig_matrix':[list(row) for row in rig.matrix_world],'mesh_matrix':[list(row) for row in mesh.matrix_world],'verts':len(mesh.data.vertices),'bones':{b.name:{'head':list(b.head_local),'tail':list(b.tail_local)} for b in rig.data.bones},'actions':[a.name for a in bpy.data.actions]}
(root/'Reports~/ExistingMeleeRig.json').write_text(json.dumps(report,indent=2))
