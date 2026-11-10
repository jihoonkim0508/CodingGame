import bpy,math
from pathlib import Path
from mathutils import Vector
root=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
bpy.ops.wm.open_mainfile(filepath=str(root/'Blender~/AttackRobot.blend'))
rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
rig.animation_data.action=bpy.data.actions['Melee_Attack']
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
scene.render.resolution_x=480;scene.render.resolution_y=480;scene.render.resolution_percentage=100
if scene.world is None:scene.world=bpy.data.worlds.new('Preview World')
scene.world.color=(.19,.22,.27);scene.view_settings.view_transform='AgX'
def point(ob,target):ob.rotation_euler=(Vector(target)-ob.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=(3,-5,2.8));camera=bpy.context.object;point(camera,(0,0,1.07));camera.data.type='ORTHO';camera.data.ortho_scale=3.9;scene.camera=camera
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.015));floor=bpy.context.object
mat=bpy.data.materials.new('Preview floor');mat.diffuse_color=(.075,.11,.16,1);floor.data.materials.append(mat)
for location,power,size in [((2,-4,6),550,4),((-4,-1,3),250,4),((1,4,4),350,3)]:
    bpy.ops.object.light_add(type='AREA',location=location);light=bpy.context.object;light.data.energy=power;light.data.size=size;point(light,(0,0,.8))
out=root/'Previews~/Melee_Attack';out.mkdir(exist_ok=True)
for f in range(1,32,2):
    scene.frame_set(f);scene.render.filepath=str(out/f'{f:03d}.png');bpy.ops.render.render(write_still=True)
print('MELEE_SWING_RENDERS_COMPLETE')
