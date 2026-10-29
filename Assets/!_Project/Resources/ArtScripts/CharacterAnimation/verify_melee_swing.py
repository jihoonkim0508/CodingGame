import bpy,json
from pathlib import Path
from mathutils.bvhtree import BVHTree
from mathutils import Vector
root=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
snap=Path(__file__).parent/'Verification~/Snapshots/AttackRobotBeforeSwing.fbx'

def read(path):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path))
    mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    return {'positions':[mesh.matrix_world@v.co for v in mesh.data.vertices],
            'faces':[tuple(p.vertices) for p in mesh.data.polygons],
            'uv':[v.uv.copy() for v in mesh.data.uv_layers[0].data],
            'bones':{b.name:b.matrix_local.copy() for b in rig.data.bones}}
before=read(snap);after=read(root/'FBX/AttackRobot.fbx')
assert len(before['positions'])==len(after['positions'])==697
position_error=max((a-b).length for a,b in zip(before['positions'],after['positions']))
uv_error=max((a-b).length for a,b in zip(before['uv'],after['uv']))
bone_error=max(abs(before['bones'][name][i][j]-after['bones'][name][i][j]) for name in before['bones'] for i in range(4) for j in range(4))
assert before['faces']==after['faces'] and position_error<1e-5 and uv_error<1e-6 and bone_error<1e-5
bpy.ops.wm.open_mainfile(filepath=str(root/'Blender~/AttackRobot.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
rig.animation_data.action=bpy.data.actions['Melee_Attack']
groups={v.index:mesh.vertex_groups[max(v.groups,key=lambda g:g.weight).group].name for v in mesh.data.vertices}
sword=[list(p.vertices) for p in mesh.data.polygons if all(groups[i]=='Sword' for i in p.vertices)]
body=[list(p.vertices) for p in mesh.data.polygons if all(groups[i] not in {'Sword','Arm_R'} for i in p.vertices)]
collisions=[];sword_heights=[]
for step in range(61):
    frame=1+step*.5;bpy.context.scene.frame_set(int(frame),subframe=frame%1)
    evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());baked=evaluated.to_mesh()
    points=[evaluated.matrix_world@v.co for v in baked.vertices]
    overlaps=BVHTree.FromPolygons(points,sword,all_triangles=False).overlap(BVHTree.FromPolygons(points,body,all_triangles=False))
    if overlaps:collisions.append({'frame':frame,'triangle_pairs':len(overlaps)})
    sword_heights.append(min(points[i].z for p in sword for i in p))
    evaluated.to_mesh_clear()
report={'mesh_vertices':697,'source_position_error':position_error,'uv_error':uv_error,'rest_bone_error':bone_error,'body_intersection_samples':collisions,'sample_count':61,'min_sword_height':min(sword_heights)}
(root/'Reports~/MeleeSwingGeometry.json').write_text(json.dumps(report,indent=2))
assert not collisions,collisions
print('MELEE_SWING_GEOMETRY_PASS: original geometry/UV/rest bones preserved; no blade/body surface intersections at 61 samples')
