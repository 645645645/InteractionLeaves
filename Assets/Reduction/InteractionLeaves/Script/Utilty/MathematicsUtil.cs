using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine.Jobs;


public static class MathematicsUtil
{
    public const float PI = 3.14159265f;
    public const float kEpsilon = 0.000001F;

    // Degrees-to-radians conversion constant (RO).
    public const float Deg2Rad = PI * 2F / 360F;

    // Radians-to-degrees conversion constant (RO).
    public const float Rad2Deg = 1F / Deg2Rad;

    public static readonly float3 right = new float3(1, 0, 0);
    public static readonly float3 up = new float3(0, 1, 0);
    public static readonly float3 forward = new float3(0, 0, 1);
    public static readonly float3 one = new float3(1, 1, 1);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int bitmask(bool2 value)
    {
        int mask = 0;
        if (value.x) mask |= 0x01;
        if (value.y) mask |= 0x02;
        return mask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int bitmask(bool3 value)
    {
        int mask = 0;
        if (value.x) mask |= 0x01;
        if (value.y) mask |= 0x02;
        if (value.z) mask |= 0x04;
        return mask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int bitmask(bool4 value)
    {
        int mask = 0;
        if (value.x) mask |= 0x01;
        if (value.y) mask |= 0x02;
        if (value.z) mask |= 0x04;
        if (value.w) mask |= 0x08;
        return mask;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 LocalToWorldPosition(float3 parentPosition, quaternion parentRotation, float3 targetLocalPosition)
    {
        return math.mul(parentRotation, targetLocalPosition) + parentPosition;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion LocalToWorldRotation(quaternion parentRotation, quaternion targetLocalRotation)
    {
        return math.mul(parentRotation, targetLocalRotation);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 WorldToLocalPosition(float3 parentPosition, quaternion parentRotation, float3 targetWorldPosition)
    {
        return float3.zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion WorldToLocalRotation(quaternion parentRotation, quaternion targetWorldRotation)
    {
        return quaternion.identity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetMatrixPosition(float4x4 matrix)
    {
        return matrix.c3.xyz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion GetMatrixRotation(float4x4 matrix)
    {
        return quaternion.LookRotation(matrix.c2.xyz, matrix.c1.xyz);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetMatrixScale(float4x4 matrix)
    {
        float x = Length(matrix.c0);
        float y = Length(matrix.c1);
        float z = Length(matrix.c2);
        return new float3(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 MatrixMultiplyPoint3x4(float4x4 matrix, float4 pos)
    {
        return matrix.c0.xyz * pos.x + matrix.c1.xyz * pos.y + matrix.c2.xyz * pos.z + matrix.c3.xyz * pos.w;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 MatrixMultiplyPoint3x4(float4x4 matrix, float3 pos, float w)
    {
        return matrix.c0.xyz * pos.x + matrix.c1.xyz * pos.y + matrix.c2.xyz * pos.z + matrix.c3.xyz * w;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetLossyScale(TransformAccess transformAccess)
    {
        return transformAccess.localToWorldMatrix.lossyScale;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Length(float2 vec)
    {
        float lengthSQ = math.lengthsq(vec);
        return lengthSQ > math.EPSILON ? (float)math.sqrt(lengthSQ) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Length(float3 vec)
    {
        float lengthSQ = math.lengthsq(vec);
        return lengthSQ > math.EPSILON ? (float)math.sqrt(lengthSQ) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Length(float4 vec)
    {
        float lengthSQ = math.lengthsq(vec);
        return lengthSQ > math.EPSILON ? (float)math.sqrt(lengthSQ) : 0f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 Normalize(float2 vec)
    {
        float magsqr = math.dot(vec, vec);
        return magsqr > math.EPSILON ? (vec * new float2(math.rsqrt(magsqr))) : float2.zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 Normalize(float3 vec)
    {
        float magsqr = math.dot(vec, vec);
        return magsqr > math.EPSILON ? (vec * new float3(math.rsqrt(magsqr))) : float3.zero;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float4 Normalize(float4 vec)
    {
        float magsqr = math.dot(vec, vec);
        return magsqr > math.EPSILON ? (vec * new float4(math.rsqrt(magsqr))) : float4.zero;
    }

    //lossyscale有负缩放要补反方向... sign(parentScale)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 TransformDirection(float3 parentWorldPos, quaternion parentWorldRot, float3 localDir)
    {
        float4x4 Local2WorldTR = float4x4.TRS(float3.zero, parentWorldRot, 1);
        //求个逆就有误差了..
        float4x4 Local2WorldTR_IT = math.transpose(math.inverse(Local2WorldTR));
        // return math.mul(Local2WorldTR_IT, new float4(localDir, 0)).xyz;
        return math.mul((float3x3)Local2WorldTR_IT, localDir).xyz;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 InverseTransformDirection(float3 parentWorldPos, quaternion parentWorldRot, float3 worldDir)
    {
        float4x4 World2LocalTR_I = float4x4.TRS(float3.zero, parentWorldRot, 1);
        float4x4 World2LocalTR_IT = math.transpose((World2LocalTR_I));
        return math.mul((float3x3)World2LocalTR_IT, worldDir).xyz;
    }

    // public static quaternion FromToRotation(float3 fromDirection, float3 toDirection)
    // {
    //     float fromSq = math.dot(fromDirection, fromDirection);
    //     float toSq = math.dot(toDirection, toDirection);
    //     if(fromSq <= math.EPSILON || toSq <= math.EPSILON)
    //         return quaternion.identity;
    //     
    //     float3 unitFrom = fromDirection * math.rsqrt(fromSq);
    //     float3 unitTo = toDirection * math.rsqrt(toSq);
    //     float d = math.dot(unitFrom, unitTo);
    //     if (d >= 1f)
    //     {
    //         return quaternion.identity;
    //     }
    //     else if(d <= -1f)
    //     {
    //         float3 axis = math.cross(unitFrom, right);
    //         return quaternion.AxisAngle(math.normalize(axis), PI);
    //     }
    //     else
    //     {
    //         float s = 1 + d;
    //         float3 v = math.cross(unitFrom, unitTo);
    //         quaternion result = new quaternion(v.x, v.y, v.z, s);
    //         return math.normalize(result);
    //     }
    // }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion FromToRotation(float3 from, float3 to)
    {
        float3
            v1 = Normalize(from),
            v2 = Normalize(to),
            cr = math.cross(v1, v2);
        float4 q = new float4(cr, 1 + math.dot(v1, v2));
        return math.normalize(q);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion QuaternionMultiply(quaternion lhs, quaternion rhs)
    {
        return new quaternion(
            (float)((double)lhs.value.w * (double)rhs.value.x + (double)lhs.value.x * (double)rhs.value.w + (double)lhs.value.y * (double)rhs.value.z - (double)lhs.value.z * (double)rhs.value.y),
            (float)((double)lhs.value.w * (double)rhs.value.y + (double)lhs.value.y * (double)rhs.value.w + (double)lhs.value.z * (double)rhs.value.x - (double)lhs.value.x * (double)rhs.value.z),
            (float)((double)lhs.value.w * (double)rhs.value.z + (double)lhs.value.z * (double)rhs.value.w + (double)lhs.value.x * (double)rhs.value.y - (double)lhs.value.y * (double)rhs.value.x),
            (float)((double)lhs.value.w * (double)rhs.value.w - (double)lhs.value.x * (double)rhs.value.x - (double)lhs.value.y * (double)rhs.value.y - (double)lhs.value.z * (double)rhs.value.z));
    }

    // Rotates the point /point/ with /rotation/.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 QuaternionMultiplyPoint(quaternion rotation, float3 point)
    {
        float x = rotation.value.x * 2F;
        float y = rotation.value.y * 2F;
        float z = rotation.value.z * 2F;
        float xx = rotation.value.x * x;
        float yy = rotation.value.y * y;
        float zz = rotation.value.z * z;
        float xy = rotation.value.x * y;
        float xz = rotation.value.x * z;
        float yz = rotation.value.y * z;
        float wx = rotation.value.w * x;
        float wy = rotation.value.w * y;
        float wz = rotation.value.w * z;

        float3 res;
        res.x = (1F - (yy + zz)) * point.x + (xy - wz) * point.y + (xz + wy) * point.z;
        res.y = (xy + wz) * point.x + (1F - (xx + zz)) * point.y + (yz - wx) * point.z;
        res.z = (xz - wy) * point.x + (yz + wx) * point.y + (1F - (xx + yy)) * point.z;
        return res;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion QuaternionNormalize(quaternion q)
    {
        float mag = math.sqrt(math.dot(q, q));

        if (mag < math.EPSILON)
            return quaternion.identity;

        return new quaternion(q.value.x / mag, q.value.y / mag, q.value.z / mag, q.value.w / mag);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool QuaternionIsEqualUsingDot(float dot)
    {
        // Returns false in the presence of NaN values.
        return dot > 1.0f - kEpsilon;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool QuaternionEquals(quaternion lhs, quaternion rhs)
    {
        return QuaternionIsEqualUsingDot(math.dot(lhs, rhs));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float QuaternionAngle(quaternion a, quaternion b)
    {
        float dot = math.min(math.abs(math.dot(a, b)), 1.0F);
        return QuaternionIsEqualUsingDot(dot) ? 0.0f : math.acos(dot) * 2.0F * Rad2Deg;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion QuaternionRotateTowards(quaternion from, quaternion to, float maxDegreesDelta)
    {
        float angle = QuaternionAngle(from, to);
        if (angle == 0.0f) return to;
        return QuaternionSlerpUnclamped(from, to, math.min(1.0f, maxDegreesDelta / angle));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion QuaternionSlerpUnclamped(quaternion a, quaternion b, float t)
    {
        // 计算点积并处理对跖点（最短路径）
        float dot = math.dot(a, b);
        bool negative = dot < 0.0f;
        float absDot = math.abs(dot);
        float cosTheta = math.clamp(absDot, -1.0f, 1.0f);
        float theta = math.acos(cosTheta);

        if (math.abs(theta) < kEpsilon)
        {
            return b;
        }

        // 处理对跖点：反转其中一个四元数以确保最短路径
        quaternion target = negative ? QuaternionInverse(b) : b;
        float sinTheta = math.sin(theta);
        float invSinTheta = 1.0f / sinTheta;

        // 球面插值公式：a * sin((1-t)θ) + target * sin(tθ)
        float factorA = math.sin((1.0f - t) * theta) * invSinTheta;
        float factorB = math.sin(t * theta) * invSinTheta;

        quaternion result;
        result.value = a.value * factorA + target.value * factorB;
        result.value = math.normalize(result.value);

        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static quaternion QuaternionInverse(quaternion q)
    {
        float normSq = math.lengthsq(q);

        if (normSq < math.EPSILON)
        {
            return quaternion.identity;
        }

        float invNormSq = 1.0f * math.rcp(normSq);
        quaternion conjugate = new quaternion(q.value.x * -1, q.value.y * -1, q.value.z * -1, q.value.w);

        return new quaternion(
            conjugate.value.x * invNormSq,
            conjugate.value.y * invNormSq,
            conjugate.value.z * invNormSq,
            conjugate.value.w * invNormSq
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 RotatePointAroundAxis(float3 point, float3 ori, float3 axis, float angle)
    {
        axis = Normalize(axis);
        quaternion rotation = quaternion.AxisAngle(axis, angle);

        return math.mul(rotation, point - ori) + ori;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetBoundsMin(
        float3 a, float3 b, float3 c, float3 d,
        float3 e, float3 f, float3 g, float3 h)
    {
        return new float3(
            GetBoundsMinByAxis(0, a, b, c, d, e, f, g, h),
            GetBoundsMinByAxis(1, a, b, c, d, e, f, g, h),
            GetBoundsMinByAxis(2, a, b, c, d, e, f, g, h)
        );
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetBoundsMax(
        float3 a, float3 b, float3 c, float3 d,
        float3 e, float3 f, float3 g, float3 h)
    {
        return new float3(
            GetBoundsMaxByAxis(0, a, b, c, d, e, f, g, h),
            GetBoundsMaxByAxis(1, a, b, c, d, e, f, g, h),
            GetBoundsMaxByAxis(2, a, b, c, d, e, f, g, h)
        );
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetBoundsMinByAxis(int index,
        float3 a, float3 b, float3 c, float3 d,
        float3 e, float3 f, float3 g, float3 h)
    {
        return math.min(math.min(math.min(a[index], b[index]), math.min(c[index], d[index])), math.min(math.min(e[index], f[index]), math.min(g[index], h[index])));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float GetBoundsMaxByAxis(int index,
        float3 a, float3 b, float3 c, float3 d,
        float3 e, float3 f, float3 g, float3 h)
    {
        return math.max(math.max(math.max(a[index], b[index]), math.max(c[index], d[index])), math.max(math.max(e[index], f[index]), math.max(g[index], h[index])));
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AABBOverlap(in float3 particlePosition, in float particleRadius, in float3 min, in float3 max)
    {
        float3 
            pMin = particlePosition - particleRadius,
            pMax = particlePosition + particleRadius;
        return (pMax.x >= min.x && pMin.x <= max.x) &&
               (pMax.y >= min.y && pMin.y <= max.y) &&
               (pMax.z >= min.z && pMin.z <= max.z);
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool AABBOverlap(in float3 aMin, in float3 aMax, in float3 bMin, in float3 bMax)
    {
        return (aMax.x >= bMin.x && aMin.x <= bMax.x) &&
               (aMax.y >= bMin.y && aMin.y <= bMax.y) &&
               (aMax.z >= bMin.z && aMin.z <= bMax.z);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float2 UnitVectorToOctahedron(float3 N)
    {
        float2 Oct = N.xy;
        if (N.z < 0)
        {
            Oct = (1 - math.abs(N.yx)) * new  float2(N.x >= 0 ? 1 : -1, N.y >= 0 ? 1 : -1);
        }
        return Oct;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 OctahedronToUnitVector(float2 Oct)
    {
        float3 N = new float3(Oct, 1 - math.dot(1, math.abs(Oct)));
        if (N.z < 0)
        {
            // N.xy = (1 - math.abs(N.yx)) * (N.xy >= 0 ? new float2(1, 1) : new float2(-1, -1));
            N.xy = (1 - math.abs(N.yx)) * math.select(new float2(-1, -1), new float2(1, 1), N.xy >= 0);
        }
        return math.normalize(N);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InFrustum(in float4x4 cullingMatrix, in float4 pos)
    {
        float4 p = math.mul(cullingMatrix, pos);
        return p.w > p.x && -p.w < p.x && p.w > p.y && (-p.w < p.y) && p.w > p.z && -p.w < p.z;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InFrustum(in float4x4 cullingMatrix, in float3 pos)
    {
        float4 p = math.mul(cullingMatrix, new float4(pos,1));
        return p.w > p.x && -p.w < p.x && p.w > p.y && (-p.w < p.y) && p.w > p.z && -p.w < p.z;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InFrustum(in float4 p)
    {
        return p.w > p.x && -p.w < p.x && p.w > p.y && (-p.w < p.y) && p.w > p.z && -p.w < p.z;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InSphereSpacial(in float3 point, float pointRadius , in float3 center, float radius)
    {
        float3 d = point - center;
        float d2 = math.dot(d, d);
        float r = pointRadius + radius;
        return d2 < r * r;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool InSphereSpacial(in float3 point, float pointRadius , in float4 centerParams)
    {
        float3 d = point - centerParams.xyz;
        float d2 = math.dot(d, d);
        float r = pointRadius + centerParams.w;
        return d2 < r * r;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool PointInRect(in float3 p1, in float3 p2, in float3 p3, in float3 p4, in float3 p)
    {
        bool inRect1 = math.dot(GetCross(p1, p2, p), GetCross(p3, p4, p)) >= 0;
        bool inRect2 = math.dot(GetCross(p2, p3, p), GetCross(p4, p1, p)) >= 0;
        return inRect1 && inRect2;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float3 GetCross(in float3 p1, in float3 p2, in float3 p)
    {
        return math.cross(p2 - p1, p - p1);
    }
}