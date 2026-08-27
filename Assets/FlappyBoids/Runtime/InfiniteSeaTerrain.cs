using System;
using UnityEngine;

namespace FlappyBoids
{
    [DefaultExecutionOrder(150)]
    [DisallowMultipleComponent]
    public sealed class InfiniteSeaTerrain : MonoBehaviour
    {
        [SerializeField, Min(8f)] private float _rearMargin = 38f;
        [SerializeField] private MarchingCubesSeaChunk[] _chunks = Array.Empty<MarchingCubesSeaChunk>();

        private BoidSwarm _swarm;
        private Vector3 _initialPosition;

        public void Configure(MarchingCubesSeaChunk[] chunks) =>
            _chunks = chunks ?? Array.Empty<MarchingCubesSeaChunk>();

        private void Awake()
        {
            _initialPosition = transform.position;
            if (_chunks == null || _chunks.Length == 0)
                _chunks = GetComponentsInChildren<MarchingCubesSeaChunk>(true);
            _swarm = FindAnyObjectByType<BoidSwarm>();
        }

        public void AdvanceCourse(float distance)
        {
            if (distance <= 0f || _swarm == null || _chunks.Length == 0) return;
            transform.position += Vector3.back * distance;
            while (TryGetExtremes(out MarchingCubesSeaChunk earliest, out int latestIndex) &&
                   _swarm.Center.z > earliest.transform.position.z + earliest.Length * 0.5f + _rearMargin)
            {
                earliest.SetChunkIndex(latestIndex + 1);
            }
        }

        public void ResetTerrain()
        {
            transform.position = _initialPosition;
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
}
