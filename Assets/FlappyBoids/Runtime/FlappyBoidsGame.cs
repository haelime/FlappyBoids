using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace FlappyBoids
{
    [DisallowMultipleComponent]
    public sealed class FlappyBoidsGame : MonoBehaviour
    {
        public enum RunState
        {
            Ready,
            Playing,
            GameOver
        }

        public const int GatePoolSize = 12;
        public const float DefaultCourseSpeed = 8.1f;
        public const float DefaultGateSpacing = 18f;
        public const float DefaultInitialHoleDiameter = 6.1f;
        public const float DefaultMinimumHoleDiameter = 3.8f;
        public const float DefaultHoleShrinkPerGate = 0.09f;

        [Header("Scene-authored references")]
        [SerializeField] private BoidSwarm _swarm;
        [SerializeField] private FlappyBoidsCamera _followCamera;
        [SerializeField] private FlappyBoidsAudio _audio;
        [SerializeField] private FlappyBoidsHud _hud;
        [SerializeField] private GateWall[] _authoredGates = Array.Empty<GateWall>();

        [Header("Infinite gate difficulty")]
        [SerializeField, Min(1f)] private float _courseSpeed = DefaultCourseSpeed;
        [SerializeField, Min(8f)] private float _gateSpacing = DefaultGateSpacing;
        [SerializeField, Min(1f)] private float _initialHoleDiameter = DefaultInitialHoleDiameter;
        [SerializeField, Min(1f)] private float _minimumHoleDiameter = DefaultMinimumHoleDiameter;
        [SerializeField, Min(0f)] private float _holeShrinkPerGate = DefaultHoleShrinkPerGate;

        private readonly List<GateWall> _gates = new List<GateWall>(GatePoolSize);
        private GateSnapshot[] _initialGateStates = Array.Empty<GateSnapshot>();
        private InfiniteSeaTerrain _seaTerrain;
        private int _nextGateSequence;
        private bool _built;
        private bool _hasHudSignature;
        private HudSignature _lastHudSignature;
        private IDisposable _anyButtonListener;
        private bool _anyButtonPressed;

        public event Action HudStateChanged;

        public RunState State { get; private set; }
        public int WallsPassed { get; private set; }
        public bool Won { get; private set; }
        public bool NewBest { get; private set; }
        public BoidSwarm Swarm => _swarm;
        public float MinimumHoleDiameter => _minimumHoleDiameter;
        public float NextHoleDiameter => GetNextGate()?.Diameter ?? _minimumHoleDiameter;
        public float NextGateDistance
        {
            get
            {
                GateWall next = GetNextGate();
                return next == null || _swarm == null ? 0f : Mathf.Max(0f, next.Z - _swarm.Center.z);
            }
        }

        public void ConfigureScene(
            BoidSwarm swarm,
            FlappyBoidsCamera followCamera,
            FlappyBoidsAudio audio,
            FlappyBoidsHud hud,
            GateWall[] gates)
        {
            _swarm = swarm;
            _followCamera = followCamera;
            _audio = audio;
            _hud = hud;
            _authoredGates = gates ?? Array.Empty<GateWall>();
        }

        private void Start()
        {
            // Scene-owned initialization also works when this scene is loaded after application startup.
            Build();
        }

        private void OnEnable()
        {
            _anyButtonListener = InputSystem.onAnyButtonPress.Call(_ => _anyButtonPressed = true);
        }

        private void OnDisable()
        {
            _anyButtonListener?.Dispose();
            _anyButtonListener = null;
            _anyButtonPressed = false;
        }

        public void Build()
        {
            if (_built) return;

            ResolveSceneReferences();
            if (_swarm == null || _followCamera == null || _audio == null || _hud == null || _gates.Count == 0)
            {
                Debug.LogError(
                    "FlappyBoids scene authoring is incomplete. Run Tools/Flappy Boids/Rebuild Authored Scene.", this);
                return;
            }

            NormalizeDifficultySettings();
            PrepareInitialGatePool();

            _built = true;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;

            _swarm.Configure(_gates, _courseSpeed);
            State = RunState.Ready;
            _followCamera.Configure(this);
            _hud.Configure(this);
            UpdateGuidance();
            PublishHudStateIfChanged(true);
        }

        private void ResolveSceneReferences()
        {
            if (_swarm == null) _swarm = GetComponentInChildren<BoidSwarm>(true);
            if (_followCamera == null) _followCamera = GetComponentInChildren<FlappyBoidsCamera>(true);
            if (_audio == null) _audio = GetComponentInChildren<FlappyBoidsAudio>(true);
            if (_hud == null) _hud = GetComponentInChildren<FlappyBoidsHud>(true);
            if (_seaTerrain == null) _seaTerrain = GetComponentInChildren<InfiniteSeaTerrain>(true);

            _gates.Clear();
            if (_authoredGates == null || _authoredGates.Length == 0)
                _authoredGates = GetComponentsInChildren<GateWall>(true);
            _gates.AddRange(_authoredGates);
            _gates.RemoveAll(gate => gate == null);
            _gates.Sort((left, right) => left.Z.CompareTo(right.Z));
        }

        private void Update()
        {
            if (!_built || _swarm == null) return;
            UpdateGuidance();
            bool startPressed = _anyButtonPressed;
            _anyButtonPressed = false;
            Keyboard keyboard = Keyboard.current;
            float horizontal = 0f;
            bool flap = false;
            bool restart = false;
            if (keyboard != null)
            {
                horizontal = (keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                             (keyboard.leftArrowKey.isPressed ? 1f : 0f);
                flap = keyboard.spaceKey.wasPressedThisFrame;
                restart = keyboard.rKey.wasPressedThisFrame;
            }

            if (State == RunState.Ready)
            {
                _swarm.SetInput(horizontal, false);
                if (startPressed) BeginRun(horizontal);
                PublishHudStateIfChanged();
                return;
            }

            if (State == RunState.GameOver)
            {
                if (flap || restart) RestartAndLaunch();
                PublishHudStateIfChanged();
                return;
            }

            _swarm.SetInput(horizontal, flap);
            if (flap) _audio.PlayFlap();
            if (_swarm.RemovedSinceLastFrame > 0) _audio.PlayHit(_swarm.RemovedSinceLastFrame);

            AdvancePooledCourse(Time.deltaTime);
            RecyclePassedGates();
            UpdateGuidance();

            if (_swarm.AliveCount <= 0)
                FinishRun(false);
            PublishHudStateIfChanged();
        }

        private void FinishRun(bool won)
        {
            if (State != RunState.Playing) return;
            Won = won;
            State = RunState.GameOver;
            _swarm.Stop();
            NewBest = FlappyBoidsRecord.SaveRun(WallsPassed, _swarm.AliveCount);
            _audio.PlayFinish(won);
            PublishHudStateIfChanged(true);
        }

        public void BeginRun(float horizontalInput = 0f)
        {
            if (!_built) Build();
            if (!_built || State != RunState.Ready) return;

            State = RunState.Playing;
            _swarm.Begin();
            _swarm.SetInput(horizontalInput, true);
            _audio.PlayFlap();
            PublishHudStateIfChanged(true);
        }

        private void RestartAndLaunch()
        {
            RestoreInitialGatePool();
            if (_seaTerrain != null) _seaTerrain.ResetTerrain();
            WallsPassed = 0;
            Won = false;
            NewBest = false;
            _swarm.ResetSwarm();
            _swarm.Begin();
            _swarm.SetInput(0f, true);
            State = RunState.Playing;
            _audio.PlayFlap();
            PublishHudStateIfChanged(true);
        }

        private void RecyclePassedGates()
        {
            bool obstaclesChanged = false;
            while (_gates.Count > 0 &&
                   _swarm.Center.z > _gates[0].Z + GateWall.Thickness + 1f)
            {
                GateWall recycled = _gates[0];
                GateWall previousLast = _gates[_gates.Count - 1];
                Vector2 nextCenter = CalculateHoleCenter(_nextGateSequence, previousLast.HoleCenter);
                recycled.transform.position = new Vector3(
                    nextCenter.x, nextCenter.y, previousLast.Z + _gateSpacing);
                recycled.SetHoleDiameter(CalculateHoleDiameter(
                    _nextGateSequence, _initialHoleDiameter,
                    _holeShrinkPerGate, _minimumHoleDiameter));
                recycled.Passed = false;

                _gates.RemoveAt(0);
                _gates.Add(recycled);
                _nextGateSequence++;
                WallsPassed++;
                obstaclesChanged = true;
                _audio.PlayGate();
            }

            if (obstaclesChanged) _swarm.RefreshGateObstacles(_gates);
        }

        private void AdvancePooledCourse(float deltaTime)
        {
            float distance = _courseSpeed * Mathf.Max(0f, deltaTime);
            Vector3 displacement = Vector3.back * distance;
            for (int i = 0; i < _gates.Count; i++)
                if (_gates[i] != null) _gates[i].transform.position += displacement;
            if (_seaTerrain != null) _seaTerrain.AdvanceCourse(distance);
        }

        private void PrepareInitialGatePool()
        {
            for (int i = 0; i < _gates.Count; i++)
            {
                _gates[i].SetHoleDiameter(CalculateHoleDiameter(
                    i, _initialHoleDiameter, _holeShrinkPerGate, _minimumHoleDiameter));
                _gates[i].Passed = false;
            }

            _initialGateStates = new GateSnapshot[_gates.Count];
            for (int i = 0; i < _gates.Count; i++)
            {
                _initialGateStates[i] = new GateSnapshot
                {
                    Gate = _gates[i],
                    Position = _gates[i].transform.position,
                    Diameter = _gates[i].Diameter
                };
            }
            _nextGateSequence = _gates.Count;
        }

        private void RestoreInitialGatePool()
        {
            _gates.Clear();
            for (int i = 0; i < _initialGateStates.Length; i++)
            {
                GateSnapshot state = _initialGateStates[i];
                if (state.Gate == null) continue;
                state.Gate.transform.position = state.Position;
                state.Gate.SetHoleDiameter(state.Diameter);
                state.Gate.Passed = false;
                _gates.Add(state.Gate);
            }
            _gates.Sort((left, right) => left.Z.CompareTo(right.Z));
            _nextGateSequence = _gates.Count;
            _swarm.RefreshGateObstacles(_gates);
        }

        private void NormalizeDifficultySettings()
        {
            _courseSpeed = Mathf.Max(1f, _courseSpeed);
            _gateSpacing = Mathf.Max(8f, _gateSpacing);
            _initialHoleDiameter = Mathf.Max(1f, _initialHoleDiameter);
            _minimumHoleDiameter = Mathf.Clamp(
                _minimumHoleDiameter, 1f, _initialHoleDiameter);
            _holeShrinkPerGate = Mathf.Max(0f, _holeShrinkPerGate);
        }

        public static float CalculateHoleDiameter(
            int gateIndex,
            float initialDiameter = DefaultInitialHoleDiameter,
            float shrinkPerGate = DefaultHoleShrinkPerGate,
            float minimumDiameter = DefaultMinimumHoleDiameter)
        {
            float initial = Mathf.Max(1f, initialDiameter);
            float minimum = Mathf.Clamp(minimumDiameter, 1f, initial);
            return Mathf.Max(minimum, initial - Mathf.Max(0, gateIndex) * Mathf.Max(0f, shrinkPerGate));
        }

        public static Vector2 CalculateHoleCenter(int gateIndex, Vector2 previousCenter)
        {
            if (gateIndex <= 0) return new Vector2(0f, 5.5f);
            var random = new System.Random(unchecked(9147 + gateIndex * 48611));
            Vector2 target = new Vector2(
                Mathf.Lerp(-3.25f, 3.25f, (float)random.NextDouble()),
                Mathf.Lerp(3.25f, 8.65f, (float)random.NextDouble()));
            return Vector2.Lerp(previousCenter, target, 0.72f);
        }

        public Vector3 GetCameraPathPoint(float cameraZ)
        {
            if (_gates.Count == 0)
            {
                Vector3 center = _swarm != null ? _swarm.Center : new Vector3(0f, 5.5f, cameraZ);
                return new Vector3(center.x, center.y, cameraZ);
            }

            Vector2 routePoint = InterpolateClampedHole(
                new Vector2(0f, 5.5f), 0f, _gates[0].HoleCenter, _gates[0].Z, cameraZ);
            for (int i = 1; i < _gates.Count; i++)
            {
                GateWall previous = _gates[i - 1];
                GateWall next = _gates[i];
                if (cameraZ > next.Z) continue;
                if (cameraZ >= previous.Z)
                    routePoint = InterpolateClampedHole(
                        previous.HoleCenter, previous.Z, next.HoleCenter, next.Z, cameraZ);
                break;
            }

            if (cameraZ >= _gates[_gates.Count - 1].Z)
                routePoint = _gates[_gates.Count - 1].HoleCenter;
            return new Vector3(routePoint.x, routePoint.y, cameraZ);
        }

        public static Vector2 InterpolateClampedHole(
            Vector2 from, float fromZ, Vector2 to, float toZ, float sampleZ)
        {
            float t = toZ <= fromZ ? 1f : Mathf.Clamp01((sampleZ - fromZ) / (toZ - fromZ));
            t = t * t * (3f - 2f * t);
            return Vector2.LerpUnclamped(from, to, t);
        }

        private GateWall GetNextGate()
        {
            for (int i = 0; i < _gates.Count; i++)
                if (!_gates[i].Passed) return _gates[i];
            return null;
        }

        private void UpdateGuidance()
        {
            if (_swarm == null) return;
            GateWall next = GetNextGate();
            if (next != null)
                _swarm.SetGuidanceHole(next.HoleCenter, Mathf.Max(0.1f, next.Radius - BoidSwarm.BirdRadius));
        }

        private void PublishHudStateIfChanged(bool force = false)
        {
            if (_swarm == null) return;
            var signature = new HudSignature
            {
                Alive = _swarm.AliveCount,
                Walls = WallsPassed,
                NextDistanceMeters = Mathf.RoundToInt(NextGateDistance),
                ApertureTenths = Mathf.RoundToInt(NextHoleDiameter * 10f),
                FitPercent = Mathf.RoundToInt(_swarm.FormationFit * 100f),
                State = State,
                NewBest = NewBest
            };
            if (!force && _hasHudSignature && signature.Equals(_lastHudSignature)) return;
            _hasHudSignature = true;
            _lastHudSignature = signature;
            HudStateChanged?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            if (_authoredGates == null || _authoredGates.Length == 0) return;
            Gizmos.color = new Color(0.95f, 0.48f, 0.16f, 0.9f);
            Vector3 previous = new Vector3(0f, 5.5f, 0f);
            for (int i = 0; i < _authoredGates.Length; i++)
            {
                GateWall gate = _authoredGates[i];
                if (gate == null) continue;
                Vector3 next = new Vector3(gate.HoleCenter.x, gate.HoleCenter.y, gate.Z);
                Gizmos.DrawLine(previous, next);
                Gizmos.DrawWireSphere(next, 0.32f);
                previous = next;
            }
        }

        private struct GateSnapshot
        {
            public GateWall Gate;
            public Vector3 Position;
            public float Diameter;
        }

        private struct HudSignature : IEquatable<HudSignature>
        {
            public int Alive;
            public int Walls;
            public int NextDistanceMeters;
            public int ApertureTenths;
            public int FitPercent;
            public RunState State;
            public bool NewBest;

            public bool Equals(HudSignature other) =>
                Alive == other.Alive && Walls == other.Walls &&
                NextDistanceMeters == other.NextDistanceMeters &&
                ApertureTenths == other.ApertureTenths && FitPercent == other.FitPercent &&
                State == other.State && NewBest == other.NewBest;
        }
    }

}
