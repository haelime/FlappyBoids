using UnityEngine;
using UnityEngine.UI;

namespace FlappyBoids
{
    [DisallowMultipleComponent]
    public sealed class FlappyBoidsHud : MonoBehaviour
    {
        [Header("Scene-authored HUD")]
        [SerializeField] private Text _schoolCount;
        [SerializeField] private Text _gatesCount;
        [SerializeField] private Text _nextDistance;
        [SerializeField] private Text _aperture;
        [SerializeField] private Text _fitPercent;
        [SerializeField] private Image _fitFill;
        [SerializeField] private GameObject _gameplayLayer;
        [SerializeField] private GameObject _readyPanel;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultTitle;
        [SerializeField] private Text _resultStats;
        [SerializeField] private Text _resultBest;

        private FlappyBoidsGame _game;

        public bool HasAuthoredHierarchy =>
            _schoolCount != null && _gatesCount != null && _nextDistance != null &&
            _aperture != null && _fitPercent != null && _fitFill != null &&
            _gameplayLayer != null && _readyPanel != null && _resultPanel != null && _resultTitle != null &&
            _resultStats != null && _resultBest != null;

        public void ConfigureView(
            Text schoolCount,
            Text gatesCount,
            Text nextDistance,
            Text aperture,
            Text fitPercent,
            Image fitFill,
            GameObject gameplayLayer,
            GameObject readyPanel,
            GameObject resultPanel,
            Text resultTitle,
            Text resultStats,
            Text resultBest)
        {
            _schoolCount = schoolCount;
            _gatesCount = gatesCount;
            _nextDistance = nextDistance;
            _aperture = aperture;
            _fitPercent = fitPercent;
            _fitFill = fitFill;
            _gameplayLayer = gameplayLayer;
            _readyPanel = readyPanel;
            _resultPanel = resultPanel;
            _resultTitle = resultTitle;
            _resultStats = resultStats;
            _resultBest = resultBest;
        }

        public void Configure(FlappyBoidsGame game)
        {
            if (_game != null) _game.HudStateChanged -= Refresh;
            _game = game;
            if (_game != null) _game.HudStateChanged += Refresh;
            Refresh();
        }

        private void OnDestroy()
        {
            if (_game != null) _game.HudStateChanged -= Refresh;
        }

        private void Refresh()
        {
            if (_game == null || _game.Swarm == null) return;

            int alive = _game.Swarm.AliveCount;
            float formationFit = _game.Swarm.FormationFit;
            if (_schoolCount != null) _schoolCount.text = $"{alive:00} / {BoidSwarm.StartingBoids}";
            if (_gatesCount != null) _gatesCount.text = $"{_game.WallsPassed:00}";
            if (_nextDistance != null) _nextDistance.text = $"{_game.NextGateDistance:0} m";
            if (_aperture != null) _aperture.text = $"{_game.NextHoleDiameter:0.0} m";
            if (_fitPercent != null) _fitPercent.text = $"{formationFit * 100f:0}%";
            if (_fitFill != null)
            {
                _fitFill.fillAmount = formationFit;
                _fitFill.color = formationFit >= 0.8f
                    ? new Color(0.22f, 0.72f, 0.96f, 1f)
                    : formationFit >= 0.55f
                        ? new Color(0.10f, 0.47f, 0.78f, 1f)
                        : new Color(0.07f, 0.25f, 0.62f, 1f);
            }

            bool ready = _game.State == FlappyBoidsGame.RunState.Ready;
            bool gameOver = _game.State == FlappyBoidsGame.RunState.GameOver;
            bool playing = _game.State == FlappyBoidsGame.RunState.Playing;
            if (_gameplayLayer != null && _gameplayLayer.activeSelf != playing)
                _gameplayLayer.SetActive(playing);
            if (_readyPanel != null && _readyPanel.activeSelf != ready) _readyPanel.SetActive(ready);
            if (_resultPanel != null && _resultPanel.activeSelf != gameOver) _resultPanel.SetActive(gameOver);

            if (!gameOver) return;
            if (_resultTitle != null)
            {
                _resultTitle.text = "RUN OVER";
                _resultTitle.color = new Color(0.14f, 0.62f, 0.86f, 1f);
            }
            if (_resultStats != null)
                _resultStats.text = $"GATES  {_game.WallsPassed:00}     FINAL SCHOOL  {alive:00}";
            if (_resultBest != null)
                _resultBest.text =
                    $"BEST  {FlappyBoidsRecord.BestWalls:00} gates / {FlappyBoidsRecord.BestSurvivors:00} fish" +
                    (_game.NewBest ? "\nNEW BEST RUN" : string.Empty);
        }
    }
}
