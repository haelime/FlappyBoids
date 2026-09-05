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

        [Header("Authored hierarchy")]
        [SerializeField] private MeshFilter _meshFilter;
        [SerializeField] private MeshRenderer _meshRenderer;

        [Header("Rugged rock authoring")]
        [SerializeField] private int _seed = 1337;
        [SerializeField, Range(0.15f, 1.8f)] private float _roughness = 1.05f;
        [SerializeField, Range(0f, 1.2f)] private float _ridgeStrength = 0.64f;
        [SerializeField, Range(0f, 0.8f)] private float _chipStrength = 0.24f;
        [SerializeField, Range(16, 36)] private int _lateralCells = 20;
        [SerializeField, Range(12, 28)] private int _lengthCells = 16;
        [SerializeField] private bool _faceted = true;

        private Mesh _mesh;
        private bool _rebuilding;

        public int ChunkIndex => _chunkIndex;
        public float Length => _length;
        public int TriangleCount => _mesh == null ? 0 : _mesh.triangles.Length / 3;

        public void Configure(
            int chunkIndex,
            Material rockMaterial,
            MeshFilter meshFilter,
            MeshRenderer meshRenderer,
            float length = DefaultLength)
        {
            _initialChunkIndex = chunkIndex;
            _chunkIndex = chunkIndex;
            _length = Mathf.Max(12f, length);
            _rockMaterial = rockMaterial;
            _meshFilter = meshFilter;
            _meshRenderer = meshRenderer;
            ApplyPosition();
            Rebuild();
        }

        public void SetChunkIndex(int chunkIndex)
        {
            if (_chunkIndex == chunkIndex) return;
            _chunkIndex = chunkIndex;
            ApplyPosition();
        }

        public void ResetChunk()
        {
            _chunkIndex = _initialChunkIndex;
            ApplyPosition();
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

        public void Rebuild()
        {
            if (_rebuilding || !isActiveAndEnabled) return;
            if (_meshFilter == null) _meshFilter = GetComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshFilter == null || _meshRenderer == null)
            {
                Debug.LogError(
                    $"{nameof(MarchingCubesSeaChunk)} on '{name}' requires an authored MeshFilter and MeshRenderer.",
                    this);
                return;
            }
            _rebuilding = true;
            try
            {
                _meshRenderer.sharedMaterial = _rockMaterial;
                float worldStart = _chunkIndex * _length;
                Mesh replacement = MarchingCubesMeshBuilder.Build(
                    $"Marching Cubes Sea Chunk {_chunkIndex}",
                    new Bounds(new Vector3(0f, 5f, 0f), new Vector3(25f, 16f, _length)),
                    new Vector3Int(_lateralCells, Mathf.Max(12, Mathf.RoundToInt(_lateralCells * 0.62f)), _lengthCells),
                    point => TerrainDensity(point, worldStart));
                if (_faceted) MarchingCubesMeshBuilder.MakeFaceted(replacement);
                DestroyGeneratedMesh(_mesh);
                _mesh = replacement;
                _mesh.hideFlags = HideFlags.DontSave;
                _meshFilter.sharedMesh = _mesh;
            }
            finally
            {
                _rebuilding = false;
            }
        }

        private float TerrainDensity(Vector3 point, float worldStart)
        {
            float worldZ = worldStart + point.z;
            Vector2 floorPoint = new Vector2(point.x, worldZ);
            Vector2 sidePoint = new Vector2(point.y, worldZ);
            float floorOutward = RockOutward(floorPoint, _seed);
            float sideOutward = RockOutward(sidePoint, _seed + 431);
            float floorSurface = -0.12f - floorOutward;
            float sideSurface = GateWall.CorridorHalfWidth + 0.20f + sideOutward;
            float floorRock = floorSurface - point.y;
            float leftRock = -sideSurface - point.x;
            float rightRock = point.x - sideSurface;
            return Mathf.Max(floorRock, Mathf.Max(leftRock, rightRock));
        }

        private float RockOutward(Vector2 point, int seed)
        {
            float mass = RuggedRockNoise.Mass(point, seed, 0.105f);
            float ridge = RuggedRockNoise.Ridge(point, seed, 0.31f);
            float chips = RuggedRockNoise.Chips(point, seed, 0.78f);
            return Mathf.Max(
                0.12f,
                0.18f + mass * _roughness + ridge * _ridgeStrength - chips * _chipStrength);
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
