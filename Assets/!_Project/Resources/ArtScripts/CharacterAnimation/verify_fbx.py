import bpy,json
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
build=json.loads((ROOT/'Reports~'/'AnimationBuild.json').read_text());report=[]
for model in build:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT/'FBX'/f"{model['model']}_Animated.fbx"))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    assert len(rig.data.bones)==len(model['bones'])
    item={'model':model['model'],'bones':len(rig.data.bones),'vertices':len(mesh.data.vertices),'actions':[]}
    assert len(mesh.data.vertices)==model['vertices']
    assert len(mesh.vertex_groups)==len(set(g.name for g in mesh.vertex_groups))
    for expected in model['clips']:
        action=next(a for a in bpy.data.actions if a.name.endswith(expected['clip']))
        rig.animation_data.action=action
        first,last=map(int,action.frame_range);samples=[]
        for f in [first,round(first+(last-first)*.44),last]:
            bpy.context.scene.frame_set(f);dg=bpy.context.evaluated_depsgraph_get();ev=mesh.evaluated_get(dg);result=ev.to_mesh()
            points=[ev.matrix_world@v.co for v in result.vertices];samples.append(points);ev.to_mesh_clear()
        movement=max((a-b).length for a,b in zip(samples[0],samples[1]));seam=max((a-b).length for a,b in zip(samples[0],samples[-1]))
        assert movement>.005,(expected['clip'],'no animation')
        if expected['loop']:assert seam<.0001,(expected['clip'],'loop mismatch',seam)
        item['actions'].append({'clip':action.name,'range':[first,last],'movement':movement,'seam':seam})
    assert len(bpy.data.actions)==len(model['clips']),(model['model'],[a.name for a in bpy.data.actions])
    report.append(item)
(ROOT/'Reports~'/'FbxRoundtrip.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('FBX_ROUNDTRIP_PASS')
