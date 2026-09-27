using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkLab.Tests
{
    /// <summary>Corner location never permits water impulses through a solid obstruction.</summary>
    public sealed class WaterOcclusionRegressionTests
    {
        SceneSetup[] _previousScenes;
        SimulationMode _previousSimulationMode;
        bool _previousAutoSyncTransforms;
        bool _restoreEnvironment;
        SinkWorld _world;
        FoodScrap _food;
        Camera _aimCamera;
        Transform _nozzle;
        Collider _blocker;
        Vector3 _floorImpact;

        [SetUp]
        public void SetUp()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Assert.That(SceneManager.GetSceneAt(index).isDirty, Is.False,
                    "Save loaded scenes before running the water occlusion regressions.");
            }

            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            _previousSimulationMode = Physics.simulationMode;
            _previousAutoSyncTransforms = Physics.autoSyncTransforms;
            _restoreEnvironment = true;
            try
            {
                Physics.simulationMode = SimulationMode.Script;
                Physics.autoSyncTransforms = false;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                CreateCornerFixture();
            }
            catch
            {
                TearDown();
                throw;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (!_restoreEnvironment)
            {
                return;
            }

            try
            {
                if (_world != null)
                {
                    Object.DestroyImmediate(_world.gameObject);
                }
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                FoodScrap.Active.RemoveWhere(food => food == null);
                StainPatch.Active.RemoveWhere(stain => stain == null);
                if (_previousScenes != null && _previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(_previousScenes);
                }
            }
            finally
            {
                Physics.autoSyncTransforms = _previousAutoSyncTransforms;
                Physics.simulationMode = _previousSimulationMode;
                _restoreEnvironment = false;
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CornerFloorSplashCannotCrossBlocker_WithUnblockedFloorPositiveControl(bool wide)
        {
            _world.water.WideSpray = wide;
            AimAt(_floorImpact, new Vector3(-.2f, .5f, 0f));
            AssertBlockedSprayLeavesFoodStill();
            Assert.That(_world.water.LastHitNormal.y, Is.GreaterThan(.99f),
                "The spray must hit the floor; the blocker only interrupts its route to the scrap.");
            AssertUnblockedFloorSplashPushesFood();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void WallHitCannotPushFoodInCorner_WithUnblockedFloorPositiveControl(bool wide)
        {
            _world.water.WideSpray = wide;
            AimAt(_food.Body.worldCenterOfMass, Vector3.left * .7f);
            AssertBlockedSprayLeavesFoodStill();
            Assert.That(Mathf.Abs(_world.water.LastHitNormal.y), Is.LessThan(.01f),
                "This case must actually strike the vertical blocker near the corner scrap.");
            AssertUnblockedFloorSplashPushesFood();
        }

        [Test]
        public void IndirectGuidanceKeepsRaisedFoodDisplacementHorizontal()
        {
            Vector3 guidance = _world.water.GuidePush(
                new Vector3(0f, -1f, 1f), new Vector3(.05f, .2f, .1f), false);

            Assert.That(guidance.y, Is.EqualTo(0f).Within(.00001f),
                "A scrap above the impact must not add lift to ground flow.");
            Assert.That(guidance.z, Is.GreaterThan(.8f), "Flow must retain downstream guidance.");
            Assert.That(guidance.x, Is.GreaterThan(0f), "Horizontal lateral guidance must remain available.");
        }

        void CreateCornerFixture()
        {
            FoodScrap.Active.RemoveWhere(food => food == null);
            StainPatch.Active.RemoveWhere(stain => stain == null);
            _world = EditModePrefabFactory.InstantiateLevel();
            _world.transform.position = new Vector3(40f, 0f, 30f);

            foreach (FoodScrap candidate in _world.foods)
            {
                candidate.CaptureSpawn();
                if (_food == null && candidate.GetComponent<SphereCollider>() != null)
                {
                    _food = candidate;
                    FoodScrap.Active.Add(candidate);
                }
                else
                {
                    candidate.gameObject.SetActive(false);
                    FoodScrap.Active.Remove(candidate);
                }
            }
            Assert.That(_food, Is.Not.Null, "The saved level must supply the regression's physical scrap.");

            // This elevated stage keeps the saved basin geometry out of the ray paths, while the
            // scrap's sink-local X/Z remains inside the former corner-assistance zone.
            Transform sink = _world.sink.transform;
            Vector3 foodCenter = sink.TransformPoint(new Vector3(1.18f, 4.1f, .73f));
            _food.Body.useGravity = false;
            _food.Body.isKinematic = false;
            _food.Body.linearVelocity = Vector3.zero;
            _food.Body.angularVelocity = Vector3.zero;
            _food.Body.position += foodCenter - _food.Body.worldCenterOfMass;
            _food.transform.position = _food.Body.position;

            CreateBox("Regression floor", new Vector3(1.1f, 3.9f, .73f), new Vector3(1f, .2f, 1f));
            _blocker = CreateBox(
                "Regression solid blocker", new Vector3(1.08f, 4.2f, .73f), new Vector3(.025f, .4f, .3f));
            _floorImpact = sink.TransformPoint(new Vector3(.96f, 4f, .73f));

            _aimCamera = new GameObject("Regression aim camera").AddComponent<Camera>();
            _aimCamera.enabled = false;
            _aimCamera.nearClipPlane = .01f;
            _nozzle = new GameObject("Regression nozzle").transform;
            _world.water.aimCamera = _aimCamera;
            _world.water.nozzle = _nozzle;
            _world.water.Pressure = 1f;
            _world.water.SetSpraying(false);
            Physics.SyncTransforms();

            Assert.That(_food.GetComponent<Collider>().bounds.min.x, Is.GreaterThan(_blocker.bounds.max.x),
                "The scrap must not touch the blocker before water is applied.");
        }

        Collider CreateBox(string name, Vector3 localPosition, Vector3 scale)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(_world.sink.transform, false);
            box.transform.localPosition = localPosition;
            box.transform.localScale = scale;
            return box.GetComponent<Collider>();
        }

        void AimAt(Vector3 target, Vector3 offset)
        {
            Quaternion rotation = Quaternion.LookRotation(-offset, Vector3.up);
            _aimCamera.transform.SetPositionAndRotation(target + offset, rotation);
            _nozzle.SetPositionAndRotation(_aimCamera.transform.position, rotation);
            Physics.SyncTransforms();
        }

        void AssertBlockedSprayLeavesFoodStill()
        {
            Vector3 initialPosition = _food.Body.position;
            _world.water.SetSpraying(true);
            _world.water.SimulateSpray(.05f);
            Assert.That(_world.water.HasHit, Is.True, "An actual collider must receive the spray.");
            Physics.Simulate(.02f);

            Assert.That(_food.Body.linearVelocity.sqrMagnitude, Is.LessThan(.0000001f),
                "Corner location must not bypass the solid obstruction.");
            Assert.That(Vector3.Distance(_food.Body.position, initialPosition), Is.LessThan(.0001f));
        }

        void AssertUnblockedFloorSplashPushesFood()
        {
            _blocker.enabled = false;
            AimAt(_floorImpact, new Vector3(-.2f, .5f, 0f));
            _world.water.SimulateSpray(.05f);
            Assert.That(_world.water.LastHitNormal.y, Is.GreaterThan(.99f));
            Physics.Simulate(.02f);

            Assert.That(_food.Body.linearVelocity.x, Is.GreaterThan(.001f),
                "Removing the obstruction must allow the same production floor flow to move the scrap.");
        }
    }
}
