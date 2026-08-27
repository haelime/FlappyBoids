using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlappyBoids
{
    /// <summary>
    /// Builds an isosurface by connecting the twelve edge intersections of every cube.
    /// Face-centre disambiguation keeps neighbouring cubes on the same topology without
    /// relying on a giant, opaque triangle lookup table.
    /// </summary>
    public static class MarchingCubesMeshBuilder
    {
        private static readonly int[,] EdgeCorners =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
            { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
            { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
        };

        private static readonly int[,] FaceEdges =
        {
            { 0, 1, 2, 3 }, { 4, 5, 6, 7 },
            { 0, 9, 4, 8 }, { 2, 10, 6, 11 },
            { 8, 7, 11, 3 }, { 1, 10, 5, 9 }
        };

        private static readonly int[,] FaceCorners =
        {
            { 0, 1, 2, 3 }, { 4, 5, 6, 7 },
            { 0, 1, 5, 4 }, { 3, 2, 6, 7 },
            { 0, 4, 7, 3 }, { 1, 2, 6, 5 }
        };

        public static Mesh Build(
            string name,
            Bounds bounds,
            Vector3Int cells,
            Func<Vector3, float> sampleDensity)
        {
            if (sampleDensity == null) throw new ArgumentNullException(nameof(sampleDensity));
            cells.x = Mathf.Max(1, cells.x);
            cells.y = Mathf.Max(1, cells.y);
            cells.z = Mathf.Max(1, cells.z);

            Vector3 step = new Vector3(
                bounds.size.x / cells.x,
                bounds.size.y / cells.y,
                bounds.size.z / cells.z);
            Vector3 minimum = bounds.min;
            var vertices = new List<Vector3>(cells.x * cells.y * 2);
            var triangles = new List<int>(cells.x * cells.y * 6);
            var vertexCache = new Dictionary<QuantizedPoint, int>();
            var cornerPositions = new Vector3[8];
            var cornerDensity = new float[8];
            var edgePositions = new Vector3[12];
            var edgeActive = new bool[12];
            var neighbours = new int[12, 2];
            var degree = new int[12];
            var visited = new bool[12];
            var loop = new List<int>(12);

            for (int z = 0; z < cells.z; z++)
            for (int y = 0; y < cells.y; y++)
            for (int x = 0; x < cells.x; x++)
            {
                Vector3 origin = minimum + Vector3.Scale(new Vector3(x, y, z), step);
                FillCorners(origin, step, cornerPositions, cornerDensity, sampleDensity);
                bool firstSolid = cornerDensity[0] >= 0f;
                bool uniform = true;
                for (int corner = 1; corner < 8; corner++)
                    if ((cornerDensity[corner] >= 0f) != firstSolid)
                    {
                        uniform = false;
                        break;
                    }
                if (uniform) continue;

                Array.Clear(edgeActive, 0, edgeActive.Length);
                Array.Clear(degree, 0, degree.Length);
                Array.Clear(visited, 0, visited.Length);
                for (int edge = 0; edge < 12; edge++)
                {
                    int a = EdgeCorners[edge, 0];
                    int b = EdgeCorners[edge, 1];
                    if ((cornerDensity[a] >= 0f) == (cornerDensity[b] >= 0f)) continue;
                    float denominator = cornerDensity[a] - cornerDensity[b];
                    float t = Mathf.Abs(denominator) < 0.000001f
                        ? 0.5f
                        : Mathf.Clamp01(cornerDensity[a] / denominator);
                    edgePositions[edge] = Vector3.LerpUnclamped(cornerPositions[a], cornerPositions[b], t);
                    edgeActive[edge] = true;
                }

                for (int face = 0; face < 6; face++)
                    ConnectFace(face, cornerDensity, edgeActive, neighbours, degree);

                for (int start = 0; start < 12; start++)
                {
                    if (!edgeActive[start] || visited[start] || degree[start] < 2) continue;
                    loop.Clear();
                    int previous = -1;
                    int current = start;
                    do
                    {
                        if (visited[current] && current != start) break;
                        visited[current] = true;
                        loop.Add(current);
                        int next = neighbours[current, 0] != previous
                            ? neighbours[current, 0]
                            : neighbours[current, 1];
                        previous = current;
                        current = next;
                    } while (current != start && current >= 0 && loop.Count <= 12);

                    if (current == start && loop.Count >= 3)
                        TriangulateLoop(loop, edgePositions, vertices, triangles, vertexCache, sampleDensity, step);
                }
            }

            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static void MakeFaceted(Mesh mesh)
        {
            if (mesh == null) throw new ArgumentNullException(nameof(mesh));
            int[] sourceTriangles = mesh.triangles;
            Vector3[] sourceVertices = mesh.vertices;
            var facetedVertices = new Vector3[sourceTriangles.Length];
            var facetedTriangles = new int[sourceTriangles.Length];
            for (int i = 0; i < sourceTriangles.Length; i++)
            {
                facetedVertices[i] = sourceVertices[sourceTriangles[i]];
                facetedTriangles[i] = i;
            }

            mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.vertices = facetedVertices;
            mesh.triangles = facetedTriangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        private static void FillCorners(
            Vector3 origin,
            Vector3 step,
            Vector3[] positions,
            float[] density,
            Func<Vector3, float> sample)
        {
            positions[0] = origin;
            positions[1] = origin + new Vector3(step.x, 0f, 0f);
            positions[2] = origin + new Vector3(step.x, step.y, 0f);
            positions[3] = origin + new Vector3(0f, step.y, 0f);
            positions[4] = origin + new Vector3(0f, 0f, step.z);
            positions[5] = origin + new Vector3(step.x, 0f, step.z);
            positions[6] = origin + step;
            positions[7] = origin + new Vector3(0f, step.y, step.z);
            for (int i = 0; i < 8; i++) density[i] = sample(positions[i]);
        }

        private static void ConnectFace(
            int face,
            float[] density,
            bool[] active,
            int[,] neighbours,
            int[] degree)
        {
            int count = 0;
            int first = -1;
            int second = -1;
            for (int i = 0; i < 4; i++)
            {
                int edge = FaceEdges[face, i];
                if (!active[edge]) continue;
                if (count == 0) first = i;
                else if (count == 1) second = i;
                count++;
            }

            if (count == 2)
            {
                AddConnection(
                    FaceEdges[face, first], FaceEdges[face, second], neighbours, degree);
                return;
            }
            if (count != 4) return;

            float centre = 0f;
            for (int i = 0; i < 4; i++) centre += density[FaceCorners[face, i]];
            bool centreMatchesFirst = (centre >= 0f) == (density[FaceCorners[face, 0]] >= 0f);
            if (centreMatchesFirst)
            {
                AddConnection(FaceEdges[face, 0], FaceEdges[face, 1], neighbours, degree);
                AddConnection(FaceEdges[face, 2], FaceEdges[face, 3], neighbours, degree);
            }
            else
            {
                AddConnection(FaceEdges[face, 3], FaceEdges[face, 0], neighbours, degree);
                AddConnection(FaceEdges[face, 1], FaceEdges[face, 2], neighbours, degree);
            }
        }

        private static void AddConnection(int a, int b, int[,] neighbours, int[] degree)
        {
            if (degree[a] < 2) neighbours[a, degree[a]++] = b;
            if (degree[b] < 2) neighbours[b, degree[b]++] = a;
        }

        private static void TriangulateLoop(
            List<int> loop,
            Vector3[] edgePositions,
            List<Vector3> vertices,
            List<int> triangles,
            Dictionary<QuantizedPoint, int> cache,
            Func<Vector3, float> sampleDensity,
            Vector3 step)
        {
            int first = GetVertex(edgePositions[loop[0]], vertices, cache);
            for (int i = 1; i < loop.Count - 1; i++)
            {
                int second = GetVertex(edgePositions[loop[i]], vertices, cache);
                int third = GetVertex(edgePositions[loop[i + 1]], vertices, cache);
                Vector3 a = vertices[first];
                Vector3 b = vertices[second];
                Vector3 c = vertices[third];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                Vector3 centre = (a + b + c) / 3f;
                float epsilon = Mathf.Max(0.005f, Mathf.Min(step.x, Mathf.Min(step.y, step.z)) * 0.08f);
                Vector3 gradient = new Vector3(
                    sampleDensity(centre + Vector3.right * epsilon) - sampleDensity(centre - Vector3.right * epsilon),
                    sampleDensity(centre + Vector3.up * epsilon) - sampleDensity(centre - Vector3.up * epsilon),
                    sampleDensity(centre + Vector3.forward * epsilon) - sampleDensity(centre - Vector3.forward * epsilon));
                if (Vector3.Dot(normal, gradient) > 0f)
                {
                    int swap = second;
                    second = third;
                    third = swap;
                }
                triangles.Add(first);
                triangles.Add(second);
                triangles.Add(third);
            }
        }

        private static int GetVertex(
            Vector3 point,
            List<Vector3> vertices,
            Dictionary<QuantizedPoint, int> cache)
        {
            var key = new QuantizedPoint(point);
            if (cache.TryGetValue(key, out int index)) return index;
            index = vertices.Count;
            vertices.Add(point);
            cache.Add(key, index);
            return index;
        }

        private readonly struct QuantizedPoint : IEquatable<QuantizedPoint>
        {
            private readonly int _x;
            private readonly int _y;
            private readonly int _z;

            public QuantizedPoint(Vector3 point)
            {
                _x = Mathf.RoundToInt(point.x * 10000f);
                _y = Mathf.RoundToInt(point.y * 10000f);
                _z = Mathf.RoundToInt(point.z * 10000f);
            }

            public bool Equals(QuantizedPoint other) =>
                _x == other._x && _y == other._y && _z == other._z;
            public override bool Equals(object obj) => obj is QuantizedPoint other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = _x;
                    hash = hash * 397 ^ _y;
                    return hash * 397 ^ _z;
                }
            }
        }
    }

    // Serialized MonoBehaviours live in matching source files below Unity's one-script-per-file rule.
#if false
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MarchingCubesGateVisual : MonoBehaviour
    {
        [SerializeField] private Material _rockMaterial;
        [SerializeField] private Material _lipMaterial;

        private Mesh _rockMesh;
        private Mesh _lipMesh;
        private bool _rebuilding;

        public void Configure(Material rockMaterial, Material lipMaterial)
        {
            _rockMaterial = rockMaterial;
            _lipMaterial = lipMaterial;
            Rebuild();
        }

        private void OnEnable() => Rebuild();

        public void Rebuild()
        {
            if (_rebuilding || !isActiveAndEnabled) return;
            GateWall gate = GetComponent<GateWall>();
            if (gate == null) return;
            _rebuilding = true;
            try
            {
                MeshFilter rockFilter = GetComponent<MeshFilter>();
                if (rockFilter == null) rockFilter = gameObject.AddComponent<MeshFilter>();
                MeshRenderer rockRenderer = GetComponent<MeshRenderer>();
                if (rockRenderer == null) rockRenderer = gameObject.AddComponent<MeshRenderer>();
                rockRenderer.sharedMaterial = _rockMaterial;
                ReplaceMesh(ref _rockMesh, MarchingCubesMeshBuilder.Build(
                    "Marching Cubes Gate Rock",
                    new Bounds(Vector3.zero, new Vector3(26f, 20f, 3.2f)),
                    new Vector3Int(30, 24, 6),
                    point => GateDensity(point, gate.Radius)));
                rockFilter.sharedMesh = _rockMesh;

                Transform lip = transform.Find("Marching Cubes Mineral Lip");
                if (lip == null)
                {
                    var lipObject = new GameObject("Marching Cubes Mineral Lip");
                    lipObject.transform.SetParent(transform, false);
                    lip = lipObject.transform;
                }
                MeshFilter lipFilter = lip.GetComponent<MeshFilter>();
                if (lipFilter == null) lipFilter = lip.gameObject.AddComponent<MeshFilter>();
                MeshRenderer lipRenderer = lip.GetComponent<MeshRenderer>();
                if (lipRenderer == null) lipRenderer = lip.gameObject.AddComponent<MeshRenderer>();
                lipRenderer.sharedMaterial = _lipMaterial;
                float lipSpan = gate.Diameter + 2.2f;
                ReplaceMesh(ref _lipMesh, MarchingCubesMeshBuilder.Build(
                    "Marching Cubes Mineral Lip",
                    new Bounds(Vector3.zero, new Vector3(lipSpan, lipSpan, 2.2f)),
                    new Vector3Int(24, 24, 6),
                    point => LipDensity(point, gate.Radius)));
                lipFilter.sharedMesh = _lipMesh;
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private static float GateDensity(Vector3 point, float radius)
        {
            float roughness = Mathf.PerlinNoise(
                point.x * 0.19f + 18.4f,
                point.y * 0.17f + 42.7f) * 0.34f;
            float slab = GateWall.Thickness * 0.75f + roughness - Mathf.Abs(point.z);
            float outsideHole = new Vector2(point.x, point.y).magnitude - radius;
            return Mathf.Min(slab, outsideHole);
        }

        private static float LipDensity(Vector3 point, float radius)
        {
            float radial = new Vector2(point.x, point.y).magnitude;
            float ringDistance = Mathf.Sqrt(
                Mathf.Pow(radial - (radius + 0.20f), 2f) + point.z * point.z);
            return 0.20f - ringDistance;
        }

        private static void ReplaceMesh(ref Mesh current, Mesh replacement)
        {
            DestroyGeneratedMesh(current);
            current = replacement;
            current.hideFlags = HideFlags.DontSave;
        }

        private void OnDestroy()
        {
            DestroyGeneratedMesh(_rockMesh);
            DestroyGeneratedMesh(_lipMesh);
        }

        private static void DestroyGeneratedMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MarchingCubesSeaChunk : MonoBehaviour
    {
        public const float DefaultLength = 28f;

        [SerializeField] private int _initialChunkIndex;
        [SerializeField] private int _chunkIndex;
        [SerializeField] private float _length = DefaultLength;
        [SerializeField] private Material _rockMaterial;

        private Mesh _mesh;
        private bool _rebuilding;

        public int ChunkIndex => _chunkIndex;
        public float Length => _length;

        public void Configure(int chunkIndex, Material rockMaterial, float length = DefaultLength)
        {
            _initialChunkIndex = chunkIndex;
            _chunkIndex = chunkIndex;
            _length = Mathf.Max(12f, length);
            _rockMaterial = rockMaterial;
            ApplyPosition();
            Rebuild();
        }

        public void SetChunkIndex(int chunkIndex)
        {
            if (_chunkIndex == chunkIndex) return;
            _chunkIndex = chunkIndex;
            ApplyPosition();
            Rebuild();
        }

        public void ResetChunk()
        {
            _chunkIndex = _initialChunkIndex;
            ApplyPosition();
            Rebuild();
        }

        private void OnEnable()
        {
            ApplyPosition();
            Rebuild();
        }

        private void ApplyPosition()
        {
            Vector3 local = transform.localPosition;
            local.x = 0f;
            local.y = 0f;
            local.z = _chunkIndex * _length;
            transform.localPosition = local;
        }

        private void Rebuild()
        {
            if (_rebuilding || !isActiveAndEnabled) return;
            _rebuilding = true;
            try
            {
                MeshFilter filter = GetComponent<MeshFilter>();
                if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
                MeshRenderer renderer = GetComponent<MeshRenderer>();
                if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _rockMaterial;
                float worldStart = _chunkIndex * _length;
                Mesh replacement = MarchingCubesMeshBuilder.Build(
                    $"Marching Cubes Sea Chunk {_chunkIndex}",
                    new Bounds(new Vector3(0f, 5f, 0f), new Vector3(25f, 16f, _length)),
                    new Vector3Int(24, 14, 20),
                    point => TerrainDensity(point, worldStart));
                DestroyGeneratedMesh(_mesh);
                _mesh = replacement;
                _mesh.hideFlags = HideFlags.DontSave;
                filter.sharedMesh = _mesh;
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private static float TerrainDensity(Vector3 point, float worldStart)
        {
            float worldZ = worldStart + point.z;
            float floorNoise = Mathf.PerlinNoise(point.x * 0.12f + 7.3f, worldZ * 0.055f + 11.9f);
            float sideNoise = Mathf.PerlinNoise(point.y * 0.11f + 29.1f, worldZ * 0.047f + 3.8f);
            float floorSurface = -0.12f - floorNoise * 0.72f;
            float sideSurface = GateWall.CorridorHalfWidth + 0.20f + sideNoise * 0.72f;
            float floorRock = floorSurface - point.y;
            float leftRock = -sideSurface - point.x;
            float rightRock = point.x - sideSurface;
            return Mathf.Max(floorRock, Mathf.Max(leftRock, rightRock));
        }

        private void OnDestroy() => DestroyGeneratedMesh(_mesh);

        private static void DestroyGeneratedMesh(Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh);
            else DestroyImmediate(mesh);
        }
    }

    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class InfiniteSeaTerrain : MonoBehaviour
    {
        [SerializeField, Min(8f)] private float _rearMargin = 38f;
        [SerializeField] private MarchingCubesSeaChunk[] _chunks = Array.Empty<MarchingCubesSeaChunk>();

        private BoidSwarm _swarm;

        public void Configure(MarchingCubesSeaChunk[] chunks) =>
            _chunks = chunks ?? Array.Empty<MarchingCubesSeaChunk>();

        private void Awake()
        {
            if (_chunks == null || _chunks.Length == 0)
                _chunks = GetComponentsInChildren<MarchingCubesSeaChunk>(true);
            _swarm = FindAnyObjectByType<BoidSwarm>();
        }

        private void LateUpdate()
        {
            if (_swarm == null || _swarm.AliveCount <= 0 || _chunks.Length == 0) return;
            while (TryGetExtremes(out MarchingCubesSeaChunk earliest, out int latestIndex) &&
                   _swarm.Center.z > earliest.transform.position.z + earliest.Length * 0.5f + _rearMargin)
            {
                earliest.SetChunkIndex(latestIndex + 1);
            }
        }

        public void ResetTerrain()
        {
            for (int i = 0; i < _chunks.Length; i++)
                if (_chunks[i] != null) _chunks[i].ResetChunk();
        }

        private bool TryGetExtremes(out MarchingCubesSeaChunk earliest, out int latestIndex)
        {
            earliest = null;
            latestIndex = int.MinValue;
            for (int i = 0; i < _chunks.Length; i++)
            {
                MarchingCubesSeaChunk chunk = _chunks[i];
                if (chunk == null) continue;
                if (earliest == null || chunk.ChunkIndex < earliest.ChunkIndex) earliest = chunk;
                latestIndex = Mathf.Max(latestIndex, chunk.ChunkIndex);
            }
            return earliest != null;
        }
    }
#endif
}
