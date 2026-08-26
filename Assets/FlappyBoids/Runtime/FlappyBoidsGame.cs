using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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

        public const int TotalGates = 12;

        [Header("Scene-authored references")]
        [SerializeField] private BoidSwarm _swarm;
        [SerializeField] private FlappyBoidsCamera _followCamera;
        [SerializeField] private FlappyBoidsAudio _audio;
        [SerializeField] private FlappyBoidsHud _hud;
        [SerializeField] private GateWall[] _authoredGates = Array.Empty<GateWall>();

        private readonly List<GateWall> _gates = new List<GateWall>(TotalGates);
        private bool _built;

        public RunState State { get; private set; }
        public int WallsPassed { get; private set; }
        public bool Won { get; private set; }
        public bool NewBest { get; private set; }
        public BoidSwarm Swarm => _swarm;
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

            _built = true;
            Application.targetFrameRate = 120;
            QualitySettings.vSyncCount = 0;

            _swarm.Configure(_gates);
            _followCamera.Configure(this);
            _hud.Configure(this);
            UpdateGuidance();
            State = RunState.Ready;
        }

        private void ResolveSceneReferences()
        {
            if (_swarm == null) _swarm = GetComponentInChildren<BoidSwarm>(true);
            if (_followCamera == null) _followCamera = GetComponentInChildren<FlappyBoidsCamera>(true);
            if (_audio == null) _audio = GetComponentInChildren<FlappyBoidsAudio>(true);
            if (_hud == null) _hud = GetComponentInChildren<FlappyBoidsHud>(true);

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
                if (flap)
                {
                    State = RunState.Playing;
                    _swarm.Begin();
                    _swarm.SetInput(horizontal, true);
                    _audio.PlayFlap();
                }
                return;
            }

            if (State == RunState.GameOver)
            {
                if (flap || restart) RestartAndLaunch();
                return;
            }

            _swarm.SetInput(horizontal, flap);
            if (flap) _audio.PlayFlap();
            if (_swarm.RemovedSinceLastFrame > 0) _audio.PlayHit(_swarm.RemovedSinceLastFrame);

            for (int i = 0; i < _gates.Count; i++)
            {
                GateWall gate = _gates[i];
                if (!gate.Passed && _swarm.Center.z > gate.Z + GateWall.Thickness + 1f)
                {
                    gate.Passed = true;
                    WallsPassed++;
                    _audio.PlayGate();
                }
            }
            UpdateGuidance();

            if (_swarm.AliveCount <= 0)
                FinishRun(false);
            else if (WallsPassed >= TotalGates)
                FinishRun(true);
        }

        private void FinishRun(bool won)
        {
            if (State != RunState.Playing) return;
            Won = won;
            State = RunState.GameOver;
            _swarm.Stop();
            NewBest = FlappyBoidsRecord.SaveRun(WallsPassed, _swarm.AliveCount);
            _audio.PlayFinish(won);
        }

        private void RestartAndLaunch()
        {
            for (int i = 0; i < _gates.Count; i++) _gates[i].Passed = false;
            WallsPassed = 0;
            Won = false;
            NewBest = false;
            _swarm.ResetSwarm();
            _swarm.Begin();
            _swarm.SetInput(0f, true);
            State = RunState.Playing;
            _audio.PlayFlap();
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

        private void OnDrawGizmosSelected()
        {
            if (_authoredGates == null || _authoredGates.Length == 0) return;
            Gizmos.color = new Color(0.15f, 0.95f, 0.90f, 0.9f);
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
    }
}
