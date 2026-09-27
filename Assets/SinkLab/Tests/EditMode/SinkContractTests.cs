using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkLab.Tests
{
    /// <summary>
    /// Contracts exercised against the same populated world used by the playable scene.
    /// These tests deliberately control time, input, and aim instead of simulating a mouse.
    /// </summary>
    public sealed class SinkContractTests
    {
        SinkWorld world;
        Drain drain;
        Camera testCamera;
        Transform testNozzle;
        SceneSetup[] previousScenes;
        SimulationMode previousSimulationMode;
        bool previousAutoSyncTransforms;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        bool restoreEnvironment;

        [SetUp]
        public void SetUp()
        {
            // Physics.Raycast uses the default physics scene. Starting with a separate
            // empty scene prevents a saved sink from becoming an invisible duplicate.
            // Never discard the user's unsaved scene to achieve that isolation.
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False,
                    "Save loaded scenes before running the sink contract tests.");
            }

            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            previousSimulationMode = Physics.simulationMode;
            previousAutoSyncTransforms = Physics.autoSyncTransforms;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            restoreEnvironment = true;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.autoSyncTransforms = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PruneDestroyedRegistrations();
                CreateTestWorld();
            }
            catch
            {
                // NUnit does not promise TearDown when SetUp itself fails.
                TearDown();
                throw;
            }
        }

        void CreateTestWorld()
        {
            world = EditModePrefabFactory.InstantiateLevel();

            Assert.That(world, Is.Not.Null);
            Assert.That(world.foods, Is.Not.Null.And.Not.Empty,
                "A vacuous empty world cannot establish the cleaning contracts.");
            Assert.That(world.stains, Is.Not.Null.And.Not.Empty);
            Assert.That(world.water, Is.Not.Null);
            drain = world.GetComponentInChildren<Drain>(true);
            Assert.That(drain, Is.Not.Null);

            // EditMode does not guarantee MonoBehaviour Start/OnEnable callbacks.
            // Supply their lifecycle setup without replacing any gameplay components.
            foreach (FoodScrap food in world.foods)
            {
                food.CaptureSpawn();
                FoodScrap.Active.Add(food);
            }
            foreach (StainPatch stain in world.stains)
            {
                StainPatch.Active.Add(stain);
            }
            testCamera = new GameObject("Contract test aim camera").AddComponent<Camera>();
            testCamera.enabled = false;
            testCamera.nearClipPlane = .01f;
            testNozzle = new GameObject("Contract test nozzle").transform;
            world.water.aimCamera = testCamera;
            world.water.nozzle = testNozzle;
            world.water.Pressure = 1f;
            world.water.SetSpraying(false);
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (!restoreEnvironment)
            {
                return;
            }

            try
            {
                if (world != null)
                {
                    foreach (FoodScrap food in world.foods ?? Array.Empty<FoodScrap>())
                    {
                        FoodScrap.Active.Remove(food);
                    }
                    foreach (StainPatch stain in world.stains ?? Array.Empty<StainPatch>())
                    {
                        StainPatch.Active.Remove(stain);
                    }
                }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PruneDestroyedRegistrations();
                if (previousScenes != null && previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                }
            }
            finally
            {
                Physics.autoSyncTransforms = previousAutoSyncTransforms;
                Physics.simulationMode = previousSimulationMode;
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
                restoreEnvironment = false;
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void CompletionRequiresEveryFoodAndEveryStain_InEitherOrder(bool foodFirst)
        {
            Assert.That(world.IsComplete, Is.False);
            Assert.That(world.FoodRemaining, Is.EqualTo(world.foods.Length));
            Assert.That(world.StainsRemaining, Is.EqualTo(world.stains.Length));

            if (foodFirst)
            {
                foreach (FoodScrap food in world.foods)
                {
                    Consume(food);
                }
                Assert.That(world.FoodRemaining, Is.Zero);
                Assert.That(world.IsComplete, Is.False, "Unwashed stains must block completion.");
                for (int i = 0; i < world.stains.Length - 1; i++)
                {
                    world.stains[i].Wash(100f);
                }
                Assert.That(world.StainsRemaining, Is.EqualTo(1));
                Assert.That(world.IsComplete, Is.False);
                world.stains[world.stains.Length - 1].Wash(100f);
            }
            else
            {
                foreach (StainPatch stain in world.stains)
                {
                    stain.Wash(100f);
                }
                Assert.That(world.StainsRemaining, Is.Zero);
                Assert.That(world.IsComplete, Is.False, "Undrained food must block completion.");
                for (int i = 0; i < world.foods.Length - 1; i++)
                {
                    Consume(world.foods[i]);
                }
                Assert.That(world.FoodRemaining, Is.EqualTo(1));
                Assert.That(world.IsComplete, Is.False);
                Consume(world.foods[world.foods.Length - 1]);
            }

            Assert.That(world.FoodRemaining, Is.Zero);
            Assert.That(world.StainsRemaining, Is.Zero);
            Assert.That(world.IsComplete, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void DestroyedExpectedObjectRemainsUnfinished_WhenEverythingElseIsClean(bool destroyFood)
        {
            // Retain the original expected-object slot. Unity's destroyed-object null
            // semantics must not turn accidental disappearance into cleaning progress.
            if (destroyFood)
            {
                UnityEngine.Object.DestroyImmediate(world.foods[0].gameObject);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(world.stains[0].gameObject);
            }
            for (int i = destroyFood ? 1 : 0; i < world.foods.Length; i++)
            {
                Consume(world.foods[i]);
            }
            for (int i = destroyFood ? 0 : 1; i < world.stains.Length; i++)
            {
                world.stains[i].Wash(100f);
            }
            Assert.That(world.FoodRemaining, Is.EqualTo(destroyFood ? 1 : 0));
            Assert.That(world.StainsRemaining, Is.EqualTo(destroyFood ? 0 : 1));
            Assert.That(world.IsComplete, Is.False,
                "Every expected object must have reached its actual clean/drained state.");
        }

        [Test]
        public void DrainRejectsDistantAndHighFood_ThenConsumesOnceInsideOpening()
        {
            FoodScrap food = world.foods[0];
            Vector3 center = DrainCenter();
            int initialCount = drain.DrainedCount;

            PlaceFoodCenter(food, new Vector3(center.x + drain.radius + .5f,
                drain.captureHeight - .1f, center.z));
            Assert.That(drain.TryConsume(food), Is.False, "Being below the floor is insufficient.");
            Assert.That(food.IsDrained, Is.False);

            PlaceFoodCenter(food, new Vector3(center.x, drain.captureHeight + .5f, center.z));
            Assert.That(drain.TryConsume(food), Is.False, "Being directly above the drain is insufficient.");
            Assert.That(food.IsDrained, Is.False);
            Assert.That(drain.DrainedCount, Is.EqualTo(initialCount));

            Consume(food);
            Assert.That(drain.DrainedCount, Is.EqualTo(initialCount + 1));
            Assert.That(drain.TryConsume(food), Is.False, "A drained scrap must not count twice.");
            Assert.That(drain.DrainedCount, Is.EqualTo(initialCount + 1));
        }

        [Test]
        public void CircularOpeningBlocksSquareCorners_AndLetsFoodFallThroughItsCenter()
        {
            Assert.That(drain.squareOpening, Is.False,
                "Saved capture bounds must match the authored circular opening before Play.");
            ParkFoodClearOfSink();
            FoodScrap food = world.foods[0];
            // Keep the actual collider shape while giving this aperture check generous clearance.
            food.transform.localScale *= 0.5f;
            Vector3 center = DrainCenter();
            float diagonalOffset = drain.radius * 0.85f;
            PlaceFoodCenter(food, center + new Vector3(diagonalOffset, 0.22f, diagonalOffset));
            food.Body.useGravity = true;
            food.Body.isKinematic = false;
            food.Body.linearVelocity = Vector3.zero;
            food.Body.angularVelocity = Vector3.zero;
            food.Body.WakeUp();

            for (int step = 0; step < 100; step++)
            {
                Physics.Simulate(0.02f);
                drain.TryConsume(food);
            }

            Assert.That(food.IsDrained, Is.False,
                "The area outside the circle must be solid even within the former square gap.");
            Assert.That(food.Body.worldCenterOfMass.y, Is.GreaterThan(drain.captureHeight),
                "The saved opening collider must support the scrap above capture depth.");

            PlaceFoodCenter(food, center + Vector3.up * 0.22f);
            food.Body.linearVelocity = Vector3.zero;
            food.Body.angularVelocity = Vector3.zero;
            food.Body.WakeUp();
            Assert.That(drain.TryConsume(food), Is.False,
                "Being above the opening must not count as collection.");

            // EditMode does not call FixedUpdate. Apply only the production collection check
            // after each real physics step; the body must fall through the saved geometry.
            for (int step = 0; step < 100 && !food.IsDrained; step++)
            {
                Physics.Simulate(0.02f);
                drain.TryConsume(food);
            }

            Assert.That(food.IsDrained, Is.True);
            Assert.That(drain.DrainedCount, Is.EqualTo(1));
            Assert.That(food.Body.worldCenterOfMass.y, Is.LessThan(drain.captureHeight));
        }

        [Test]
        public void DrainAcceptanceFollowsATranslatedAndYawRotatedLevel()
        {
            world.transform.SetPositionAndRotation(new Vector3(5f, 2f, 7f), Quaternion.Euler(0f, 37f, 0f));
            Physics.SyncTransforms();
            Transform frame = drain.drainCenter != null ? drain.drainCenter : drain.transform;
            FoodScrap food = world.foods[0];
            PlaceFoodCenter(food, frame.TransformPoint(new Vector3(drain.radius + .2f, -.15f, 0f)));
            Assert.That(drain.TryConsume(food), Is.False);
            PlaceFoodCenter(food, frame.TransformPoint(new Vector3(0f, .1f, 0f)));
            Assert.That(drain.TryConsume(food), Is.False);
            PlaceFoodCenter(food, frame.TransformPoint(new Vector3(drain.radius * .85f, -.15f, drain.radius * .85f)));
            Assert.That(drain.TryConsume(food), Is.False,
                "A point in the former square corner lies outside the circular capture area.");
            PlaceFoodCenter(food, frame.TransformPoint(new Vector3(drain.radius * .45f, -.15f, drain.radius * .45f)));
            Assert.That(drain.TryConsume(food), Is.True,
                "A reusable sink's opening and capture depth must follow its transform.");
            Assert.That(food.IsDrained, Is.True);
        }

        [Test]
        public void DrainRejectsForeignAndUnownedFoodEvenInsideItsOpening()
        {
            SinkWorld other = EditModePrefabFactory.InstantiateLevel();
            other.transform.position = new Vector3(12f, 0f, 0f);
            FoodScrap foreign = other.foods[0];
            foreign.CaptureSpawn();
            Vector3 center = DrainCenter();
            center.y = drain.captureHeight - .1f;
            PlaceFoodCenter(foreign, center);
            Assert.That(drain.TryConsume(foreign), Is.False,
                "A level must not collect food owned by a different level.");
            Assert.That(foreign.IsDrained, Is.False);
            Assert.That(drain.DrainedCount, Is.Zero);
            FoodScrap unowned = EditModePrefabFactory.Instantiate("Mess/FoodCube.prefab").GetComponent<FoodScrap>();
            unowned.CaptureSpawn();
            PlaceFoodCenter(unowned, center);
            Assert.That(drain.TryConsume(unowned), Is.False,
                "An unowned scene object must not become this level's cleaning progress.");
            Consume(world.foods[0]);
            Assert.That(drain.DrainedCount, Is.EqualTo(1));
        }

        [Test]
        public void OwnedWaterWashesItsOwnStainButNotAnOverlappingForeignLevelsStain()
        {
            ParkFoodClearOfSink();
            SinkWorld other = EditModePrefabFactory.InstantiateLevel();
            other.transform.position = new Vector3(12f, 0f, 0f);
            StainPatch own = world.stains[0];
            StainPatch foreign = other.stains[0];
            foreign.transform.position = own.transform.position;
            StainPatch.Active.Add(foreign);
            AimAt(own.transform.position, new Vector3(0f, .75f, -.25f));
            SpraySteps(4, .05f, true);
            Assert.That(own.Remaining, Is.LessThan(1f), "The overlapping aim must actually wash the owned stain.");
            Assert.That(foreign.Remaining, Is.EqualTo(1f),
                "Water effects must stay within the level that owns the faucet.");
        }

        [Test]
        public void OwnedWaterCannotPushAnotherLevelsFood_WithOwnedFoodPositiveControl()
        {
            FoodScrap owned = SuspendFoodForForceTest();
            PlaceFoodCenter(owned, new Vector3(10f, 4f, 9f));
            SinkWorld other = EditModePrefabFactory.InstantiateLevel();
            other.transform.position = new Vector3(12f, 0f, 0f);
            FoodScrap foreign = other.foods[0];
            foreign.CaptureSpawn();
            foreign.Body.useGravity = false;
            foreign.Body.linearVelocity = Vector3.zero;
            foreign.Body.angularVelocity = Vector3.zero;
            PlaceFoodCenter(foreign, new Vector3(0f, 4f, 0f));
            FoodScrap.Active.Add(foreign);
            Vector3 original = foreign.Body.position;
            world.water.SetSpraying(true);
            for (int i = 0; i < 6; i++)
            {
                world.water.SimulateSpray(.02f);
                Physics.Simulate(.02f);
            }
            Assert.That(Vector3.Distance(foreign.Body.position, original), Is.LessThan(.0001f));
            Assert.That(foreign.Body.linearVelocity.sqrMagnitude, Is.LessThan(.0000001f));

            PlaceFoodCenter(foreign, new Vector3(10f, 4f, 9f));
            PlaceFoodCenter(owned, new Vector3(0f, 4f, 0f));
            world.water.SimulateSpray(.05f);
            Physics.Simulate(.02f);
            Assert.That(owned.Body.linearVelocity.sqrMagnitude, Is.GreaterThan(.000001f),
                "The same faucet and aim must push a scrap owned by this level.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WaterOffDoesNotWash_AndTurningItOnDoes(bool wide)
        {
            ParkFoodClearOfSink();
            StainPatch stain = world.stains[0];
            AimAt(stain.transform.position, new Vector3(0f, .75f, -.25f));
            world.water.WideSpray = wide;

            SpraySteps(10, .05f, false);
            Assert.That(world.stains.All(item => item.Remaining == 1f), Is.True);
            Assert.That(world.water.HasHit, Is.False);

            SpraySteps(4, .05f, true);
            Assert.That(stain.Remaining, Is.LessThan(1f), "The same unblocked aim must wash when enabled.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WaterOffDoesNotMoveFood_AndTurningItOnTransfersMomentum(bool wide)
        {
            FoodScrap food = SuspendFoodForForceTest();
            world.water.WideSpray = wide;
            Vector3 originalPosition = food.Body.position;

            for (int i = 0; i < 8; i++)
            {
                world.water.SimulateSpray(.02f);
                Physics.Simulate(.02f);
            }
            Assert.That(Vector3.Distance(food.Body.position, originalPosition), Is.LessThan(.0001f));
            Assert.That(food.Body.linearVelocity.sqrMagnitude, Is.LessThan(.0000001f));
            Assert.That(food.Body.angularVelocity.sqrMagnitude, Is.LessThan(.0000001f));

            world.water.SetSpraying(true);
            world.water.SimulateSpray(.05f);
            Physics.Simulate(.02f);
            Assert.That(food.Body.linearVelocity.sqrMagnitude, Is.GreaterThan(.000001f),
                "The same aim must transfer momentum with the faucet on.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SolidOccluderPreventsWashing_WithUnblockedPositiveControl(bool wide)
        {
            ParkFoodClearOfSink();
            StainPatch stain = world.stains[0];
            AimAt(stain.transform.position, new Vector3(0f, .75f, -.25f));
            world.water.WideSpray = wide;
            Collider blocker = BlockPathTo(stain.transform.position);

            SpraySteps(10, .05f, true);
            Assert.That(stain.Remaining, Is.EqualTo(1f), "Water must not wash through a solid surface.");

            blocker.enabled = false;
            Physics.SyncTransforms();
            SpraySteps(4, .05f, true);
            Assert.That(stain.Remaining, Is.LessThan(1f), "Removing only the occluder must expose the stain.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SolidOccluderPreventsFoodImpulse_WithUnblockedPositiveControl(bool wide)
        {
            FoodScrap food = SuspendFoodForForceTest();
            world.water.WideSpray = wide;
            Vector3 originalPosition = food.Body.position;
            Collider blocker = BlockPathTo(food.Body.worldCenterOfMass);

            world.water.SetSpraying(true);
            for (int i = 0; i < 6; i++)
            {
                world.water.SimulateSpray(.02f);
                Physics.Simulate(.02f);
            }
            Assert.That(Vector3.Distance(food.Body.position, originalPosition), Is.LessThan(.0001f));
            Assert.That(food.Body.linearVelocity.sqrMagnitude, Is.LessThan(.0000001f));

            blocker.enabled = false;
            Physics.SyncTransforms();
            world.water.SimulateSpray(.05f);
            Physics.Simulate(.02f);
            Assert.That(food.Body.linearVelocity.sqrMagnitude, Is.GreaterThan(.000001f));
        }

        [Test]
        public void StainWashIgnoresInvalidAmounts_ClampsAtClean_AndResetRestoresAppearance()
        {
            StainPatch stain = world.stains[0];
            Vector3 originalScale = stain.transform.localScale;
            Renderer renderer = stain.GetComponentInChildren<Renderer>(true);
            Assert.That(renderer, Is.Not.Null);

            stain.Wash(.25f);
            Assert.That(stain.Remaining, Is.EqualTo(.75f).Within(.00001f));
            foreach (float invalid in new[] { -10f, 0f, float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                stain.Wash(invalid);
            }
            Assert.That(stain.Remaining, Is.EqualTo(.75f).Within(.00001f));

            stain.Wash(100f);
            stain.Wash(100f);
            Assert.That(stain.Remaining, Is.Zero);
            Assert.That(stain.IsClean, Is.True);
            Assert.That(renderer.enabled, Is.False);

            stain.ResetStain();
            Assert.That(stain.Remaining, Is.EqualTo(1f));
            Assert.That(stain.IsClean, Is.False);
            Assert.That(renderer.enabled, Is.True);
            Assert.That(Vector3.Distance(stain.transform.localScale, originalScale), Is.LessThan(.00001f));
        }

        [TestCase(-20f, 0f)]
        [TestCase(20f, 1f)]
        [TestCase(.37f, .37f)]
        public void PressureStaysInNormalizedRange(float input, float expected)
        {
            world.water.Pressure = input;
            Assert.That(world.water.Pressure, Is.EqualTo(expected).Within(.00001f));
        }

        [Test]
        public void ResetRestoresAllFoodPosesMotionAndVisibility_AndAllStains()
        {
            FoodSnapshot[] snapshots = world.foods.Select(food => new FoodSnapshot(food)).ToArray();
            Vector3[] stainScales = world.stains.Select(stain => stain.transform.localScale).ToArray();

            for (int i = 0; i < world.foods.Length; i++)
            {
                FoodScrap food = world.foods[i];
                food.Body.position += new Vector3(.6f, .3f, -.4f);
                food.Body.rotation = Quaternion.Euler(35f, 82f, -24f);
                food.Body.linearVelocity = new Vector3(1.2f, .4f, -.7f);
                food.Body.angularVelocity = new Vector3(.8f, 1.1f, -.5f);
                if (i % 2 == 0)
                {
                    Consume(food);
                }
            }
            for (int i = 0; i < world.stains.Length; i++)
            {
                world.stains[i].Wash(i % 2 == 0 ? 100f : .35f);
            }
            world.water.SetSpraying(true);

            world.ResetRun();
            Physics.SyncTransforms();

            for (int i = 0; i < world.foods.Length; i++)
            {
                snapshots[i].AssertRestored(world.foods[i]);
            }
            for (int i = 0; i < world.stains.Length; i++)
            {
                StainPatch stain = world.stains[i];
                Assert.That(stain.Remaining, Is.EqualTo(1f), stain.name);
                Assert.That(stain.IsClean, Is.False, stain.name);
                Assert.That(Vector3.Distance(stain.transform.localScale, stainScales[i]), Is.LessThan(.00001f));
                Assert.That(stain.GetComponentInChildren<Renderer>(true).enabled, Is.True);
            }
            Assert.That(drain.DrainedCount, Is.Zero);
            Assert.That(world.FoodRemaining, Is.EqualTo(world.foods.Length));
            Assert.That(world.StainsRemaining, Is.EqualTo(world.stains.Length));
            Assert.That(world.IsComplete, Is.False);
            Assert.That(world.water.IsSpraying, Is.False);
        }

        Vector3 DrainCenter() => drain.drainCenter != null ? drain.drainCenter.position : drain.transform.position;

        static void PruneDestroyedRegistrations()
        {
            FoodScrap.Active.RemoveWhere(item => item == null);
            StainPatch.Active.RemoveWhere(item => item == null);
        }

        void Consume(FoodScrap food)
        {
            Vector3 center = DrainCenter();
            center.y = drain.captureHeight - .1f;
            PlaceFoodCenter(food, center);
            Assert.That(drain.TryConsume(food), Is.True,
                "A scrap inside the opening should be collected: " + food.name);
            Assert.That(food.IsDrained, Is.True);
        }

        static void PlaceFoodCenter(FoodScrap food, Vector3 center)
        {
            food.Body.position += center - food.Body.worldCenterOfMass;
            food.transform.position = food.Body.position;
            Physics.SyncTransforms();
        }

        void ParkFoodClearOfSink()
        {
            for (int i = 0; i < world.foods.Length; i++)
            {
                FoodScrap food = world.foods[i];
                food.Body.useGravity = false;
                food.Body.linearVelocity = Vector3.zero;
                food.Body.angularVelocity = Vector3.zero;
                PlaceFoodCenter(food, new Vector3(10f + i, 4f, 10f));
            }
        }

        FoodScrap SuspendFoodForForceTest()
        {
            ParkFoodClearOfSink();
            FoodScrap food = world.foods[0];
            food.Body.isKinematic = false;
            PlaceFoodCenter(food, new Vector3(0f, 4f, 0f));
            AimAt(food.Body.worldCenterOfMass, new Vector3(0f, .5f, -.75f));
            return food;
        }

        void AimAt(Vector3 target, Vector3 offset)
        {
            testCamera.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(-offset, Vector3.up));
            testNozzle.SetPositionAndRotation(testCamera.transform.position, testCamera.transform.rotation);
            Physics.SyncTransforms();
        }

        Collider BlockPathTo(Vector3 target)
        {
            GameObject blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "Contract test solid occluder";
            blocker.transform.SetPositionAndRotation(Vector3.Lerp(testNozzle.position, target, .5f),
                Quaternion.LookRotation(target - testNozzle.position));
            blocker.transform.localScale = new Vector3(.9f, .9f, .08f);
            Physics.SyncTransforms();
            return blocker.GetComponent<Collider>();
        }

        void SpraySteps(int count, float dt, bool enabled)
        {
            world.water.SetSpraying(enabled);
            for (int i = 0; i < count; i++)
            {
                world.water.SimulateSpray(dt);
            }
        }

        sealed class FoodSnapshot
        {
            readonly Vector3 position;
            readonly Quaternion rotation;
            readonly bool gravity;
            readonly bool kinematic;
            readonly Renderer[] renderers;
            readonly bool[] rendererEnabled;
            readonly Collider[] colliders;
            readonly bool[] colliderEnabled;

            public FoodSnapshot(FoodScrap food)
            {
                position = food.transform.position;
                rotation = food.transform.rotation;
                gravity = food.Body.useGravity;
                kinematic = food.Body.isKinematic;
                renderers = food.GetComponentsInChildren<Renderer>(true);
                rendererEnabled = renderers.Select(item => item.enabled).ToArray();
                colliders = food.GetComponentsInChildren<Collider>(true);
                colliderEnabled = colliders.Select(item => item.enabled).ToArray();
                Assert.That(renderers, Is.Not.Empty);
                Assert.That(colliders, Is.Not.Empty);
            }

            public void AssertRestored(FoodScrap food)
            {
                Assert.That(food.IsDrained, Is.False, food.name);
                Assert.That(food.gameObject.activeSelf, Is.True, food.name);
                Assert.That(Vector3.Distance(food.transform.position, position), Is.LessThan(.00001f), food.name);
                Assert.That(Quaternion.Angle(food.transform.rotation, rotation), Is.LessThan(.05f), food.name);
                Assert.That(food.Body.linearVelocity.sqrMagnitude, Is.LessThan(.0000001f), food.name);
                Assert.That(food.Body.angularVelocity.sqrMagnitude, Is.LessThan(.0000001f), food.name);
                Assert.That(food.Body.useGravity, Is.EqualTo(gravity), food.name);
                Assert.That(food.Body.isKinematic, Is.EqualTo(kinematic), food.name);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Assert.That(renderers[i].enabled, Is.EqualTo(rendererEnabled[i]));
                }
                for (int i = 0; i < colliders.Length; i++)
                {
                    Assert.That(colliders[i].enabled, Is.EqualTo(colliderEnabled[i]));
                }
            }
        }
    }
}
