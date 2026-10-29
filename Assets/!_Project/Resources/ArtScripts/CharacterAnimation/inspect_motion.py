import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Blender~'/'MeleeRobot_Animated.blend'))
rig=bpy.data.objects['MeleeRobot_Rig'];ob=bpy.data.objects['MeleeRobot_Mesh']
rig.animation_data.action=bpy.data.actions['Melee_Attack']
report=[]
for f in [1,7,14,16,23,31]:
    bpy.context.scene.frame_set(f);ev=ob.evaluated_get(bpy.context.evaluated_depsgraph_get());mesh=ev.to_mesh()
    group=ob.vertex_groups['Sword'].index
    indices=[v.index for v in ob.data.vertices if any(g.group==group for g in v.groups)]
    points=[ev.matrix_world@mesh.vertices[i].co for i in indices];center=sum(points,Vector())/len(points)
    report.append({'frame':f,'sword_center':list(center),'min':[min(p[k] for p in points) for k in range(3)],'max':[max(p[k] for p in points) for k in range(3)],'sword_scale':list(rig.pose.bones['Sword'].scale)})
    ev.to_mesh_clear()
(ROOT/'Reports~'/'SwordMotion.json').write_text(json.dumps(report,indent=2),encoding='utf8')
