"""Anatomical seated-arm IK and measured thigh/palm contact for the existing LakeGirl rig."""
import bpy
from mathutils import Vector,Matrix,Quaternion
from mathutils.bvhtree import BVHTree
import math


class SeatedContacts:
    def __init__(self,rig,mesh):
        self.rig=rig;self.mesh=mesh
        self.order=[]
        def walk(bone):
            self.order.append(bone.name)
            for child in bone.children:walk(child)
        for bone in rig.data.bones:
            if bone.parent is None:walk(bone)
        self.rest={bone.name:bone.matrix_local.copy() for bone in rig.data.bones}
        self.relative={name:(self.rest[rig.data.bones[name].parent.name].inverted()@self.rest[name]
                       if rig.data.bones[name].parent else self.rest[name]) for name in self.order}
        bpy.context.view_layer.update();evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());data=evaluated.to_mesh()
        transform=rig.matrix_world.inverted()@evaluated.matrix_world
        try:
            data.calc_loop_triangles();points=[transform@v.co for v in data.vertices]
            self.targets={};W=self.fk()
            for side in ['Left','Right']:
                group=mesh.vertex_groups.get(side+'UpperLeg')
                if group is None:raise RuntimeError('Missing thigh skin weights')
                weighted={v.index for v in mesh.data.vertices if any(g.group==group.index and g.weight>.22 for g in v.groups)}
                triangles=[tuple(t.vertices) for t in data.loop_triangles if sum(i in weighted for i in t.vertices)>=2]
                if not triangles:raise RuntimeError('No weighted thigh surface')
                tree=BVHTree.FromPolygons(points,triangles,all_triangles=True)
                hip=W[side+'UpperLeg'].translation;knee=W[side+'LowerLeg'].translation
                sample=hip.lerp(knee,.30);point,normal,index,distance=tree.ray_cast(sample+Vector((0,0,.35)),Vector((0,0,-1)),.7)
                if point is None:raise RuntimeError('Lap contact ray missed actual deformed thigh')
                if normal.z<0:normal.negate()
                normal.normalize();forward=knee-hip;forward-=normal*forward.dot(normal);forward.normalize()
                hand=self.rest[side+'Hand'];middle=self.rest[side+'MiddleProximal'].translation
                direction=(middle-hand.translation).normalized()
                a=self.rest[side+'IndexProximal'].translation-hand.translation;b=self.rest[side+'LittleProximal'].translation-hand.translation
                dorsal=a.cross(b).normalized()
                if dorsal.z<0:dorsal.negate()
                dorsal-=direction*dorsal.dot(direction);dorsal.normalize()
                self.targets[side]={'point':point,'normal':normal,'forward':forward,'rest_forward':direction,'rest_dorsal':dorsal,
                                    'palm_offset':hand.inverted()@(hand.translation+direction*.05),'tree':tree}
        finally:evaluated.to_mesh_clear()
        self.last={};self.previous_angles={}

    def fk(self):
        W={}
        for name in self.order:
            bone=self.rig.pose.bones[name]
            q=bone.rotation_quaternion.copy() if bone.rotation_mode=='QUATERNION' else bone.rotation_euler.to_quaternion()
            basis=Matrix.Translation(bone.location)@q.to_matrix().to_4x4()@Matrix.Diagonal((*bone.scale,1))
            parent=self.rig.data.bones[name].parent
            W[name]=(W[parent.name]@self.relative[name]@basis) if parent else self.relative[name]@basis
        return W

    def world_rotation(self,name,rotation,W):
        parent=self.rig.data.bones[name].parent
        basis=(W[parent.name]@self.relative[name]) if parent else self.relative[name]
        q=(basis.to_3x3().normalized().inverted()@rotation).to_quaternion()
        bone=self.rig.pose.bones[name];bone.rotation_mode='XYZ';angles=q.to_euler('XYZ',self.previous_angles.get(name,bone.rotation_euler));bone.rotation_euler=angles;self.previous_angles[name]=angles.copy()

    def reach(self,side,target):
        W=self.fk();upper=side+'UpperArm';lower=side+'LowerArm';hand=side+'Hand'
        shoulder=W[upper].translation;elbow=W[lower].translation;wrist=W[hand].translation
        a=(elbow-shoulder).length;b=(wrist-elbow).length;delta=target-shoulder;length=min(delta.length,a+b-.002);direction=delta.normalized()
        desired_elbow=Vector((.22 if side=='Left' else -.22,.03,1.04));pole=desired_elbow-shoulder;pole-=direction*pole.dot(direction)
        if pole.length<1e-5:pole=Vector((1 if side=='Left' else -1,0,0))
        pole.normalize();cosine=max(-1,min(1,(a*a+length*length-b*b)/(2*a*length)))
        wanted=shoulder+direction*a*cosine+pole*a*math.sqrt(max(0,1-cosine*cosine))
        rotation=(elbow-shoulder).rotation_difference(wanted-shoulder).to_matrix()@W[upper].to_3x3().normalized();self.world_rotation(upper,rotation,W)
        W=self.fk();elbow=W[lower].translation;wrist=W[hand].translation
        rotation=(wrist-elbow).rotation_difference(target-elbow).to_matrix()@W[lower].to_3x3().normalized();self.world_rotation(lower,rotation,W)

    def solve(self,film,gesture):
        self.last={}
        for side,target in self.targets.items():
            normal=target['normal'];forward=target['forward'];lift=.075*max(0,gesture) if film=='dominion' else 0
            point=target['point']+forward*(.012*gesture)
            wrist=point-forward*.05+normal*(.02+lift)
            self.reach(side,wrist);W=self.fk()
            rest_frame=Matrix((target['rest_forward'],target['rest_dorsal'].cross(target['rest_forward']),target['rest_dorsal'])).transposed()
            frame=Matrix((forward,normal.cross(forward),normal)).transposed()
            orientation=frame@rest_frame.transposed()@self.rest[side+'Hand'].to_3x3().normalized()
            self.world_rotation(side+'Hand',orientation,W)
            # Small relaxed fingers stay on the thigh; only the selected Dominion gets a deliberate deep curl.
            curl=math.radians(29*max(0,gesture) if film=='dominion' else max(0,1+.7*gesture))
            axis=normal.cross(forward).normalized()
            for name in self.order:
                if not name.startswith(side) or not any(k in name for k in ['Index','Middle','Ring','Little']):continue
                W=self.fk();factor=1 if 'Proximal' in name else .7 if 'Intermediate' in name else .4
                self.world_rotation(name,Matrix.Rotation(curl*factor,3,axis)@W[name].to_3x3().normalized(),W)
            W=self.fk();palm=W[side+'Hand']@target['palm_offset'];error=(W[side+'Hand'].translation-wrist).length
            lateral=abs(W[side+'LowerArm'].translation.x-W[side+'UpperArm'].translation.x)
            self.last[side]={'target_rig':list(point),'normal_rig':list(normal),'palm_center_rig':list(palm),
                             'wrist_ik_error_m':error,'elbow_lateral_offset_m':lateral,'intended_palm_center_clearance_m':.02+lift,
                             'measured_palm_normal_clearance_m':(palm-point).dot(normal)}
            if error>.012:raise RuntimeError('Natural seated hand cannot reach actual lap surface: '+side+' '+str(error))
        return self.last
