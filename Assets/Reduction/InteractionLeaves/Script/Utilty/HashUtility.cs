using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace UnityEngine.PBD
{
    public static class HashUtility
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int HashCoords(int3 grid, int numCells)
        {
            var h = (grid.x * 92837111) ^ (grid.y * 689287499) ^ (grid.z * 283923481);
            return Mathf.Abs(h) % numCells;
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Hash(float3 v, float cellSize, int numCells)
        {
            return GridToHash(PosToGrid(v, cellSize), numCells);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Hash(float2 v, float cellSize, int numCells)
        {
            return GridToHash(PosToGrid(v, cellSize), numCells);
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int3 PosToGrid(float3 pos, float cellSize)
        {
            return new int3(math.floor(pos / cellSize));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int2 PosToGrid(float2 v, float cellSize)
        {
            return new int2(math.floor(v / cellSize));
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GridToHash(int3 grid, int numCells)
        {
            unchecked
            {
                // Simple int3 hash based on a pseudo mix of :
                // 1) https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
                // 2) https://en.wikipedia.org/wiki/Jenkins_hash_function
                int hash = grid.x;
                hash = (hash * 397) ^ grid.y;
                hash = (hash * 397) ^ grid.z;
                hash += hash << 3;
                hash ^= hash >> 11;
                hash += hash << 15;
                return hash % numCells;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GridToHash(int2 grid, int numCells)
        {
            unchecked
            {
                // Simple int3 hash based on a pseudo mix of :
                // 1) https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
                // 2) https://en.wikipedia.org/wiki/Jenkins_hash_function
                int hash = grid.x;
                hash = (hash * 397) ^ grid.y;
                hash += hash << 3;
                hash ^= hash >> 11;
                hash += hash << 15;
                return hash;
            }
        }
        
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong Hash(ulong hash, ulong key)
        {
            const ulong m = 0xc6a4a7935bd1e995UL;
            const int r = 47;

            ulong h = hash;
            ulong k = key;

            k *= m;
            k ^= k >> r;
            k *= m;

            h ^= k;
            h *= m;

            h ^= h >> r;
            h *= m;
            h ^= h >> r;

            return h;
        }
        
        //小物体
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateAABBCellHashesFast(float3 min, float3 max, float cellSize, int numCells, NativeList<int> outHashes)
        {
            int3 minGrid = PosToGrid(min, cellSize);
            int3 maxGrid = PosToGrid(max, cellSize);
        
            for (int x = minGrid.x; x <= maxGrid.x; x++)
            {
                for (int y = minGrid.y; y <= maxGrid.y; y++)
                {
                    for (int z = minGrid.z; z <= maxGrid.z; z++)
                    {
                        int3 gridPos = new int3(x, y, z);
                        int hash = GridToHash(gridPos, numCells);
                        outHashes.Add(hash);
                    }
                }
            }
        }

        //大物体
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void CalculateAABBCellHashes(float3 min, float3 max, float cellSize, int numCells, NativeList<int> outHashes, NativeList<float3>.ParallelWriter pos)
        {
            int3 voxelCount = new int3(
                math.max(1, (int)math.ceil((max.x - min.x - cellSize) / cellSize)),
                math.max(1, (int)math.ceil((max.y - min.y - cellSize) / cellSize)),
                math.max(1, (int)math.ceil((max.z - min.z - cellSize) / cellSize))
            );
        
            float3 voxelSize = (max - min) / new float3(voxelCount);
        
            // 为每个体素计算哈希值
            for (int x = 0; x <= voxelCount.x; x++)
            {
                for (int y = 0; y <= voxelCount.y; y++)
                {
                    for (int z = 0; z <= voxelCount.z; z++)
                    {
                        int3 gridPos = new int3(x, y, z);
                        float3 voxelCenter = gridPos * voxelSize + min;
                        int hash = GridToHash(gridPos, numCells);
                        outHashes.Add(hash);
                        pos.AddNoResize(voxelCenter);
                    }
                }
            }
        }
    }
}