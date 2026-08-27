using System.Collections;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

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
            Assert.That(Object.FindObjectsByType<GateWall>().Length,
                Is.EqualTo(FlappyBoidsGame.GatePoolSize),
                "The infinite course should reuse a fixed-size gate pool.");
            Assert.That(Object.FindAnyObjectByType<InfiniteSeaTerrain>(), Is.Not.Null,
                "The old corridor must be replaced by the recyclable Marching Cubes sea terrain.");
            Assert.That(Object.FindObjectsByType<MarchingCubesSeaChunk>().Length, Is.EqualTo(12));
            GateWall[] gates = Object.FindObjectsByType<GateWall>();
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
            Assert.That(game.Swarm.AliveCount, Is.EqualTo(BoidSwarm.StartingBoids));

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

            GateWall[] recycledGates = Object.FindObjectsByType<GateWall>();
            float recycledFurthestZ = float.MinValue;
            for (int i = 0; i < recycledGates.Length; i++)
                recycledFurthestZ = Mathf.Max(recycledFurthestZ, recycledGates[i].Z);

            Assert.That(game.WallsPassed, Is.GreaterThanOrEqualTo(1));
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
