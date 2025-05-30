using System;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Events;

namespace UnityEngine.PBD
{
    [DisallowMultipleComponent]
    public class PBDColliderBase : MonoBehaviour
    {
        [_ReadOnlyInPlayMode] public PBDColliderType m_ColliderType = PBDColliderType.None;
        
        [_ReadOnlyInPlayMode] public bool m_bStatic = false;

        [_ReadOnlyInPlayMode] public bool m_bStick = true;

        [_ReadOnlyInPlayMode] public Vector3 m_Center = float3.zero;

        #region OnBoundsChangedEvent
        
        private bool _noRenderCom;
        private Renderer _renderer;
        protected Bounds _lastBound;
        private readonly WaitForSeconds _waitForSeconds = new (0.1f);
        private Coroutine _wait;
        

        private bool HasBoundsChanged()
        {
            if (_renderer is null)
            {
                _noRenderCom = true;
                return false;
            }

            return !_lastBound.Equals(_renderer.bounds);
        }

        private IEnumerator CheckBoundsChange()
        {
            while (true)
            {
                yield return _waitForSeconds;
                
                if (HasBoundsChanged())
                {
                    OnBoundsChangged();
                    _lastBound = _renderer.bounds;
                }
                if (_noRenderCom)
                    break;
            }
        }
        protected virtual void Start()
        {
        }

        protected virtual void OnBoundsChangged()
        {
            if (InteractionOfLeavesManager.IsInstance()) 
                InteractionOfLeavesManager.Instance?.UpdateColliderInfo(this);
        }
        
        protected virtual void OnEnable()
        {
            //后添加进场景的onEnable能比前面的Awake更早
            // if (InteractionOfLeavesManager.IsInstance()) 
                InteractionOfLeavesManager.Instance?.RegistRigibody(this);
            _wait = StartCoroutine(CheckBoundsChange());
        }

        protected virtual void OnDisable()
        {
            //..
            if (InteractionOfLeavesManager.IsInstance()) 
                InteractionOfLeavesManager.Instance?.UnRegistRigibody(this);
            StopCoroutine(_wait);
        }

        #endregion

        protected void InitializeBounds()
        {
            if (!_noRenderCom && _renderer == null)
                _renderer = GetComponentInChildren<Renderer>();
            if (_renderer is null)
                _noRenderCom = true;
            else
                _lastBound = _renderer.bounds;
        }
        
        protected virtual void OnDrawGizmosSelected()
        {
            if(Application.isPlaying)
            {
                var bound = _lastBound;

                float3
                    center = bound.center,
                    extend = bound.extents;

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

        }
    }

  
}