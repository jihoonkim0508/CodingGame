"""Resolve both the blade direction and its face normal, keeping roll explicit."""
import numpy as np
from mathutils import Vector,Matrix

def sword_frame(mesh,rest_direction,horizontal,finish):
    group=mesh.vertex_groups['Sword'].index
    indices={v.index for v in mesh.data.vertices if any(g.group==group for g in v.groups)}
    parents={i:i for i in indices}
    def find(i):
        while parents[i]!=i:parents[i]=parents[parents[i]];i=parents[i]
        return i
    for edge in mesh.data.edges:
        a,b=edge.vertices
        if a in indices and b in indices:parents[find(b)]=find(a)
    parts={}
    for i in indices:parts.setdefault(find(i),[]).append(i)
    blade=max(parts.values(),key=lambda part:max(mesh.data.vertices[i].co.z for i in part)-min(mesh.data.vertices[i].co.z for i in part))
    coordinates=np.array([list(mesh.data.vertices[i].co) for i in blade])
    _,axes=np.linalg.eigh(np.cov(coordinates,rowvar=False))
    normal=Vector(axes[:,0].tolist())
    if normal.y<0:normal=-normal
    normal=(normal-rest_direction*normal.dot(rest_direction)).normalized()
    width=normal.cross(rest_direction).normalized()
    target_normal=(Vector((0,0,1))-horizontal*horizontal.z).normalized()
    target_width=target_normal.cross(horizontal).normalized()
    source=Matrix((width,normal,rest_direction)).transposed()
    target=Matrix((target_width,target_normal,horizontal)).transposed()
    flat=(target@source.transposed()).to_quaternion()
    approved=finish.inverted()@rest_direction.rotation_difference(finish@horizontal)
    shortest=rest_direction.rotation_difference(horizontal)
    return shortest.inverted()@flat,shortest.inverted()@approved,normal
