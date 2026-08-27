using UnityEngine;

namespace FlappyBoids
{
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
}
