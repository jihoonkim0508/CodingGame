import bpy,math
from pathlib import Path
from mathutils import Vector
ROOT=Path('C:/Fork/CodingGame/Assets/!_Project/Resources/Models/CharacterAnimation')
def material(name,texture):
    m=bpy.data.materials.new(name);m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.7
    t=m.node_tree.nodes.new('ShaderNodeTexImage');t.image=bpy.data.images.load(str(texture));m.node_tree.links.new(t.outputs['Color'],p.inputs['Base Color']);return m
def point(ob,at):ob.rotation_euler=(Vector(at)-ob.location).to_track_quat('-Z','Y').to_euler()
for name in ['Monster','MeleeRobot','RangedRobot']:
    bpy.ops.wm.open_mainfile(filepath=str(ROOT/'Blender~'/f'{name}_Imported.blend'))
    for ob in bpy.context.scene.objects:
        if ob.type=='MESH':ob.data.materials.clear();ob.data.materials.append(material(name,ROOT/'Textures'/f'{name}_Albedo.tga'))
    scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
    scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
    scene.world.color=(.2,.2,.2);scene.view_settings.view_transform='AgX'
    bpy.ops.object.camera_add(location=(3,-5,3));camera=bpy.context.object;point(camera,(0,0,1));camera.data.type='ORTHO';camera.data.ortho_scale=3.1;scene.camera=camera
    bpy.ops.object.light_add(type='AREA',location=(2,-4,6));bpy.context.object.data.energy=500;bpy.context.object.data.shape='DISK';bpy.context.object.data.size=5;point(bpy.context.object,(0,0,1))
    bpy.ops.object.light_add(type='AREA',location=(-4,-1,3));bpy.context.object.data.energy=200;bpy.context.object.data.size=4;point(bpy.context.object,(0,0,1))
    scene.render.filepath=str(ROOT/'Previews~'/f'{name}_Rest.png');bpy.ops.render.render(write_still=True)

