import bpy,json
from pathlib import Path
from mathutils import Vector
root=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
bpy.ops.wm.open_mainfile(filepath=str(root/'Blender~/AttackRobot.blend'))
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH');rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
parents=list(range(len(mesh.data.vertices)))
def find(i):
    while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
    return i
for edge in mesh.data.edges:
    a,b=map(find,edge.vertices);parents[b]=a
components={}
for i in range(len(parents)):components.setdefault(find(i),[]).append(i)
report=[]
for part in components.values():
    v=mesh.data.vertices[part[0]];group=mesh.vertex_groups[max(v.groups,key=lambda g:g.weight).group].name
    if group not in {'Sword','Arm_R'}:continue
    points=[mesh.data.vertices[i].co for i in part]
    report.append({'bone':group,'indices':part,'count':len(part),'min':[min(p[k] for p in points) for k in range(3)],'max':[max(p[k] for p in points) for k in range(3)]})
(root/'Reports~/SwordComponents.json').write_text(json.dumps(report,indent=2))
print(json.dumps([{k:v for k,v in item.items() if k!='indices'} for item in report],indent=2))
