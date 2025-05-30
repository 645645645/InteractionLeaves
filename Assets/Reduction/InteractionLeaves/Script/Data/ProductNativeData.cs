using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace UnityEngine.PBD
{
    public struct ProductNativeData
    {
        // struct ForceQueueOrder : IEquatable<ForceQueueOrder>
        // {
        //     public readonly PBDForceApplicationOrder Order;
        //     public int Index;       //整体的
        //     public int OrderIndex;  //分类中的
        //     private readonly int _hash;
        //
        //     public ForceQueueOrder(PBDForceApplicationOrder order, int hash, int index = -1, int orderIndex = -1)
        //     {
        //         Order = order;
        //         Index = index;
        //         OrderIndex = orderIndex;
        //         _hash = hash;
        //     }
        //
        //     public bool Equals(ForceQueueOrder other)
        //     {
        //         return _hash.Equals(other._hash);
        //     }
        // }
        
        //runtime
        public NativeArray<uint> trianglesArrayUInt;
        public NativeArray<ushort> trianglesArrayUShort;
        public NativeArray<float3> verticesArray;
        public NativeArray<float2> normalArray;
        public NativeArray<half2> meshUVsArray;
        public NativeArray<float3> verticesForRendering;
        public NativeArray<float2> normalForRendering;
        public NativeArray<half2> uvsForRendering;

        public NativeArray<Position> m_positions;
        public NativeArray<PredictedPositions> m_predictedPositions;
        public NativeArray<Velocity> m_velocities;
        public NativeArray<Normal> m_normals;
        public NativeArray<Radius> m_radius;
        public NativeArray<InvMass> m_invmasses;
        public NativeArray<Area> m_areas;
        public NativeArray<ExtForce> m_extForces;
        public NativeArray<ParticleCollisionConstraint> m_particleCollisionConstraints;
        public NativeArray<RigiCollisionConstraint> m_rigiCollisionConstraints;
        public NativeArray<IsNeedUpdate> m_isNeedUpdates;
        public NativeArray<IsNeedRender> m_isNeedRenders;
        public NativeArray<QuadPredictedPositions> m_quadPredictedPosition;
        public NativeArray<QuadVelocity> m_quadVelocity;
        public NativeArray<QuadInvMass> m_quadInvMass;
        public NativeList<int> m_UpdateList;
        public NativeList<int> m_StaticList;
        public NativeList<int> m_ExtDynamicForceList;
        public NativeList<int> m_ExtPostDynamicForceList;
        public NativeList<int> m_RenderList;
        public NativeArray<int> m_RenderCounter;
        
        public NativeList<JobHandle> updateMeshPosJobHandles;
        public NativeList<JobHandle> updateMeshUvJobHandles;
        public NativeList<JobHandle> updateMeshNormalJobHandles;
        
        //
        public NativeArray<int2> m_disContraintIndexes;
        public NativeArray<DistanceConstraint> m_distanceConstraints;
        public NativeArray<BendConstraint> m_bendConstraints;
        public NativeMultiHashMap<int, int> m_collisionNeighours;
        public NativeList<ParticleHash> m_particleHashes;
        public NativeHashMap<int, HashRange> m_particleHashRanges;
        public NativeArray<int3> m_neighborOffsets;

        public NativeArray<PBDBounds> m_sceneBounds;
        public NativeList<PBDCustomColliderInfo> m_rigibodyColliders;
        public TransformAccessArray m_rigibodyColliderTrasnforms;
        
        private NativeList<int> m_colliderIDsList;
        private Queue<PBDColliderBase> m_rigiAddQueue;
        private Queue<PBDColliderBase> m_rigiRemoveQueue;
        private Queue<PBDColliderBase> m_rigiUpdateQueue;

        public NativeList<PBDForceField> m_preForceFields;
        public TransformAccessArray m_preForceTransforms;
        public NativeList<PBDForceField> m_forceFields;
        public TransformAccessArray m_forceTransforms;
        public NativeList<PBDForceField> m_postForceFields;
        public TransformAccessArray m_postForceTransforms;
        // private NativeList<ForceQueueOrder> m_forceDic;
        private NativeList<int> m_preForceIDList;
        private NativeList<int> m_forceIDList;
        private NativeList<int> m_postForceIDList;
        private NativeList<int> m_allForceIDList;
        
        // private NativeList<ForceQueueOrder> m_forceTypesList;
        
        private Queue<PBDForce> m_forceAddQueue;
        private Queue<PBDForce> m_forceRemoveQueue;
        private Queue<PBDForce> m_forceUpdateQueue;
        
        
        public NativeArray<float4> skinSplitParams;
        public NativeArray<float3> productParams;

        private bool colliderListIsCreat => m_rigibodyColliders.IsCreated &&
                                            m_rigibodyColliderTrasnforms.isCreated &&
                                            m_colliderIDsList.IsCreated;

        private bool forceListIsCreat => m_forceFields.IsCreated && m_forceTransforms.isCreated &&
                                         m_preForceFields.IsCreated && m_preForceTransforms.isCreated &&
                                         m_postForceFields.IsCreated && m_postForceTransforms.isCreated &&
                                         m_preForceIDList.IsCreated && m_forceIDList.IsCreated && 
                                         m_postForceIDList.IsCreated && m_allForceIDList.IsCreated;

        public bool Initialize(in ProductLeavesPropertiesData data)
        {
            var leaveTpyeCount = data.LeavesTypeCount;
            if (leaveTpyeCount <= 0)
            {
                Debug.LogError($"LeavesTexSplitParams has a problem = {leaveTpyeCount}");
                return false;
            }
            skinSplitParams = new NativeArray<float4>(leaveTpyeCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            data.InitSkinParams(ref skinSplitParams);

            productParams = new NativeArray<float3>(5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            var maxLeavesCount = data.MaxLeavesCount;
            var verticesCount = data.VerticesCount;
            var trianglesCount = data.TrianglesCount;
            m_positions = new NativeArray<Position>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_predictedPositions = new NativeArray<PredictedPositions>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_velocities = new NativeArray<Velocity>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_normals = new NativeArray<Normal>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_radius = new NativeArray<Radius>(maxLeavesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_invmasses = new NativeArray<InvMass>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_areas = new NativeArray<Area>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_extForces = new NativeArray<ExtForce>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_particleCollisionConstraints = new NativeArray<ParticleCollisionConstraint>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_rigiCollisionConstraints = new NativeArray<RigiCollisionConstraint>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_isNeedUpdates = new NativeArray<IsNeedUpdate>(maxLeavesCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            m_isNeedRenders = new NativeArray<IsNeedRender>(maxLeavesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            m_quadPredictedPosition = new NativeArray<QuadPredictedPositions>(maxLeavesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_quadVelocity = new NativeArray<QuadVelocity>(maxLeavesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_quadInvMass = new NativeArray<QuadInvMass>(maxLeavesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            m_distanceConstraints = new NativeArray<DistanceConstraint>(maxLeavesCount * 5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_bendConstraints = new NativeArray<BendConstraint>(maxLeavesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            //girdBase
            m_collisionNeighours = new NativeMultiHashMap<int, int>(verticesCount * 27, Allocator.Persistent);
            //compactHash
            m_particleHashes = new NativeList<ParticleHash>(verticesCount, Allocator.Persistent);
            m_particleHashRanges = new NativeHashMap<int, HashRange>(verticesCount, Allocator.Persistent);

            int colliderCapacity = 128;
            m_rigibodyColliders = new NativeList<PBDCustomColliderInfo>(colliderCapacity, Allocator.Persistent);
            m_rigibodyColliderTrasnforms = new TransformAccessArray(colliderCapacity, data.DesignJobThreadNum);
            m_colliderIDsList = new NativeList<int>(colliderCapacity, Allocator.Persistent);

            m_sceneBounds = new NativeArray<PBDBounds>(1, Allocator.Persistent, NativeArrayOptions.UninitializedMemory); 
            
            m_rigiAddQueue = new Queue<PBDColliderBase>(colliderCapacity);
            m_rigiRemoveQueue = new Queue<PBDColliderBase>(colliderCapacity);
            m_rigiUpdateQueue = new Queue<PBDColliderBase>(colliderCapacity);

            int forceCapacity = 128;
            m_forceFields = new NativeList<PBDForceField>(forceCapacity, Allocator.Persistent);
            m_forceTransforms = new TransformAccessArray(forceCapacity, data.DesignJobThreadNum);
            m_preForceFields = new NativeList<PBDForceField>(forceCapacity, Allocator.Persistent);
            m_preForceTransforms = new TransformAccessArray(forceCapacity, data.DesignJobThreadNum);
            m_postForceFields = new NativeList<PBDForceField>(forceCapacity, Allocator.Persistent);
            m_postForceTransforms = new TransformAccessArray(forceCapacity, data.DesignJobThreadNum);
            m_preForceIDList = new NativeList<int>(forceCapacity, Allocator.Persistent);
            m_forceIDList = new NativeList<int>(forceCapacity, Allocator.Persistent);
            m_postForceIDList = new NativeList<int>(forceCapacity, Allocator.Persistent);
            m_allForceIDList = new NativeList<int>(forceCapacity, Allocator.Persistent);

            m_forceAddQueue = new Queue<PBDForce>(forceCapacity);
            m_forceRemoveQueue = new Queue<PBDForce>(forceCapacity);
            m_forceUpdateQueue = new Queue<PBDForce>(forceCapacity);

            InitializeNeighborOffsets();
            InitializeDisContraintIndexes();
            
            m_UpdateList = new NativeList<int>(maxLeavesCount, Allocator.Persistent);
            m_StaticList = new NativeList<int>(maxLeavesCount, Allocator.Persistent);
            m_ExtDynamicForceList = new NativeList<int>(maxLeavesCount, Allocator.Persistent);
            m_ExtPostDynamicForceList = new NativeList<int>(maxLeavesCount, Allocator.Persistent);
            m_RenderList = new NativeList<int>(maxLeavesCount, Allocator.Persistent);
            m_RenderCounter = new NativeArray<int>(1, Allocator.Persistent, NativeArrayOptions.ClearMemory);

            switch (data.indexFormat)
            {
                case IndexFormat.UInt16:
                    trianglesArrayUShort = new NativeArray<ushort>(trianglesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    break;
                case IndexFormat.UInt32:
                    trianglesArrayUInt = new NativeArray<uint>(trianglesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    break;
            }
            verticesArray = new NativeArray<float3>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            normalArray = new NativeArray<float2>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            meshUVsArray = new NativeArray<half2>(verticesCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            // verticesForRendering = new NativeArray<float3>(verticesCount, Allocator.Persistent);
            // normalForRendering = new NativeArray<float2>(verticesCount, Allocator.Persistent);
            uvsForRendering = new NativeArray<half2>(verticesCount, Allocator.Persistent);
            
            int threadNum = data.DesignJobThreadNum;
            updateMeshPosJobHandles = new NativeList<JobHandle>(threadNum, Allocator.Persistent);
            updateMeshUvJobHandles = new NativeList<JobHandle>(threadNum, Allocator.Persistent);
            updateMeshNormalJobHandles = new NativeList<JobHandle>(threadNum, Allocator.Persistent);

            return true;
        }
        
        private void InitializeNeighborOffsets()
        {
            m_neighborOffsets = new NativeArray<int3>(27, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            int index = 0;

            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        m_neighborOffsets[index++] = new int3(x, y, z);
                    }
                }
            }
        }

        private void InitializeDisContraintIndexes()
        {
            m_disContraintIndexes = new NativeArray<int2>(5, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            m_disContraintIndexes[0] = new int2(1, 0);
            m_disContraintIndexes[1] = new int2(1, 2);
            m_disContraintIndexes[2] = new int2(1, 3);
            m_disContraintIndexes[3] = new int2(2, 0);
            m_disContraintIndexes[4] = new int2(2, 3);
        }

        public void Dispose()
        {
            if (m_positions.IsCreated) m_positions.Dispose();
            if (m_predictedPositions.IsCreated) m_predictedPositions.Dispose();
            if (m_velocities.IsCreated) m_velocities.Dispose();
            if (m_normals.IsCreated) m_normals.Dispose();
            if (m_radius.IsCreated) m_radius.Dispose();
            if (m_invmasses.IsCreated) m_invmasses.Dispose();
            if (m_areas.IsCreated) m_areas.Dispose();
            if (m_extForces.IsCreated) m_extForces.Dispose();
            if (m_isNeedUpdates.IsCreated) m_isNeedUpdates.Dispose();
            if (m_isNeedRenders.IsCreated) m_isNeedRenders.Dispose();
            if (m_distanceConstraints.IsCreated) m_distanceConstraints.Dispose();
            if (m_bendConstraints.IsCreated) m_bendConstraints.Dispose();

            if (m_quadPredictedPosition.IsCreated) m_quadPredictedPosition.Dispose();
            if (m_quadVelocity.IsCreated) m_quadVelocity.Dispose();
            if (m_quadInvMass.IsCreated) m_quadInvMass.Dispose();

            if (m_collisionNeighours.IsCreated) m_collisionNeighours.Dispose();
            if (m_particleHashes.IsCreated) m_particleHashes.Dispose();
            if (m_particleHashRanges.IsCreated) m_particleHashRanges.Dispose();
            if (m_neighborOffsets.IsCreated) m_neighborOffsets.Dispose();
            if (m_disContraintIndexes.IsCreated) m_disContraintIndexes.Dispose();

            if (m_particleCollisionConstraints.IsCreated) m_particleCollisionConstraints.Dispose();
            if (m_rigiCollisionConstraints.IsCreated) m_rigiCollisionConstraints.Dispose();
            if (m_rigibodyColliders.IsCreated) m_rigibodyColliders.Dispose();
            if (m_rigibodyColliderTrasnforms.isCreated) m_rigibodyColliderTrasnforms.Dispose();
            if (m_colliderIDsList.IsCreated) m_colliderIDsList.Dispose();

            if (m_forceFields.IsCreated) m_forceFields.Dispose();
            if (m_forceTransforms.isCreated) m_forceTransforms.Dispose();
            if (m_preForceFields.IsCreated) m_preForceFields.Dispose();
            if (m_preForceTransforms.isCreated) m_preForceTransforms.Dispose();
            if (m_postForceFields.IsCreated) m_postForceFields.Dispose();
            if (m_postForceTransforms.isCreated) m_postForceTransforms.Dispose();
            if (m_preForceIDList.IsCreated) m_preForceIDList.Dispose();
            if (m_forceIDList.IsCreated) m_forceIDList.Dispose();
            if (m_postForceIDList.IsCreated) m_postForceIDList.Dispose();
            if (m_allForceIDList.IsCreated) m_allForceIDList.Dispose();

            if (m_sceneBounds.IsCreated) m_sceneBounds.Dispose();

            if (trianglesArrayUInt.IsCreated) trianglesArrayUInt.Dispose();
            if (trianglesArrayUShort.IsCreated) trianglesArrayUShort.Dispose();
            if (verticesArray.IsCreated) verticesArray.Dispose();
            if (normalArray.IsCreated) normalArray.Dispose();
            if (meshUVsArray.IsCreated) meshUVsArray.Dispose();

            if (verticesForRendering.IsCreated) verticesForRendering.Dispose();
            if (normalForRendering.IsCreated) normalForRendering.Dispose();
            if (uvsForRendering.IsCreated) uvsForRendering.Dispose();

            if (m_UpdateList.IsCreated) m_UpdateList.Dispose();
            if (m_StaticList.IsCreated) m_StaticList.Dispose();
            if (m_ExtDynamicForceList.IsCreated) m_ExtDynamicForceList.Dispose();
            if (m_ExtPostDynamicForceList.IsCreated) m_ExtPostDynamicForceList.Dispose();
            if (m_RenderList.IsCreated) m_RenderList.Dispose();
            if (m_RenderCounter.IsCreated) m_RenderCounter.Dispose();

            if (updateMeshPosJobHandles.IsCreated) updateMeshPosJobHandles.Dispose();
            if (updateMeshNormalJobHandles.IsCreated) updateMeshNormalJobHandles.Dispose();
            if (updateMeshUvJobHandles.IsCreated) updateMeshUvJobHandles.Dispose();

            if (productParams.IsCreated) productParams.Dispose();
            if (skinSplitParams.IsCreated) skinSplitParams.Dispose();
            
            m_rigiAddQueue?.Clear();
            m_rigiRemoveQueue?.Clear();
            m_rigiUpdateQueue?.Clear();

            m_forceAddQueue?.Clear();
            m_forceRemoveQueue?.Clear();
            m_forceUpdateQueue?.Clear();
        }

        //-------------------------------------------------
        public bool AddCollider<T>(in T pbdCollider) where T : PBDColliderBase
        {
            if (pbdCollider == null || !colliderListIsCreat)
                return false;

            if (m_rigiAddQueue.Contains(pbdCollider))
                return false;

            if (m_rigiRemoveQueue.Contains(pbdCollider))
                return false;
                
            
            m_rigiAddQueue?.Enqueue(pbdCollider);
                
            return true;
        }

        public bool RemoveCollider<T>(in T pbdCollider) where T : PBDColliderBase
        {
            if (pbdCollider == null || !colliderListIsCreat)
                return false;
            
            if (m_rigiRemoveQueue.Contains(pbdCollider))
                return false;

            if (m_rigiAddQueue.Contains(pbdCollider))
                return false;
            
            m_rigiRemoveQueue?.Enqueue(pbdCollider);
            return true;
        }

        public bool UpdateCollider<T>(in T pbdCollider) where T : PBDColliderBase
        {
            if (pbdCollider == null || !colliderListIsCreat)
                return false;
            
            if (m_rigiUpdateQueue.Contains(pbdCollider))
                return false;
            
            m_rigiUpdateQueue?.Enqueue(pbdCollider);
            return true;
        }

        private void ProcessAddColliderInfo()
        {
            if (m_rigiAddQueue == null)
                return;
            if (!colliderListIsCreat)
                return;
            bool bUpdate = m_rigiAddQueue.Count > 0;
            while (m_rigiAddQueue.TryDequeue(out var pbdCollider))
            {
                var isCollider = pbdCollider is PBDCustomCollider;
                if (isCollider)
                {
                    int id = pbdCollider.GetHashCode();
                    if (m_colliderIDsList.Contains(id))
                        continue;
                    var collider = pbdCollider as PBDCustomCollider;
                    m_rigibodyColliders.AddNoResize(collider.PbdCustomCollider);
                    m_rigibodyColliderTrasnforms.Add(collider.transform);
                    m_colliderIDsList.AddNoResize(id);
                }
            }
            if (bUpdate)
                InteractionOfLeavesManager.Instance?.UpdateSceneBounds();
        }

        private void ProcessRemoveColliderInfo()
        {
            if (m_rigiRemoveQueue == null)
                return;
            if (!colliderListIsCreat)
                return;
            bool bUpdate = m_rigiRemoveQueue.Count > 0;
            while (m_rigiRemoveQueue.TryDequeue(out var pbdCollider))
            {
                var isCollider = pbdCollider is PBDCustomCollider;
                if (isCollider)
                {
                    int index = m_colliderIDsList.IndexOf(pbdCollider.GetHashCode());
                    if (index < 0)
                        continue;

                    m_rigibodyColliders.RemoveAtSwapBack(index);
                    m_rigibodyColliderTrasnforms.RemoveAtSwapBack(index);
                    m_colliderIDsList.RemoveAtSwapBack(index);

                }

            }
            if (bUpdate)
                InteractionOfLeavesManager.Instance?.UpdateSceneBounds();
        }

        private void ProcessUpdateCollider()
        {
            if (m_rigiUpdateQueue == null)
                return;

            if (!colliderListIsCreat)
                return;

            bool bUpdate = m_rigiUpdateQueue.Count > 0;

            while (m_rigiUpdateQueue.TryDequeue(out var pbdCollider))
            {

                var isCollider = pbdCollider is PBDCustomCollider;
                if (isCollider)
                {
                    int index = m_colliderIDsList.IndexOf(pbdCollider.GetHashCode());
                    if (index < 0)
                        continue;
                    
                    var collider = pbdCollider as PBDCustomCollider;
                    m_rigibodyColliders[index] = collider.PbdCustomCollider;
                }

            }

            if (bUpdate)
                InteractionOfLeavesManager.Instance?.UpdateSceneBounds();
        }

        public void ProcessRigiBodyQueue()
        {
            ProcessAddColliderInfo();
            ProcessRemoveColliderInfo();
            ProcessUpdateCollider();
        }
        
        //-----------------------------------------------


        public bool AddForce<T>(in T force) where T : PBDForce
        {
            if (force == null)
                return false;

            if (m_forceAddQueue.Contains(force))
                return false;

            if (m_forceRemoveQueue.Contains(force))
                return false;

            m_forceAddQueue?.Enqueue(force);
            return true;
        }

        public bool RemoveForce<T>(in T force) where T : PBDForce
        {
            if (force == null)
                return false;
            
            if (m_forceRemoveQueue.Contains(force))
                return false;

            if (m_forceAddQueue.Contains(force))
                return false;
            
            m_forceRemoveQueue?.Enqueue(force);
            return true;
        }

        public bool UpdateForce<T>(in T force) where T : PBDForce
        {
            if (force == null)
                return false;
            
            if (m_forceUpdateQueue.Contains(force))
                return false;
            
            m_forceUpdateQueue?.Enqueue(force);
            return true;
        }

        private void ProcessAddForceInfo()
        {
            if (m_forceAddQueue == null)
                return;

            if (!forceListIsCreat)
                return;

            while (m_forceAddQueue.TryDequeue(out var force))
            {
                if (force)
                {
                    int hash = force.GetHashCode();
                    
                    if(m_allForceIDList.Contains(hash))
                        continue;

                    PBDForceApplicationOrder order = force.force.Order;

                    switch (order)
                    {
                        case PBDForceApplicationOrder.PreDynamics:
                            m_preForceIDList.Add(hash);
                            m_preForceFields.AddNoResize(force.force);
                            m_preForceTransforms.Add(force.transform);
                            break;
                        case PBDForceApplicationOrder.Dynamics:
                            m_forceIDList.Add(hash);
                            m_forceFields.AddNoResize(force.force);
                            m_forceTransforms.Add(force.transform);
                            break;
                        case PBDForceApplicationOrder.PostDynamics:
                            m_postForceIDList.Add(hash);
                            m_postForceFields.AddNoResize(force.force);
                            m_postForceTransforms.Add(force.transform);
                            break;
                    }

                    m_allForceIDList.AddNoResize(hash);
                }
            }
        }

        private void ProcessRemoveForceInfo()
        {
            if(m_forceRemoveQueue == null)
                return;
            
            if(!forceListIsCreat)
                return;

            while (m_forceRemoveQueue.TryDequeue(out var force))
            {
                int hash = force.GetHashCode();

                int index = m_allForceIDList.IndexOf(hash);
                
                if(index < 0)
                    continue;
                
                PBDForceApplicationOrder order = force.force.Order;
                
                m_allForceIDList.RemoveAtSwapBack(index);

                switch (order)
                {
                    case PBDForceApplicationOrder.PreDynamics:
                        index = m_preForceIDList.IndexOf(hash);
                        m_preForceFields.RemoveAtSwapBack(index);
                        m_preForceTransforms.RemoveAtSwapBack(index);
                        m_preForceIDList.RemoveAtSwapBack(index);
                        break;
                    case PBDForceApplicationOrder.Dynamics:
                        index = m_forceIDList.IndexOf(hash);
                        m_forceFields.RemoveAtSwapBack(index);
                        m_forceTransforms.RemoveAtSwapBack(index);
                        m_forceIDList.RemoveAtSwapBack(index);
                        break;
                    case PBDForceApplicationOrder.PostDynamics:
                        index = m_postForceIDList.IndexOf(hash);
                        m_postForceFields.RemoveAtSwapBack(index);
                        m_postForceTransforms.RemoveAtSwapBack(index);
                        m_postForceIDList.RemoveAtSwapBack(index);
                        break;
                }
            }
            
        }

        
        private void ProcessUpdateForceInfo()
        {
            if (m_forceUpdateQueue == null)
                return;
            
            if(!forceListIsCreat)
                return;
            
            //可能 只需要增删就够用了。。
            // bool bUpdate = m_forceUpdateQueue.Count > 0;

            while (m_forceUpdateQueue.TryDequeue(out var force))
            {
                int hash = force.GetHashCode();
                
                int index = m_allForceIDList.IndexOf(hash);
                
                if(index < 0)
                    continue;
                
                PBDForceApplicationOrder order = force.force.Order;
                
                switch (order)
                {
                    case PBDForceApplicationOrder.PreDynamics:
                        index = m_preForceIDList.IndexOf(hash);
                        m_preForceFields[index] = force.force;
                        break;
                    case PBDForceApplicationOrder.Dynamics:
                        index = m_forceIDList.IndexOf(hash);
                        m_forceFields[index] = force.force;
                        break;
                    case PBDForceApplicationOrder.PostDynamics:
                        index = m_postForceIDList.IndexOf(hash);
                        m_postForceFields[index] = force.force;
                        break;
                }
                
            }
            
        }

        public void ProcessForceQueue()
        {
            ProcessAddForceInfo();
            ProcessRemoveForceInfo();
            ProcessUpdateForceInfo();
        }
    }
}