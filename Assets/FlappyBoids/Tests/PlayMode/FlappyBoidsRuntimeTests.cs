using System.Collections;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FlappyBoids.Tests
{
    public sealed class FlappyBoidsRuntimeTests
    {
        [UnityTest]
        public IEnumerator RuntimeBootstrap_CreatesAndAdvancesDotsFlock()
        {
            SceneManager.LoadScene("FlappyBoids");
            yield return null;
            yield return null;
            FlappyBoidsGame game = Object.FindAnyObjectByType<FlappyBoidsGame>();

            Assert.That(game, Is.Not.Null, "The authored FlappyBoids scene must contain the game root.");
            Assert.That(game.Swarm, Is.Not.Null);
            Assert.That(Object.FindAnyObjectByType<Canvas>(), Is.Not.Null,
                "The HUD must be a scene-authored Canvas, not runtime-only IMGUI.");
            Assert.That(Object.FindObjectsByType<GateWall>(FindObjectsSortMode.None).Length,
                Is.EqualTo(FlappyBoidsGame.TotalGates));
            Assert.That(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Length,
                Is.GreaterThan(0), "The authored scene must contain bubbling particle VFX.");
            AudioSource[] audioSources = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            bool foundAmbientLoop = false;
            for (int i = 0; i < audioSources.Length; i++)
                foundAmbientLoop |= audioSources[i].loop && audioSources[i].clip != null;
            Assert.That(foundAmbientLoop, Is.True, "The authored underwater ambience must be assigned and looped.");
            Assert.That(game.Swarm.AliveCount, Is.EqualTo(BoidSwarm.StartingBoids));

            EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            EntityQuery query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<BoidAgent>());
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(BoidSwarm.StartingBoids));

            float startZ = game.Swarm.Center.z;
            game.Swarm.Begin();
            game.Swarm.SetInput(1f, true);
            for (int i = 0; i < 12; i++) yield return null;

            Assert.That(game.Swarm.Center.z, Is.GreaterThan(startZ), "ECS flock should advance down the course.");
            Assert.That(query.CalculateEntityCount(), Is.EqualTo(BoidSwarm.StartingBoids),
                "No boid should be lost before the first gate.");
            query.Dispose();

            Object.Destroy(game.gameObject);
            yield return null;
        }
    }
}
