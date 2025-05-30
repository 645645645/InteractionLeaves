using System;
using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine.Jobs;

namespace UnityEngine.PBD
{
    public static class PBDCollisionUtil
    {
        //将粒子限制在plane normal正向
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OutsidePlane(in float3 particlePosition, float particleRadius, in float3 center,
            in float3 up, in float3 forward, in float3 right, in float4 size, bool isInfinitePlane,
            ref PointConstraints delta, int index, ref float3 velocity, ref PBDCollisionHit hit, float elasticity, float friction, bool useConcatNormal = true)
        {
            float3 toParticle = particlePosition - center;
            float d = math.dot(toParticle, up);
            if (d < particleRadius)
            {
                if (isInfinitePlane)
                {
                    float insertDepth = particleRadius - d;
                    float3 pushout = up * (insertDepth + 1e-5f);

                    if (useConcatNormal)
                    {
                        float3 concatNormal = CalculateContactNormal(toParticle + pushout + up * size.w);
                        float3 prejictedDelta = ReboundPositionDelta(insertDepth, pushout, concatNormal, elasticity, friction);

                        delta.AddConstraint(index, pushout);
                        // delta.AddConstraint(index, prejictedDelta);
                        // velocity = math.lerp(velocity, prejictedDelta, velocity);
                        velocity = prejictedDelta; //罪恶的开始

                        hit.hitConcatDelta = prejictedDelta;
                        hit.hitConcatNormal = concatNormal;
                    }
                    else
                    {
                        delta.AddConstraint(index, pushout);
                        velocity = ReboundVelocity(velocity, up, elasticity, friction);
                        hit.hitConcatDelta = pushout;
                        hit.hitConcatNormal = up;
                    }
                    hit.isHit = true;
                    hit.insertDepth = insertDepth;
                    hit.hitSurfacePos = pushout + particlePosition;
                    hit.hitActorCenter = center + up * 1e-5f;
                    hit.hitNormal = up;

                    return true;
                }
                else
                {
                    float3 pProjToPlane = toParticle - d * up;

                    float
                        x = math.dot(pProjToPlane, right) * 2,
                        y = math.dot(pProjToPlane, forward) * 2;
                    if (math.abs(x) < size.x && math.abs(y) < size.y)
                    {
                        float insertDepth = particleRadius - d;
                        float3 pushout = up * (insertDepth + 1e-5f);
                        if (useConcatNormal)
                        {
                            float3 concatNormal = CalculateContactNormal(toParticle + pushout + up * size.w);
                            float3 prejictedDelta = ReboundPositionDelta(insertDepth, pushout, concatNormal, elasticity, friction);

                            delta.AddConstraint(index, pushout);
                            // delta.AddConstraint(index, prejictedDelta);
                            velocity = prejictedDelta;
                            hit.hitConcatDelta = prejictedDelta;
                            hit.hitConcatNormal = concatNormal;
                        }
                        else
                        {
                            delta.AddConstraint(index, pushout);
                            velocity = ReboundVelocity(velocity, up, elasticity, friction);
                            hit.hitConcatDelta = pushout;
                            hit.hitConcatNormal = up;
                        }
                        hit.isHit = true;
                        hit.insertDepth = insertDepth;
                        hit.hitSurfacePos = pProjToPlane + center;
                        hit.hitActorCenter = center + up * 1e-5f;
                        hit.hitNormal = up;

                        return true;
                    }
                }
            }

            return false;
        }

        //将粒子限制在box外
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OutsideBox(in float3 particlePosition, float particleRadius, in float3 center,
            in float3 up, in float3 forward, in float3 right, in float4 size,
            ref PointConstraints delta, int index, ref float3 velocity, ref PBDCollisionHit hit, float elasticity, float friction, bool useConcatNormal = true)
        {
            float3 toParticle = particlePosition - center;
            float3 localPos = new float3(
                math.dot(toParticle, right),
                math.dot(toParticle, up),
                math.dot(toParticle, forward)
            );

            float3 halfSize = size.xyz * 0.5f + particleRadius;

            // 计算各轴穿透深度（正值表示穿透）
            float3 penetration = halfSize - math.abs(localPos);

            if (penetration.x > 0 && penetration.y > 0 && penetration.z > 0)
            {
                float minPenetration = math.cmin(penetration);
                int collisionAxis = 0;
                if (penetration.y == minPenetration) collisionAxis = 1;
                if (penetration.z == minPenetration) collisionAxis = 2;

                float3 normal = float3.zero;
                float sign = math.sign(localPos[collisionAxis]);
                switch (collisionAxis)
                {
                    case 0: normal = right * sign; break;
                    case 1: normal = up * sign; break;
                    case 2: normal = forward * sign; break;
                }

                float3 pushout = normal * (minPenetration);
                
                //box特别处理一下 朝上的面黏住
                if (normal.y > 0.707f && useConcatNormal)
                {
                    float3 concatNormal = CalculateContactNormal(toParticle + pushout + normal * size.w);
                    float3 prejictedDelta = ReboundPositionDelta(minPenetration, pushout, concatNormal, elasticity, friction);
                    
                    delta.AddConstraint(index, pushout);
                    // delta.AddConstraint(index, prejictedDelta);
                    velocity = prejictedDelta;
                    
                    hit.hitConcatDelta = prejictedDelta;
                    hit.hitConcatNormal = concatNormal;
                }
                else
                {
                    delta.AddConstraint(index, pushout);
                    velocity = ReboundVelocity(velocity, normal, elasticity, friction);
                    
                    hit.hitConcatDelta = velocity;
                    hit.hitConcatNormal = normal;
                }

                hit.isHit = true;
                hit.hitSurfacePos = pushout + particlePosition;
                hit.hitActorCenter = center;
                hit.hitNormal = normal;
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OutsideSphere(in float3 particlePosition, float particleRadius,
            float3 sphereCenter, float sphereRadius,
            ref PointConstraints particleDelta, int index, ref float3 particleVelocity, ref PBDCollisionHit hit, 
            float elasticity, float friction, bool useConcatNormal = true)
        {
            float r = sphereRadius + particleRadius;
            float r2 = r * r;
            float3 d = particlePosition - sphereCenter;
            float dlen2 = math.lengthsq(d);

            // if is inside sphere, project onto sphere surface
            if (dlen2 > 0 && dlen2 < r2)
            {
                float dlen = math.sqrt(dlen2);
                d /= dlen; //n
                // particleDelta += (r - dlen) * d; //沿d方向挤出，假设穿透距离很小的近似
                // particleVelocity = ReboundVelocity(particleVelocity, d, in elasticity, in friction);

                ReboundPositionDeltaInSphere(in particlePosition,
                    r, in sphereCenter, 
                    dlen, in d,
                    ref particleDelta, index, ref particleVelocity, ref hit,
                    elasticity, friction, useConcatNormal);
                
                return true;
            }


            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OutsideCapsule(in float3 particlePosition, float particleRadius,
            float3 capsuleP0, float3 capsuleP1,
            float capsuleRadius, float dirlen,
            ref PointConstraints particleDelta, int index, ref float3 particleVelocity, ref PBDCollisionHit hit,
            float elasticity, float friction, bool useConcatNormal = true)
        {
            float r = capsuleRadius + particleRadius;
            float r2 = r * r;
            float3 dir = capsuleP1 - capsuleP0;
            float3 d = particlePosition - capsuleP0;
            float t = math.dot(d, dir);

            if (t <= 0)
            {
                // check sphere1
                float dlen2 = math.lengthsq(d);
                if (dlen2 > 0 && dlen2 < r2)
                {
                    float dlen = math.sqrt(dlen2);
                    d /= dlen; //n
                    // particleDelta += (r - dlen) * d; 
                    //
                    // particleVelocity = ReboundVelocity(particleVelocity, d, in elasticity, in friction);

                    ReboundPositionDeltaInSphere(in particlePosition,
                        r, in capsuleP0, 
                        dlen, in d,
                        ref particleDelta, index, ref particleVelocity, ref hit,
                        elasticity, friction, useConcatNormal);
                    
                    // particleVelocity = ReboundVelocity(particleVelocity, d, elasticity, friction);
                    return true;
                }
            }
            else
            {
                float dirlen2 = dirlen * dirlen;
                if (t >= dirlen2)
                {
                    // check sphere2
                    d = particlePosition - capsuleP1;
                    float dlen2 = math.lengthsq(d);
                    if (dlen2 > 0 && dlen2 < r2)
                    {
                        float dlen = math.sqrt(dlen2);
                        d /= dlen;
                        // particleDelta += (r - dlen) * d;
                        //
                        // particleVelocity = ReboundVelocity(particleVelocity, d, in elasticity, in friction);

                        ReboundPositionDeltaInSphere(in particlePosition,
                            r, in capsuleP1, 
                            dlen, in d,
                            ref particleDelta, index, ref particleVelocity, ref hit,
                            elasticity, friction, useConcatNormal);

                        // particleVelocity = ReboundVelocity(particleVelocity, d, elasticity, friction);
                        return true;
                    }
                }
                else
                {
                    // check cylinder
                    float3 q = d - dir * (t / dirlen2);//近似
                    float qlen2 = math.lengthsq(q);
                    if (qlen2 > 0 && qlen2 < r2)
                    {
                        float qlen = math.sqrt(qlen2);
                        q /= qlen;
                        // particleDelta += q * (r - qlen);
                        //
                        // particleVelocity = ReboundVelocity(particleVelocity, q, in elasticity, in friction);
                        
                        float insertDepth = r - qlen;
                        float3 pushout = q * insertDepth;
                        float3 center = (capsuleP0 + capsuleP1) * 0.5f;
                        if (useConcatNormal)
                        {
                            float3 concatNormal = CalculateContactNormal(particlePosition - center);
                            float3 prejictedDelta = ReboundPositionDelta(insertDepth, pushout, concatNormal, elasticity, friction);

                            particleDelta.AddConstraint(index, pushout);
                            // particleDelta.AddConstraint(index, prejictedDelta);
                            particleVelocity = prejictedDelta;
                            hit.hitConcatDelta = prejictedDelta;
                            hit.hitConcatNormal = concatNormal;
                        }
                        else
                        {
                            particleDelta.AddConstraint(index, pushout);
                            particleVelocity = ReboundVelocity(particleVelocity, q, elasticity, friction);
                    
                            hit.hitConcatDelta = particleVelocity;
                            hit.hitConcatNormal = q;
                        }

                        hit.isHit = true;
                        hit.insertDepth = insertDepth;
                        hit.hitSurfacePos = pushout + particlePosition;
                        hit.hitActorCenter = center;
                        hit.hitNormal = q;

                        return true;
                    }
                }
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool OutsideCapsule2(in float3 particlePosition, float particleRadius,
            float3 capsuleP0, float3 capsuleP1,
            float capsuleRadius0, float capsuleRadius1, float dirlen,
            ref PointConstraints particleDelta, in int index, ref float3 particleVelocity, ref PBDCollisionHit hit, 
            float elasticity, float friction, bool useConcatNormal = true)
        {
            float3 dir = capsuleP1 - capsuleP0;
            float3 d = particlePosition - capsuleP0;
            float t = math.dot(d, dir);

            if (t <= 0)
            {
                // check sphere1
                float r = capsuleRadius0 + particleRadius;
                float r2 = r * r;
                float dlen2 = math.lengthsq(d);
                if (dlen2 > 0 && dlen2 < r2)
                {
                    float dlen = math.sqrt(dlen2);
                    d /= dlen;
                    // particleDelta += (r - dlen) * d;
                    //
                    // particleVelocity = ReboundVelocity(particleVelocity, d, in elasticity, in friction);
                    ReboundPositionDeltaInSphere(in particlePosition,
                        r, in capsuleP0, 
                        dlen, in d,
                        ref particleDelta, index, ref particleVelocity, ref hit,
                        elasticity, friction, useConcatNormal);

                    // particleVelocity = ReboundVelocity(particleVelocity, d, elasticity, friction);
                    return true;
                }
            }
            else
            {
                float dirlen2 = dirlen * dirlen;
                if (t >= dirlen2)
                {
                    // check sphere2
                    float r = capsuleRadius1 + particleRadius;
                    float r2 = r * r;
                    d = particlePosition - capsuleP1;
                    float dlen2 = math.lengthsq(d);
                    if (dlen2 > 0 && dlen2 < r2)
                    {
                        float dlen = math.sqrt(dlen2);
                        d /= dlen;
                        // particleDelta += (r - dlen) * d;
                        //
                        // particleVelocity = ReboundVelocity(particleVelocity, d, in elasticity, in friction);

                        ReboundPositionDeltaInSphere(in particlePosition,
                            r, in capsuleP1, 
                            dlen, in d,
                            ref particleDelta, index, ref particleVelocity, ref hit,
                            elasticity, friction, useConcatNormal);
                        
                        // particleVelocity = ReboundVelocity(particleVelocity, d, elasticity, friction);
                        return true;
                    }
                }
                else
                {
                    // check cylinder
                    float3 q = d - dir * (t / dirlen2);
                    float qlen2 = math.lengthsq(q);

                    float klen = math.dot(d, dir / dirlen);
                    float r = math.lerp(capsuleRadius0, capsuleRadius1, klen / dirlen) + particleRadius;
                    float r2 = r * r;

                    if (qlen2 > 0 && qlen2 < r2)
                    {
                        float qlen = math.sqrt(qlen2);
                        q /= qlen;
                        // particleDelta += q * (r - qlen);
                        //
                        // particleVelocity = ReboundVelocity(particleVelocity, q, in elasticity, in friction);
                        
                        float insertDepth = r - qlen;
                        float3 pushout = q * insertDepth;
                        float3 center = (capsuleP0 + capsuleP1) * 0.5f;
                        if (useConcatNormal)
                        {
                            float3 concatNormal = CalculateContactNormal(particlePosition - center);
                            float3 prejictedDelta = ReboundPositionDelta(insertDepth, pushout, concatNormal, elasticity, friction);

                            particleDelta.AddConstraint(index, pushout);
                            // particleDelta.AddConstraint(index, prejictedDelta);

                            particleVelocity = prejictedDelta;
                            // particleDelta += prejictedDelta;
                            // particleVelocity = ReboundVelocity(particleVelocity, q, elasticity, friction);
                            // particleVelocity = prejictedDelta;
                            hit.hitConcatDelta = prejictedDelta;
                            hit.hitConcatNormal = concatNormal;
                        }
                        else
                        {
                            particleDelta.AddConstraint(index, pushout);
                            particleVelocity = ReboundVelocity(particleVelocity, q, elasticity, friction);
                    
                            hit.hitConcatDelta = particleVelocity;
                            hit.hitConcatNormal = q;
                        }

                        hit.isHit = true;
                        hit.insertDepth = insertDepth;
                        hit.hitSurfacePos = pushout + particlePosition;
                        hit.hitActorCenter = center;
                        hit.hitNormal = q;

                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 废弃
        /// </summary>
        /// <param name="particlePosition"></param>
        /// <param name="op">sphereCenter-particlePosition</param>
        /// <param name="sphereCenter"></param>
        /// <param name="R2">r*r</param>
        /// <param name="particleDelta"></param>
        /// <param name="particleVelocity"></param>
        /// <returns></returns>
        static bool HitInSpherePrecise(in float3 particlePosition, float3 op, float3 sphereCenter, float R2,
            ref PointConstraints particleDelta, int index, ref float3 particleVelocity, float elasticity, float friction)
        {
            float3 rayDir = MathematicsUtil.Normalize(-particleVelocity);

            bool bHit = InSphereRayIntersection(op, rayDir, R2, out float ti);

            float3 insertOffset = ti * rayDir;

            float3 hitPos = particlePosition + insertOffset;

            float3 hitNormal = MathematicsUtil.Normalize(hitPos - sphereCenter);

            particleVelocity = ReboundVelocity(particleVelocity, hitNormal, elasticity, friction);

            float3 newPos = hitPos + MathematicsUtil.Normalize(particleVelocity) * math.length(insertOffset);

            particleDelta.SetConstraint(index, newPos - particlePosition);

            return bHit;
        }

        //https://zhuanlan.zhihu.com/p/136763389
        static bool InSphereRayIntersection(
            float3 op, float3 rayDirection, float R2, out float t)
        {
            // float3 op = sphereCenter - rayOrigin;
            float a2 = math.dot(op, op);
            float l = math.dot(rayDirection, op);
            // float R2 = sphereRadius * sphereRadius;
            // if (a2 > R2 && l < 0)
            // {
            //     t = -1;
            //     return false;
            // }

            float m2 = a2 - l * l;
            float q2 = R2 - m2;

            if (q2 > 0)
            {
                float q = math.sqrt(q2);
                // if (a2 > R2)
                //     t = l - q;
                // else
                t = l + q;

                return true;
            }

            t = -1;
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 ReboundVelocity(float3 particleVelocity, in float3 normal, float elasticity, float friction)
        {
            float normalVelocity = math.dot(particleVelocity, normal);
            if (normalVelocity < 0) // 仅处理相互靠近的情况
            {
                // float totalMass = particleMass + otherMass;
                // float massRatio = (particleMass - elasticity * otherMass) / totalMass;

                float reflectedNormalVelocity = -normalVelocity * elasticity;

                float3 tangentVelocity = particleVelocity - normal * normalVelocity;

                tangentVelocity *= math.max(1 - friction - reflectedNormalVelocity * elasticity, 0);

                particleVelocity = tangentVelocity + normal * reflectedNormalVelocity;
                // particleVelocity = tangentVelocity;
                // particleVelocity = 0;
            }

            return particleVelocity;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void ReboundPositionDeltaInSphere(in float3 particlePosition,
            float radiusSum, in float3 sphereCenter,
            float dislen, in float3 concatNormal,
            ref PointConstraints particleDelta, int index, ref float3 particleVelocity, ref PBDCollisionHit hit, 
            float elasticity, float friction, bool useConcatNormal = true)
        {
            float insertDepth = radiusSum - dislen;
            float3 pushout = concatNormal * insertDepth;
            if (useConcatNormal)
            {
                float3 prejictedDelta = ReboundPositionDelta(insertDepth, pushout, concatNormal, elasticity, friction);
                // particleDelta += prejictedDelta;
                particleDelta.AddConstraint(index, pushout);
                // particleDelta.AddConstraint(index, prejictedDelta);

                particleVelocity = prejictedDelta;

                hit.hitConcatDelta = prejictedDelta;
                hit.hitConcatNormal = concatNormal;
            }
            else
            {
                particleDelta.AddConstraint(index, pushout);
                particleVelocity = ReboundVelocity(particleVelocity, concatNormal, elasticity, friction);
                    
                hit.hitConcatDelta = particleVelocity;
                hit.hitConcatNormal = concatNormal;
            }
            hit.isHit = true;
            hit.insertDepth = insertDepth;
            hit.hitSurfacePos = pushout + particlePosition;
            hit.hitActorCenter = sphereCenter;
            hit.hitNormal = concatNormal;
        }

        //elasticity 当动摩擦用
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 ReboundPositionDelta(float insertDepth, in float3 pushout,
            in float3 concatNormal, float elasticity, float friction)
        {
            float3 prejictedDelta = (math.dot(pushout, concatNormal)) * concatNormal;
            // float3 prejictedDelta = math.length(math.cross(pushout, concatNormal)) * concatNormal;
            float predictedDeltaLen = math.length(prejictedDelta);

            if (predictedDeltaLen >= friction * insertDepth)
                prejictedDelta *= math.min(1, elasticity * insertDepth / predictedDeltaLen);

            return prejictedDelta;
        }

        // 计算接触法向量
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 CalculateContactNormal(float3 particlePosition, float3 otherPosition)
        {
            float3 diff = particlePosition - otherPosition;
            return math.normalize(diff);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static float3 CalculateContactNormal(float3 centerToParticle)
        {
            return math.normalize(centerToParticle);
        }


        // public float GetPlaneDistanceToPoint(in float3 particlePosition, in float3 center, in float3 normal)
        // {
        //     float3 vectorFromPlane = particlePosition - center;
        //     return math.dot(vectorFromPlane, normal);
        // }
    }
}