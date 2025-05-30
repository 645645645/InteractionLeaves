using System.Collections;

namespace UnityEngine.PBD
{
    [DisallowMultipleComponent]
    public class PBDForce : MonoBehaviour
    {
        public PBDForceType m_ForceType;
        
        [Tooltip("一般是xz水平方向，y竖直")]
        public Vector3 m_Force = Vector3.zero;
        
        public Vector3 m_Center = Vector3.zero;
        
        // [Delayed]
        public float m_Radius = 0.5f;

        [Range(1, 60)] public float m_LerpPosRatio = 10;

        private PBDForceType _lastType;
        private Vector3 _lastForce;
        private Vector3 _lastCenter;
        private float _lastRadius;
        private readonly WaitForSeconds _waitForSeconds = new (0.1f);
        private Coroutine _wait;
        
        //transform内容job直接拿，这些自己加事件监控
        private bool HasValueChanged()
        {
            return !_lastForce.Equals(m_Force) ||
                   !_lastCenter .Equals(m_Center) ||
                   !_lastRadius.Equals(m_Radius);
        }
        
        private bool HasTypeChanged()
        {
            return !_lastType.Equals(m_ForceType);
        }

        private IEnumerator CheckValueChange()
        {
            while (true)
            {
                yield return _waitForSeconds;
                
                if (HasTypeChanged())
                {
                    OnBeforeTypeChangged();
                    yield return 0;
                    
                    OnAfterTypeChangged();
                }
                if (HasValueChanged())
                {
                    OnValueChangged();
                    _lastForce = m_Force;
                    _lastCenter = m_Center;
                    _lastRadius = m_Radius;
                }
            }
        }

        private void OnValueChangged()
        {
            if (Application.isPlaying)
            {
                Prepare();
                if (InteractionOfLeavesManager.IsInstance()) 
                    InteractionOfLeavesManager.Instance?.UpdateForceInfo(this);
            }
        }

        private void OnBeforeTypeChangged()
        {
            if (Application.isPlaying)
            {
                if (InteractionOfLeavesManager.IsInstance())
                    InteractionOfLeavesManager.Instance?.UnRegistForce(this);
            }
        }

        private void OnAfterTypeChangged()
        {
            if (Application.isPlaying)
            {
                _lastType = m_ForceType;
                Prepare();
                InteractionOfLeavesManager.Instance?.RegistForce(this);
            }
            
        }

        [HideInInspector]
        public PBDForceField force;

        private void OnEnable()
        {
            _wait = StartCoroutine(CheckValueChange());
            Prepare();
            _lastType = m_ForceType;
            InteractionOfLeavesManager.Instance?.RegistForce(this);
        }

        private void OnDisable()
        {
            //..
            if (InteractionOfLeavesManager.IsInstance()) 
                InteractionOfLeavesManager.Instance?.UnRegistForce(this);
            StopCoroutine(_wait);
        }

        private void OnValidate()
        {
            if(m_Radius < 0)
                m_Radius = Mathf.Max(m_Radius, 0);
            
            if (m_ForceType == PBDForceType.Viscosity &&
                (m_Force.x < -1 || m_Force.x > 1 ||
                m_Force.y < -1 || m_Force.y > 1 ||
                m_Force.z < -1 || m_Force.z > 1))
            {
                m_Force = new Vector3()
                {
                    x = Mathf.Max(m_Force.x, 0),
                    y = Mathf.Max(m_Force.y, 0),
                    z = Mathf.Max(m_Force.z, 0),
                };
            }

            // Prepare();

        }

        void Prepare()
        {
            force.ForceType = m_ForceType;
            force.Force = m_Force;
            force.Center = m_Center;
            force.Radius = m_Radius;
            force.LerpRatio = m_LerpPosRatio;
            force.DeltaTime = Time.fixedDeltaTime;
            force.loacl2World = transform.localToWorldMatrix;
            force.Prepare();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;

            Prepare();

            switch (m_ForceType)
            {
                case PBDForceType.BernoulliForce:
                case PBDForceType.DelayBernoulliForce:
                    Gizmos.DrawWireSphere(force.C0, force.Radius);
                    Gizmos.DrawWireSphere(force.C1, force.Radius);
                    break;
                case PBDForceType.Vortex:
                case PBDForceType.Repulsion:
                case PBDForceType.Viscosity:
                case PBDForceType.ViscosityHeart:
                    Gizmos.DrawWireSphere(force.C0, force.Radius);
                    break;
            }
        }
    }
}