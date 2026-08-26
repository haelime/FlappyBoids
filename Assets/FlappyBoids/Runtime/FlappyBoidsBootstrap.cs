using UnityEngine;

namespace FlappyBoids
{
    public static class FlappyBoidsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateGame()
        {
            FlappyBoidsGame game = Object.FindAnyObjectByType<FlappyBoidsGame>();
            if (game != null)
            {
                game.Build();
                return;
            }

            Debug.LogWarning(
                "No authored FlappyBoidsGame was found. Open Assets/FlappyBoids/Scenes/FlappyBoids.unity.");
        }
    }
}
