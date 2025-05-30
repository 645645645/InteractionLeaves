using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace UnityEngine.PBD
{
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct FillTrianglesJobUShort : IJobParallelFor
    {
        //常驻 只生成一次
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeSlice<ushort> triangles;

        public void Execute(int index)
        {
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            int3 traingesA = math.mad(index, 6, new int3(0, 1, 2));
            int3 traingesB = math.mad(index, 6, new int3(3, 4, 5));
            
            triangles[traingesA.x] = (ushort)indices[0];
            triangles[traingesA.y] = (ushort)indices[2];
            triangles[traingesA.z] = (ushort)indices[1];
            triangles[traingesB.x] = (ushort)indices[1];
            triangles[traingesB.y] = (ushort)indices[2];
            triangles[traingesB.z] = (ushort)indices[3];
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct FillTrianglesJobUInt : IJobParallelFor
    {
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeSlice<uint> triangles;

        public void Execute(int index)
        {
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            int3 traingesA = math.mad(index, 6, new int3(0, 1, 2));
            int3 traingesB = math.mad(index, 6, new int3(3, 4, 5));
            
            triangles[traingesA.x] = (uint)indices[0];
            triangles[traingesA.y] = (uint)indices[2];
            triangles[traingesA.z] = (uint)indices[1];
            triangles[traingesB.x] = (uint)indices[1];
            triangles[traingesB.y] = (uint)indices[2];
            triangles[traingesB.z] = (uint)indices[3];
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct CreatQuadMeshDataAppendJob : IJobParallelFor
    {
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<DistanceConstraint> distanceConstraints;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<BendConstraint> bendConstraints;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions> PredictedPositions;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Velocity> Velocities;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Normal> Normals;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Radius> Radius;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<InvMass> InvMasses;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadInvMass> QuadInvMasses;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Area> Areas;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<IsNeedUpdate> IsNeedUpdates;

        //mesh ===============
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float3> vertices;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float2> normal;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeSlice<half2> uvs;
        //===================

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<float4>.ReadOnly skinParams;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<float3>.ReadOnly productMinMax;

        [ReadOnly] public float4x4 local2World;

        [ReadOnly] public int offset; //生成

        [ReadOnly] public float Radius2Rigibody;

        public void Execute(int index)
        {
            index += offset;
            index %= IsNeedUpdates.Length;
            int vertexStart = index * 4;

            var random = Unity.Mathematics.Random.CreateFromIndex((uint)vertexStart);

            int type = random.NextInt(0, skinParams.Length);
            float4 uvST = skinParams[type];

            float3 pos = random.NextFloat3(productMinMax[0], productMinMax[1]);

            // quaternion rot = quaternion.Euler(pos * 30);
            quaternion rot = random.NextQuaternionRotation();

            float3 foldAngleRange = productMinMax[2];
            float2 scaleRange = productMinMax[3].xy;
            float2 massRange = productMinMax[4].xy;
            float scale = random.NextFloat(scaleRange.x, scaleRange.y);
            float angle = random.NextFloat(foldAngleRange.x, foldAngleRange.y);
            float mass = random.NextFloat(massRange.x, massRange.y);
            float radius = random.NextFloat(scaleRange.x, scaleRange.y);
            radius *= (0.25f * Radius2Rigibody);

            CreatQuadMeshData(index, vertexStart, uvST, radius,
                pos, rot, scale, angle, type, mass);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreatQuadMeshData(int quadID, int verticesOffset, float4 uvST, float radius,
            float3 pos, quaternion rot, float scale, float curveAngele, int type, float mass)
        {
            int index0 = 0 + verticesOffset,
                index1 = 1 + verticesOffset,
                index2 = 2 + verticesOffset,
                index3 = 3 + verticesOffset;

            float3 p0 = new float3(0, 0, 0);
            float3 p1 = new float3(1, 0, 0);
            float3 p2 = new float3(0, 0, 1);
            float3 p3 = new float3(1, 0, 1);

            //对折
            p3 = MathematicsUtil.RotatePointAroundAxis(p3, p1, p1 - p2, curveAngele);

            float4x4 trs = float4x4.TRS(pos, rot, scale);
            p0 = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(p0, 1));
            p1 = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(p1, 1));
            p2 = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(p2, 1));
            p3 = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(p3, 1));

            p0 = MathematicsUtil.MatrixMultiplyPoint3x4(local2World, new float4(p0, 1));
            p1 = MathematicsUtil.MatrixMultiplyPoint3x4(local2World, new float4(p1, 1));
            p2 = MathematicsUtil.MatrixMultiplyPoint3x4(local2World, new float4(p2, 1));
            p3 = MathematicsUtil.MatrixMultiplyPoint3x4(local2World, new float4(p3, 1));

            float3
                a = (p0 - p1),
                b = (p2 - p1),
                c = (p3 - p1),
                d = (p0 - p2),
                e = (p3 - p2);

            int dStart = quadID * 5;
            CreatDistanceConstraint(dStart, a, b, c, d, e);

            float3 perpA = math.cross(a, b);
            float3 perpB = math.cross(b, c);
            float perpLenA = math.length(perpA);
            float perpLenB = math.length(perpB);
            float areaA = perpLenA * 0.5f;
            float areaB = perpLenB * 0.5f;
            float areaP = (areaA + areaB) * 0.5f;

            float3 na = perpA / perpLenA;
            float3 nb = perpB / perpLenB;
            float3 np = math.normalize(na + nb);

            float dot = math.clamp(math.dot(na, nb), -1, 1);
            float angle = math.acos(dot);

            bendConstraints[quadID] = new BendConstraint()
            {
                // index0 = index0,
                // index1 = index1,
                // index2 = index2,
                // index3 = index3,
                restAngle = angle, // rad
                // isUpdate = isUpdate,
            };

            //把Create放流程末尾就得更新mesh..
            vertices[index0] = p0;
            vertices[index1] = p1;
            vertices[index2] = p2;
            vertices[index3] = p3;
            
            float2 
                octNA = MathematicsUtil.UnitVectorToOctahedron(na),
                octNB = MathematicsUtil.UnitVectorToOctahedron(nb),
                octNP = MathematicsUtil.UnitVectorToOctahedron(np);
            
            normal[index0] = octNA;
            normal[index1] = octNP;
            normal[index2] = octNP;
            normal[index3] = octNB;

            uvs[index0] = new half2(math.mad(new float2(0, 0), uvST.xy, uvST.zw));
            uvs[index1] = new half2(math.mad(new float2(1, 0), uvST.xy, uvST.zw));
            uvs[index2] = new half2(math.mad(new float2(0, 1), uvST.xy, uvST.zw));
            uvs[index3] = new half2(math.mad(new float2(1, 1), uvST.xy, uvST.zw));

            float density;

            switch (type)
            {
                case 0:
                    density = 1.2f;
                    break;
                case 1:
                    density = 1;
                    break;
                case 2:
                    density = 1.4f;
                    break;
                case 3:
                    density = 1.2f;
                    break;
                default:
                    density = 1.5f;
                    break;
            }

            mass = mass * density * 0.008f;
            // mass = mass * density * areaP;   

            float 
                invMass0 = 2.4f / mass,
                invMass1 = 2.9f / mass,
                invMass2 = 1.6f / mass,
                invMass3 = 2f / mass;

            // float quadInvMass = 1.0f / (mass * (1.0f / 2.4f + 1.0f / 2.9f + 1.0f / 1.6f + 1.0f / 2.0f));
            float quadInvMass = 1.0f / (mass * 1.886494f);

            QuadInvMasses[quadID] = new QuadInvMass() { Value = quadInvMass };
            Radius[quadID] = new Radius() { Value = radius };

            CreatParticleDataAppend(index0, p0, na, invMass0, areaA);
            CreatParticleDataAppend(index1, p1, na, invMass1, areaP);
            CreatParticleDataAppend(index2, p2, nb, invMass2, areaP);
            CreatParticleDataAppend(index3, p3, nb, invMass3, areaB);
            IsNeedUpdates[quadID] = new IsNeedUpdate() { Value = true };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreatParticleDataAppend(int index,
            float3 pos, float3 nor, float invMass, float area)
        {
            PredictedPositions[index] = new PredictedPositions() { Value = pos };
            Velocities[index] = new Velocity() { Value = float3.zero };
            Normals[index] = new Normal() { Value = nor };
            InvMasses[index] = new InvMass() { Value = invMass };
            Areas[index] = new Area() { Value = area };
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreatDistanceConstraint(int dStart, float3 a, float3 b, float3 c, float3 d, float3 e)
        {
            distanceConstraints[dStart] = new DistanceConstraint() { restLength = math.length(a) };
            distanceConstraints[dStart + 1] = new DistanceConstraint() { restLength = math.length(b) };
            distanceConstraints[dStart + 2] = new DistanceConstraint() { restLength = math.length(c) };
            distanceConstraints[dStart + 3] = new DistanceConstraint() { restLength = math.length(d) };
            distanceConstraints[dStart + 4] = new DistanceConstraint() { restLength = math.length(e) };
        }
    }


    //记录模拟开始前位置
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct InitializeJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Position> Positions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            for (int i = 0; i < 4; i++)
            {
                int indexs = indices[i];
                Positions[indexs] = new Position() { Value = PredictedPositions[indexs].Value };
            }
        }
    }

    #region ExtForce


    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ReadForceTransformJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction]
        public NativeList<PBDForceField> ForceFields;

        [ReadOnly] public float DeltaTime;
        
        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
            {
                PBDForceField force = ForceFields[index];
                force.loacl2World = transform.localToWorldMatrix;
                force.DeltaTime = DeltaTime;
                force.Prepare();
                ForceFields[index] = force;
            }
        }
    }
    
    //力场激活static部分,不过滤用的
    //开了byQuad就用上一帧碰撞前的quadPos
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtForceSetUpdateJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly StaticList;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;

        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDForceField>.ReadOnly ForceFields;
        
        //现在这个不需要激活static 
        // [ReadOnly, NativeDisableParallelForRestriction]
        // public NativeArray<PBDForceField>.ReadOnly PostForceFields;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<IsNeedUpdate> IsNeedUpdates;
        
        
        [ReadOnly] public bool ByQuad;
        
        public void Execute(int index)
        {
            if(StaticList.Length < 1 || ForceFields.Length < 1)
                return;
            
            index = StaticList[index];

            if (ByQuad)
                SetUpdateByQuad(index);
            else
                SetUpdateByParticle(index);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetUpdateByQuad(int index)
        {
            float3 pos = QuadPredictedPositions[index].Value;
            
            IsSetUpdate(index, pos);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetUpdateByParticle(int index)
        {
            int start = index * 4;
            float3 pos = float3.zero;
            for (int i = start; i < start + 4; i++)
            {
                pos += PredictedPositions[i].Value;
            }
            pos *= 0.25f;

            IsSetUpdate(index, pos);
        }

        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void IsSetUpdate(int index, in float3 pos)
        {
            for (int i = 0; i < ForceFields.Length; i++)
            {
                if (ForceFields[i].IsInRange(pos))
                {
                    IsNeedUpdates[index] = new IsNeedUpdate() { Value = true };
                    return;
                }
            }

            // for (int i = 0; i < PostForceFields.Length; i++)
            // {
            //     if (PostForceFields[i].IsInRange(pos))
            //     {
            //         IsNeedUpdates[index] = new IsNeedUpdate() { Value = true };
            //         return;
            //     }
            // }
        }
    }

    
    //开了byQuad就用上一帧碰撞前的quadPos
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtDynamicForceSetUpdateJob : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDForceField>.ReadOnly ForceFields;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<IsNeedUpdate> IsNeedUpdates;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeList<int>.ParallelWriter ExtDynamicForceList;

        [ReadOnly] public int QuadCount;
        
        [ReadOnly] public bool ByQuad;
        
        public void Execute()
        {
            if(ForceFields.Length < 1)
                return;

            if (ByQuad)
            {
                for (int index = 0; index < QuadCount; index++)
                {
                    float3 pos = QuadPredictedPositions[index].Value;
                    for (int i = 0; i < ForceFields.Length; i++)
                    {
                        if (ForceFields[i].IsInRange(pos))
                        {
                            IsNeedUpdates[index] = new IsNeedUpdate() { Value = true };
                            ExtDynamicForceList.AddNoResize(index);
                            break;
                        }
                    }
                }
            }
            else
            {
                for (int index = 0; index < QuadCount; index++)
                {
                    int start = index * 4;
                    float3 pos = float3.zero;
                    for (int i = start; i < start + 4; i++)
                    {
                        pos += PredictedPositions[i].Value;
                    }

                    pos *= 0.25f;

                    for (int i = 0; i < ForceFields.Length; i++)
                    {
                        if (ForceFields[i].IsInRange(pos))
                        {
                            IsNeedUpdates[index] = new IsNeedUpdate() { Value = true };
                            ExtDynamicForceList.AddNoResize(index);
                            break;
                        }
                    }
                }
            }
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtPostDynamicForceSetUpdateJob : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDForceField>.ReadOnly PostForceFields;
        
        // [WriteOnly, NativeDisableParallelForRestriction]
        // public NativeArray<IsNeedUpdate> IsNeedUpdates;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeList<int>.ParallelWriter ExtPostDynamicForceList;

        [ReadOnly] public int QuadCount;
        
        [ReadOnly] public bool ByQuad;
        
        public void Execute()
        {
            if(PostForceFields.Length < 1)
                return;

            if (ByQuad)
            {
                for (int index = 0; index < QuadCount; index++)
                {
                    float3 pos = QuadPredictedPositions[index].Value;

                    for (int i = 0; i < PostForceFields.Length; i++)
                    {
                        if (PostForceFields[i].IsInRange(pos))
                        {
                            // IsNeedUpdates[index] = new IsNeedUpdate() { Value = true };
                            ExtPostDynamicForceList.AddNoResize(index);
                            break;
                        }
                    }
                }
            }
            else
            {
                for (int index = 0; index < QuadCount; index++)
                {
                    int start = index * 4;
                    float3 pos = float3.zero;
                    for (int i = start; i < start + 4; i++)
                    {
                        pos += PredictedPositions[i].Value;
                    }

                    pos *= 0.25f;

                    for (int i = 0; i < PostForceFields.Length; i++)
                    {
                        if (PostForceFields[i].IsInRange(pos))
                        {
                            // IsNeedUpdates[index] = new IsNeedUpdate() { Value = true };
                            ExtPostDynamicForceList.AddNoResize(index);
                            break;
                        }
                    }
                }
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtForceJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [NativeDisableParallelForRestriction] 
        public NativeArray<PredictedPositions> PredictedPositions;

        [NativeDisableParallelForRestriction] 
        public NativeArray<Velocity> Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Normal>.ReadOnly Normals;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<InvMass>.ReadOnly InvMasses;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Area>.ReadOnly Areas;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDForceField>.ReadOnly ForceFields;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDForceField>.ReadOnly PostForceFields;

        [ReadOnly] public float3 gravity;
        [ReadOnly] public float3 wind;
        [ReadOnly] public float damping;
        [ReadOnly] public float deltaTime;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            for (int i = 0; i < 4; i++)
            {
                int pIndex = indices[i];
                
                float3
                    position = PredictedPositions[pIndex].Value,
                    normal = Normals[pIndex].Value,
                    velocity = Velocities[pIndex].Value,
                    area = Areas[pIndex].Value;

                float
                    invMass = InvMasses[pIndex].Value,
                    windDotN = math.dot(velocity - wind, normal);

                if (windDotN < 0)
                {
                    normal *= -1;
                    windDotN *= -1;
                }

                float3 force = windDotN * normal * area;
                for (int j = 0; j < ForceFields.Length; j++)
                    force += ForceFields[j].CaculateForce(in position, in velocity);

                //重力应该分到PreDynamic
                velocity += (force * invMass + gravity) * deltaTime;
                velocity *= math.max(-damping * invMass * deltaTime + 1, 0);
                // velocity *= math.exp(-damping * invMass * deltaTime);
  
                for (int j = 0; j < PostForceFields.Length; j++)
                    velocity += PostForceFields[j].CaculateForce(in position, in velocity);

                position = velocity * deltaTime + position;

                Velocities[pIndex] = new Velocity() { Value = velocity };
                PredictedPositions[pIndex] = new PredictedPositions() { Value = position };
            }
        }
    }

    //仅计算合力
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtPreDynamicForceJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction] 
        public NativeArray<Velocity>.ReadOnly Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Normal>.ReadOnly Normals;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Area>.ReadOnly Areas;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<ExtForce> ExtForces;

        // 不传mass 重力挪到下阶段
        // [ReadOnly] public float3 gravity;
        [ReadOnly] public float3 wind;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            for (int i = 0; i < 4; i++)
            {
                int pIndex = indices[i];
                
                float3
                    normal = Normals[pIndex].Value,
                    velocity = Velocities[pIndex].Value,
                    area = Areas[pIndex].Value;

                float
                    // invMass = InvMasses[i].Value,
                    windDotN = math.dot(velocity - wind, normal);

                if (windDotN < 0)
                {
                    normal *= -1;
                    windDotN *= -1;
                }
                
                float3 force = windDotN * normal * area;

                ExtForces[pIndex] = new ExtForce() { Value = force };
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtDynamicForceJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly ExtForceList;
        
        [ReadOnly, NativeDisableParallelForRestriction] 
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction] 
        public NativeArray<Velocity>.ReadOnly Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDForceField>.ReadOnly ForceFields;
        
        [NativeDisableParallelForRestriction]
        public NativeArray<ExtForce> ExtForces;
        
        public void Execute(int index)
        {
            index = ExtForceList[index];
            int start = index * 4;
            for (int i = start; i < start + 4; i++)
            {
                float3
                    position = PredictedPositions[i].Value,
                    velocity = Velocities[i].Value,
                    force = ExtForces[i].Value;

                for (int j = 0; j < ForceFields.Length; j++)
                    force += ForceFields[j].CaculateForce(in position, in velocity);
                
                ExtForces[i] = new ExtForce() { Value = force };
            }
            
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtVelocityJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [NativeDisableParallelForRestriction] 
        public NativeArray<Velocity> Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<InvMass>.ReadOnly InvMasses;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<ExtForce>.ReadOnly ExtForces;

        [ReadOnly] public float3 gravity;
        [ReadOnly] public float damping;
        [ReadOnly] public float deltaTime;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            for (int i = 0; i < 4; i++)
            {
                int pIndex = indices[i];
                
                float3
                    velocity = Velocities[pIndex].Value,
                    force = ExtForces[pIndex].Value;

                float
                    invMass = InvMasses[pIndex].Value;

                velocity += (force * invMass + gravity) * deltaTime;
                velocity *= math.max(-damping * invMass * deltaTime + 1, 0);
                // velocity *= math.exp(-damping * invMass * deltaTime);
                

                Velocities[pIndex] = new Velocity() { Value = velocity };
            }
        }
    }
    
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtPostDynamicForceJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly ExtForceList;

        [ReadOnly, NativeDisableParallelForRestriction] 
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [NativeDisableParallelForRestriction] 
        public NativeArray<Velocity> Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDForceField>.ReadOnly PostForceFields;
        

        public void Execute(int index)
        {
            index = ExtForceList[index];
            int start = index * 4;
            for (int i = start; i < start + 4; i++)
            {
                float3
                    position = PredictedPositions[i].Value,
                    velocity = Velocities[i].Value;

                for (int j = 0; j < PostForceFields.Length; j++)
                    velocity += PostForceFields[j].CaculateForce(in position, in velocity);
                
                Velocities[i] = new Velocity() { Value = velocity };
            }
        }
    }
    

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ExtPredictedUpdateJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions> PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction] 
        public NativeArray<Velocity>.ReadOnly Velocities;

        [ReadOnly] public float deltaTime;
        
        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            for (int i = 0; i < 4; i++)
            {
                int pIndex = indices[i];
                
                float3
                    position = PredictedPositions[pIndex].Value,
                    velocity = Velocities[pIndex].Value;

                position += velocity * deltaTime;

                PredictedPositions[pIndex] = new PredictedPositions() { Value = position };
            }
        }
    }





    #endregion
    
    
    
    #region Distance
    

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct DistanceConstraintJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [NativeDisableParallelForRestriction] public NativeArray<PredictedPositions> PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<InvMass>.ReadOnly InvMasses;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<DistanceConstraint>.ReadOnly DistanceConstraints;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int2>.ReadOnly DisContraintIndexes;

        [ReadOnly] public float ComppressStiffness;
        [ReadOnly] public float StretchStiffness;

        public void Execute(int index)
        {
            index = UpdateList[index];
            
            //每个quad五个边约束,输入长度还是quad数量
            int pStart = index * 4;
            int dStart = index * 5;
            for (int i = 0; i < 5; i++)
            {
                int dIndex = i + dStart;
                DistanceConstraint cnstr = DistanceConstraints[dIndex];

                // int indexA = cnstr.idA, indexB = cnstr.idB;
                int2 edge = DisContraintIndexes[i];
                int indexA = edge.x + pStart,
                    indexB = edge.y + pStart;

                float3 predPosA = PredictedPositions[indexA].Value,
                    predPosB = PredictedPositions[indexB].Value;

                float invMassA = InvMasses[indexA].Value,
                    invMassB = InvMasses[indexB].Value;

                float restLen = cnstr.restLength;

                float3 dir = predPosB - predPosA;
                float length = math.length(dir);
                float invMass = invMassA + invMassB;
                if (invMass <= math.EPSILON || length <= math.EPSILON)
                    continue;

                dir /= length;

                float3 dP = float3.zero;
                if (length <= restLen) //compress
                    dP = ComppressStiffness * dir * (length - restLen) / invMass;
                else //stretch
                    dP = StretchStiffness * dir * (length - restLen) / invMass;

                predPosA += dP * invMassA;
                predPosB -= dP * invMassB;

                PredictedPositions[indexA] = new PredictedPositions() { Value = predPosA };
                PredictedPositions[indexB] = new PredictedPositions() { Value = predPosB };
            }
        }
    }
    

    #endregion
    
    

    #region Bending
    

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct BendConstraintJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [NativeDisableParallelForRestriction] 
        public NativeArray<PredictedPositions> PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<InvMass>.ReadOnly InvMasses;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<BendConstraint>.ReadOnly BendConstraints;

        [ReadOnly] public float BendStiffness;

#if UNITY_EDITOR
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float3> DebugArray;
#endif

        public void Execute(int index)
        {
            index = UpdateList[index];
            
            // int pStart = index * 4,
            //     bStart = index;
            BendConstraint cnstr = BendConstraints[index];
            
            // int index0 = pStart,
            //     index1 = pStart + 1,
            //     index2 = pStart + 2,
            //     index3 = pStart + 3;
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));

            float3
                point0 = PredictedPositions[indices.x].Value,
                point1 = PredictedPositions[indices.y].Value,
                point2 = PredictedPositions[indices.z].Value,
                point3 = PredictedPositions[indices.w].Value;

            float
                invMass0 = InvMasses[indices.x].Value,
                invMass1 = InvMasses[indices.y].Value,
                invMass2 = InvMasses[indices.z].Value,
                invMass3 = InvMasses[indices.w].Value;

            float3 
                crr0 = float3.zero,
                crr1 = float3.zero,
                crr2 = float3.zero,
                crr3 = float3.zero;

            // if (solve_BendConstraint_matthias(
            //         point1, invMass1,
            //         point0, invMass0,
            //         point2, invMass2,
            //         point3, invMass3,
            //         cnstr.restAngle,
            //         BendStiffness,
            //         ref crr1, ref crr0, ref crr2, ref crr3
            if (solve_BendConstraint_rbridson(
                    point0, invMass0,
                    point3, invMass3,
                    point2, invMass2,
                    point1, invMass1,
                    cnstr.restAngle,
                    BendStiffness,
                    ref crr0, ref crr3, ref crr2, ref crr1
                ))
            {
                point0 += crr0;
                point1 += crr1;
                point2 += crr2;
                point3 += crr3;

                PredictedPositions[indices.x] = new PredictedPositions() { Value = point0 };
                PredictedPositions[indices.y] = new PredictedPositions() { Value = point1 };
                PredictedPositions[indices.z] = new PredictedPositions() { Value = point2 };
                PredictedPositions[indices.w] = new PredictedPositions() { Value = point3 };

#if UNITY_EDITOR
                DebugArray[indices.x] = crr0;
                DebugArray[indices.y] = crr1;
                DebugArray[indices.z] = crr2;
                DebugArray[indices.w] = crr3;
#endif
            }
        }

        //https://matthias-research.github.io/pages/publications/posBasedDyn.pdf
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool solve_BendConstraint_matthias(
            float3 p0, float invMass0,
            float3 p1, float invMass1,
            float3 p2, float invMass2,
            float3 p3, float invMass3,
            float restAngle,
            float stiffness,
            ref float3 crr0, ref float3 crr1, ref float3 crr2, ref float3 crr3)
        {
            float3
                a = (p1 - p0),
                b = (p2 - p0),
                c = (p3 - p0);

            float3
                n1 = math.cross(a, b),
                n2 = math.cross(a, c);

            float invLen1 = 1.0f / math.length(n1);
            float invLen2 = 1.0f / math.length(n2);

            n1 *= invLen1;
            n2 *= invLen2;
            // n1 = math.normalize(n1);
            // n2 = math.normalize(n2);

            float d = math.dot(n1, n2);

            d = math.clamp(d, -1, 1);

            float currentAngle = math.acos(d);

            float constraint = currentAngle - restAngle;

            if (math.abs(constraint) < 1e-5f)
                return false;

            float3 dp3 = (math.cross(a, n1) + math.cross(n2, a) * d) * invLen2;
            float3 dp2 = (math.cross(a, n2) + math.cross(n1, a) * d) * invLen1;
            float3 dp1 = -(math.cross(b, n2) + math.cross(n1, b) * d) * invLen1
                         - (math.cross(c, n1) + math.cross(n2, c) * d) * invLen2;
            float3 dp0 = -dp1 - dp2 - dp3;

            float w_sum =
                invMass0 * math.dot(dp0, dp0) +
                invMass1 * math.dot(dp1, dp1) +
                invMass2 * math.dot(dp2, dp2) +
                invMass3 * math.dot(dp3, dp3);

            if (w_sum < 1e-10f)
                return false;

            float lambda = -constraint * math.sqrt(1 - d * d) / w_sum;

            lambda *= stiffness;

            // if (math.dot(math.cross(n1, n2), a) > 0)
            //     lambda = -lambda;

            crr0 = lambda * invMass0 * dp0;
            crr1 = lambda * invMass1 * dp1;
            crr2 = lambda * invMass2 * dp2;
            crr3 = lambda * invMass3 * dp3;

            return true;
        }

        //https://www.cs.ubc.ca/~rbridson/docs/cloth2003.pdf
        //顶点序号与文内的不一至
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        bool solve_BendConstraint_rbridson(
            float3 p0, float invMass0,
            float3 p1, float invMass1,
            float3 p2, float invMass2,
            float3 p3, float invMass3,
            float restAngle,
            float stiffness,
            ref float3 crr0, ref float3 crr1, ref float3 crr2, ref float3 crr3)
        {
            if (invMass0 < math.EPSILON && invMass1 < math.EPSILON)
                return false;

            float3 e = p3 - p2;
            float elen = math.length(e);

            if (elen < math.EPSILON)
                return false;

            float invElen = 1.0f / elen;

            float3 n1 = math.cross(p2 - p0, p3 - p0),
                n2 = math.cross(p3 - p1, p2 - p1);
            n1 /= math.lengthsq(n1);
            n2 /= math.lengthsq(n2);

            float3 d0 = elen * n1;
            float3 d1 = elen * n2;
            float3 d2 = math.dot(p0 - p3, e) * invElen * n1 + math.dot(p1 - p3, e) * invElen * n2;
            float3 d3 = math.dot(p2 - p0, e) * invElen * n1 + math.dot(p2 - p1, e) * invElen * n2;

            n1 = math.normalize(n1);
            n2 = math.normalize(n2);

            float dot = math.dot(n1, n2);
            dot = math.clamp(dot, -1, 1);

            float phi = math.acos(dot);
            // float phi = (-0.6981317f * dot * dot - 0.8726646f) * dot + 1.570796f;

            float lambda =
                invMass0 * math.lengthsq(d0) +
                invMass1 * math.lengthsq(d1) +
                invMass2 * math.lengthsq(d2) +
                invMass3 * math.lengthsq(d3);

            if (lambda == 0)
                return false;

            lambda = (phi - restAngle) / lambda * stiffness;

            if (math.dot(math.cross(n1, n2), e) > 0)
                lambda = -lambda;

            crr0 = -invMass0 * lambda * d0;
            crr1 = -invMass0 * lambda * d1;
            crr2 = -invMass0 * lambda * d2;
            crr3 = -invMass0 * lambda * d3;
            return true;
        }
    }

    #endregion

    
    
    
    #region Particle2ParticleCollision

    [BurstCompile]
    public struct ClearParticleHashJob : IJob
    {
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeList<ParticleHash> ParticleHashes;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeHashMap<int, HashRange> hashRanges;
        
        public void Execute()
        {
            ParticleHashes.Clear();
            hashRanges.Clear();
        }
    }
    
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct CalculateHashesJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeList<ParticleHash>.ParallelWriter ParticleHashes;
        
        [ReadOnly] public float4 filterParams;//pos radius

        [ReadOnly] public float cellRadius;

        [ReadOnly] public bool collisionByQuad;

        public void Execute(int index)
        {
            int quad = UpdateList[index];

            int numCells = UpdateList.Length;

            if (collisionByQuad)
                AddHashByQuad(quad, numCells);
            else
                AddHashByParticle(quad, numCells * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddHashByQuad(int quad, int numCells)
        {
            float3 pos = QuadPredictedPositions[quad].Value;
            
            if (!MathematicsUtil.InSphereSpacial(in pos, cellRadius, in filterParams))
                return;
            
            AddToHashList(quad, pos, numCells);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddHashByParticle(int quad, int numCells)
        {
            int start = quad * 4;
            for (int i = start; i < start + 4; i++)
            {
                float3 pos = PredictedPositions[i].Value;
                
                if (!MathematicsUtil.InSphereSpacial(in pos, cellRadius * 0.5f, in filterParams))
                    continue;

                AddToHashList(i, pos, numCells);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddToHashList(int index, float3 pos, int numCells)
        {
            int3 cellPos = HashUtility.PosToGrid(pos, cellRadius);
            int hash = HashUtility.GridToHash(cellPos, numCells);
            ParticleHashes.AddNoResize(new ParticleHash()
            {
                Index = index,
                Hash = hash,
            });
        }
    }

    [BurstCompile]
    public struct CaculateParticleHashRangesJob : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<ParticleHash>.ReadOnly SortedHashes;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeHashMap<int, HashRange>.ParallelWriter hashRanges;

        public void Execute()
        {
            if (SortedHashes.Length < 1)
                return;

            int currentHash = SortedHashes[0].Hash;
            int start = 0;
            for (int i = 1; i < SortedHashes.Length; i++)
            {
                int nextHash = SortedHashes[i].Hash;
                if (nextHash != currentHash)
                {
                    hashRanges.TryAdd(currentHash, new HashRange()
                    {
                        Start = start,
                        End = i - 1,
                    });

                    currentHash = nextHash;
                    start = i;
                }
            }

            hashRanges.TryAdd(currentHash, new HashRange()
            {
                Start = start,
                End = SortedHashes.Length - 1,
            });
        }
    }

    /// <summary>
    /// 粒子-粒子
    /// </summary>
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct SPHOptimizedCollisionDetectionJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Velocity>.ReadOnly Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<InvMass>.ReadOnly InvMasses;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadVelocity>.ReadOnly QuadVelocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadInvMass>.ReadOnly QuadInvMasses;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<ParticleCollisionConstraint> ParticleCollisionConstraints;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<ParticleHash>.ReadOnly SortedHashes;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeHashMap<int, HashRange> hashRanges;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int3>.ReadOnly neighborOffsets;
        
        [ReadOnly] public float4 filterParams;//pos radius
        
        [ReadOnly] public float radius;

        [ReadOnly] public float cellRadius;

        [ReadOnly] public float CollisionStiffness;

        [ReadOnly] public bool collisionByQuad;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int numCells = UpdateList.Length;

            if (collisionByQuad)
                CollisionIntersectionByQuad(index, numCells);
            else
                CollisionIntersectionByParticles(index, numCells * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CollisionIntersectionByQuad(int index, int numCells)
        {
            float3
                posA = QuadPredictedPositions[index].Value,
                velocity = QuadVelocities[index].Value,
                delta = float3.zero;
            
            if (!MathematicsUtil.InSphereSpacial(in posA, cellRadius, in filterParams))
                return;
            
            float radiusSum = radius + radius;
            
            float 
                radiusSumSq = radiusSum * radiusSum,
                invMassA = QuadInvMasses[index].Value;

            int cnstrsCount = 0;

            int3 cellPos = HashUtility.PosToGrid(posA, cellRadius);

            for (int offset = 0; offset < neighborOffsets.Length; offset++)
            {
                int3 offsetStep = neighborOffsets[offset];
                if (math.dot(offsetStep, velocity) < 0)
                    continue;
                int3 offsetPos = cellPos + offsetStep;
                int nextHash = HashUtility.GridToHash(offsetPos, numCells);

                if (hashRanges.TryGetValue(nextHash, out var hashRange))
                {
                    for (int i = hashRange.Start; i <= hashRange.End; i++)
                    {
                        int indexB = SortedHashes[i].Index;

                        if (indexB <= index)
                            continue;
                        
                        float3 posB = QuadPredictedPositions[indexB].Value;
                        float invMassB = QuadInvMasses[indexB].Value;

                        float3 dir = posA - posB;

                        float disSq = math.lengthsq(dir);

                        if (disSq > radiusSumSq || disSq <= math.EPSILON)
                            continue;


                        float dis = math.sqrt(disSq);
                        float invMass = invMassA + invMassB;

                        float3 dP = CollisionStiffness * (dis - radiusSum) * (dir / dis) / invMass;

                        delta -= dP * invMassA;
                        cnstrsCount++;
                    }
                }
            }

            if (cnstrsCount > 0)
            {
                var deltaPosition = new ParticleCollisionConstraint()
                {
                    Delta = delta,
                    ConstraintsCount = cnstrsCount,
                };

                int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
                ParticleCollisionConstraints[indices.x] = deltaPosition;
                ParticleCollisionConstraints[indices.y] = deltaPosition;
                ParticleCollisionConstraints[indices.z] = deltaPosition;
                ParticleCollisionConstraints[indices.w] = deltaPosition;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CollisionIntersectionByParticles(int index, int numCells)
        {
            float radiusSum = radius + radius;
            float radiusSumSq = radiusSum * radiusSum;

            int start = index * 4;

            for (int indexA = start; indexA < start + 4; indexA++)
            {
                float3
                    posA = PredictedPositions[indexA].Value,
                    velocity = Velocities[indexA].Value,
                    delta = float3.zero;
                
                if (!MathematicsUtil.InSphereSpacial(in posA, cellRadius * 0.5f, in filterParams))
                    continue;
                
                float invMassA = InvMasses[indexA].Value;

                int3 cellPos = HashUtility.PosToGrid(posA, cellRadius);
                // int hash = HashUtility.GridToHash(cellPos, numCells);

                int cnstrsCount = 0;
                for (int offset = 0; offset < neighborOffsets.Length; offset++)
                {
                    int3 offsetStep = neighborOffsets[offset];
                    if (math.dot(offsetStep, velocity) < 0)
                        continue;
                    int3 offsetPos = cellPos + offsetStep;
                    int nextHash = HashUtility.GridToHash(offsetPos, numCells);

                    if (hashRanges.TryGetValue(nextHash, out var hashRange))
                    {
                        for (int i = hashRange.Start; i <= hashRange.End; i++)
                        {
                            int indexB = SortedHashes[i].Index;

                            if (indexB <= index)
                                continue;

                            float3 posB = PredictedPositions[indexB].Value;

                            float3 dir = posA - posB;

                            float disSq = math.lengthsq(dir);

                            if (disSq > radiusSumSq || disSq <= math.EPSILON)
                                continue;


                            float dis = math.sqrt(disSq);

                            float invMassB = InvMasses[indexB].Value;
                            float invMass = invMassA + invMassB;

                            float3 dP = CollisionStiffness * (dis - radiusSum) * (dir / dis) / invMass;

                            delta -= dP * invMassA;
                            cnstrsCount++;
                        }
                    }
                }

                if (cnstrsCount > 0)
                {
                    ParticleCollisionConstraints[indexA] = new ParticleCollisionConstraint()
                    {
                        Delta = delta,
                        //
                        ConstraintsCount = cnstrsCount,
                    };
                }
            }
        }
    }

    [BurstCompile]
    public struct ClearHashNeighbours : IJob
    {
        [WriteOnly] public NativeMultiHashMap<int, int> hashMap;
        public void Execute()
        {
            hashMap.Clear();
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct BuildHashNeighbours : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;

        [WriteOnly] public NativeMultiHashMap<int, int>.ParallelWriter hashMap;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int3>.ReadOnly neighborOffsets;
        
        [ReadOnly] public float4 filterParams;//pos radius
        
        [ReadOnly] public float cellRadius;

        [ReadOnly] public bool collisionByQuad;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int numCells = UpdateList.Length;
            if (collisionByQuad)
                AddNeighboursByQuad(index, numCells);
            else
                AddNeighboursByParticles(index, numCells * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddNeighboursByQuad(int index, int numCells)
        {
            float3 position = QuadPredictedPositions[index].Value;
            if (MathematicsUtil.InSphereSpacial(in position, cellRadius, in filterParams))
            {
                CreatNeighbours(index, position, numCells);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void AddNeighboursByParticles(int index, int numCells)
        {
            int start = index * 4;
            for (int i = start; i < start + 4; i++)
            {
                var position = PredictedPositions[i].Value;
                if (MathematicsUtil.InSphereSpacial(in position, cellRadius * 0.5f, in filterParams))
                {
                    CreatNeighbours(i, position, numCells);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CreatNeighbours(int index, float3 pos, int numCells)
        {
            int3 cellPos = HashUtility.PosToGrid(pos, cellRadius);
        
            for (int offset = 0; offset < neighborOffsets.Length; offset++)
            {
                int3 offsetPos = cellPos + neighborOffsets[offset];
                int hash = HashUtility.GridToHash(offsetPos, numCells);
                hashMap.Add(hash, index);
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct InterParticlesCollisions : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<InvMass>.ReadOnly InvMasses;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadInvMass>.ReadOnly QuadInvMasses;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<ParticleCollisionConstraint> ParticleCollisionConstraints;

        [ReadOnly] public NativeMultiHashMap<int, int> hashMap;
        
        [ReadOnly] public float4 filterParams;//pos radius

        [ReadOnly] public float radius;

        [ReadOnly] public float cellRadius;

        [ReadOnly] public float CollisionStiffness;

        [ReadOnly] public bool collisionByQuad;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int numCells = UpdateList.Length;
            if (collisionByQuad)
                CollisionIntersectionByQuad(index, numCells);
            else
                CollisionIntersectionByParticles(index, numCells * 4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CollisionIntersectionByQuad(int index, int numCells)
        {
            float3 posA = QuadPredictedPositions[index].Value;
            
            if (!MathematicsUtil.InSphereSpacial(in posA, cellRadius, in filterParams))
                return;
            
            float radiusSum = radius + radius;
            float radiusSumSq = radiusSum * radiusSum,
                invMassA = QuadInvMasses[index].Value;

            int3 cellPos = HashUtility.PosToGrid(posA, cellRadius);
            int hash = HashUtility.GridToHash(cellPos, numCells);

            bool found = hashMap.TryGetFirstValue(hash, out int indexB, out var iterator);

            float3 delta = float3.zero;
            int cnstrsCount = 0;

            while (found)
            {
                if (index == indexB)
                {
                    found = hashMap.TryGetNextValue(out indexB, ref iterator);
                    continue;
                }

                float3 posB = QuadPredictedPositions[indexB].Value;
                float invMassB = QuadInvMasses[indexB].Value;

                float3 dir = posA - posB;

                float disSq = math.lengthsq(dir);

                if (disSq > radiusSumSq || disSq <= math.EPSILON)
                {
                    found = hashMap.TryGetNextValue(out indexB, ref iterator);
                    continue;
                }

                float dis = math.sqrt(disSq);

                float invMass = invMassA + invMassB;

                float3 dP = CollisionStiffness * (dis - radiusSum) * (dir / dis) / invMass;

                delta -= dP * invMassA;
                cnstrsCount++;

                found = hashMap.TryGetNextValue(out indexB, ref iterator);
            }

            if (cnstrsCount > 0)
            {
                var deltaPosition = new ParticleCollisionConstraint()
                {
                    Delta = delta,
                    ConstraintsCount = cnstrsCount,
                };

                int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
                ParticleCollisionConstraints[indices[0]] = deltaPosition;
                ParticleCollisionConstraints[indices[1]] = deltaPosition;
                ParticleCollisionConstraints[indices[2]] = deltaPosition;
                ParticleCollisionConstraints[indices[3]] = deltaPosition;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        void CollisionIntersectionByParticles(int index, int numCells)
        {
            float radiusSum = radius + radius;
            float radiusSumSq = radiusSum * radiusSum;

            int start = index * 4;

            for (int indexA = start; indexA < start + 4; indexA++)
            {
                float3 posA = PredictedPositions[indexA].Value;
                
                if (!MathematicsUtil.InSphereSpacial(in posA, cellRadius * 0.5f, in filterParams))
                    continue;
                
                float invMassA = InvMasses[indexA].Value;

                int3 cellPos = HashUtility.PosToGrid(posA, cellRadius);
                int hash = HashUtility.GridToHash(cellPos, numCells);

                bool found = hashMap.TryGetFirstValue(hash, out int indexB, out var iterator);

                float3 delta = float3.zero;
                int cnstrsCount = 0;
                while (found)
                {
                    if (indexA == indexB || indexB / 4 == index)
                    {
                        found = hashMap.TryGetNextValue(out indexB, ref iterator);
                        continue;
                    }

                    float3 posB = PredictedPositions[indexB].Value;

                    float3 dir = posA - posB;

                    float disSq = math.lengthsq(dir);

                    if (disSq > radiusSumSq || disSq <= math.EPSILON)
                    {
                        found = hashMap.TryGetNextValue(out indexB, ref iterator);
                        continue;
                    }

                    float dis = math.sqrt(disSq);

                    float invMassB = InvMasses[indexB].Value;
                    float invMass = invMassA + invMassB;

                    float3 dP = CollisionStiffness * (dis - radiusSum) * (dir / dis) / invMass;

                    delta -= dP * invMassA;
                    cnstrsCount++;

                    found = hashMap.TryGetNextValue(out indexB, ref iterator);
                }

                if (cnstrsCount > 0)
                {
                    ParticleCollisionConstraints[indexA] = new ParticleCollisionConstraint()
                    {
                        Delta = delta,
                        //
                        ConstraintsCount = cnstrsCount,
                    };
                }
            }
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ReCaculateQuadVelocityJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Velocity>.ReadOnly Velocities;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadVelocity> QuadVelocities;
        
        public void Execute(int index)
        {
            index = UpdateList[index];
            int start = index * 4;
            
            float3 velocity = float3.zero;
            for (int i = start; i < start + 4; i++)
                velocity += Velocities[i].Value;
            velocity *= 0.25f;

            QuadVelocities[index] = new QuadVelocity() { Value = velocity };
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct AddParticleCollisionConstraintToPosition : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [NativeDisableParallelForRestriction] 
        public NativeArray<PredictedPositions> PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<ParticleCollisionConstraint>.ReadOnly ParticleCollisionConstraints;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int start = index * 4;
            for (int i = start; i < start + 4; i++)
            {
                var position = PredictedPositions[i].Value;
                var delta = ParticleCollisionConstraints[i];
                int cnstrsCount = delta.ConstraintsCount;
                if (cnstrsCount > 0)
                {
                    position += delta.Delta;
                    PredictedPositions[i] = new PredictedPositions() { Value = position };
                }
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ClearParticleCollisionConstraint : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<ParticleCollisionConstraint> ParticleCollisionConstraints;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            var partocleCnstr = new ParticleCollisionConstraint()
            {
                Delta = float3.zero, 
                ConstraintsCount = 0
            };
            ParticleCollisionConstraints[indices[0]] = partocleCnstr;
            ParticleCollisionConstraints[indices[1]] = partocleCnstr;
            ParticleCollisionConstraints[indices[2]] = partocleCnstr;
            ParticleCollisionConstraints[indices[3]] = partocleCnstr;
        }
    }

    #endregion
    
    
    

    #region RigiBodyCollision
    
    [BurstCompile]
    public struct ClearRigiCollisionConstraint : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<RigiCollisionConstraint> RigiCollisionConstraints;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            var rigiCnstr = new RigiCollisionConstraint()
            {
                Delta = float3.zero,
                Velocity = float3.zero,
                Normal = float3.zero,
                ConstraintsCount = 0
            };
            RigiCollisionConstraints[indices[0]] = rigiCnstr;
            RigiCollisionConstraints[indices[1]] = rigiCnstr;
            RigiCollisionConstraints[indices[2]] = rigiCnstr;
            RigiCollisionConstraints[indices[3]] = rigiCnstr;
        }
    }
    

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct AddRigiCollisionConstraintToPositionByDivi : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        // [ReadOnly, NativeDisableParallelForRestriction]
        // public NativeArray<Position>.ReadOnly Positions;

        [NativeDisableParallelForRestriction] 
        public NativeArray<PredictedPositions> PredictedPositions;

        [NativeDisableParallelForRestriction] 
        public NativeArray<Velocity> Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<RigiCollisionConstraint>.ReadOnly RigiCollisionConstraints;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<IsNeedUpdate> IsNeedUpdates;

        [ReadOnly] public float Threshold;

        public void Execute(int index)
        {
            index = UpdateList[index];
            int start = index * 4;
            float thresholdSQ = Threshold;
            bool4 beUpdates = true;
            bool hasCollision = false;
            
            for (int i = 0; i <  4; i++)
            {
                int pIndex = i + start;
                var rigiCnst = RigiCollisionConstraints[pIndex];
                int cnstrsCount = rigiCnst.ConstraintsCount;
                if (cnstrsCount > 0)
                {
                    var position = PredictedPositions[pIndex].Value;

                    var delta = rigiCnst.Delta;
                    var velocity = rigiCnst.Velocity;

                    Velocities[pIndex] = new Velocity() { Value = velocity };
                    PredictedPositions[pIndex] = new PredictedPositions() { Value = position + delta / rigiCnst.ConstraintsCount };
                    
                    if ((math.lengthsq(velocity) < thresholdSQ))
                        beUpdates[i] = false;

                    hasCollision = true;
                }
            }

            if (hasCollision)
            {
                int countbits = math.countbits((uint)MathematicsUtil.bitmask(beUpdates));
                IsNeedUpdates[index] = new IsNeedUpdate() { Value = countbits > 1 };
            }
        }
    }
    
    [BurstCompile]
    public struct ReadRigibodyColliderTransformJob : IJobParallelForTransform
    {
        [NativeDisableParallelForRestriction] 
        public NativeArray<PBDCustomColliderInfo> collider;

        public void Execute(int index, TransformAccess transform)
        {
            if (transform.isValid)
            {
                PBDCustomColliderInfo data = collider[index];
                if (data.bStatic)
                    return;
                data.Position = transform.position;
                data.Rotation = transform.rotation;
                data.Scale = MathematicsUtil.GetLossyScale(transform);
                data.Prepare();
                collider[index] = data;
            }
        }
    }

    [BurstCompile]
    public struct UpdateSenceBounds : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDCustomColliderInfo>.ReadOnly Colliders;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDBounds> SceneBounds;

        public void Execute()
        {
            if (Colliders.Length < 1)
                return;

            float3 min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < Colliders.Length; i++)
            {
                var c = Colliders[i];
                min = math.min(min, c.boundsMin);
                max = math.max(max, c.boundsMax);
            }

            SceneBounds[0] = new PBDBounds()
            {
                Min = min,
                Max = max,
            };
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ReCaculateQuadPosition : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions> QuadPredictedPositions;
        
        public void Execute(int index)
        {
            index = UpdateList[index];
            int start = index * 4;
            float3 pos = float3.zero;
            for (int i = start; i < start + 4; i++)
                pos += PredictedPositions[i].Value;

            pos *= 0.25f;

            QuadPredictedPositions[index] = new QuadPredictedPositions() { Value = pos };
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct RigibodyCollisionJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDCustomColliderInfo>.ReadOnly Colliders;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Radius>.ReadOnly Radius;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Velocity>.ReadOnly Velocities;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<QuadPredictedPositions>.ReadOnly QuadPredictedPositions;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<RigiCollisionConstraint> RigiCollisionConstraints;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDBounds>.ReadOnly SceneBounds;

        [ReadOnly] public float QuadRadius;
        [ReadOnly] public float Friction;
        [ReadOnly] public float Elasticity;

#if UNITY_EDITOR
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDCollisionHit> DebugArray;
#endif
        public void Execute(int index)
        {
            index = UpdateList[index];
            int start = index * 4;
            float qRadius = QuadRadius;

            PBDBounds scence = SceneBounds[0];

            float3 quadPos = QuadPredictedPositions[index].Value;

            if (!MathematicsUtil.AABBOverlap(in quadPos, in qRadius, in scence.Min, in scence.Max))
                return;

            PointConstraints rigiConstraints = new PointConstraints();

            QuadCollisionHitInfos quadHitInfos = new QuadCollisionHitInfos();
            
            float pRadius = Radius[index].Value;
            
            //没考虑质量
            for (int j = 0; j < Colliders.Length; j++)
            {
                var c = Colliders[j];
                if (!MathematicsUtil.AABBOverlap(in quadPos, in qRadius, in c.boundsMin, in c.boundsMax))
                    continue;
                for (int i = 0; i < 4; i++)
                {
                    int pIndex = start + i;
                    PBDCollisionHit hit = quadHitInfos.GetHit(i);

                    float3
                        position = PredictedPositions[pIndex].Value,
                        velocity = Velocities[pIndex].Value;
                    // velocity = float3.zero;
                    if (c.Collide(in position, pRadius, Elasticity, Friction, ref rigiConstraints, i, ref velocity, ref hit))
                    {
                        rigiConstraints.IncrementHitCount(i);
                        quadHitInfos.SetHit(i, hit);
                    }
                }
            }

            for (int i = 0; i < 4; i++)
            {
                int hitCount = rigiConstraints.GetHitCount(i);
                int pIndex = start + i;
                PBDCollisionHit hit = quadHitInfos.GetHit(i);
                if (hitCount > 0)
                {
                    RigiCollisionConstraints[pIndex] = new RigiCollisionConstraint()
                    {
                        Delta = rigiConstraints[i].xyz,
                        Velocity = hit.hitConcatDelta,
                        Normal = hit.hitNormal,
                        InsertDepth = hit.insertDepth,
                        ConstraintsCount = hitCount,
                        // LastHit = hit,
                    };
                }
#if UNITY_EDITOR
                DebugArray[pIndex] = hit;
#endif

            }

        }
    }
    
#if UNITY_EDITOR
    [BurstCompile]
    public struct ClearDebugHitInfo : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<PBDCollisionHit> DebugArray;
        public void Execute(int index)
        {
            index = UpdateList[index];
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            PBDCollisionHit clearHit = new PBDCollisionHit(){ isHit = false };
            DebugArray[indices.x] = clearHit;
            DebugArray[indices.y] = clearHit;
            DebugArray[indices.z] = clearHit;
            DebugArray[indices.w] = clearHit;
        }
    }

#endif

    #endregion

    
    #region LastUpdateData
    

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct UpdateVelocityJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Position>.ReadOnly Positions;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Velocity> Velocities;

        [ReadOnly] public float InvDeltaTime;

        public void Execute(int index)
        {
            index = UpdateList[index];
            
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
               
            float3x4 predictedPositions = new float3x4(
                PredictedPositions[indices.x].Value,
                PredictedPositions[indices.y].Value,
                PredictedPositions[indices.z].Value,
                PredictedPositions[indices.w].Value
            );
            
            float3x4 oldPositions = new float3x4(
                Positions[indices.x].Value,
                Positions[indices.y].Value,
                Positions[indices.z].Value,
                Positions[indices.w].Value
            );
            
            float3x4 velocities = (predictedPositions - oldPositions) * InvDeltaTime;
            
            Velocities[indices.x] = new Velocity() { Value = velocities.c0 };
            Velocities[indices.y] = new Velocity() { Value = velocities.c1 };
            Velocities[indices.z] = new Velocity() { Value = velocities.c2 };
            Velocities[indices.w] = new Velocity() { Value = velocities.c3 };
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct UpdateNormalAreaJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Normal> Normals;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<Area> Areas;
        public void Execute(int index)
        {
            index = UpdateList[index];

            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            
            float3
                p0 = PredictedPositions[indices.x].Value,
                p1 = PredictedPositions[indices.y].Value,
                p2 = PredictedPositions[indices.z].Value,
                p3 = PredictedPositions[indices.w].Value;
            
            float3
                a = (p0 - p1),
                b = (p2 - p1),
                c = (p3 - p1);
            float3 perpA = math.cross(a, b);
            float3 perpB = math.cross(b, c);
            float perpLenA = math.length(perpA);
            float perpLenB = math.length(perpB);
            float areaA = perpLenA * 0.5f;
            float areaB = perpLenB * 0.5f;
            float areaP = (areaA + areaB) * 0.5f;

            Areas[indices.x] = new Area() { Value = areaA };
            Areas[indices.y] = new Area() { Value = areaP };
            Areas[indices.z] = new Area() { Value = areaP };
            Areas[indices.w] = new Area() { Value = areaB };
            
            float3 
                na = (perpA / perpLenA),
                nb = (perpB / perpLenB);

            Normals[indices.x] = new Normal() { Value = na };
            Normals[indices.y] = new Normal() { Value = na };
            Normals[indices.z] = new Normal() { Value = nb };
            Normals[indices.w] = new Normal() { Value = nb };
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct UpdateMeshPosJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float3> pos;
        
        public void Execute(int index)
        {
            index = UpdateList[index];

            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            
            pos[indices.x] = PredictedPositions[indices.x].Value;
            pos[indices.y] = PredictedPositions[indices.y].Value;
            pos[indices.z] = PredictedPositions[indices.z].Value;
            pos[indices.w] = PredictedPositions[indices.w].Value;
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct UpdateMeshNormalJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<int>.ReadOnly UpdateList;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Normal>.ReadOnly Normals;
        
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<float2> normal;
        
        public void Execute(int index)
        {
            index = UpdateList[index];

            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));
            
            float3 
                na = Normals[indices.x].Value,
                nb = Normals[indices.w].Value;

            float3 np = math.normalize(na + nb);

            float2 
                octNA = MathematicsUtil.UnitVectorToOctahedron(na),
                octNB = MathematicsUtil.UnitVectorToOctahedron(nb),
                octNP = MathematicsUtil.UnitVectorToOctahedron(np);
            
            normal[indices.x] = octNA;
            normal[indices.y] = octNP;
            normal[indices.z] = octNP;
            normal[indices.w] = octNB;
        }
    }
    
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct UpdateFrustumMeshPosJob : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeList<int> RenderList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions>.ReadOnly PredictedPositions;
        
        [WriteOnly, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction]
        public NativeArray<float3> pos;
        
        [ReadOnly] public int JobID;

        [ReadOnly] public int JobNum;
        
        public void Execute()
        {
            int length = RenderList.Length;
            if(length < 1 || JobNum < 1)
                return;

            int jobLength = (length + 1) / JobNum;
            int start = jobLength * JobID,
                end = math.min(math.mad(jobLength, JobID, jobLength), length);

            for (int index = start; index < end; index++)
            {
                int quadID = RenderList[index];
            
                if(quadID < 0)
                    return;

                int4 indiceStep = new int4(0, 1, 2, 3);

                int4
                    indices = math.mad(index, 4, indiceStep),
                    quadIndices = math.mad(quadID, 4, indiceStep);
            
                pos[indices.x] = PredictedPositions[quadIndices.x].Value;
                pos[indices.y] = PredictedPositions[quadIndices.y].Value;
                pos[indices.z] = PredictedPositions[quadIndices.z].Value;
                pos[indices.w] = PredictedPositions[quadIndices.w].Value;
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct UpdateFrustumMeshNormalJob : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeList<int> RenderList;
        
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<Normal>.ReadOnly Normals;
        
        [WriteOnly, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction]
        public NativeArray<float2> normal;
        
        [ReadOnly] public int JobID;

        [ReadOnly] public int JobNum;

        public void Execute()
        {
            int length = RenderList.Length;
            if (length < 1 || JobNum < 1)
                return;

            int jobLength = (length + 1) / JobNum;
            int start = jobLength * JobID,
                end = math.min(math.mad(jobLength, JobID, jobLength), length);

            for (int index = start; index < end; index++)
            {
                int quadID = RenderList[index];

                if (quadID < 0)
                    return;

                int4 indiceStep = new int4(0, 1, 2, 3);

                int4
                    indices = math.mad(index, 4, indiceStep),
                    quadIndices = math.mad(quadID, 4, indiceStep);

                float3
                    na = Normals[quadIndices.x].Value,
                    nb = Normals[quadIndices.y].Value;

                float3 np = math.normalize(na + nb);

                float2
                    octNA = MathematicsUtil.UnitVectorToOctahedron(na),
                    octNB = MathematicsUtil.UnitVectorToOctahedron(nb),
                    octNP = MathematicsUtil.UnitVectorToOctahedron(np);


                normal[indices.x] = octNA;
                normal[indices.y] = octNP;
                normal[indices.z] = octNP;
                normal[indices.w] = octNB;
            }
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct UpdateFrustumMeshUVJob : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeList<int> RenderList;

        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<half2>.ReadOnly uvs;

        [WriteOnly, NativeDisableParallelForRestriction, NativeDisableContainerSafetyRestriction]
        public NativeArray<half2> uvsForRendering;

        [ReadOnly] public int JobID;

        [ReadOnly] public int JobNum;

        public void Execute()
        {
            int length = RenderList.Length;
            if (length < 1 || JobNum < 1)
                return;

            int jobLength = (length + 1) / JobNum;
            int start = jobLength * JobID,
                end = math.min(math.mad(jobLength, JobID, jobLength), length);

            for (int index = start; index < end; index++)
            {
                int quadID = RenderList[index];

                if (quadID < 0)
                    return;

                int4 indiceStep = new int4(0, 1, 2, 3);

                int4
                    indices = math.mad(index, 4, indiceStep),
                    quadIndices = math.mad(quadID, 4, indiceStep);

                uvsForRendering[indices.x] = uvs[quadIndices.x];
                uvsForRendering[indices.y] = uvs[quadIndices.y];
                uvsForRendering[indices.z] = uvs[quadIndices.z];
                uvsForRendering[indices.w] = uvs[quadIndices.w];
            }
        }
    }


    #endregion

    [BurstCompile]
    public struct ClearJobHandleListJob : IJob
    {
        [WriteOnly]
        public NativeList<JobHandle> List;

        public void Execute()
        {
            List.Clear();
        }
    }

    [BurstCompile]
    public struct ClearListJob : IJob
    {
        [WriteOnly]
        public NativeList<int> List;

        public void Execute()
        {
            List.Clear();
        }
    }

    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct ClearNativeArrayJob : IJobParallelFor
    {
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<int> NativeArray;
        
        public void Execute(int index)
        {
            NativeArray[index] = -1;
        }
    }


    //多线程标记在视锥内
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct CaculateInFrustumJob : IJobParallelFor
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<PredictedPositions> PredictedPositions;

        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeArray<IsNeedRender> IsNeedRenders;

        [ReadOnly] public float4x4 CullingMatrix;

        public void Execute(int index)
        {
            int4 indices = math.mad(index, 4, new int4(0, 1, 2, 3));

            for (int j = 0; j < 4; j++)
            {
                if (MathematicsUtil.InFrustum(in CullingMatrix,  PredictedPositions[indices[j]].Value))
                {
                    IsNeedRenders[index] = new IsNeedRender() { Value = true };
                    return;
                }
            }
            IsNeedRenders[index] = new IsNeedRender() { Value = false };
        }
    }
    
    //单线程顺序收集
    [BurstCompile(FloatPrecision.Low, FloatMode.Fast)]
    public struct CollectInFrustumJob : IJob
    {
        [ReadOnly, NativeDisableParallelForRestriction]
        public NativeArray<IsNeedRender>.ReadOnly IsNeedRenders;

        [WriteOnly]
        public NativeList<int> RenderList;
        
        [WriteOnly] public NativeArray<int> RenderCounter;

        [ReadOnly] public int QuadCount;
        
        public void Execute()
        {
            int writeIndex = 0;
            for (int index = 0; index < QuadCount; index ++)
            {
                if (IsNeedRenders[index].Value)
                {
                    RenderList.AddNoResize(index);
                    writeIndex++;
                }
            }

            RenderCounter[0] = writeIndex;
        }
    }


    [BurstCompile]
    public struct CollectNeedUpdateQuadIndex : IJob
    {
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeList<int>.ParallelWriter UpdateList;

        [ReadOnly, NativeDisableParallelForRestriction] 
        public NativeArray<IsNeedUpdate>.ReadOnly IsNeedUpdates;

        [ReadOnly] public int Length;

        public void Execute()
        {
            for (int i = 0; i < Length; i++)
            {
                if (IsNeedUpdates[i].Value)
                    UpdateList.AddNoResize(i);
            }
        }
    }
    
    
    [BurstCompile]
    public struct CollectStaticQuadIndex : IJob
    {
        [WriteOnly, NativeDisableParallelForRestriction]
        public NativeList<int>.ParallelWriter StaticList;

        [ReadOnly, NativeDisableParallelForRestriction] 
        public NativeArray<IsNeedUpdate>.ReadOnly IsNeedUpdates;

        [ReadOnly] public int Length;

        public void Execute()
        {
            for (int i = 0; i < Length; i++)
            {
                if (!IsNeedUpdates[i].Value)
                    StaticList.AddNoResize(i);
            }
        }
    }
}