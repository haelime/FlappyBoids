using UnityEngine;
using UnityEngine.UI;

namespace FlappyBoids
{
    [DisallowMultipleComponent]
    public sealed class FlappyBoidsHud : MonoBehaviour
    {
        [Header("Scene-authored HUD")]
        [SerializeField] private Text _schoolCount;
        [SerializeField] private Text _pipesCount;
        [SerializeField] private Text _routeStatus;
        [SerializeField] private Image _fitFill;
        [SerializeField] private GameObject _readyPanel;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultTitle;
        [SerializeField] private Text _resultStats;
        [SerializeField] private Text _resultBest;

        private FlappyBoidsGame _game;

        public void ConfigureView(
            Text schoolCount,
            Text pipesCount,
            Text routeStatus,
            Image fitFill,
            GameObject readyPanel,
            GameObject resultPanel,
            Text resultTitle,
            Text resultStats,
            Text resultBest)
        {
            _schoolCount = schoolCount;
            _pipesCount = pipesCount;
            _routeStatus = routeStatus;
            _fitFill = fitFill;
            _readyPanel = readyPanel;
            _resultPanel = resultPanel;
            _resultTitle = resultTitle;
            _resultStats = resultStats;
            _resultBest = resultBest;
        }

        public void Configure(FlappyBoidsGame game)
        {
            _game = game;
            Refresh();
        }

        private void LateUpdate()
        {
            Refresh();
        }

        private void Refresh()
        {
            if (_game == null || _game.Swarm == null) return;

            int alive = _game.Swarm.AliveCount;
            float formationFit = _game.Swarm.FormationFit;
            if (_schoolCount != null) _schoolCount.text = $"{alive:00} / {BoidSwarm.StartingBoids}";
            if (_pipesCount != null) _pipesCount.text = $"{_game.WallsPassed:00} / {FlappyBoidsGame.TotalGates}";
            if (_routeStatus != null)
                _routeStatus.text = $"NEXT PIPE  {_game.NextGateDistance:0}m    FLOCK FIT  {formationFit * 100f:0}%";
            if (_fitFill != null)
            {
                _fitFill.fillAmount = formationFit;
                _fitFill.color = formationFit >= 0.8f
                    ? new Color(0.36f, 0.66f, 0.43f, 1f)
                    : formationFit >= 0.55f
                        ? new Color(0.95f, 0.58f, 0.18f, 1f)
                        : new Color(0.88f, 0.25f, 0.16f, 1f);
            }

            bool ready = _game.State == FlappyBoidsGame.RunState.Ready;
            bool gameOver = _game.State == FlappyBoidsGame.RunState.GameOver;
            if (_readyPanel != null && _readyPanel.activeSelf != ready) _readyPanel.SetActive(ready);
            if (_resultPanel != null && _resultPanel.activeSelf != gameOver) _resultPanel.SetActive(gameOver);

            if (!gameOver) return;
            if (_resultTitle != null)
            {
                _resultTitle.text = _game.Won ? "SCHOOL MADE IT!" : "SCHOOL LOST";
                _resultTitle.color = _game.Won
                    ? new Color(0.91f, 0.87f, 0.74f, 1f)
                    : new Color(0.88f, 0.25f, 0.16f, 1f);
            }
            if (_resultStats != null)
                _resultStats.text = $"PIPES  {_game.WallsPassed:00}     FINAL FISH  {alive:00}";
            if (_resultBest != null)
                _resultBest.text =
                    $"BEST  {FlappyBoidsRecord.BestWalls:00} pipes / {FlappyBoidsRecord.BestSurvivors:00} fish" +
                    (_game.NewBest ? "\nNEW BEST RUN" : string.Empty);
        }
    }
}
