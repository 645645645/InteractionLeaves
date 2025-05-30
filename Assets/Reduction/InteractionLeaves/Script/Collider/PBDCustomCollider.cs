
using Unity.Mathematics;

namespace UnityEngine.PBD
{
    //去掉了无限大平面 做AABB过滤要特别处理
    
    [AddComponentMenu("PBD/PBD Custom Collider")]
    public class PBDCustomCollider : PBDColliderBase
    {
        
        [_ReadOnlyInPlayMode] public float3 m_Size = new float3(1, 1, 1);
        
        [_ReadOnlyInPlayMode] [Tooltip("The radius of the sphere or capsule.")]
        public float m_Radius = 0.5f;

        [_ReadOnlyInPlayMode] [Tooltip("The height of the capsule.")]
        public float m_Height = 2;

        [_ReadOnlyInPlayMode] [Tooltip("The other radius of the capsule.")]
        public float m_Radius2 = 0.5f;
        
        private bool hasInitialized = false;

        [HideInInspector]
        public PBDCustomColliderInfo colliderInfo;
        
        public PBDCustomColliderInfo PbdCustomCollider => colliderInfo;

        protected override void OnEnable()
        {
            InitializeBounds();
            Prepare();
            base.OnEnable();
        }
        
        void OnValidate()
        {
            switch (m_ColliderType)
            {
                case PBDColliderType.Plane:
                case PBDColliderType.Box:
                    m_Size = math.max(m_Size, float3.zero);
                    break;
                case PBDColliderType.Sphere:
                case PBDColliderType.Capsule:
                case PBDColliderType.AsymmetricalCapsule:
                    m_Radius = Mathf.Max(m_Radius, 0);
                    m_Height = Mathf.Max(m_Height, 0);
                    m_Radius2 = Mathf.Max(m_Radius2, 0);
                    break;
                default:
                    break;
            }
            gameObject.isStatic = m_bStatic;

            InitializeBounds();
            Prepare();
        }

        private void Prepare()
        {
            if (!hasInitialized)
            {
                colliderInfo = new PBDCustomColliderInfo();
                hasInitialized = true;
            }

            colliderInfo.CollideType = m_ColliderType;
            colliderInfo.bStatic = m_bStatic;
            colliderInfo.useConcatNormal = m_bStick;
            colliderInfo.Center = m_Center;
            colliderInfo.Position = transform.position;
            colliderInfo.Rotation = transform.rotation;
            switch (m_ColliderType)
            {
                case PBDColliderType.Plane:
                    colliderInfo.Size = m_Size;
                    colliderInfo.Scale = transform.lossyScale;
                    var bounds = _lastBound;
                    float3 size = bounds.size;
                    bool3 isZero = size.xyz < 1e-3f;

                    int zeroCount = math.countbits((uint)MathematicsUtil.bitmask(new bool3(isZero)));
                    if (zeroCount != 1)
                    {
                        colliderInfo.boundsMin = _lastBound.min;
                        colliderInfo.boundsMax = _lastBound.max;
                        break;
                    }
                    uint zeroMask = (uint)MathematicsUtil.bitmask(isZero);
                    float3 up = math.select(0f, 1f, new bool3(
                        (zeroMask & 1) != 0,
                        (zeroMask & 2) != 0,
                        (zeroMask & 4) != 0
                    ));
                    size = m_Size + up * 2;
                    bounds.size = size;
                    colliderInfo.boundsMin = bounds.min;
                    colliderInfo.boundsMax = bounds.max;
                    _lastBound = bounds;
                    break;
                case PBDColliderType.Box:
                    colliderInfo.Size = m_Size;
                    colliderInfo.Scale = transform.lossyScale;
                    colliderInfo.boundsMin = _lastBound.min;
                    colliderInfo.boundsMax = _lastBound.max;
                    break;
                case PBDColliderType.Sphere:
                case PBDColliderType.Capsule:
                case PBDColliderType.AsymmetricalCapsule:
                    colliderInfo.Size = new float3(m_Height, m_Radius, m_Radius2);
                    float4 scale = colliderInfo.ScaleSize;
                    scale.x = transform.lossyScale.x;
                    colliderInfo.ScaleSize = scale;
                    
                    colliderInfo.boundsMin = _lastBound.min;
                    colliderInfo.boundsMax = _lastBound.max;
                    break;
            }

            colliderInfo.Prepare();
        }

        protected override void OnDrawGizmosSelected()
        {
            if (!isActiveAndEnabled)
                return;
            // base.OnDrawGizmosSelected();

            Prepare();
            
            DrawAABB(in colliderInfo.boundsMin, in colliderInfo.boundsMax);
            
            Gizmos.color = Color.yellow;
            switch (m_ColliderType)
            {
                case PBDColliderType.Plane:
                    DrawPlane(in colliderInfo.C0, colliderInfo.Up);
                    DrawCube(in colliderInfo.Size, in colliderInfo.C0, in colliderInfo.Rotation, in colliderInfo.Scale);
                    break;
                case PBDColliderType.Box:
                    DrawCube(in colliderInfo.Size, in colliderInfo.C0, in colliderInfo.Rotation, in colliderInfo.Scale);
                    break;
                case PBDColliderType.Sphere:
                    Gizmos.DrawWireSphere(colliderInfo.C0, colliderInfo.ScaleSize.y);
                    break;
                case PBDColliderType.Capsule:
                    DrawCapsule(colliderInfo.C0, colliderInfo.Up, colliderInfo.ScaleSize.y, colliderInfo.ScaleSize.y);
                    break;
                case PBDColliderType.AsymmetricalCapsule:
                    DrawCapsule(colliderInfo.C0, colliderInfo.Up, colliderInfo.ScaleSize.y, colliderInfo.ScaleSize.z);
                    break;
            }
        }

        private void DrawAABB(in float3 min, in float3 max)
        {
            float3
                center = (max + min) * 0.5f,
                extend = (max - min) * 0.5f;
            float3
                ex1 = new float3(-1, -1, -1) * extend,
                ex2 = new float3(-1, 1, -1) * extend,
                ex3 = new float3(1, 1, -1) * extend,
                ex4 = new float3(1, -1, -1) * extend;

            float3
                a = center + ex1,
                b = center + ex2,
                c = center + ex3,
                d = center + ex4,
                e = center - ex3,
                f = center - ex4,
                g = center - ex1,
                h = center - ex2;

            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(a, d);
            Gizmos.DrawLine(e, f);
            Gizmos.DrawLine(f, g);
            Gizmos.DrawLine(g, h);
            Gizmos.DrawLine(h, e);
            Gizmos.DrawLine(a, e);
            Gizmos.DrawLine(b, f);
            Gizmos.DrawLine(c, g);
            Gizmos.DrawLine(d, h);
        }
        
        private void DrawPlane(in float3 center, in float3 up)
        {
            Gizmos.DrawLine(center, center + up);
        }

        private void DrawCube(in float3 size, in float3 pos, in quaternion rot, in float3 scale)
        {
            float3 extent = size * 0.5f;
            float4x4 trs = float4x4.TRS(pos, rot, scale);
            float3 ex1 = new float3(-1, -1, -1) * extent;
            float3 ex2 = new float3(-1, 1, -1) * extent;
            float3 ex3 = new float3(1, 1, -1) * extent;
            float3 ex4 = new float3(1, -1, -1) * extent;

            float3
                a = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(ex1, 1)),
                b = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(ex2, 1)),
                c = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(ex3, 1)),
                d = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(ex4, 1)),
                e = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(-ex3, 1)),
                f = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(-ex4, 1)),
                g = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(-ex1, 1)),
                h = MathematicsUtil.MatrixMultiplyPoint3x4(trs, new float4(-ex2, 1));

            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(a, d);
            Gizmos.DrawLine(e, f);
            Gizmos.DrawLine(f, g);
            Gizmos.DrawLine(g, h);
            Gizmos.DrawLine(h, e);
            Gizmos.DrawLine(a, e);
            Gizmos.DrawLine(b, f);
            Gizmos.DrawLine(c, g);
            Gizmos.DrawLine(d, h);
        }

        static void DrawCapsule(Vector3 c0, Vector3 c1, float radius0, float radius1)
        {
            Gizmos.DrawLine(c0, c1);
            Gizmos.DrawWireSphere(c0, radius0);
            Gizmos.DrawWireSphere(c1, radius1);
        }
    }
}