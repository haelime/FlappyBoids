using UnityEngine;

namespace FlappyBoids
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MarchingCubesGateVisual : MonoBehaviour
    {
        [SerializeField] private Material _rockMaterial;
        [SerializeField] private Material _lipMaterial;

        [Header("Rugged rock authoring")]
        [SerializeField] private int _seed = 917;
        [SerializeField, Range(0.15f, 1.8f)] private float _roughness = 0.92f;
        [SerializeField, Range(0f, 1.2f)] private float _ridgeStrength = 0.58f;
        [SerializeField, Range(0f, 0.8f)] private float _chipStrength = 0.28f;
        [SerializeField, Range(24, 44)] private int _crossSectionCells = 34;
        [SerializeField, Range(6, 12)] private int _depthCells = 8;
        [SerializeField] private bool _faceted = true;

        private Mesh _rockMesh;
        private Mesh _lipMesh;
        private bool _rebuilding;

        public int TriangleCount => _rockMesh == null ? 0 : _rockMesh.triangles.Length / 3;

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
                Mesh ruggedRock = MarchingCubesMeshBuilder.Build(
                    "Marching Cubes Gate Rock",
                    new Bounds(Vector3.zero, new Vector3(26f, 20f, 4.2f)),
                    new Vector3Int(_crossSectionCells, Mathf.RoundToInt(_crossSectionCells * 0.78f), _depthCells),
                    point => GateDensity(point, gate.Radius));
                if (_faceted) MarchingCubesMeshBuilder.MakeFaceted(ruggedRock);
                ReplaceMesh(ref _rockMesh, ruggedRock);
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

        private float GateDensity(Vector3 point, float radius)
        {
            Vector2 surfacePoint = new Vector2(point.x, point.y);
            float mass = RuggedRockNoise.Mass(surfacePoint, _seed, 0.16f);
            float ridge = RuggedRockNoise.Ridge(surfacePoint, _seed, 0.43f);
            float chips = RuggedRockNoise.Chips(surfacePoint, _seed, 0.91f);
            float outwardDepth = 0.18f + mass * _roughness + ridge * _ridgeStrength;
            outwardDepth = Mathf.Max(0.12f, outwardDepth - chips * _chipStrength);
            float slab = GateWall.Thickness * 0.5f + outwardDepth - Mathf.Abs(point.z);
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
}
