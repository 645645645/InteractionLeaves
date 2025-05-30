using System;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Properties;
using UnityEngine.Jobs;
using UnityEngine.Rendering;

namespace UnityEngine.PBD
{
    public class InteractionOfLeavesManager : CreateSingleton<InteractionOfLeavesManager>
    {
        public enum PhysicsUpdateMode
        {
            RealTimeFrameRate,
            FixedTimeFrameRate,
        }
        
        const MeshUpdateFlags UPDATE_FLAGS_SILENT =
            MeshUpdateFlags.DontValidateIndices |
            MeshUpdateFlags.DontNotifyMeshUsers |
            MeshUpdateFlags.DontRecalculateBounds |
            MeshUpdateFlags.DontResetBoneBounds;
        
        static readonly VertexAttributeDescriptor[] MESH_BUFFER_PARAMS = new []
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 2, 1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float16, 2, 2),
        };

        public int targetFrameRate = -1;

        public PhysicsUpdateMode physicsUpdateMode = PhysicsUpdateMode.RealTimeFrameRate;
        
        public ProductLeavesPropertiesData m_propertiesData;

        private ProductNativeData m_nativeData;

#if UNITY_EDITOR
        [DontCreateProperty,Space(20)] 
        public PBDDebugDrawer debugDrawer;
#endif
        
        private float m_deltaTime;
        private float m_productNumSumByFrame = 0;
        private int m_checkIndex = 0;
        private int m_quadCount = 0;
        private int m_updateCount;
        private int m_staticCount;
        private int m_extDynamicCount;
        private int m_extPostDynamicCount;
        private int m_renderCount;
        private int m_quadBatchCount;
        private int m_updateQuadBatchCount;
        private int m_staticQuadBatchCount;
        private int m_extDynamicBatchCount;
        private int m_extPostDynamicBatchCount;
        
        private Mesh mesh;

        private JobHandle _lastJobHandle = default;
        
        
        
        //=========================================================================================
        /// <summary>
        /// Reload Domain 対策
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Init()
        {
            InitMember();
        }
        

        protected override void InitSingleton()
        {
            Initial();
        }

        private void OnDisable()
        {
            Dispose();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                _clearHashMapJobHandle.Complete();
                _lastJobHandle.Complete();

                debugDrawer.DrawDebugGizmos(m_updateCount, ref m_nativeData.m_UpdateList);
                debugDrawer.DrawDebugGizmosAll(m_quadCount);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Bounds area = m_propertiesData.Area;

            float3
                center = area.center + transform.position,
                extend = area.extents;

            float3
                ex1 = transform.TransformVector(new float3(-1, -1, -1) * extend),
                ex2 = transform.TransformVector(new float3(-1, 1, -1) * extend),
                ex3 = transform.TransformVector(new float3(1, 1, -1) * extend),
                ex4 = transform.TransformVector(new float3(1, -1, -1) * extend);

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

        private void OnValidate()
        {
            if (Application.isPlaying)
            {
                _lastJobHandle.Complete();
                ReflushParams();
            }

            ref var propertiesData = ref m_propertiesData;

            switch (propertiesData.indexFormat)
            {
                case IndexFormat.UInt16:
                    propertiesData.MaxLeavesCount = Mathf.Min(propertiesData.MaxLeavesCount, ushort.MaxValue / 6);
                    break;
                case IndexFormat.UInt32:
                    propertiesData.MaxLeavesCount = (int)Mathf.Min(propertiesData.MaxLeavesCount, uint.MaxValue / 6);
                    break;
            }
        }
#endif
        
        void ReflushParams()
        {
            m_propertiesData?.InitProductParams(ref m_nativeData.productParams);
        }
        
        void Initial()
        {
            Application.targetFrameRate = targetFrameRate;
            ref var nativeData = ref m_nativeData;
            ref var properties = ref m_propertiesData;
            
            if(!nativeData.Initialize(properties))
                return;
            
            var maxLeavesCount = properties.MaxLeavesCount;
            var verticesCount = properties.VerticesCount;
            // var trianglesCount = properties.TrianglesCount;

            switch (properties.indexFormat)
            {
                case IndexFormat.UInt16:
                    var fillUvAndTranglesUShort = new FillTrianglesJobUShort()
                    {
                        triangles = nativeData.trianglesArrayUShort,
                    };
                    _lastJobHandle = fillUvAndTranglesUShort.ScheduleByRef(maxLeavesCount, (maxLeavesCount + 1) / 2, _lastJobHandle);
                    break;
                case IndexFormat.UInt32:
                    var fillUvAndTranglesUInt = new FillTrianglesJobUInt()
                    {
                        triangles = nativeData.trianglesArrayUInt,
                    };
                    _lastJobHandle = fillUvAndTranglesUInt.ScheduleByRef(maxLeavesCount, (maxLeavesCount + 1) / 2, _lastJobHandle);
                    break;
            }

            ReflushParams();

            mesh = new Mesh()
            {
                name = "leavesRoot",
            };
            GetComponent<MeshFilter>().mesh = mesh;
            GetComponent<MeshRenderer>().enabled = true;
            mesh.SetVertexBufferParams(verticesCount, MESH_BUFFER_PARAMS);
            
#if UNITY_EDITOR
            debugDrawer.Init(verticesCount,
                in nativeData.m_predictedPositions,
                in nativeData.m_normals,
                in nativeData.m_isNeedUpdates);
#endif
            // ReflushMesh();
            
            IndexFormat format = properties.indexFormat;
            int trianglesCount = maxLeavesCount * 6;

            mesh.SetIndexBufferParams(trianglesCount, format);

            _lastJobHandle.Complete();

            switch (format)
            {
                case IndexFormat.UInt16:
                    mesh.SetIndexBufferData(nativeData.trianglesArrayUShort, 0, 0, trianglesCount, UPDATE_FLAGS_SILENT);
                    break;
                case IndexFormat.UInt32:
                    mesh.SetIndexBufferData(nativeData.trianglesArrayUInt, 0, 0, trianglesCount, UPDATE_FLAGS_SILENT);
                    break;
            }
        }
        
        //----缓存队列
        public void RegistRigibody<T>(in T pbdCollider) where T : PBDColliderBase
        {
            m_nativeData.AddCollider(in pbdCollider);
        }

        public void UnRegistRigibody<T>(in T pbdCollider) where T : PBDColliderBase
        {
            m_nativeData.RemoveCollider(in pbdCollider);
        }

        public void UpdateColliderInfo<T>(in T pbdCollider) where T : PBDColliderBase
        {
            m_nativeData.UpdateCollider(in pbdCollider);
        }

        public void UpdateSceneBounds()
        {
            m_UpdateScenceBounds = true;
        }

        private bool m_UpdateScenceBounds = false;


        public void RegistForce<T>(in T force) where T : PBDForce
        {
            m_nativeData.AddForce(force);
        }

        public void UnRegistForce<T>(in T force) where T : PBDForce
        {
            m_nativeData.RemoveForce(force);
        }

        public void UpdateForceInfo<T>(in T force) where T : PBDForce
        {
            m_nativeData.UpdateForce(force);
        }
        
        
        //-----------
        
        void Dispose()
        {
            _lastJobHandle.Complete();
            _clearHashMapJobHandle.Complete();
            _clearHashMapJobHandleSPH.Complete();
            m_nativeData.Dispose();
            Destroy(mesh);

#if UNITY_EDITOR
            debugDrawer.Dispose();
#endif
        }

        private bool m_quadHasFull = false;

        private void ReflushMesh()
        {
            if (m_quadCount < 1) return;
            ref var properties = ref m_propertiesData;
            ref var nativeData = ref m_nativeData;

            Vector3 size = properties.Area.size;
            
            IndexFormat format = properties.indexFormat;

            if (properties.IsFrustumCullingOn && m_renderCount > 0)
            {
                int vertexCount = m_renderCount * properties.VerticesLimit;
                int trianglesCount = m_renderCount * 6;

                // mesh.SetVertexBufferData(nativeData.verticesForRendering, 0, 0, vertexCount, 0, UPDATE_FLAGS_SILENT);
                // mesh.SetVertexBufferData(nativeData.normalForRendering, 0, 0, vertexCount, 1, UPDATE_FLAGS_SILENT);
                mesh.SetVertexBufferData(nativeData.verticesArray, 0, 0, vertexCount, 0, UPDATE_FLAGS_SILENT);
                mesh.SetVertexBufferData(nativeData.normalArray, 0, 0, vertexCount, 1, UPDATE_FLAGS_SILENT);

                mesh.SetVertexBufferData(nativeData.uvsForRendering, 0, 0, vertexCount, 2, UPDATE_FLAGS_SILENT);

                var subMesh = new SubMeshDescriptor(0, trianglesCount, MeshTopology.Triangles);
                mesh.SetSubMesh(0, subMesh, UPDATE_FLAGS_SILENT);
                
                subMesh.bounds = new Bounds(transform.position + Vector3.down * size.y, 4 * size);
                mesh.bounds = subMesh.bounds;

            }
            else
            {
                int vertexCount = m_quadCount * properties.VerticesLimit;
                int trianglesCount = m_quadCount * 6;

                mesh.SetVertexBufferData(nativeData.verticesArray, 0, 0, vertexCount, 0, UPDATE_FLAGS_SILENT);
                mesh.SetVertexBufferData(nativeData.normalArray, 0, 0, vertexCount, 1, UPDATE_FLAGS_SILENT);

                if (!m_quadHasFull)
                {
                    mesh.SetVertexBufferData(nativeData.meshUVsArray, 0, 0, vertexCount, 2, UPDATE_FLAGS_SILENT);

                    var subMesh = new SubMeshDescriptor(0, trianglesCount, MeshTopology.Triangles);
                    mesh.SetSubMesh(0, subMesh, UPDATE_FLAGS_SILENT);
                
                    subMesh.bounds = new Bounds(transform.position + Vector3.down * size.y, 4 * size);
                    mesh.bounds = subMesh.bounds;

                    if (m_quadCount == properties.MaxLeavesCount)
                    {
                        m_quadHasFull = true;
                    }
                }
            }
        }

        JobHandle CreatNewQuad(JobHandle dep, float deltaTime)
        {
            ref var properties = ref m_propertiesData; 
            ref var nativeData = ref m_nativeData;

            m_productNumSumByFrame += deltaTime * properties.productNumPerSecond;
            if (m_productNumSumByFrame > 1)
            {
                var creatJob = new CreatQuadMeshDataAppendJob()
                {
                    distanceConstraints = nativeData.m_distanceConstraints,
                    bendConstraints = nativeData.m_bendConstraints,
                    PredictedPositions = nativeData.m_predictedPositions,
                    Velocities = nativeData.m_velocities,
                    Normals = nativeData.m_normals,
                    Radius = nativeData.m_radius,
                    InvMasses = nativeData.m_invmasses,
                    QuadInvMasses = nativeData.m_quadInvMass,
                    Areas = nativeData.m_areas,
                    IsNeedUpdates = nativeData.m_isNeedUpdates,
                    vertices = nativeData.verticesArray,
                    normal = nativeData.normalArray,
                    uvs = nativeData.meshUVsArray,
                    skinParams = nativeData.skinSplitParams.AsReadOnly(),
                    productMinMax = nativeData.productParams.AsReadOnly(),
                    local2World = transform.localToWorldMatrix,
                    offset = m_checkIndex,
                    Radius2Rigibody = properties.R_Particle2RigiRadius,
                };
                //每帧生成新的
                int curFrameProductNum = (int)m_productNumSumByFrame;
                m_checkIndex += curFrameProductNum;
                m_checkIndex %= properties.MaxLeavesCount;
                m_productNumSumByFrame -= curFrameProductNum;
                m_quadCount += curFrameProductNum;
                m_quadCount = Mathf.Min(properties.MaxLeavesCount, m_quadCount);
                
                return  creatJob.ScheduleByRef(curFrameProductNum, Mathf.Max((curFrameProductNum + 1) / 2, 1), dep);
            }

            return dep;
        }

        JobHandle PrepareClear(JobHandle dep)
        {
            ref var nativeData = ref m_nativeData;
            
            var clearClearParticleCollisionConstraintJob = new ClearParticleCollisionConstraint()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                ParticleCollisionConstraints = nativeData.m_particleCollisionConstraints,
            };

            var clearRigiCollisionConstraintJob = new ClearRigiCollisionConstraint()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                RigiCollisionConstraints = nativeData.m_rigiCollisionConstraints,
            };
            
            var clearParticleJobHandle = clearClearParticleCollisionConstraintJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);

            var clearRigiJobHandle = clearRigiCollisionConstraintJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            
            
            var combine = JobHandle.CombineDependencies(clearParticleJobHandle, clearRigiJobHandle);
#if UNITY_EDITOR
            if (debugDrawer.bDrawDebug)
            {
                var clearDebugHitJob = new ClearDebugHitInfo
                {
                    UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                    DebugArray = debugDrawer.__HitArray,
                };
                var clearDebugHitJobHandle = clearDebugHitJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                return JobHandle.CombineDependencies(combine, clearDebugHitJobHandle);
            }
            return combine;
#else

            return combine;
#endif
        }

        JobHandle PrepareReadCollider(JobHandle dep)
        {
            ref var nativeData = ref m_nativeData;
            
            var readColliderJob = new ReadRigibodyColliderTransformJob()
            {
                collider = nativeData.m_rigibodyColliders,
            };
            
            dep = readColliderJob.ScheduleReadOnlyByRef(nativeData.m_rigibodyColliderTrasnforms, m_propertiesData.DesignJobThreadNum, dep);

            if (m_UpdateScenceBounds)
            {
                m_UpdateScenceBounds = false;

                var updateSceneBounds = new UpdateSenceBounds()
                {
                    Colliders = nativeData.m_rigibodyColliders.AsParallelReader(),
                    SceneBounds = nativeData.m_sceneBounds,
                };
                dep = updateSceneBounds.ScheduleByRef(dep);
            }

            
            return dep;
        }

        JobHandle PreparReadForce(JobHandle dep, float deltaTime)
        {
            ref var nativeData = ref m_nativeData;

            var readPreForceTrasnformJob = new ReadForceTransformJob()
            {
                ForceFields = nativeData.m_preForceFields,
                DeltaTime = deltaTime,
            };       
            
            var readForceTrasnformJob = new ReadForceTransformJob()
            {
                ForceFields = nativeData.m_forceFields,
                DeltaTime = deltaTime,
            };      
            
            var readPostForceTrasnformJob = new ReadForceTransformJob()
            {
                ForceFields = nativeData.m_postForceFields,
                DeltaTime = deltaTime,
            };
            
            var readPreForceJobHandle = readPreForceTrasnformJob.ScheduleByRef(nativeData.m_preForceTransforms, dep);
            var readForceJobHandle = readForceTrasnformJob.ScheduleByRef(nativeData.m_forceTransforms, dep);
            var readPostForceJobHandle = readPostForceTrasnformJob.ScheduleByRef(nativeData.m_postForceTransforms, dep);

            return JobHandle.CombineDependencies(readPreForceJobHandle, readForceJobHandle, readPostForceJobHandle);
        }

        JobHandle InitExtDistanceBending(JobHandle dep, float deltaTime)
        {
            ref var properties = ref m_propertiesData;
            ref var nativeData = ref m_nativeData;

            var initJob = new InitializeJob()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                Positions = nativeData.m_positions,
                PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
            };

            var distanceJob = new DistanceConstraintJob()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions,
                InvMasses = nativeData.m_invmasses.AsReadOnly(),
                DistanceConstraints = nativeData.m_distanceConstraints.AsReadOnly(),
                DisContraintIndexes = nativeData.m_disContraintIndexes.AsReadOnly(),
                // IsNeedUpdates = m_isNeedUpdates,
                ComppressStiffness = properties.m_CompressStiffness,
                StretchStiffness = properties.m_StretchStiffness,
            };

            var bendJob = new BendConstraintJob()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions,
                InvMasses = nativeData.m_invmasses.AsReadOnly(),
                BendConstraints = nativeData.m_bendConstraints.AsReadOnly(),
                // IsNeedUpdates = m_isNeedUpdates,
                BendStiffness = properties.m_BendStiffness,

#if UNITY_EDITOR
                DebugArray = debugDrawer.__BendDirArray,
#endif
            };

            dep = initJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            if (properties.extForceFilter)
            {
                var extPreDynamicForceJob = new ExtPreDynamicForceJob()
                {
                    UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                    Velocities = nativeData.m_velocities.AsReadOnly(),
                    Normals = nativeData.m_normals.AsReadOnly(),
                    Areas = nativeData.m_areas.AsReadOnly(),
                    ExtForces = nativeData.m_extForces,
                    wind = properties.m_WindFroce,
                };

                var extDynamicForeceJob = new ExtDynamicForceJob()
                {
                    ExtForceList = nativeData.m_ExtDynamicForceList.AsParallelReader(),
                    PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                    Velocities = nativeData.m_velocities.AsReadOnly(),
                    ForceFields = nativeData.m_forceFields.AsParallelReader(),
                    ExtForces = nativeData.m_extForces,
                };

                var extVelocityJob = new ExtVelocityJob()
                {
                    UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                    Velocities = nativeData.m_velocities,
                    InvMasses = nativeData.m_invmasses.AsReadOnly(),
                    ExtForces = nativeData.m_extForces.AsReadOnly(),
                    gravity = properties.m_Gravity,
                    damping = properties.m_Damping,
                    deltaTime = deltaTime,
                };

                var extPostDynamicForceJob = new ExtPostDynamicForceJob()
                {
                    ExtForceList = nativeData.m_ExtPostDynamicForceList.AsParallelReader(),
                    PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                    Velocities = nativeData.m_velocities,
                    PostForceFields = nativeData.m_postForceFields.AsParallelReader(),
                };

                var extPredectedUpdateJob = new ExtPredictedUpdateJob()
                {
                    UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                    PredictedPositions = nativeData.m_predictedPositions,
                    Velocities = nativeData.m_velocities.AsReadOnly(),
                    deltaTime = deltaTime,
                };

                dep = extPreDynamicForceJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                dep = extDynamicForeceJob.ScheduleByRef(m_extDynamicCount, m_extDynamicBatchCount, dep);
                dep = extVelocityJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                dep = extPostDynamicForceJob.ScheduleByRef(m_extPostDynamicCount, m_extPostDynamicBatchCount, dep);
                dep = extPredectedUpdateJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            }
            else
            {
                var extJob = new ExtForceJob()
                {
                    UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                    PredictedPositions = nativeData.m_predictedPositions,
                    Velocities = nativeData.m_velocities,
                    Normals = nativeData.m_normals.AsReadOnly(),
                    InvMasses = nativeData.m_invmasses.AsReadOnly(),
                    Areas = nativeData.m_areas.AsReadOnly(),
                    // PreForceFields = nativeData.m_preForceFields.AsParallelReader(),
                    ForceFields = nativeData.m_forceFields.AsParallelReader(),
                    PostForceFields = nativeData.m_postForceFields.AsParallelReader(),
                    damping = properties.m_Damping,
                    wind = properties.m_WindFroce,
                    gravity = properties.m_Gravity,
                    deltaTime = deltaTime,
                };

                dep = extJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            }

            dep = distanceJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);

            dep = bendJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);

            return dep;
        }

        JobHandle Particle2ParticleCollision(JobHandle dep)
        {
            ref var properties = ref m_propertiesData; 
            ref var nativeData = ref m_nativeData;
            
            bool byQuad = properties.collisionByQuad;
            
            var addDeltaJob = new AddParticleCollisionConstraintToPosition()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions,
                ParticleCollisionConstraints = nativeData.m_particleCollisionConstraints.AsReadOnly(),
            };

            var caculateQuadVelocityJob = new ReCaculateQuadVelocityJob()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                Velocities = nativeData.m_velocities.AsReadOnly(),
                QuadVelocities = nativeData.m_quadVelocity,
            };

            var caculateQuadPosJob = new ReCaculateQuadPosition()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                QuadPredictedPositions = nativeData.m_quadPredictedPosition,
            };
            var clearHashNeighboursJob = new ClearHashNeighbours()
            {
                hashMap = m_nativeData.m_collisionNeighours,
            };

            switch (properties.particlesCollisionMode)
            {
                case ParticlesCollisionMode.GridBasedHash:
            
                    // nativeData.m_collisionNeighours.Clear();//to job
                    
                    var buildCollisionHashJob = new BuildHashNeighbours()
                    {
                        UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                        PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                        QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                        hashMap = nativeData.m_collisionNeighours.AsParallelWriter(),
                        neighborOffsets = nativeData.m_neighborOffsets.AsReadOnly(),
                        filterParams = properties.P_FilterParams,
                        cellRadius = properties.P_CellRadius,
                        collisionByQuad = byQuad,
                    };

                    var p2pcollisionJob = new InterParticlesCollisions()
                    {
                        UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                        PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                        InvMasses = nativeData.m_invmasses.AsReadOnly(),
                        QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                        QuadInvMasses = nativeData.m_quadInvMass.AsReadOnly(),
                        ParticleCollisionConstraints = nativeData.m_particleCollisionConstraints,
                        hashMap = nativeData.m_collisionNeighours,
                        filterParams = properties.P_FilterParams,
                        radius = properties.P_ParticlesRadius,
                        cellRadius = properties.P_CellRadius,
                        CollisionStiffness = properties.m_CollisionStiffness,
                        collisionByQuad = byQuad,
                    };
                    
                    if (byQuad)
                        dep = caculateQuadPosJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                    
                    dep = buildCollisionHashJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                    dep = p2pcollisionJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                    
                    _clearHashMapJobHandle = clearHashNeighboursJob.ScheduleByRef(dep);
                    
                    dep = addDeltaJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                    break;
                
                case ParticlesCollisionMode.CompactHash:
                    var buildHashArrayJob = new CalculateHashesJob()
                    {
                        UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                        PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                        QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                        ParticleHashes = nativeData.m_particleHashes.AsParallelWriter(),
                        filterParams = properties.P_FilterParams,
                        cellRadius = properties.P_CellRadius,
                        collisionByQuad = byQuad,
                    };
                    var sortJob = nativeData.m_particleHashes.SortJob();
                    var buildHashRangeJob = new CaculateParticleHashRangesJob()
                    {
                        SortedHashes = nativeData.m_particleHashes.AsParallelReader(),
                        hashRanges = nativeData.m_particleHashRanges.AsParallelWriter(),
                    };
                    var p2pcollisionSPHJob = new SPHOptimizedCollisionDetectionJob()
                    {
                        UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                        PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                        Velocities = nativeData.m_velocities.AsReadOnly(),
                        InvMasses = nativeData.m_invmasses.AsReadOnly(),
                        QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                        QuadVelocities = nativeData.m_quadVelocity.AsReadOnly(),
                        QuadInvMasses = nativeData.m_quadInvMass.AsReadOnly(),
                        ParticleCollisionConstraints = nativeData.m_particleCollisionConstraints,
                        SortedHashes = nativeData.m_particleHashes.AsParallelReader(),
                        hashRanges = nativeData.m_particleHashRanges,
                        neighborOffsets = nativeData.m_neighborOffsets.AsReadOnly(),
                        filterParams = properties.P_FilterParams,
                        radius = properties.P_ParticlesRadius,
                        cellRadius = properties.P_CellRadius,
                        CollisionStiffness = properties.m_CollisionStiffness,
                        collisionByQuad = byQuad,
                    };
                    var clearHash = new ClearParticleHashJob()
                    {
                        ParticleHashes = nativeData.m_particleHashes,
                        hashRanges = nativeData.m_particleHashRanges,
                    };
                    
                    JobHandle caculateQuadPosJobHandle = dep,
                        caculateQuadVelocityJobHandle = dep;
                    if (byQuad)
                    {
                        caculateQuadPosJobHandle = caculateQuadPosJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, caculateQuadPosJobHandle);
                        caculateQuadVelocityJobHandle = caculateQuadVelocityJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, caculateQuadVelocityJobHandle);
                    }
                    
                    
                    if (byQuad)
                        dep = caculateQuadPosJobHandle;

                    dep = buildHashArrayJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                    dep = sortJob.Schedule(dep);
                    dep = buildHashRangeJob.ScheduleByRef(dep);
                    
                    if(byQuad)
                        dep = JobHandle.CombineDependencies(dep, caculateQuadVelocityJobHandle);
                    var particleCollisionJobHandle = p2pcollisionSPHJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                    var addeltaJobHandle = addDeltaJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, particleCollisionJobHandle);
                    dep = addeltaJobHandle;
                    
                    _clearHashMapJobHandleSPH = clearHash.ScheduleByRef(particleCollisionJobHandle);
                    break;
            }
            return dep;
        }

        JobHandle Particle2RigibodyCollision(JobHandle dep)
        {
            ref var properties = ref m_propertiesData;
            ref var nativeData = ref m_nativeData;
            
            var addDeltaByDivi = new AddRigiCollisionConstraintToPositionByDivi()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions,
                RigiCollisionConstraints = nativeData.m_rigiCollisionConstraints.AsReadOnly(),
                Velocities = nativeData.m_velocities,
                IsNeedUpdates = nativeData.m_isNeedUpdates,
                Threshold = properties.StaticVelocityThreshold,
            };

            var caculateQuadPosJob = new ReCaculateQuadPosition()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                QuadPredictedPositions = nativeData.m_quadPredictedPosition,
            };

            var p2RigiCollisionJob = new RigibodyCollisionJob()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                Colliders = nativeData.m_rigibodyColliders.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                Radius = nativeData.m_radius.AsReadOnly(),
                Velocities = nativeData.m_velocities.AsReadOnly(),
                QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                RigiCollisionConstraints = nativeData.m_rigiCollisionConstraints,
                SceneBounds = nativeData.m_sceneBounds.AsReadOnly(),
                QuadRadius = properties.R_QuadRadius,
                Elasticity = properties.m_Elasticity,
                Friction = properties.m_Friction,
#if UNITY_EDITOR
                DebugArray = debugDrawer.__HitArray,
#endif
            };
            dep = caculateQuadPosJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            dep = p2RigiCollisionJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            dep = addDeltaByDivi.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            return dep;
        }

        JobHandle UpdateDataCombine(JobHandle dep, float deltaTime)
        {
            ref var nativeData = ref m_nativeData;
            ref var properties = ref m_propertiesData;
            
            var updateVelocityJob = new UpdateVelocityJob()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                Positions = nativeData.m_positions.AsReadOnly(),
                Velocities = nativeData.m_velocities,
                InvDeltaTime = 1f / deltaTime,
            };
            var updateNormalAreaJob = new UpdateNormalAreaJob()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                Normals = nativeData.m_normals,
                Areas = nativeData.m_areas,
            };
            
            var updateNormalAreaJobHandle = updateNormalAreaJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
            
            var updateVelocityJobHandle = updateVelocityJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);

            if (properties.IsFrustumCullingOn)
            {
                
                var caculateInFrustumJob = new CaculateInFrustumJob
                {
                    PredictedPositions = nativeData.m_predictedPositions,
                    IsNeedRenders = nativeData.m_isNeedRenders,
                    CullingMatrix = properties.focusCamera.cullingMatrix,
                };
                var collectInFrustumJob = new CollectInFrustumJob()
                {
                    IsNeedRenders = nativeData.m_isNeedRenders.AsReadOnly(),
                    RenderList = nativeData.m_RenderList,
                    RenderCounter = nativeData.m_RenderCounter,
                    QuadCount = m_quadCount,
                };

                int threadNum = properties.DesignJobThreadNum;
                
                var updateMeshPosJob = new UpdateFrustumMeshPosJob()
                {
                    RenderList = nativeData.m_RenderList,
                    PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                    // pos = nativeData.verticesForRendering,
                    pos = nativeData.verticesArray,
                    JobNum = threadNum,
                };

                var updateMeshNormalJob = new UpdateFrustumMeshNormalJob()
                {
                    RenderList = nativeData.m_RenderList,
                    Normals = nativeData.m_normals.AsReadOnly(),
                    // normal = nativeData.normalForRendering,
                    normal = nativeData.normalArray,
                    JobNum = threadNum,
                };

                var updateMeshUvJob = new UpdateFrustumMeshUVJob()
                {
                    RenderList = nativeData.m_RenderList,
                    uvs = nativeData.meshUVsArray.AsReadOnly(),
                    uvsForRendering = nativeData.uvsForRendering,
                    JobNum = threadNum,
                };
                
                var caculateInFrustumHandle = caculateInFrustumJob.ScheduleByRef(m_quadCount, m_quadBatchCount, dep);
                
                var collectRenderHandle = collectInFrustumJob.ScheduleByRef(caculateInFrustumHandle);
                
                var canUpdateMeshNormal = JobHandle.CombineDependencies(collectRenderHandle, updateNormalAreaJobHandle);

                //多IJob模拟IJobParallelFor
                for (int i = 0; i < threadNum; i++)
                {
                    updateMeshPosJob.JobID = i;
                    nativeData.updateMeshPosJobHandles.Add(updateMeshPosJob.ScheduleByRef(collectRenderHandle));

                    updateMeshUvJob.JobID = i;
                    nativeData.updateMeshUvJobHandles.Add(updateMeshUvJob.ScheduleByRef(collectRenderHandle));

                    updateMeshNormalJob.JobID = i;
                    nativeData.updateMeshNormalJobHandles.Add(updateMeshNormalJob.ScheduleByRef(canUpdateMeshNormal));
                }
                var updateMeshPosJobHandle = JobHandle.CombineDependencies(nativeData.updateMeshPosJobHandles);
                var updateMeshNormalJobHandle = JobHandle.CombineDependencies(nativeData.updateMeshNormalJobHandles);
                var updateMeshUvJobHandle = JobHandle.CombineDependencies(nativeData.updateMeshUvJobHandles);
                
                var combine =  JobHandle.CombineDependencies(updateMeshPosJobHandle, updateMeshNormalJobHandle,updateMeshUvJobHandle );

                return JobHandle.CombineDependencies(updateVelocityJobHandle, combine);
            }
            else
            {
                var updateMeshPosJob = new UpdateMeshPosJob()
                {
                    UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                    PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                    pos = nativeData.verticesArray,
                };

                var updateMeshNormalJob = new UpdateMeshNormalJob()
                {
                    UpdateList = nativeData.m_UpdateList.AsParallelReader(),
                    Normals = nativeData.m_normals.AsReadOnly(),
                    normal = nativeData.normalArray,
                };
            
                var updateMeshPosJobHandle = updateMeshPosJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, dep);
                var updateMeshNormalJobHandle = updateMeshNormalJob.ScheduleByRef(m_updateCount, m_updateQuadBatchCount, updateNormalAreaJobHandle);
                
                return JobHandle.CombineDependencies(updateMeshPosJobHandle, updateMeshNormalJobHandle, updateVelocityJobHandle);
            }
        }


        //臃肿 不想改了
        JobHandle SimulateEndClearAndCollect(JobHandle dep, float deltaTime)
        {
            ref var nativeData = ref m_nativeData;
            ref var propertiesData = ref m_propertiesData;
            
            //end for clear and collect
            
            var clearUpdateListJob = new ClearListJob()
            {
                List = nativeData.m_UpdateList,
            };            
            
            var collectNeedUpdateJob = new CollectNeedUpdateQuadIndex()
            {
                UpdateList = nativeData.m_UpdateList.AsParallelWriter(),
                IsNeedUpdates = nativeData.m_isNeedUpdates.AsReadOnly(),
                Length = m_quadCount,
            };


            JobHandle 
                clearRenderListHandle = default,
                clearJobHandle = default;
            if (m_propertiesData.IsFrustumCullingOn)
            {
                var clearRenderListJob = new ClearListJob()
                {
                    List = nativeData.m_RenderList,
                };
                
                clearRenderListHandle = clearRenderListJob.ScheduleByRef(dep);

                var clearJobhandleJob1 = new ClearJobHandleListJob()
                {
                    List = nativeData.updateMeshPosJobHandles,
                }; 
                var clearJobhandleJob2 = new ClearJobHandleListJob()
                {
                    List = nativeData.updateMeshNormalJobHandles,
                }; 
                var clearJobhandleJob3 = new ClearJobHandleListJob()
                {
                    List = nativeData.updateMeshUvJobHandles,
                };
                var clearJobHandleJobHandle1 = clearJobhandleJob1.ScheduleByRef(dep);
                var clearJobHandleJobHandle2 = clearJobhandleJob2.ScheduleByRef(dep);
                var clearJobHandleJobHandle3 = clearJobhandleJob3.ScheduleByRef(dep);
                clearJobHandle = JobHandle.CombineDependencies(clearJobHandleJobHandle1,clearJobHandleJobHandle2,clearJobHandleJobHandle3);
            }

            if (propertiesData.extForceFilter)
            {
                var clearExtDynamicListJob = new ClearListJob()
                {
                    List = nativeData.m_ExtDynamicForceList,
                };
            
                var clearExtPostDynamicListJob = new ClearListJob()
                {
                    List = nativeData.m_ExtPostDynamicForceList,
                };
            
                var extDynamicSetUpdateJob = new ExtDynamicForceSetUpdateJob()
                {
                    PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                    QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                    ForceFields = nativeData.m_forceFields.AsParallelReader(),
                    IsNeedUpdates = nativeData.m_isNeedUpdates,
                    ExtDynamicForceList = nativeData.m_ExtDynamicForceList.AsParallelWriter(),
                    QuadCount = m_quadCount,
                    ByQuad = m_propertiesData.collisionByQuad,
                };
            
                var extPostDynamicSetUpdateJob = new ExtPostDynamicForceSetUpdateJob()
                {
                    PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                    QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                    PostForceFields = nativeData.m_postForceFields.AsParallelReader(),
                    ExtPostDynamicForceList = nativeData.m_ExtPostDynamicForceList.AsParallelWriter(),
                    QuadCount = m_quadCount,
                    ByQuad = m_propertiesData.collisionByQuad,
                };
                
                var clearExtDynamicListJobHandle = clearExtDynamicListJob.ScheduleByRef(dep);
                var clearExtPostDynamicListJobHandle = clearExtPostDynamicListJob.ScheduleByRef(dep);

                var clearUpdateListHandle = clearUpdateListJob.ScheduleByRef(dep);
            
                var creatNewQuadJobHandle = CreatNewQuad(dep, deltaTime);// <----------Creat

                var canSetupExtJobHanled = JobHandle.CombineDependencies(clearExtDynamicListJobHandle, clearExtPostDynamicListJobHandle, creatNewQuadJobHandle);
            
                var extSetUpdateJobHandle = extDynamicSetUpdateJob.ScheduleByRef(canSetupExtJobHanled);

                var extPostSetUpdateJobJobHandle = extPostDynamicSetUpdateJob.ScheduleByRef(canSetupExtJobHanled);

                var canCollectUpdateListJobHandle = JobHandle.CombineDependencies(clearUpdateListHandle, extSetUpdateJobHandle, clearRenderListHandle);
            
                var collectNeedUpdateJobHandle = collectNeedUpdateJob.ScheduleByRef(canCollectUpdateListJobHandle);
            
                dep = JobHandle.CombineDependencies(collectNeedUpdateJobHandle, clearJobHandle, extPostSetUpdateJobJobHandle);
            }
            else
            {
            
                var extSetUpdateJob = new ExtForceSetUpdateJob()
                {
                    StaticList = nativeData.m_StaticList.AsParallelReader(),
                    PredictedPositions = nativeData.m_predictedPositions.AsReadOnly(),
                    QuadPredictedPositions = nativeData.m_quadPredictedPosition.AsReadOnly(),
                    ForceFields = nativeData.m_forceFields.AsParallelReader(),
                    // PostForceFields = nativeData.m_postForceFields.AsParallelReader(),
                    IsNeedUpdates = nativeData.m_isNeedUpdates,
                    ByQuad = m_propertiesData.collisionByQuad,
                };
            
                var clearStaticListJob = new ClearListJob()
                {
                    List = nativeData.m_StaticList,
                };

                var collectStaticJob = new CollectStaticQuadIndex()
                {
                    StaticList = nativeData.m_StaticList.AsParallelWriter(),
                    IsNeedUpdates = nativeData.m_isNeedUpdates.AsReadOnly(),
                    Length = m_quadCount,
                };
                
                var clearUpdateListHandle = clearUpdateListJob.ScheduleByRef(dep);
            
                var creatNewQuadJobHandle = CreatNewQuad(dep, deltaTime);// <----------Creat
            
                var extSetUpdateJobHandle = extSetUpdateJob.ScheduleByRef(m_staticCount, m_staticQuadBatchCount, creatNewQuadJobHandle);
                var clearStaticListHandle = clearStaticListJob.ScheduleByRef(extSetUpdateJobHandle);
                var collectStaticJobHandle = collectStaticJob.ScheduleByRef(clearStaticListHandle);

                var canCollectUpdateListJobHandle = JobHandle.CombineDependencies(clearUpdateListHandle, extSetUpdateJobHandle, clearRenderListHandle);
            
                var collectNeedUpdateJobHandle = collectNeedUpdateJob.ScheduleByRef(canCollectUpdateListJobHandle);
            
                dep = JobHandle.CombineDependencies(collectStaticJobHandle, collectNeedUpdateJobHandle, clearJobHandle);
            }

            return dep;
        }
        
        public void AfterEarlyUpdate()
        {
        }

        private void FixedUpdate()
        {
            if (physicsUpdateMode == PhysicsUpdateMode.FixedTimeFrameRate)
            {
                m_deltaTime = Time.fixedDeltaTime;
                SchedulePhysics(m_deltaTime);
            }
            
        }

        public void AfterFixedUpdate()
        {
        }

        public void AfterUpdate()
        {
        }

        public void BeforeLateUpdate()
        {
        }

        public void AfterLateUpdate()
        {
        }

        public void PostLateUpdate()
        {
        }

        private float m_Accumulator;
        public void AfterRendering()
        {
            switch (physicsUpdateMode)
            {
                case PhysicsUpdateMode.RealTimeFrameRate:
                    m_deltaTime = Time.deltaTime;
                    SchedulePhysics(m_deltaTime);
                    break;
            }
        }

        void SchedulePhysics(float deltaTime)
        {
            _lastJobHandle.Complete();
            //-------主线程可以读写job数据的空间


            ref var properties = ref m_propertiesData;
            ref var nativeData = ref m_nativeData;

            int threadNum = m_propertiesData.DesignJobThreadNum;

            m_quadBatchCount = (m_quadCount + 1) / threadNum;

            m_updateCount = nativeData.m_UpdateList.Length;
            m_updateQuadBatchCount = (m_updateCount + 1) / threadNum;

            if (properties.frustumCulling)
                m_renderCount = nativeData.m_RenderCounter[0];
            // m_renderCount = nativeData.m_RenderList.Length;

            if (properties.extForceFilter)
            {
                m_extDynamicCount = nativeData.m_ExtDynamicForceList.Length;
                m_extPostDynamicBatchCount = (m_extDynamicCount + 1) / threadNum;

                m_extPostDynamicCount = nativeData.m_ExtPostDynamicForceList.Length;
                m_extPostDynamicBatchCount = (m_extPostDynamicCount + 1) / threadNum;
            }
            else
            {
                m_staticCount = nativeData.m_StaticList.Length;
                m_staticQuadBatchCount = (m_staticCount + 1) / threadNum;
            }

            nativeData.ProcessRigiBodyQueue();
            nativeData.ProcessForceQueue();
            ReflushMesh();

            //------------
            ScheduleAllJob(deltaTime);
        }

        void ScheduleAllJob(float deltaTime)
        {
            ref var nativeData = ref m_nativeData;

            if (m_updateCount > 0)
            {
                var clearRenderCounter = new ClearNativeArrayJob()
                {
                    NativeArray = nativeData.m_RenderCounter,
                };;
                
                var clearRenderCounterJobHandle = clearRenderCounter.ScheduleByRef(1, 1, _lastJobHandle);
                
                var initExtDistanceBending = InitExtDistanceBending(_lastJobHandle, deltaTime);
                var prepareReadHandle = PrepareReadCollider(_lastJobHandle);
                
                var prepareClearJobHandle = PrepareClear(_lastJobHandle);

                _lastJobHandle = JobHandle.CombineDependencies(initExtDistanceBending, prepareReadHandle, prepareClearJobHandle);

                var prepareReadForceJobHandle = PreparReadForce(_lastJobHandle, deltaTime);//延迟一帧不生效 不care

                _lastJobHandle = JobHandle.CombineDependencies(clearRenderCounterJobHandle, _clearHashMapJobHandle, prepareReadForceJobHandle);
                
                
                _lastJobHandle = Particle2ParticleCollision(_lastJobHandle);

                _lastJobHandle = Particle2RigibodyCollision(_lastJobHandle);

                _lastJobHandle = UpdateDataCombine(_lastJobHandle, deltaTime);
            }

            _lastJobHandle = SimulateEndClearAndCollect(_lastJobHandle, deltaTime);
            
            //用了sortJob必须过一次compelete
            _lastJobHandle = JobHandle.CombineDependencies(_clearHashMapJobHandleSPH, _lastJobHandle);

            JobHandle.ScheduleBatchedJobs();
        }

        JobHandle _clearHashMapJobHandle = default;//这个clear耗时巨大
        JobHandle _clearHashMapJobHandleSPH = default;
    }
}