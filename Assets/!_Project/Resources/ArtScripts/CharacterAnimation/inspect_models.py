import bpy, json, math
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
report={}
for name in ['Monster','MeleeRobot','RangedRobot']:
    bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'Sources~'/f'{name}_Source.fbx'))
    objects=[]
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH':
            objects.append({'name':ob.name,'type':ob.type,'location':list(ob.location)});continue
        vs=[ob.matrix_world@v.co for v in ob.data.vertices]
        parent=list(range(len(vs)))
        def find(i):
            while parent[i]!=i:parent[i]=parent[parent[i]];i=parent[i]
            return i
        for e in ob.data.edges:
            a,b=map(find,e.vertices);parent[b]=a
        groups={}
        for i in range(len(vs)):groups.setdefault(find(i),[]).append(i)
        comps=[]
        for indices in groups.values():
            pts=[vs[i] for i in indices]
            lo=[min(p[k] for p in pts) for k in range(3)];hi=[max(p[k] for p in pts) for k in range(3)]
            comps.append({'verts':len(indices),'center':[(a+b)/2 for a,b in zip(lo,hi)],'min':lo,'max':hi})
        objects.append({'name':ob.name,'type':ob.type,'verts':len(vs),'faces':len(ob.data.polygons),'materials':[m.name if m else None for m in ob.data.materials],'uv_layers':len(ob.data.uv_layers),'min':[min(v[k] for v in vs) for k in range(3)],'max':[max(v[k] for v in vs) for k in range(3)],'components':comps})
    report[name]=objects
    bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Blender~'/f'{name}_Imported.blend'))
(ROOT/'Reports~'/'ModelStructure.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('MODEL_INSPECTION_COMPLETE')

