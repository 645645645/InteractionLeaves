using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace UnityEngine.PBD
{
    
    [BurstCompatible]
    public struct PBDCustomColliderInfo
    {
        public bool bStatic;
        public bool useConcatNormal;

        public PBDColliderType CollideType;                     //for sphere capsule
        public float3 Size;                                     //(Height, Radius, Radius2)
        public float3 Center;                                   //Center                        
        
        //w: depth for concat center
        public float4 ScaleSize;                                //(Scale, ScaledRadius, ScaledRaidus2, C01Distance)
        public float3 C0;                                       //C0
        public float3 Up;                //Plane Noraml         //C1
        public float3 Forward;
        public float3 Right;
        //bounds
        public float3 boundsMin;
        public float3 boundsMax;
        
        //world
        public float3 Position;
        public float3 Scale;
        public quaternion Rotation;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Prepare()
        {
            switch (CollideType)
            {
                case PBDColliderType.Plane:
                case PBDColliderType.Box:
                    PreparePlaneAndBox();
                    break;
                case PBDColliderType.Sphere:
                case PBDColliderType.Capsule:
                case PBDColliderType.AsymmetricalCapsule:
                    PrepareSpherAndCapsule();
                    break;
                case PBDColliderType.None:
                default:
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PreparePlaneAndBox()
        {
            ref var box = ref this;
            bool3 isZero = box.Size.xyz < math.EPSILON;

            int noZeroCount = math.countbits((uint)MathematicsUtil.bitmask(new bool3(!isZero)));
            int CollideType = math.select(2, math.select(1, 0, noZeroCount < 2), noZeroCount < 3);
            if (CollideType > 0)
            {
                box.ScaleSize.xyz = box.Scale * box.Size;
                float4x4 trs = float4x4.TRS(box.Position, box.Rotation, box.Scale);
                float3x3 transTRS = (float3x3)math.transpose(trs);
                if (CollideType == 1)
                {
                    box.C0 = MathematicsUtil.LocalToWorldPosition(box.Position, box.Rotation, box.Center * Scale);
                    
                    // float3 localForward;
                    // if (isZero.x)
                    // {
                    //     box.Normal = new float3(1, 0, 0);
                    //     box.ScaleSize.xy = box.ScaleSize.yz;
                    //     localForward = new float3(0, 0, 1);
                    // }
                    // else if (isZero.y)
                    // {
                    //     box.Normal = new float3(0, 1, 0);
                    //     box.ScaleSize.xy = box.ScaleSize.xz;
                    //     localForward = new float3(0, 0, 1);
                    // }
                    // else
                    // {
                    //     box.Normal = new float3(0, 0, 1);
                    //     box.ScaleSize.xy = box.ScaleSize.xy;
                    //     localForward = new float3(0, 1, 0);
                    // }
                    //
                    
                    uint zeroMask = (uint)MathematicsUtil.bitmask(isZero);
                    box.Up = math.select(0f, 1f, new bool3(
                        (zeroMask & 1) != 0,
                        (zeroMask & 2) != 0,
                        (zeroMask & 4) != 0
                    ));

                    float2 sizeSelect = math.select(
                        math.select(box.ScaleSize.xy, box.ScaleSize.xz, (zeroMask & 2) != 0),
                        box.ScaleSize.yz,
                        (zeroMask & 1) != 0
                    );
                    box.ScaleSize.xy = sizeSelect;
                    box.ScaleSize.w = math.max(box.ScaleSize.x, box.ScaleSize.y);
                    
                    // 代替条件分支
                    float3 localForward = math.mul(
                        math.float3x3(0, 0, 1, 0, 0, 1, 0, 1, 0),
                        math.select(0f, 1f, isZero)
                    );
                    
                    box.Up = math.normalize(math.mul(box.Up, transTRS));
                    box.Forward = math.normalize(math.mul(localForward, transTRS));
                    box.Right = math.normalize(math.cross(box.Up, box.Forward));
                    
                    {
                        //自己算平面的过滤AABB 补充平面厚度
                        float3 
                            center = C0;
                        float3 
                            right = box.Right * box.ScaleSize.x * 0.5f,
                            forward = box.Forward * box.ScaleSize.y * 0.5f;
                        float3 
                            extend1 = right + forward,
                            extend2 = right - forward;
                        float3
                            corner1 = center + extend1,
                            corner2 = center + extend2,
                            corner3 = center - extend2,
                            corner4 = center - extend1;
                        float3 
                            min = corner1,
                            max = corner1;
                        
                        min = math.min(min, corner2);
                        max = math.max(max, corner2);
                        min = math.min(min, corner3);
                        max = math.max(max, corner3);
                        min = math.min(min, corner4);
                        max = math.max(max, corner4);
                        
                        float minThickness = 0.5f; // 厚度阈值
                        float3 size = max - min;
        
                        float currentThickness = math.min(math.min(size.x, size.y),size.z);
                        if (currentThickness < minThickness)
                        {
                            float thicknessToAdd = (minThickness - currentThickness);
                            min -=  thicknessToAdd;
                            max +=  thicknessToAdd;
                        }
                        box.boundsMin = min;
                        box.boundsMax = max;
                    }
                }
                else if (CollideType == 2)
                {
                    box.C0 = MathematicsUtil.LocalToWorldPosition(box.Position, box.Rotation, box.Center * Scale);

                    box.Up = math.normalize(math.mul(MathematicsUtil.up, transTRS));
                    box.Forward = math.normalize(math.mul(MathematicsUtil.forward, transTRS));
                    box.Right = math.normalize(math.cross(box.Up, box.Forward));

                    box.ScaleSize.w = math.max(math.max(box.ScaleSize.x, box.ScaleSize.y), box.ScaleSize.z) * 0.5f;
                }
                else
                {
                    box.CollideType = PBDColliderType.None;
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void PrepareSpherAndCapsule()
        {
            ref var c = ref this;
            float scale = c.ScaleSize.x;//lossyScale.x
            float 
                halfHeight = c.Size.x * 0.5f,
                Radius = c.Size.y,
                Radius2 = c.Size.z;

            float3 c0 = c.Center;
            float3 c1 = c.Center;
            switch (c.CollideType)
            {
                case PBDColliderType.Sphere:
                    float r = math.max(Radius, Radius2);
                    c.ScaleSize.y = r * scale;
                    c.C0 = MathematicsUtil.LocalToWorldPosition(c.Position,
                        c.Rotation, c.Center);
                    break;
                case PBDColliderType.Capsule:

                    c.ScaleSize.y = Radius * scale;
                    
                    float h = halfHeight - Radius;
                    c0.y += h;
                    c1.y -= h;

                    c.C0 =
                        MathematicsUtil.LocalToWorldPosition(c.Position, c.Rotation, c0);
                    c.Up =
                        MathematicsUtil.LocalToWorldPosition(c.Position, c.Rotation, c1);
                    c.ScaleSize.w = math.distance(c.Up, c.C0);
                    break;
                case PBDColliderType.AsymmetricalCapsule:
                    
                    c.ScaleSize.y = Radius * scale;
                    c.ScaleSize.z = Radius2 * scale;

                    float h0 = halfHeight - Radius;
                    float h1 = halfHeight - Radius2;


                    c0.y += h0;
                    c1.y -= h1;
                    c.C0 =
                        MathematicsUtil.LocalToWorldPosition(c.Position, c.Rotation, c0);
                    c.Up =
                        MathematicsUtil.LocalToWorldPosition(c.Position, c.Rotation, c1);
                    c.ScaleSize.w = math.distance(c.Up, c.C0);
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Collide(in float3 particlePosition, float particleRadius, float elasticity, float friction, ref PointConstraints delta, in int index, ref float3 velocity, ref PBDCollisionHit hit)
        {
            switch (CollideType)
            {
                case PBDColliderType.Plane:
                    return PBDCollisionUtil.OutsidePlane(in particlePosition, particleRadius, in C0,
                        in Up, in Forward, in Right, in ScaleSize, false,
                        ref delta, index, ref velocity, ref hit,
                        elasticity, friction, useConcatNormal);
                case PBDColliderType.Box:
                    return PBDCollisionUtil.OutsideBox(in particlePosition, particleRadius, in C0,
                        in Up, in Forward, in Right, in ScaleSize,
                        ref delta, index, ref velocity, ref hit,
                        elasticity, friction, useConcatNormal);
                
                case PBDColliderType.Sphere:
                    return PBDCollisionUtil.OutsideSphere(in particlePosition, particleRadius,
                        C0, ScaleSize.y,
                        ref delta, index, ref velocity, ref hit,
                        elasticity, friction, useConcatNormal);
                case PBDColliderType.Capsule:
                    return PBDCollisionUtil.OutsideCapsule(in particlePosition, particleRadius,
                        C0, Up,
                        ScaleSize.y, ScaleSize.w,
                        ref delta, index, ref velocity, ref hit,
                        elasticity, friction, useConcatNormal);
                case PBDColliderType.AsymmetricalCapsule:
                    return PBDCollisionUtil.OutsideCapsule2(in particlePosition, particleRadius,
                        C0, Up,
                        ScaleSize.y, ScaleSize.z, ScaleSize.w,
                        ref delta, in index, ref velocity, ref hit,
                        elasticity, friction, useConcatNormal);
            }

            return false;
        }


    }
}