using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FlappyBoids.Tests
{
    public sealed class FlappyBoidsRuntimeTests
    {
        [UnityTest]
        public IEnumerator RuntimeBootstrap_KeepsDotsFlockNearItsStartWhileCourseAdvances()
        {
            SceneManager.LoadScene("FlappyBoids");
            yield return null;
            yield return null;
            FlappyBoidsGame game = Object.FindAnyObjectByType<FlappyBoidsGame>();

            Assert.That(game, Is.Not.Null, "The authored FlappyBoids scene must contain the game root.");
            Assert.That(game.Swarm, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<Canvas>(), Is.Not.Null,
                "The HUD must be a scene-authored Canvas, not runtime-only IMGUI.");
            FlappyBoidsHud hud = Object.FindAnyObjectByType<FlappyBoidsHud>();
            Assert.That(hud, Is.Not.Null);
            Assert.That(hud.HasAuthoredHierarchy, Is.True,
                "All HUD presentation references must be baked into the authored scene.");
            Assert.That(Object.FindAnyObjectByType<FlappyBoidsSafeArea>(), Is.Not.Null,
                "Critical HUD elements must live under an authored safe-area panel.");
            Transform gameplayLayer = hud.transform.Find("Safe Area/Gameplay Layer");
            Assert.That(gameplayLayer, Is.Not.Null);
            Assert.That(gameplayLayer.gameObject.activeSelf, Is.False,
                "The authored Ready-state hierarchy must match its initial runtime visibility.");
            Transform fitBar = gameplayLayer.Find("School Fit Bar");
            Assert.That(fitBar, Is.Not.Null,
                "The always-on school-fit bar must be authored under the gameplay HUD.");
            Assert.That(fitBar.gameObject.activeSelf, Is.True);
            Transform fitFillTransform = fitBar.Find("Fill");
            Assert.That(fitFillTransform, Is.Not.Null,
                "The centered fill must be part of the authored HUD hierarchy.");
            Image fitFill = fitFillTransform.GetComponent<Image>();
            Assert.That(fitFill, Is.Not.Null);
            RectTransform fitFillRect = fitFill.rectTransform;
            Assert.That(fitFillRect.anchorMin.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(fitFillRect.anchorMax.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(fitFillRect.pivot.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(fitFill.type, Is.EqualTo(Image.Type.Simple));

            float fullWidth = fitFillRect.sizeDelta.x;
            MethodInfo setFitVisual = typeof(FlappyBoidsHud).GetMethod(
                "SetFitVisual", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(setFitVisual, Is.Not.Null);
            setFitVisual.Invoke(hud, new object[] { 0.4f });
            Assert.That(fitFillRect.sizeDelta.x, Is.EqualTo(fullWidth * 0.4f).Within(0.01f),
                "School Fit must shrink the authored fill equally from both sides.");
            Assert.That(fitFillRect.anchoredPosition.x, Is.EqualTo(0f).Within(0.001f));
            setFitVisual.Invoke(hud, new object[] { 1f });
            Assert.That(Object.FindObjectsByType<GateWall>().Length,
                Is.EqualTo(FlappyBoidsGame.GatePoolSize),
                "The infinite course should reuse a fixed-size gate pool.");
            Assert.That(Object.FindAnyObjectByType<InfiniteSeaTerrain>(), Is.Not.Null,
                "The old corridor must be replaced by the recyclable Marching Cubes sea terrain.");
            Assert.That(Object.FindObjectsByType<MarchingCubesSeaChunk>().Length, Is.EqualTo(12));
            MarchingCubesSeaChunk[] seaChunks = Object.FindObjectsByType<MarchingCubesSeaChunk>();
            for (int i = 0; i < seaChunks.Length; i++)
            {
                Assert.That(seaChunks[i].GetComponent<MeshFilter>(), Is.Not.Null);
                Assert.That(seaChunks[i].GetComponent<MeshRenderer>(), Is.Not.Null);
            }
            Transform godRays = game.transform.Find("04_Environment/03_Shader Volumetric God Rays");
            Assert.That(godRays, Is.Not.Null,
                "The underwater god-ray volume must be authored in the scene hierarchy.");
            Assert.That(godRays.GetComponent<MeshRenderer>(), Is.Not.Null);
            GateWall[] gates = Object.FindObjectsByType<GateWall>();
            System.Array.Sort(gates, (left, right) => left.Z.CompareTo(right.Z));
            for (int i = 1; i < gates.Length; i++)
                Assert.That(gates[i].Z - gates[i - 1].Z,
                    Is.EqualTo(FlappyBoidsGame.DefaultGateSpacing).Within(0.01f));
            float smallestOpening = float.MaxValue;
            float largestOpening = 0f;
            for (int i = 0; i < gates.Length; i++)
            {
                smallestOpening = Mathf.Min(smallestOpening, gates[i].Diameter);
                largestOpening = Mathf.Max(largestOpening, gates[i].Diameter);
            }
            Assert.That(smallestOpening, Is.LessThan(largestOpening),
                "Authored gates should already preview the progressive difficulty curve.");
            Assert.That(smallestOpening, Is.GreaterThanOrEqualTo(FlappyBoidsGame.DefaultMinimumHoleDiameter));
            Assert.That(Object.FindObjectsByType<ParticleSystem>().Length,
                Is.GreaterThan(0), "The authored scene must contain bubbling particle VFX.");
            AudioSource[] audioSources = Object.FindObjectsByType<AudioSource>();
            bool foundAmbientLoop = false;
            for (int i = 0; i < audioSources.Length; i++)
                foundAmbientLoop |= audioSources[i].loop && audioSources[i].clip != null;
            Assert.That(foundAmbientLoop, Is.True, "The authored underwater ambience must be assigned and looped.");
            FlappyBoidsAudio feedbackAudio = Object.FindAnyObjectByType<FlappyBoidsAudio>();
            Assert.That(feedbackAudio, Is.Not.Null);
            Assert.That(feedbackAudio.HasAuthoredFeedback, Is.True,
                "Gate feedback must use scene-authored IdleTogether clips and AudioSources.");
            Assert.That(game.Swarm.AliveCount, Is.EqualTo(BoidSwarm.StartingBoids));
            Assert.That(Application.targetFrameRate, Is.EqualTo(60));

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BoidAgent>());
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(BoidSwarm.StartingBoids));

            float startZ = game.Swarm.Center.z;
            float firstGateStartZ = float.MaxValue;
            for (int i = 0; i < gates.Length; i++)
                firstGateStartZ = Mathf.Min(firstGateStartZ, gates[i].Z);
            game.BeginRun(1f);
            for (int i = 0; i < 12; i++) yield return null;

            float firstGateCurrentZ = float.MaxValue;
            for (int i = 0; i < gates.Length; i++)
                firstGateCurrentZ = Mathf.Min(firstGateCurrentZ, gates[i].Z);
            Assert.That(Mathf.Abs(game.Swarm.Center.z - startZ), Is.LessThan(0.5f),
                "The flock must not be force-translated down the course.");
            Assert.That(firstGateCurrentZ, Is.LessThan(firstGateStartZ),
                "The pooled course must move toward the stationary flock.");
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(BoidSwarm.StartingBoids),
                "No boid should be lost before the first gate.");
            query.Dispose();

            Object.Destroy(game.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator InfiniteCourse_RecyclesGateAfterFirstPass()
        {
            SceneManager.LoadScene("FlappyBoids");
            yield return null;
            yield return null;
            FlappyBoidsGame game = Object.FindAnyObjectByType<FlappyBoidsGame>();
            GateWall[] initialGates = Object.FindObjectsByType<GateWall>();
            System.Array.Sort(initialGates, (left, right) => left.Z.CompareTo(right.Z));
            GateWall firstGate = initialGates[0];
            float initialFurthestZ = float.MinValue;
            for (int i = 0; i < initialGates.Length; i++)
                initialFurthestZ = Mathf.Max(initialFurthestZ, initialGates[i].Z);

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery gateQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GateObstacle>());
            NativeArray<Entity> initialObstacleEntities = gateQuery.ToEntityArray(Allocator.Temp);
            Entity[] initialObstaclePool = initialObstacleEntities.ToArray();
            initialObstacleEntities.Dispose();
            float swarmStartZ = game.Swarm.Center.z;

            game.BeginRun();
            float elapsed = 0f;
            float nextFlap = 1.3f;
            while (game.WallsPassed == 0 && elapsed < 5f && game.Swarm.AliveCount > 0)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= nextFlap)
                {
                    game.Swarm.SetInput(0f, true);
                    nextFlap += 1.3f;
                }
                yield return null;
            }

            Assert.That(game.WallsPassed, Is.GreaterThanOrEqualTo(1));
            Assert.That(firstGate.Passed, Is.True,
                "Passing should score immediately while the wall remains in the authored pool behind the school.");
            Assert.That(firstGate.Z, Is.LessThan(game.Swarm.Center.z));

            while (firstGate.Passed && elapsed < 8f && game.Swarm.AliveCount > 0)
            {
                elapsed += Time.deltaTime;
                if (elapsed >= nextFlap)
                {
                    game.Swarm.SetInput(0f, true);
                    nextFlap += 1.3f;
                }
                yield return null;
            }

            Assert.That(firstGate.Passed, Is.False,
                "A passed wall should return to the forward pool only after it has cleared the camera.");

            GateWall[] recycledGates = Object.FindObjectsByType<GateWall>();
            float recycledFurthestZ = float.MinValue;
            for (int i = 0; i < recycledGates.Length; i++)
                recycledFurthestZ = Mathf.Max(recycledFurthestZ, recycledGates[i].Z);

            Assert.That(recycledGates.Length, Is.EqualTo(FlappyBoidsGame.GatePoolSize));
            CollectionAssert.AreEquivalent(initialGates, recycledGates,
                "Gate recycling must reuse the authored pool without spawning replacements.");
            NativeArray<Entity> recycledObstacleEntities = gateQuery.ToEntityArray(Allocator.Temp);
            CollectionAssert.AreEquivalent(initialObstaclePool, recycledObstacleEntities.ToArray(),
                "ECS gate obstacles must also be pooled instead of destroyed and recreated.");
            recycledObstacleEntities.Dispose();
            gateQuery.Dispose();
            Assert.That(recycledFurthestZ, Is.LessThanOrEqualTo(initialFurthestZ + 0.5f),
                "The course scroll should recycle a passed gate into a forward slot, not move the flock forward.");
            Assert.That(Mathf.Abs(game.Swarm.Center.z - swarmStartZ), Is.LessThan(0.75f),
                "Recycling a wall must not teleport or force-translate the flock.");
            Assert.That(game.Swarm.AliveCount, Is.GreaterThan(0));

            Object.Destroy(game.gameObject);
            yield return null;
        }
    }
}
