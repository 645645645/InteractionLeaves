using System;
using Unity.Mathematics;

namespace UnityEngine.PBD
{
    public struct Position
    {
        public float3 Value;
    }
    
    public struct Velocity
    {
        public float3 Value;
    }
    
    public struct Normal
    {
        public float3 Value;
    }

    public struct Radius
    {
        public float Value;
    }
    
    public struct InvMass
    {
        public float Value;
    }
    
    public struct Area
    {
        public float Value;
    }

    public struct IsNeedUpdate
    {
        public bool Value;
    }
    
    public struct IsNeedRender
    {
        public bool Value;
    }
    
    public struct PredictedPositions
    {
        public float3 Value;
    }

    public struct ExtForce
    {
        public float3 Value;
    }

    public struct QuadPredictedPositions
    {
        public float3 Value;
    }
    
    public struct QuadVelocity
    {
        public float3 Value;
    }

    public struct QuadInvMass
    {
        public float Value;
    }
    
    public struct ParticleCollisionConstraint
    {
        public float3 Delta;
        // public float3 Velocity;
        public int ConstraintsCount;
    }
    
    public struct RigiCollisionConstraint
    {
        // public PBDCollisionHit LastHit;
        public float3 Delta;
        public float3 Velocity;
        public float3 Normal;
        public float InsertDepth;
        // public int RigiBodyType;
        public int ConstraintsCount;
    }
    
    public struct DistanceConstraint
    {
        // public int idA;
        // public int idB;
        public float restLength;
    }
    
    public struct BendConstraint{
        // public int index0;
        // public int index1;
        // public int index2;
        // public int index3;
        public float restAngle;
    }


    public struct PBDCollisionHit
    {
        public bool isHit;
        public float insertDepth;
        public float3 hitSurfacePos;
        // public float3 hitDelta;
        public float3 hitNormal;
        public float3 hitActorCenter;
        public float3 hitConcatNormal;
        // public float3 hitOldVelocity;
        // public float3 hitNewVelocuty;
        public float3 hitConcatDelta;
    }
    


    public struct ParticleHash : IComparable<ParticleHash>
    {
        public int Index;
        public int Hash;

        public int CompareTo(ParticleHash other)
        {
            return Hash.CompareTo(other.Hash);
        }
    }

    public struct PBDBounds
    {
        public float3 Min;
        public float3 Max;
    }
    
    public struct HashRange
    {
        public int Start;
        public int End;
    }
}