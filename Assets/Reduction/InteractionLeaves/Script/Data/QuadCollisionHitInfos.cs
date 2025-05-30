using Unity.Collections;
using Unity.Mathematics;

namespace UnityEngine.PBD
{
    
    [BurstCompatible]
    public struct QuadCollisionHitInfos
    {
        private PBDCollisionHit _hit0;
        private PBDCollisionHit _hit1;
        private PBDCollisionHit _hit2;
        private PBDCollisionHit _hit3;
        
        public void SetHit(int index, PBDCollisionHit hit)
        {
            switch (index)
            {
                case 0: _hit0 = hit; break;
                case 1: _hit1 = hit; break;
                case 2: _hit2 = hit; break;
                case 3: _hit3 = hit; break;
                default: throw new System.IndexOutOfRangeException();
            }
        }
        
        public PBDCollisionHit GetHit(int index)
        {
            switch (index)
            {
                case 0:  return _hit0;
                case 1:  return _hit1;
                case 2:  return _hit2;
                case 3:  return _hit3;
                default: throw new System.IndexOutOfRangeException();
            }
        }
        
    }
    
    
        
    //4x4
    [BurstCompatible]
    public struct PointConstraints
    {
        private float3 _constraint0;
        private float3 _constraint1;
        private float3 _constraint2;
        private float3 _constraint3;
    
        private int _hitCount0;
        private int _hitCount1;
        private int _hitCount2;
        private int _hitCount3;
    
        public float3 this[int index]
        {
            get
            {
                switch (index)
                {
                    case 0: return _constraint0;
                    case 1: return _constraint1;
                    case 2: return _constraint2;
                    case 3: return _constraint3;
                    default: throw new System.IndexOutOfRangeException();
                }
            }
        }
        
        public void SetConstraint(int index, float3 value)
        {
            switch (index)
            {
                case 0: _constraint0 = value; break;
                case 1: _constraint1 = value; break;
                case 2: _constraint2 = value; break;
                case 3: _constraint3 = value; break;
                default: throw new System.IndexOutOfRangeException();
            }
        }
    
        public void IncrementHitCount(int index)
        {
            switch (index)
            {
                case 0: _hitCount0++; break;
                case 1: _hitCount1++; break;
                case 2: _hitCount2++; break;
                case 3: _hitCount3++; break;
                default: throw new System.IndexOutOfRangeException();
            }
        }
    
        public int GetHitCount(int index)
        {
            switch (index)
            {
                case 0: return _hitCount0;
                case 1: return _hitCount1;
                case 2: return _hitCount2;
                case 3: return _hitCount3;
                default: throw new System.IndexOutOfRangeException();
            }
        }
        
        public void AddConstraint(int index, float3 delta)
        {
            switch (index)
            {
                case 0: _constraint0 += delta; break;
                case 1: _constraint1 += delta; break;
                case 2: _constraint2 += delta; break;
                case 3: _constraint3 += delta; break;
                default: throw new System.IndexOutOfRangeException();
            }
        }
    
        public void Reset()
        {
            _constraint0 = float3.zero;
            _constraint1 = float3.zero;
            _constraint2 = float3.zero;
            _constraint3 = float3.zero;
            _hitCount0 = 0;
            _hitCount1 = 0;
            _hitCount2 = 0;
            _hitCount3 = 0;
        }
    }
}