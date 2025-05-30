using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using Unity.Mathematics;
using Unity.Jobs;
using UnityEngine.Jobs;
using UnityEngine.Rendering;


/*
 仿Niagara Ribbon
 demo 不考虑运行时修改
 API参考
 https://github.com/Unity-Technologies/MeshApiExamples/blob/e82668f26fbf615d3ee4b1a5941c8fe167d3d485/Assets/NoiseBall/NoiseBall.cs
*/


public class TrailProductor : MonoBehaviour
{
    struct MoveData
    {
        public double timeStamp;
        public float3 postion;
        public quaternion rotation;

        public MoveData(float3 pos, quaternion rot, double time)
        {
            postion = pos;
            rotation = rot;
            timeStamp = time;
        }
    }

    struct CrossSectionData
    {
        public quaternion localRotation;
        public float3 localPostion;
        public float3 scale;
    }

    private const int DesignJobThreadNum = 2;

    // public bool useTrail;
    public bool uvMirrorForHullMesh; //针对船体，两边uv对称
    public Transform focus;
    public Transform[] crossSection;

    public Material _trailMaterial;

    public int maxSampleCount = 50;
    [Range(0f, 1f)] public float widthAtten = 0.5f;
    [Range(0.01f, 5)] public float duration = 1;
    private Mesh _mesh;
    private MeshFilter _meshFilter;
    private MeshRenderer _meshRenderer;
    private NativeArray<float3> _meshVertices; //随机并行不能依赖其他点计算位置
    private NativeArray<float3> _meshNormals; //存运动方向

    private TransformAccessArray _head;
    private TransformAccessArray _trailRootAccess;
    private NativeArray<CrossSectionData> _crossSectionData;

    private NativeQueue<MoveData> movePointQueue;

    private NativeList<MoveData> movePointList;
    private NativeList<float> arcLengths;
    private NativeArray<MoveData> reducePoint;

    private int vertexCount, crossSectionCount;
    private int CrossSectionBatchCount;

    private JobHandle _lastJobHandle;

    //实例化到世界空间下，回避层级转换
    private GameObject _trailRoot;

    private void OnEnable()
    {
        if (focus == null)
            return;
        Initial();
    }

    private double _lastTime;
    private Vector3 _lastPoint;
    private Quaternion _lastRotation;

    private const float SampleMinSpacing = 0.05f;
    private const float SampleMinRotMigration = 1 - 0.01f;
    private const double SampleMinTimeInterval = 1 / 60d;

    private void Update()
    {
        if (focus == null)
            return;
        double currentTime = Time.timeAsDouble;
        Vector3 currentPos = focus.position;
        Quaternion currentRotation = focus.rotation;
        //下面这坨也能job化
        PruneTrailPoints(currentTime);

        Vector3 move = currentPos - _lastPoint;
        float moveLength = Vector3.Magnitude(move);
        double deltaTime = currentTime - _lastTime;

        if (moveLength > SampleMinSpacing ||
            Quaternion.Dot(currentRotation, _lastRotation) < SampleMinRotMigration ||
            deltaTime > SampleMinTimeInterval)
        {
            //帧时/位移过大 split插值, 过小则1
            int split = Mathf.Max(1, (int)(deltaTime * 66), (int)(moveLength * 10));

            AddStepQueue(currentPos, currentRotation, currentTime, split);

            UpdateMeshData();
            _lastTime = currentTime;
            _lastPoint = currentPos;
            _lastRotation = currentRotation;

            _needFlushMeshData = true;
        }
    }

    private bool _needFlushMeshData = false;

    private void LateUpdate()
    {
        if (focus == null)
            return;
        // if (useTrail)
        {
            ReflushMesh();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 front = Vector3.zero;
        for (int i = 0; i < crossSection.Length; i++)
        {
            if (crossSection[i] == null)
                continue;
            var a = crossSection[i].position;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(a, 0.01f);
            if (i > 0)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(front, a);
            }

            front = a;
        }
    }

    private void Initial()
    {
        CrossSectionBatchCount = (maxSampleCount + 1) / DesignJobThreadNum;
        _lastTime = Time.timeAsDouble;
        _lastPoint = focus.position;
        _lastRotation = focus.rotation;
        _trailRoot = new GameObject("ProductorTrailRoot");
        // _trailRoot.hideFlags = HideFlags.NotEditable | HideFlags.DontSave;
        _trailRoot.hideFlags = HideFlags.HideAndDontSave;
        _mesh = new Mesh()
        {
            name = "ProductorTrail"
        };
        _meshFilter = _trailRoot.gameObject.AddComponent<MeshFilter>();
        _meshFilter.mesh = _mesh;
        _meshRenderer = _trailRoot.gameObject.AddComponent<MeshRenderer>();
        _meshRenderer.material = _trailMaterial; //只有一个主刀光带扰动

        crossSectionCount = crossSection.Length;

        //关背面剔除
        vertexCount = crossSectionCount * maxSampleCount;

        int xSegments = crossSectionCount - 1;
        int ySegments = maxSampleCount - 1;
        int triangleCount = xSegments * ySegments * 6;

        movePointQueue = new NativeQueue<MoveData>(Allocator.Persistent);
        movePointList = new NativeList<MoveData>(1000, Allocator.Persistent);
        arcLengths = new NativeList<float>(1000, Allocator.Persistent);
        reducePoint = new NativeArray<MoveData>(maxSampleCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

        _head = new TransformAccessArray(crossSection, 2);
        _trailRootAccess = new TransformAccessArray(1, 1);
        _trailRootAccess.Add(_trailRoot.transform);
        _crossSectionData = new NativeArray<CrossSectionData>(crossSectionCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _meshVertices = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        _meshNormals = new NativeArray<float3>(vertexCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
        var meshUVs = new NativeArray<float2>(vertexCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
        var meshTriangles = new NativeArray<int>(triangleCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);

        float[] crossSectionUV_u = GetCrossSectionU(crossSectionCount);


        for (int i = 0; i < crossSectionCount; i++)
        {
            var p = crossSection[i];
            //不考虑运行时修改这部分 初始化一次
            _crossSectionData[i] = new CrossSectionData()
            {
                localPostion = focus.InverseTransformPoint(p.position),
                scale = p.lossyScale,
            };
        }


        for (int y = 0; y <= ySegments; y++)
        {
            for (int x = 0; x <= xSegments; x++)
            {
                float3 pos = crossSection[x].position;
                int vertexIndex = y * crossSectionCount + x;
                _meshVertices[vertexIndex] = pos;
                _meshNormals[vertexIndex] = float3.zero;

                float v = y / (float)ySegments;
                float2 uv = new float2(crossSectionUV_u[x], v /* v */);
                meshUVs[vertexIndex] = uv;
            }
        }

        //trianglesIndex verticesIndex
        for (int ti = 0, vi = 0, y = 0; y < ySegments; y++, vi++)
        {
            for (int x = 0; x < xSegments; x++, vi++, ti += 6)
            {
                meshTriangles[ti] = vi;
                meshTriangles[ti + 1] = vi + xSegments + 1;
                meshTriangles[ti + 2] = vi + 1;

                meshTriangles[ti + 3] = vi + 1;
                meshTriangles[ti + 4] = vi + xSegments + 1;
                meshTriangles[ti + 5] = vi + xSegments + 2;
            }
        }

        var vertexAttributeDescriptor = new[]
        {
            new VertexAttributeDescriptor(VertexAttribute.Position, VertexAttributeFormat.Float32, 3, 0),
            new VertexAttributeDescriptor(VertexAttribute.Normal, VertexAttributeFormat.Float32, 3, 1),
            new VertexAttributeDescriptor(VertexAttribute.TexCoord0, VertexAttributeFormat.Float32, 2, 2),
        };
        _mesh.SetVertexBufferParams(vertexCount, vertexAttributeDescriptor);
        _mesh.SetVertexBufferData(_meshVertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
        _mesh.SetVertexBufferData(_meshNormals, 0, 0, vertexCount, 1, MeshUpdateFlags.DontRecalculateBounds);
        _mesh.SetVertexBufferData(meshUVs, 0, 0, vertexCount, 2, MeshUpdateFlags.DontRecalculateBounds);
        _mesh.SetIndexBufferParams(triangleCount, IndexFormat.UInt32);
        _mesh.SetIndexBufferData(meshTriangles, 0, 0, triangleCount, MeshUpdateFlags.DontRecalculateBounds | MeshUpdateFlags.DontValidateIndices);

        var subMesh = new SubMeshDescriptor(0, triangleCount, MeshTopology.Triangles);
        subMesh.bounds = new Bounds(Vector3.zero, new Vector3(10, 10, 10));
        _mesh.SetSubMesh(0, subMesh, MeshUpdateFlags.DontRecalculateBounds);
        _mesh.bounds = subMesh.bounds;
        // _mesh.RecalculateBounds();

        meshTriangles.Dispose();
        meshUVs.Dispose();
    }

    float[] GetCrossSectionU(int csCount)
    {
        float[] crossSectionUVU = new float[csCount];
        float lineTotalLength = 0;
        float[] lineLength = new float[csCount];
        for (int i = 1; i < csCount; i++)
        {
            float3 dir = crossSection[i].position - crossSection[i - 1].position;
            lineTotalLength += MathematicsUtil.Length(dir);
            lineLength[i] = lineTotalLength;
        }

        if (lineTotalLength < 1e-5f)
        {
            Debug.LogError("???");
        }

        for (int i = 0; i < csCount; i++)
        {
            float u = math.clamp(lineLength[i] / lineTotalLength, 0, 1);
            //偶数对称不适合。
            if (uvMirrorForHullMesh)
                u = math.abs(math.mad(u, 2, -1));
            crossSectionUVU[i] = u;
        }

        return crossSectionUVU;
    }

    private void PruneTrailPoints(double time)
    {
        if (!movePointQueue.IsCreated)
            return;
        while (movePointQueue.Count > 0)
        {
            var oldest = movePointQueue.Peek();
            if (time - oldest.timeStamp > duration)
            {
                movePointQueue.Dequeue();
                continue;
            }

            if (movePointQueue.Count > 1020)
            {
                movePointQueue.Dequeue();
                continue;
            }

            break;
        }
    }

    void AddStepQueue(Vector3 pos, Quaternion rot, double time, int split)
    {
        if (!movePointQueue.IsCreated)
            return;

        float step = math.rcp(split);
        for (int i = split - 1; i >= 0; i--)
        {
            float len = step * i;
            var move = new MoveData()
            {
                timeStamp = math.lerp(time, _lastTime, len),
                postion = Vector3.Lerp(pos, _lastPoint, len),
                rotation = Quaternion.Slerp(rot, _lastRotation, len),
            };
            movePointQueue.Enqueue(move);
        }
    }

    void UpdateMeshData()
    {
        if (!movePointQueue.IsCreated || movePointQueue.Count < 2)
            return;

        var updateTrailRootPos = new UpdateRootWorldPos()
        {
            FocusWorldPos = _lastPoint,
        };

        var prepareReduce = new PrepareReduce()
        {
            InputPos = movePointQueue,
            MovePosList = movePointList,
            ArcLengths = arcLengths,
        };

        var reducePathLine = new ReducePathLine()
        {
            MovePosLerp = reducePoint,
            MovePosList = movePointList,
            ArcLengths = arcLengths,
        };

        var setVertices = new SetMeshVertices()
        {
            Vertices = _meshVertices,
            Normals = _meshNormals,
            HeadData = _crossSectionData,
            MovePosLerp = reducePoint,
            WidthAtten = widthAtten,
        };

#if UNITY_2020_1_OR_NEWER

        var updateTrailRootPosJobHandle = updateTrailRootPos.ScheduleByRef(_trailRootAccess, _lastJobHandle);

        //1
        _lastJobHandle = prepareReduce.ScheduleByRef(_lastJobHandle);

        //2
        _lastJobHandle = reducePathLine.ScheduleByRef(maxSampleCount, CrossSectionBatchCount, _lastJobHandle);

        //3
        _lastJobHandle = setVertices.ScheduleByRef(maxSampleCount, CrossSectionBatchCount, _lastJobHandle);

#else
        var updateTrailRootPosJobHandle = updateTrailRootPos.Schedule(_trailRootAccess, _lastJobHandle);

        //1
        _lastJobHandle = prepareReduce.Schedule(_lastJobHandle);

        //2
        _lastJobHandle = reducePathLine.Schedule(maxSampleCount, CrossSectionBatchCount, _lastJobHandle);

        //3
        _lastJobHandle = setVertices.Schedule(maxSampleCount, CrossSectionBatchCount, _lastJobHandle);
#endif

        _lastJobHandle = JobHandle.CombineDependencies(updateTrailRootPosJobHandle, _lastJobHandle);

        JobHandle.ScheduleBatchedJobs();
    }

    void ReflushMesh()
    {
        if (_needFlushMeshData)
        {
            _lastJobHandle.Complete();
            _mesh.SetVertexBufferData(_meshVertices, 0, 0, vertexCount, 0, MeshUpdateFlags.DontRecalculateBounds);
            _mesh.SetVertexBufferData(_meshNormals, 0, 0, vertexCount, 1, MeshUpdateFlags.DontRecalculateBounds);
            // _mesh.RecalculateBounds();
            _needFlushMeshData = false;
        }
    }

    void Dispose()
    {
        _lastJobHandle.Complete();
        _lastJobHandle = default;
        if (_meshVertices.IsCreated) _meshVertices.Dispose();
        if (_meshNormals.IsCreated) _meshNormals.Dispose();
        if (_head.isCreated) _head.Dispose();
        if (_trailRootAccess.isCreated) _trailRootAccess.Dispose();
        if (_crossSectionData.IsCreated) _crossSectionData.Dispose();
        if (movePointQueue.IsCreated) movePointQueue.Dispose();
        if (movePointList.IsCreated) movePointList.Dispose();
        if (arcLengths.IsCreated) arcLengths.Dispose();
        if (reducePoint.IsCreated) reducePoint.Dispose();
        Destroy(_mesh);
        Destroy(_trailRoot);
    }

    private void OnDisable()
    {
        Dispose();
    }

    [BurstCompile]
    private struct UpdateRootWorldPos : IJobParallelForTransform
    {
        [ReadOnly] public float3 FocusWorldPos;

        public void Execute(int index, TransformAccess transform)
        {
            transform.position = FocusWorldPos;
        }
    }


    [BurstCompile]
    private struct SetMeshVertices : IJobParallelFor
    {
        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float3> Vertices;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<float3> Normals;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<CrossSectionData> HeadData;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<MoveData> MovePosLerp;

        [ReadOnly] public float WidthAtten;

        public void Execute(int index)
        {
            bool isHead = index == 0; //mesh head
            int last = MovePosLerp.Length - 1; //pos head
            int pIndex = last - index;
            var headPos = MovePosLerp[last];

            //是的 少一个 postion 初始化0  so 别在shader里mromalize 乐
            float3 dir = isHead ? float3.zero : MovePosLerp[pIndex].postion - MovePosLerp[pIndex + 1].postion;

            var focus = MovePosLerp[pIndex];

            float uvV = index / (float)last;
            // float atten = math.lerp(0, uvV, WidthAtten);
            float atten = math.lerp(0, 1 - math.cos(uvV * math.PI * 0.5f), WidthAtten);

            int CrossSectionCount = HeadData.Length;

            for (int x = 0; x < CrossSectionCount; x++)
            {
                var head = HeadData[x];

                float4x4 local2World = float4x4.TRS(focus.postion, focus.rotation, head.scale);
                float3 wpos = math.mul(local2World, new float4(head.localPostion, 1)).xyz;

                //控制宽度
                wpos = math.lerp(wpos, focus.postion, atten);

                //给固定bounds 去掉RecalculateBounds
                //加了个job更新root.pos跟随focus，这里减掉head，
                //多mesh可以job算bounds
                wpos -= headPos.postion;
                int vertecesIndex = math.mad(index, CrossSectionCount, x);
                Vertices[vertecesIndex] = wpos;
                Normals[vertecesIndex] = dir;
            }
        }
    }

    [BurstCompile]
    private struct ReducePathLine : IJobParallelFor
    {
        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<MoveData> MovePosLerp;

        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeList<MoveData> MovePosList;

        //可以用重建轨迹的切线方向生成旋转替代
        //那样就不用每帧收集旋转,转移到job线程每帧重建整个路径的旋转
        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeList<float> ArcLengths;

        public void Execute(int index)
        {
            int splitMaxIndex = MovePosLerp.Length - 1;
            float totalLength = ArcLengths[ArcLengths.Length - 1];
            float splitLength = totalLength / splitMaxIndex;

            float targetArcLength = splitLength * index;
            int pIndex = FindSegmentIndexBinarySearch(targetArcLength);
            MovePosLerp[index] = CalculateInterpolatedPosition(pIndex, targetArcLength);
        }

        //不均匀分布适合二分
        private int FindSegmentIndexBinarySearch(float targetLength)
        {
            int low = 0, high = ArcLengths.Length - 1;
            while (low < high)
            {
                int mid = low + (high - low) / 2;
                if (ArcLengths[mid] < targetLength)
                    low = mid + 1;
                else
                    high = mid;
            }

            return math.clamp(low, 1, ArcLengths.Length - 2);
        }

        private MoveData CalculateInterpolatedPosition(int index, float targetLength)
        {
            int p0 = math.max(index - 2, 0);
            int p1 = math.max(index - 1, 0);
            int p2 = math.min(index, ArcLengths.Length - 1);
            int p3 = math.min(index + 1, ArcLengths.Length - 1);

            float segmentStart = ArcLengths[p1];
            float segmentEnd = ArcLengths[p2];
            float t = (targetLength - segmentStart) / (segmentEnd - segmentStart);
            t = math.clamp(t, 0, 1);

            var pos = OptimizedCatmullRom(
                MovePosList[p0].postion,
                MovePosList[p1].postion,
                MovePosList[p2].postion,
                MovePosList[p3].postion,
                t
            );

            var rot = MathematicsUtil.QuaternionSlerpUnclamped(MovePosList[p1].rotation, MovePosList[p2].rotation, t);

            return new MoveData(pos, rot, 0);
        }

        private static float3 OptimizedCatmullRom(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float t2 = t * t;
            float3 a = 2.0f * p1;
            float3 b = p2 - p0;
            float3 c = 2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3;
            float3 d = p3 - 3.0f * p2 + 3.0f * p1 - p0;

            return 0.5f * (a + (b * t) + (c * t2) + (d * t * t2));
        }
    }

    [BurstCompile]
    private struct PrepareReduce : IJob
    {
        [NativeDisableParallelForRestriction] public NativeQueue<MoveData> InputPos;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeList<MoveData> MovePosList; //不用queue，优化这个copy

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeList<float> ArcLengths;

        public void Execute()
        {
            MovePosList.Clear();
            ArcLengths.Clear();
            float totalLength = 0;
            int posCount = InputPos.Count;
            int maxIndex = posCount - 1;
            var frontPos = InputPos.Peek();
            for (int step = 0; step <= maxIndex; step++)
            {
                var ponit = InputPos.Dequeue();
                var move = ponit.postion - frontPos.postion;
                float delta = MathematicsUtil.Length(move);
                totalLength += delta;
                ArcLengths.Add(totalLength);
                MovePosList.Add(ponit);
                InputPos.Enqueue(ponit);
                frontPos = ponit;
            }
        }
    }
}