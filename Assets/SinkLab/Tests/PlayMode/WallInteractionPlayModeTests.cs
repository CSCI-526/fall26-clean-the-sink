using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SinkLab.Tests
{
    /// <summary>Exercises native contacts using the saved level's food, walls, floor and materials.</summary>
    public sealed class WallInteractionPlayModeTests
    {
        const float PhysicsStepSeconds = .005f;
        const float HardImpactSpeed = 4f;
        const float GentleImpactSpeed = .75f;

        Scene _previousActiveScene;
        Scene _testScene;
        PhysicsScene _physicsScene;
        SinkWorld _world;
        FoodScrap _food;
        SphereCollider _foodCollider;
        BoxCollider _wall;
        BoxCollider _floor;
        PhysicsMaterial _dryFoodMaterial;
        readonly List<GameObject> _suspendedWorlds = new List<GameObject>();
        readonly List<SinkPlayer> _controlledPlayers = new List<SinkPlayer>();
        float _previousTimeScale;
        bool _previousRunInBackground;
        CursorLockMode _previousCursorLock;
        bool _previousCursorVisible;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousTimeScale = Time.timeScale;
            _previousRunInBackground = Application.runInBackground;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            Time.timeScale = 1f;
            Application.runInBackground = true;

            foreach (SinkWorld existing in Object.FindObjectsByType<SinkWorld>(FindObjectsSortMode.None))
            {
                if (!existing.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (existing.player != null && existing.player.HasControl)
                {
                    _controlledPlayers.Add(existing.player);
                }
                _suspendedWorlds.Add(existing.gameObject);
                existing.gameObject.SetActive(false);
            }

            _previousActiveScene = SceneManager.GetActiveScene();
            _testScene = SceneManager.CreateScene(
                "Wall interaction regression", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            SceneManager.SetActiveScene(_testScene);
            _physicsScene = _testScene.GetPhysicsScene();
            Assert.That(_physicsScene.IsValid(), Is.True);

            _world = PlayModePrefabFactory.InstantiateLevel();
            _world.transform.position = new Vector3(40f, 0f, 30f);
            _world.player.enabled = false;
            _world.water.enabled = false;
            _world.water.SetSpraying(false);
            _world.drain.enabled = false;
            Assert.That(_world.basin, Is.Not.Null, "The saved level must initialize its production basin.");
            _world.basin.enabled = false;

            foreach (WaterVisuals visuals in _world.GetComponentsInChildren<WaterVisuals>())
            {
                visuals.enabled = false;
            }
            foreach (SinkHUD hud in _world.GetComponentsInChildren<SinkHUD>())
            {
                hud.enabled = false;
            }

            foreach (FoodScrap candidate in _world.foods)
            {
                SphereCollider sphere = candidate.GetComponent<SphereCollider>();
                if (_food == null && sphere != null)
                {
                    _food = candidate;
                    _foodCollider = sphere;
                }
                else
                {
                    candidate.gameObject.SetActive(false);
                }
            }

            Assert.That(_food, Is.Not.Null, "The saved level must include a spherical scrap.");
            _wall = RequireBoxCollider("Walls/Basin wall right");
            _floor = RequireBoxCollider("Floor/Basin floor left");
            _dryFoodMaterial = _foodCollider.sharedMaterial;
            Assert.That(_dryFoodMaterial, Is.Not.Null);
            Assert.That(Physics.bounceThreshold, Is.EqualTo(2f).Within(.0001f),
                "Exercise the production threshold; the fixture must not lower it to manufacture bounce.");

            yield return null;
            Physics.SyncTransforms();
            _food.Body.useGravity = false;
            PositionFoodForWallImpact();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_world != null)
            {
                _world.water.SetSpraying(false);
                _world.basin.enabled = false;
            }
            if (_previousActiveScene.IsValid() && _previousActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(_previousActiveScene);
            }
            if (_testScene.IsValid() && _testScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_testScene);
            }
            foreach (GameObject previousWorld in _suspendedWorlds)
            {
                if (previousWorld != null)
                {
                    previousWorld.SetActive(true);
                }
            }
            foreach (SinkPlayer previousPlayer in _controlledPlayers)
            {
                if (previousPlayer != null)
                {
                    previousPlayer.SetControl(true);
                }
            }
            _suspendedWorlds.Clear();
            _controlledPlayers.Clear();
            Time.timeScale = _previousTimeScale;
            Application.runInBackground = _previousRunInBackground;
            Cursor.lockState = _previousCursorLock;
            Cursor.visible = _previousCursorVisible;
        }

        [UnityTest]
        public IEnumerator HardDryWallImpactReboundsIntoBasin()
        {
            AssertWallResponse(HardImpactSpeed, true);
            yield break;
        }

        [UnityTest]
        public IEnumerator HardWetWallImpactReboundsIntoBasin()
        {
            yield return WetFoodThroughProductionBasin();
            AssertWallResponse(HardImpactSpeed, true);
        }

        [UnityTest]
        public IEnumerator GentleDryWallContactDoesNotSustainBounce()
        {
            AssertWallResponse(GentleImpactSpeed, false);
            yield break;
        }

        [UnityTest]
        public IEnumerator GentleWetWallContactDoesNotSustainBounce()
        {
            yield return WetFoodThroughProductionBasin();
            AssertWallResponse(GentleImpactSpeed, false);
        }

        [UnityTest]
        public IEnumerator HardDryFloorImpactDoesNotBounce()
        {
            AssertFloorResponse();
            yield break;
        }

        [UnityTest]
        public IEnumerator HardWetFloorImpactDoesNotBounce()
        {
            yield return WetFoodThroughProductionBasin();
            AssertFloorResponse();
        }

        IEnumerator WetFoodThroughProductionBasin()
        {
            // Keep the body still while real fill updates assign its wet material. Pausing basin
            // forces afterward isolates contact restitution from buoyancy and water currents.
            _food.Body.isKinematic = true;
            _world.drain.SetOpen(false);
            _world.water.Pressure = 1f;
            _world.water.WideSpray = false;
            _world.water.SetSpraying(true);
            _world.basin.enabled = true;

            float deadline = Time.realtimeSinceStartup + 8f;
            while (_world.basin.NormalizedLevel < .6f && Time.realtimeSinceStartup < deadline)
            {
                yield return new WaitForFixedUpdate();
            }

            _world.water.SetSpraying(false);
            _world.basin.enabled = false;
            Assert.That(_world.basin.NormalizedLevel, Is.GreaterThanOrEqualTo(.6f),
                "The production fill path must actually submerge this scrap.");
            Assert.That(_foodCollider.sharedMaterial, Is.Not.SameAs(_dryFoodMaterial),
                "The wet case must use the material assigned by BasinWater, not the dry material.");
            Assert.That(_foodCollider.sharedMaterial.dynamicFriction, Is.LessThanOrEqualTo(.025f));
            Assert.That(_foodCollider.sharedMaterial.bounciness, Is.Zero,
                "The food itself must not be made bouncy to pass the wall test.");
            _food.Body.isKinematic = false;
            ResetMotion();
        }

        void AssertWallResponse(float incomingSpeed, bool shouldRebound)
        {
            PositionFoodForWallImpact();
            float contactCenterX = _wall.bounds.min.x - _foodCollider.bounds.extents.x;
            _food.Body.linearVelocity = Vector3.right * incomingSpeed;

            float furthestCenterX = _food.Body.worldCenterOfMass.x;
            float strongestReboundSpeed = 0f;
            for (int step = 0; step < 500; step++)
            {
                _physicsScene.Simulate(PhysicsStepSeconds);
                furthestCenterX = Mathf.Max(furthestCenterX, _food.Body.worldCenterOfMass.x);
                strongestReboundSpeed = Mathf.Max(strongestReboundSpeed, -_food.Body.linearVelocity.x);
            }

            Assert.That(furthestCenterX, Is.GreaterThan(contactCenterX - .025f),
                "The scrap must reach the actual saved wall; free-flight slowing is not a contact test.");
            Assert.That(furthestCenterX, Is.LessThan(contactCenterX + .025f),
                "The scrap must not tunnel through the wall.");
            if (shouldRebound)
            {
                Assert.That(strongestReboundSpeed, Is.GreaterThan(.5f),
                    "An impact above the production threshold must physically rebound inward.");
            }
            else
            {
                Assert.That(strongestReboundSpeed, Is.LessThan(.12f),
                    "A gentle contact must not acquire a visible restitution kick.");
                Assert.That(Mathf.Abs(_food.Body.linearVelocity.x), Is.LessThan(.05f),
                    "Gentle contact must settle instead of sustaining motion.");
            }
        }

        void AssertFloorResponse()
        {
            ResetMotion();
            float contactCenterY = _floor.bounds.max.y + _foodCollider.bounds.extents.y;
            PlaceFoodCenter(new Vector3(_floor.bounds.center.x, contactCenterY + .12f, _floor.bounds.center.z));
            _food.Body.linearVelocity = Vector3.down * HardImpactSpeed;

            float lowestCenterY = _food.Body.worldCenterOfMass.y;
            float strongestUpwardSpeed = 0f;
            for (int step = 0; step < 200; step++)
            {
                _physicsScene.Simulate(PhysicsStepSeconds);
                lowestCenterY = Mathf.Min(lowestCenterY, _food.Body.worldCenterOfMass.y);
                strongestUpwardSpeed = Mathf.Max(strongestUpwardSpeed, _food.Body.linearVelocity.y);
            }

            Assert.That(lowestCenterY, Is.InRange(contactCenterY - .025f, contactCenterY + .025f),
                "The scrap must land on the saved floor without falling through it.");
            Assert.That(strongestUpwardSpeed, Is.LessThan(.12f),
                "Wall restitution must not turn ordinary basin-floor contacts into bouncing.");
        }

        void PositionFoodForWallImpact()
        {
            ResetMotion();
            Physics.SyncTransforms();
            float contactCenterX = _wall.bounds.min.x - _foodCollider.bounds.extents.x;
            PlaceFoodCenter(new Vector3(contactCenterX - .12f, _wall.bounds.center.y, _wall.bounds.center.z));
        }

        void PlaceFoodCenter(Vector3 center)
        {
            _food.Body.position += center - _food.Body.worldCenterOfMass;
            _food.transform.position = _food.Body.position;
            Physics.SyncTransforms();
        }

        void ResetMotion()
        {
            _food.Body.linearVelocity = Vector3.zero;
            _food.Body.angularVelocity = Vector3.zero;
            _food.Body.WakeUp();
        }

        BoxCollider RequireBoxCollider(string path)
        {
            Transform piece = _world.sink.transform.Find(path);
            Assert.That(piece, Is.Not.Null, "Saved sink is missing " + path);
            BoxCollider collider = piece.GetComponent<BoxCollider>();
            Assert.That(collider, Is.Not.Null, path);
            return collider;
        }
    }
}
