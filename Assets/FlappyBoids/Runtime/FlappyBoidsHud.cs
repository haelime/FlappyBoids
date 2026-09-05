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
        [SerializeField] private Image _fitFill;
        [SerializeField] private GameObject _gameplayLayer;
        [SerializeField] private GameObject _readyPanel;
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private Text _resultTitle;
        [SerializeField] private Text _resultStats;
        [SerializeField] private Text _resultBest;

        private FlappyBoidsGame _game;
        private float _fitFillFullWidth;

        public bool HasAuthoredHierarchy =>
            _schoolCount != null && _gatesCount != null && _fitFill != null &&
            _gameplayLayer != null && _readyPanel != null && _resultPanel != null && _resultTitle != null &&
            _resultStats != null && _resultBest != null;

        public void ConfigureView(
            Text schoolCount,
            Text gatesCount,
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
            _fitFill = fitFill;
            _gameplayLayer = gameplayLayer;
            _readyPanel = readyPanel;
            _resultPanel = resultPanel;
            _resultTitle = resultTitle;
            _resultStats = resultStats;
            _resultBest = resultBest;
            CacheFitFillWidth();
        }

        public void Configure(FlappyBoidsGame game)
        {
            if (_game != null)
            {
                _game.HudStateChanged -= Refresh;
                if (_game.Swarm != null) _game.Swarm.FormationFitChanged -= RefreshFit;
            }
            _game = game;
            if (_game != null)
            {
                _game.HudStateChanged += Refresh;
                if (_game.Swarm != null) _game.Swarm.FormationFitChanged += RefreshFit;
            }
            Refresh();
        }

        private void OnDestroy()
        {
            if (_game == null) return;
            _game.HudStateChanged -= Refresh;
            if (_game.Swarm != null) _game.Swarm.FormationFitChanged -= RefreshFit;
        }

        private void RefreshFit()
        {
            if (_game == null || _game.Swarm == null) return;
            SetFitVisual(_game.Swarm.FormationFit);
        }

        private void Refresh()
        {
            if (_game == null || _game.Swarm == null) return;

            int alive = _game.Swarm.AliveCount;
            if (_schoolCount != null) _schoolCount.text = $"{alive:00} / {BoidSwarm.StartingBoids}";
            if (_gatesCount != null) _gatesCount.text = $"{_game.WallsPassed:00}";
            SetFitVisual(Mathf.Clamp01(_game.Swarm.FormationFit));

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
                _resultStats.text = $"GATES  {_game.WallsPassed:00}";
            }

        private void SetFitVisual(float formationFit)
        {
            if (_fitFill == null) return;
            formationFit = Mathf.Clamp01(formationFit);
            CacheFitFillWidth();
            RectTransform fillRect = _fitFill.rectTransform;
            Vector2 size = fillRect.sizeDelta;
            size.x = _fitFillFullWidth * formationFit;
            fillRect.sizeDelta = size;
            _fitFill.color = formationFit >= 0.8f
                ? new Color(0.22f, 0.72f, 0.96f, 1f)
                : formationFit >= 0.55f
                    ? new Color(0.10f, 0.47f, 0.78f, 1f)
                    : new Color(0.07f, 0.25f, 0.62f, 1f);
        }

        private void CacheFitFillWidth()
        {
            if (_fitFill == null || _fitFillFullWidth > 0f) return;
            _fitFillFullWidth = Mathf.Max(0f, _fitFill.rectTransform.sizeDelta.x);
        }

    }
}
