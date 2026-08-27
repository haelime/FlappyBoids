using UnityEngine;

namespace FlappyBoids
{
    public static class RuggedRockNoise
    {
        public static float Mass(Vector2 point, int seed, float scale)
        {
            Vector2 offset = SeedOffset(seed);
            float macro = Mathf.PerlinNoise(
                point.x * scale + offset.x,
                point.y * scale + offset.y);
            float secondary = Mathf.PerlinNoise(
                point.x * scale * 2.37f + offset.y * 0.71f,
                point.y * scale * 2.37f + offset.x * 1.13f);
            float terrace = Mathf.Round(macro * 5f) * 0.2f;
            return Mathf.Clamp01(Mathf.Lerp(macro, terrace, 0.42f) * 0.72f + secondary * 0.28f);
        }

        public static float Ridge(Vector2 point, int seed, float scale)
        {
            Vector2 offset = SeedOffset(seed + 193);
            float value = Mathf.PerlinNoise(
                point.x * scale + offset.x,
                point.y * scale + offset.y);
            float ridge = 1f - Mathf.Abs(value * 2f - 1f);
            return ridge * ridge;
        }

        public static float Chips(Vector2 point, int seed, float scale)
        {
            Vector2 offset = SeedOffset(seed + 701);
            float value = Mathf.PerlinNoise(
                point.x * scale + offset.x,
                point.y * scale + offset.y);
            return value > 0.58f ? Mathf.InverseLerp(0.58f, 1f, value) : 0f;
        }

        private static Vector2 SeedOffset(int seed)
        {
            uint value = unchecked((uint)seed * 747796405u + 2891336453u);
            value = (value >> ((int)(value >> 28) + 4)) ^ value;
            value *= 277803737u;
            value = (value >> 22) ^ value;
            return new Vector2(
                (value & 0xffffu) * (64f / 65535f) + 11.7f,
                ((value >> 16) & 0xffffu) * (64f / 65535f) + 29.3f);
        }
    }
}
