using System;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;

namespace UnityEngine.PBD
{
#if UNITY_EDITOR
    [Serializable]
    public struct PBDDebugDrawer
    {
        public bool vertices;
        public bool normal;
        public bool bend;
        public bool hitNormal;
        public bool hitConcatDelta;
        public bool hitConcatNormal;

        private NativeSlice<PredictedPositions> __DebugPostition;
        private NativeSlice<Normal> __DebugQuadNormal;
        private NativeArray<float3> __DebugBendDir;
        private NativeArray<PBDCollisionHit> __DebugCollisionHit;

        private NativeSlice<IsNeedUpdate> __DebugIsUpdate;
        public bool bDrawDebug => vertices ||
                                   normal ||
                                   bend ||
                                   hitNormal ||
                                   hitConcatDelta ||
                                   hitConcatNormal;

        public NativeArray<float3> __BendDirArray => __DebugBendDir;

        public NativeArray<PBDCollisionHit> __HitArray => __DebugCollisionHit;
        
        public void Init(int vertexCount,
            in NativeArray<PredictedPositions> positions,
            in NativeArray<Normal> normals,
            in NativeArray<IsNeedUpdate> isNeedUpdates)
        {
            __DebugBendDir = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            __DebugCollisionHit = new NativeArray<PBDCollisionHit>(vertexCount, Allocator.Persistent);

            __DebugPostition = positions.Slice();
            __DebugQuadNormal = normals.Slice();
            __DebugIsUpdate = isNeedUpdates.Slice();
        }
        public void Dispose()
        {
            if (__DebugBendDir.IsCreated) __DebugBendDir.Dispose();
            if (__DebugCollisionHit.IsCreated) __DebugCollisionHit.Dispose();
        }

        public void DrawDebugGizmos(int quadCount, ref NativeList<int> updateList)
        {
            if (bDrawDebug)
            {
                for (int i = 0; i < updateList.Length; i++)
                {
                    var index = updateList[i];
                    var isUpdate = __DebugIsUpdate[index].Value;
                    var start = index * 4;
                    for (int j = 0; j < 4; j++)
                    {
                        int pIndex = j + start;
                        var pos = __DebugPostition[pIndex].Value;
                        // if (vertices)
                        // {
                        //     Gizmos.color = Color.yellow;
                        //     Gizmos.DrawWireSphere(pos, 0.01f);
                        // }
                        //
                        // if (normal)
                        // {
                        //     Gizmos.color = Color.blue;
                        //     var normal = __DebugQuadNormal[pIndex].Value;
                        //     Gizmos.DrawLine(pos, pos + normal * 0.1f);
                        // }

                        if (bend && isUpdate)
                        {
                            Gizmos.color = Color.cyan;
                            var bendDir = __DebugBendDir[pIndex];
                            Gizmos.DrawLine(pos, pos + bendDir * 10);
                        }
                        
                        var hit = __DebugCollisionHit[pIndex];
                        
                        if (hitNormal && isUpdate && hit.isHit)
                        {
                            Gizmos.color = Color.green;
                            Gizmos.DrawLine(hit.hitSurfacePos, hit.hitSurfacePos + hit.hitNormal * 0.1f);
                        }
                        
                        if (hitConcatDelta && isUpdate && hit.isHit)
                        {
                            Gizmos.color = Color.red;
                            var delta = hit.hitConcatDelta;
                            Gizmos.DrawLine(pos, pos + delta * 1f);
                        }
                        
                        if (hitConcatNormal && isUpdate && hit.isHit)
                        {
                            Gizmos.color = Color.magenta;
                            // var center = hit.hitActorCenter;
                            var concatNormal = hit.hitConcatNormal;
                            // Gizmos.DrawLine(center, center + concatNormal * 1f);
                            Gizmos.DrawLine(pos, pos + concatNormal * 0.1f);
                        }
                    }
                }
            }
        }

        public void DrawDebugGizmosAll(int quadCount)
        {
            if (bDrawDebug)
            {
                for (int i = 0; i < quadCount; i++)
                {
                    var start = i * 4;
                    // var isUpdate = __DebugIsUpdate[i].Value;
                    for (int j = start; j < start + 4; j++)
                    {
                        var pos = __DebugPostition[j].Value;
                        if (vertices)
                        {
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawWireSphere(pos, 0.01f);
                        }
            
                        if (normal)
                        {
                            Gizmos.color = Color.blue;
                            var normal = __DebugQuadNormal[j].Value;
                            Gizmos.DrawLine(pos, pos + normal * 0.1f);
                        }
            
                        // if (bend && isUpdate)
                        // {
                        //     Gizmos.color = Color.cyan;
                        //     var bendDir = __DebugBendDir[j];
                        //     Gizmos.DrawLine(pos, pos + bendDir * 100);
                        // }
                        //
                        // var hit = __DebugCollisionHit[j];
                        //
                        // if (hitNormal && isUpdate && hit.isHit)
                        // {
                        //     Gizmos.color = Color.green;
                        //     Gizmos.DrawLine(hit.hitSurfacePos, hit.hitSurfacePos + hit.hitNormal * 0.1f);
                        // }
                        //
                        // if (hitConcatDelta && isUpdate && hit.isHit)
                        // {
                        //     Gizmos.color = Color.red;
                        //     var delta = hit.hitConcatDelta;
                        //     Gizmos.DrawLine(pos, pos + delta * 100f);
                        // }
                        //
                        // if (hitConcatNormal && isUpdate && hit.isHit)
                        // {
                        //     Gizmos.color = Color.magenta;
                        //     // var center = hit.hitActorCenter;
                        //     var concatNormal = hit.hitConcatNormal;
                        //     // Gizmos.DrawLine(center, center + concatNormal * 1f);
                        //     Gizmos.DrawLine(pos, pos + concatNormal * 0.1f);
                        // }
                    }
                }
                
            }
        }
    }
#endif
}