using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SinkLab.Tests
{
    /// <summary>
    /// Exercises standing water on real level prefabs, independently of player input and jet impulses.
    /// </summary>
    public sealed class WaterMechanicsPlayModeTests
    {
        readonly List<GameObject> _suspendedWorlds = new List<GameObject>();
        readonly List<SinkPlayer> _controlledPlayers = new List<SinkPlayer>();
        readonly List<SinkWorld> _testWorlds = new List<SinkWorld>();
        SinkWorld _baselineWorld;
        SinkWorld _raisedWorld;
        float _previousTimeScale;
        bool _previousRunInBackground;
        SimulationMode _previousSimulationMode;
        CursorLockMode _previousCursorLock;
        bool _previousCursorVisible;
        bool _settingsCaptured;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _previousTimeScale = Time.timeScale;
            _previousRunInBackground = Application.runInBackground;
            _previousSimulationMode = Physics.simulationMode;
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _settingsCaptured = true;
            Time.timeScale = 1f;
            Application.runInBackground = true;
            Physics.simulationMode = SimulationMode.FixedUpdate;

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

            _baselineWorld = CreateWorld(Vector3.zero, Quaternion.identity);
            // Keep the two complete room prefabs apart while exercising both translation and yaw.
            _raisedWorld = CreateWorld(new Vector3(30f, 2f, 30f), Quaternion.Euler(0f, 37f, 0f));
            yield return null;
            yield return new WaitForFixedUpdate();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (SinkWorld world in _testWorlds)
            {
                if (world != null)
                {
                    Object.Destroy(world.gameObject);
                }
            }
            _testWorlds.Clear();
            yield return null;

            foreach (GameObject previousWorld in _suspendedWorlds)
            {
                if (previousWorld != null)
                {
                    previousWorld.SetActive(true);
                }
            }
            _suspendedWorlds.Clear();

            foreach (SinkPlayer previousPlayer in _controlledPlayers)
            {
                if (previousPlayer != null)
                {
                    previousPlayer.SetControl(true);
                }
            }
            _controlledPlayers.Clear();

            if (_settingsCaptured)
            {
                Time.timeScale = _previousTimeScale;
                Application.runInBackground = _previousRunInBackground;
                Physics.simulationMode = _previousSimulationMode;
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
                _settingsCaptured = false;
            }
        }

        [UnityTest]
        public IEnumerator WaterSurfaceAndVortexFollowTranslatedYawRotatedSink()
        {
            Assert.That(_baselineWorld.sink.transform.Find("Basin water").gameObject.activeSelf, Is.False);
            Assert.That(_raisedWorld.sink.transform.Find("Basin water").gameObject.activeSelf, Is.False,
                "Raising an empty sink must not give its water volume a positive depth.");

            yield return FillBothBasins(0.65f);
            Assert.That(_raisedWorld.basin.NormalizedLevel,
                Is.EqualTo(_baselineWorld.basin.NormalizedLevel).Within(0.0001f));
            Assert.That(_raisedWorld.basin.SurfaceY - _baselineWorld.basin.SurfaceY,
                Is.EqualTo(2f).Within(0.0001f));
            AssertSurfaceMatchesRenderedWater(_baselineWorld);
            AssertSurfaceMatchesRenderedWater(_raisedWorld);

            _baselineWorld.drain.ToggleOpen();
            _raisedWorld.drain.ToggleOpen();
            yield return new WaitForFixedUpdate();

            AssertVortexFollowsSurface(_baselineWorld);
            AssertVortexFollowsSurface(_raisedWorld);
            AssertSurfaceMatchesRenderedWater(_baselineWorld);
            AssertSurfaceMatchesRenderedWater(_raisedWorld);
        }

        [UnityTest]
        public IEnumerator RaisedYawRotatedSinkFloatsFoodAsMuchAsTheBaseline()
        {
            yield return FillBothBasins(0.85f);
            FoodScrap baselineFood = PlaceSubmergedFood(_baselineWorld);
            FoodScrap raisedFood = PlaceSubmergedFood(_raisedWorld);
            float baselineStartY = baselineFood.Body.position.y;
            float raisedStartY = raisedFood.Body.position.y;
            Physics.SyncTransforms();

            // The level is held steady, so this isolates buoyancy from the rising-water shuffle.
            for (int step = 0; step < 35; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            float baselineRise = baselineFood.Body.position.y - baselineStartY;
            float raisedRise = raisedFood.Body.position.y - raisedStartY;
            Assert.That(baselineRise, Is.GreaterThan(0.025f),
                "The origin fixture must actually float; two motionless bodies cannot establish equivalence.");
            Assert.That(raisedRise, Is.EqualTo(baselineRise).Within(0.005f),
                "Moving the sink must move the water physics along with the visible water.");
            Assert.That(raisedFood.GetComponent<Collider>().sharedMaterial,
                Is.EqualTo(baselineFood.GetComponent<Collider>().sharedMaterial));
            Assert.That(raisedFood.GetComponent<Collider>().sharedMaterial.dynamicFriction, Is.LessThan(0.03f),
                "The submerged raised scrap must receive the same standing-water friction.");
        }

        [UnityTest]
        public IEnumerator FullOpenToolDrainsHeldWater_AndResetRestoresItsCharge()
        {
            yield return FillBothBasins(0.35f);
            Drain drain = _baselineWorld.drain;
            float previousRadius = drain.StartRadius * 0.5f;
            drain.radius = previousRadius;
            float heldLevel = _baselineWorld.basin.NormalizedLevel;
            Assert.That(drain.IsOpen, Is.False);

            Assert.That(drain.TryOpenFully(), Is.True);
            Assert.That(drain.IsOpen, Is.True);
            Assert.That(drain.radius, Is.EqualTo(drain.StartRadius));
            Assert.That(drain.HasFullOpenCharge, Is.False);
            Assert.That(drain.TryOpenFully(), Is.False, "The full-open tool is available only once per run.");

            for (int step = 0; step < 10; step++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.That(_baselineWorld.basin.NormalizedLevel, Is.LessThan(heldLevel),
                "Opening the drain must release water that the plug was holding.");
            Assert.That(drain.radius, Is.LessThan(drain.StartRadius).And.GreaterThan(previousRadius));

            _baselineWorld.ResetRun();
            Assert.That(_baselineWorld.basin.NormalizedLevel, Is.Zero);
            Assert.That(_baselineWorld.basin.IsOverflowed, Is.False);
            Assert.That(_baselineWorld.water.IsSpraying, Is.False);
            Assert.That(drain.IsOpen, Is.True);
            Assert.That(drain.IsSealed, Is.False);
            Assert.That(drain.radius, Is.EqualTo(drain.StartRadius));
            Assert.That(drain.HasFullOpenCharge, Is.True);
        }

        SinkWorld CreateWorld(Vector3 position, Quaternion rotation)
        {
            SinkWorld world = PlayModePrefabFactory.InstantiateLevel();
            _testWorlds.Add(world);
            world.transform.SetPositionAndRotation(position, rotation);
            Assert.That(world.basin, Is.Not.Null, "Play mode must initialize the real standing-water component.");

            // Public pump state drives the basin without physical devices, jet forces, cameras or audio.
            world.player.gameObject.SetActive(false);
            world.water.Pressure = 1f;
            world.water.WideSpray = true;
            for (int index = 0; index < world.foods.Length; index++)
            {
                FoodScrap food = world.foods[index];
                food.Body.isKinematic = true;
                food.transform.position = world.sink.transform.TransformPoint(new Vector3(4f + index, 3f, 4f));
            }
            Physics.SyncTransforms();
            return world;
        }

        IEnumerator FillBothBasins(float targetLevel)
        {
            foreach (SinkWorld world in _testWorlds)
            {
                world.drain.SetOpen(false);
                world.water.SetSpraying(true);
            }

            float deadlineSeconds = Time.time + 10f;
            while (_baselineWorld.basin.NormalizedLevel < targetLevel ||
                   _raisedWorld.basin.NormalizedLevel < targetLevel)
            {
                Assert.That(Time.time, Is.LessThan(deadlineSeconds),
                    "The plugged basins must fill through pump input.");
                yield return new WaitForFixedUpdate();
            }

            foreach (SinkWorld world in _testWorlds)
            {
                world.water.SetSpraying(false);
                Assert.That(world.basin.IsOverflowed, Is.False);
            }
            yield return new WaitForFixedUpdate();
        }

        static FoodScrap PlaceSubmergedFood(SinkWorld world)
        {
            FoodScrap food = world.foods[0];
            Collider collider = food.GetComponent<Collider>();
            float centerHeight = BasinWater.FloorY + collider.bounds.extents.y + 0.005f;
            Vector3 localPosition = new Vector3(0.5f, centerHeight, -0.35f);
            food.Body.position = world.sink.transform.TransformPoint(localPosition);
            food.transform.position = food.Body.position;
            food.Body.isKinematic = false;
            food.Body.useGravity = true;
            food.Body.linearVelocity = Vector3.zero;
            food.Body.angularVelocity = Vector3.zero;
            food.Body.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ |
                RigidbodyConstraints.FreezeRotation;
            food.Body.WakeUp();
            return food;
        }

        static void AssertSurfaceMatchesRenderedWater(SinkWorld world)
        {
            Transform waterVolume = world.sink.transform.Find("Basin water");
            Assert.That(waterVolume, Is.Not.Null);
            Assert.That(waterVolume.gameObject.activeSelf, Is.True);
            Assert.That(waterVolume.GetComponent<MeshRenderer>().bounds.max.y,
                Is.EqualTo(world.basin.SurfaceY).Within(0.002f),
                "The world-space height used by physics must coincide with the rendered water surface.");
            Assert.That(waterVolume.localScale.y,
                Is.EqualTo(world.basin.LocalSurfaceY - BasinWater.FloorY).Within(0.0001f),
                "Translating the sink must not change the local water volume depth.");
        }

        static void AssertVortexFollowsSurface(SinkWorld world)
        {
            Transform vortex = world.sink.transform.Find("Drain vortex");
            Assert.That(vortex, Is.Not.Null);
            Assert.That(vortex.gameObject.activeSelf, Is.True);
            Assert.That(vortex.position.x, Is.EqualTo(world.drain.transform.position.x).Within(0.0001f));
            Assert.That(vortex.position.z, Is.EqualTo(world.drain.transform.position.z).Within(0.0001f));
            Assert.That(vortex.position.y, Is.EqualTo(world.basin.SurfaceY + 0.012f).Within(0.0001f));
        }
    }
}
