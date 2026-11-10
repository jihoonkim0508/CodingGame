import bpy,json,math,sys
from pathlib import Path
from mathutils import Vector

ROOT=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
build=json.loads((ROOT/'Reports~'/'AnimationBuild.json').read_text())
only=set(sys.argv[sys.argv.index('--')+1:]) if '--' in sys.argv else set()
def point(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
for model in build:
    if only and not any(c['clip'] in only for c in model['clips']):continue
    name=model['model'];bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Blender~'/f'{name}_Animated.blend'))
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
    scene.render.resolution_x=480;scene.render.resolution_y=480;scene.render.resolution_percentage=100
    scene.world.color=(.19,.22,.27);scene.view_settings.view_transform='AgX'
    bpy.ops.object.camera_add(location=(3,-5,2.8));camera=bpy.context.object
    target=(0,0,.64 if name=='Monster' else 1.07);point(camera,target);camera.data.type='ORTHO';camera.data.ortho_scale=2.65 if name=='Monster' else 3.35;scene.camera=camera
    bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));floor=bpy.context.object
    mat=bpy.data.materials.new('Preview floor');mat.diffuse_color=(.075,.11,.16,1);floor.data.materials.append(mat)
    for location,power,size in [((2,-4,6),550,4),((-4,-1,3),250,4),((1,4,4),350,3)]:
        bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=power;light.data.size=size;point(light,(0,0,.8))
    for clip in model['clips']:
        if only and clip['clip'] not in only:continue
        rig.animation_data.action=bpy.data.actions[clip['clip']]
        out=ROOT/'Previews~'/clip['clip'];out.mkdir(exist_ok=True)
        frames=list(range(1,clip['frames']+1,2))
        if frames[-1]!=clip['frames']:frames.append(clip['frames'])
        for f in frames:
            scene.frame_set(f);scene.render.filepath=str(out/f'{f:03d}.png');bpy.ops.render.render(write_still=True)
        print('PREVIEW_COMPLETE',clip['clip'],flush=True)
print('ALL_ANIMATION_RENDERS_COMPLETE')
