using UnityEngine;

namespace FlappyBoids
{
    public static class FlappyBoidsRecord
    {
        private const string BestWallsKey = "FlappyBoids.BestWalls";
        private const string BestSurvivorsKey = "FlappyBoids.BestSurvivors";
        private const string LastWallsKey = "FlappyBoids.LastWalls";
        private const string LastSurvivorsKey = "FlappyBoids.LastSurvivors";

        public static int BestWalls => PlayerPrefs.GetInt(BestWallsKey, 0);
        public static int BestSurvivors => PlayerPrefs.GetInt(BestSurvivorsKey, 0);
        public static int LastWalls => PlayerPrefs.GetInt(LastWallsKey, 0);
        public static int LastSurvivors => PlayerPrefs.GetInt(LastSurvivorsKey, 0);

        public static bool IsBetter(int walls, int survivors, int bestWalls, int bestSurvivors)
        {
            return walls > bestWalls || (walls == bestWalls && survivors > bestSurvivors);
        }

        public static bool SaveRun(int walls, int survivors)
        {
            PlayerPrefs.SetInt(LastWallsKey, walls);
            PlayerPrefs.SetInt(LastSurvivorsKey, survivors);
            bool newBest = IsBetter(walls, survivors, BestWalls, BestSurvivors);
            if (newBest)
            {
                PlayerPrefs.SetInt(BestWallsKey, walls);
                PlayerPrefs.SetInt(BestSurvivorsKey, survivors);
            }
            PlayerPrefs.Save();
            return newBest;
        }
    }
}
