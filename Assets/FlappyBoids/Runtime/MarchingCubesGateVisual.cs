using UnityEngine;

namespace FlappyBoids
{
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
}
