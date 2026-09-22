using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkLab.Tests
{
    /// <summary>
    /// Integration contracts for the deliverable loaded from disk. These tests never
    /// call SinkWorld.Create or repair the saved scene's colliders or materials.
    /// </summary>
    public sealed class SavedSceneContractTests
    {
        const string ScenePath = "Assets/SinkLab/Scenes/SinkLab.unity";
        SinkWorld world;
        SceneSetup[] previousScenes;
        SimulationMode previousSimulationMode;
        bool previousAutoSyncTransforms;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        bool restoreEnvironment;

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False,
                    "Save loaded scenes before running the saved-scene integration tests.");

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
                // Explicitly close the currently loaded scene first so this always
                // exercises serialized disk contents, even when it was already open.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PruneDestroyedRegistrations();
                Scene loaded = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                Assert.That(loaded.path, Is.EqualTo(ScenePath));
                SinkWorld[] worlds = loaded.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SinkWorld>(true)).ToArray();
                Assert.That(worlds.Length, Is.EqualTo(1), "The saved deliverable must contain one sink world.");
                world = worlds[0];
                Assert.That(world.foods, Is.Not.Null);
                Assert.That(world.foods.Length, Is.EqualTo(8));
                Assert.That(world.stains, Is.Not.Null.And.Not.Empty);
                Assert.That(world.water, Is.Not.Null);

                // Only supply runtime lifecycle registration missing in EditMode.
                // Leave persisted physics, transforms, and material references intact.
                foreach (FoodScrap food in world.foods)
                {
                    Assert.That(food, Is.Not.Null);
                    food.CaptureSpawn();
                    FoodScrap.Active.Add(food);
                }
                foreach (StainPatch stain in world.stains) StainPatch.Active.Add(stain);
                world.water.SetSpraying(false);
                Physics.SyncTransforms();
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
            if (!restoreEnvironment) return;
            try
            {
                if (world != null)
                {
                    foreach (FoodScrap food in world.foods ?? Array.Empty<FoodScrap>()) FoodScrap.Active.Remove(food);
                    foreach (StainPatch stain in world.stains ?? Array.Empty<StainPatch>()) StainPatch.Active.Remove(stain);
                }
                // Discard only the test's unsaved changes; no SaveScene/SaveAssets calls.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                PruneDestroyedRegistrations();
                if (previousScenes != null && previousScenes.Length > 0)
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
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

        [Test]
        public void EverySavedFoodColliderKeepsThePersistentLowFrictionMaterial()
        {
            PhysicsMaterial expected = null;
            foreach (FoodScrap food in world.foods)
            {
                Collider[] colliders = food.GetComponentsInChildren<Collider>(true);
                Assert.That(colliders, Is.Not.Empty, food.name);
                foreach (Collider collider in colliders)
                {
                    PhysicsMaterial material = collider.sharedMaterial;
                    Assert.That(material, Is.Not.Null,
                        food.name + " lost its friction material while the scene was saved.");
                    Assert.That(EditorUtility.IsPersistent(material), Is.True,
                        food.name + " must reference a saved asset, not a temporary material.");
                    Assert.That(AssetDatabase.GetAssetPath(material), Does.StartWith("Assets/"));
                    if (expected == null) expected = material;
                    Assert.That(material, Is.SameAs(expected), "All food colliders must retain the shared wet-food material.");
                    // This is a deliberate tuning dependency: the normal .20 N water
                    // force must overcome food-floor friction without suspending food.
                    Assert.That(material.staticFriction, Is.InRange(0f, .12f), food.name);
                    Assert.That(material.dynamicFriction, Is.InRange(0f, .12f), food.name);
                }
            }
        }

        [Test]
        public void SavedSphereFoodHasUniformScaleAndMatchingVisibleMeshAndCollider()
        {
            int sphereCount = 0;
            foreach (FoodScrap food in world.foods)
            {
                SphereCollider collider = food.GetComponent<SphereCollider>();
                if (collider == null) continue;
                sphereCount++;
                Vector3 scale = food.transform.lossyScale;
                Assert.That(scale.x, Is.GreaterThan(0f), food.name);
                Assert.That(scale.y, Is.EqualTo(scale.x).Within(.00001f), food.name);
                Assert.That(scale.z, Is.EqualTo(scale.x).Within(.00001f), food.name);
                MeshFilter filter = food.GetComponent<MeshFilter>();
                Assert.That(filter, Is.Not.Null, food.name);
                Assert.That(filter.sharedMesh, Is.Not.Null, food.name);
                Bounds mesh = filter.sharedMesh.bounds;
                Assert.That(Vector3.Distance(collider.center, mesh.center), Is.LessThan(.0001f), food.name);
                Assert.That(mesh.extents.x, Is.EqualTo(collider.radius).Within(.0001f), food.name);
                Assert.That(mesh.extents.y, Is.EqualTo(collider.radius).Within(.0001f), food.name);
                Assert.That(mesh.extents.z, Is.EqualTo(collider.radius).Within(.0001f), food.name);
            }
            Assert.That(sphereCount, Is.GreaterThan(0), "The sphere-geometry check must not be vacuous.");
        }

        [Test]
        public void SavedFoodRestingOnRealBasinMovesUnderNormalWaterImpulses()
        {
            FoodScrap food = world.foods[0];
            Assert.That(food.GetComponent<BoxCollider>(), Is.Not.Null, "Use the actual first cube-shaped scrap.");
            Assert.That(food.Body.useGravity, Is.True);
            Assert.That(food.Body.isKinematic, Is.False);
            Assert.That(world.water.waterForce, Is.EqualTo(.20f).Within(.00001f),
                "Exercise the production force, not a force increased to overcome missing friction assets.");

            // Settle the untouched saved world onto its actual basin floor first.
            for (int i = 0; i < 50; i++) Physics.Simulate(.02f);
            Physics.SyncTransforms();
            Collider foodCollider = food.GetComponent<Collider>();
            RaycastHit[] below = Physics.RaycastAll(food.Body.worldCenterOfMass + Vector3.up * .1f,
                Vector3.down, .5f, world.water.waterMask, QueryTriggerInteraction.Ignore);
            RaycastHit[] supports = below.Where(hit => hit.collider.GetComponentInParent<FoodScrap>() == null)
                .OrderBy(hit => hit.distance).ToArray();
            Assert.That(supports, Is.Not.Empty, "The test must use supported food, not a suspended body.");
            Assert.That(supports[0].collider.name, Is.EqualTo("Basin floor left"));
            Assert.That(Mathf.Abs(foodCollider.bounds.min.y - supports[0].point.y), Is.LessThan(.025f));

            Vector3 settled = food.Body.position;
            for (int i = 0; i < 15; i++) Physics.Simulate(.02f);
            Assert.That(Vector3.Distance(food.Body.position, settled), Is.LessThan(.01f),
                "Food must remain settled before water is enabled.");

            var camera = new GameObject("Saved-scene contract aim camera").AddComponent<Camera>();
            camera.enabled = false;
            camera.nearClipPlane = .01f;
            Transform nozzle = new GameObject("Saved-scene contract nozzle").transform;
            world.water.aimCamera = camera;
            world.water.nozzle = nozzle;
            world.water.WideSpray = false;
            world.water.Pressure = .58f;
            Vector3 start = food.Body.position;
            world.water.SetSpraying(true);
            for (int i = 0; i < 50; i++)
            {
                // Move only test aim/nozzle. Keep gravity, the saved materials, floor,
                // food transforms, and the production force and impulse path intact.
                Vector3 target = food.Body.worldCenterOfMass;
                Vector3 offset = new Vector3(0f, .7f, -.15f);
                camera.transform.SetPositionAndRotation(target + offset, Quaternion.LookRotation(-offset));
                nozzle.SetPositionAndRotation(camera.transform.position, camera.transform.rotation);
                Physics.SyncTransforms();
                world.water.SimulateSpray(.02f);
                Assert.That(world.water.HasHit, Is.True, "The water must reach a physical surface.");
                Physics.Simulate(.02f);
            }
            world.water.SetSpraying(false);
            Vector3 movement = food.Body.position - start;
            Assert.That(movement.z, Is.GreaterThan(.05f),
                "Regular water must overcome the saved food-floor friction and push the supported scrap.");
            Assert.That(food.Body.position.y, Is.InRange(.80f, 1f),
                "The evidence must be motion along the real basin floor.");
        }

        static void PruneDestroyedRegistrations()
        {
            FoodScrap.Active.RemoveWhere(item => item == null);
            StainPatch.Active.RemoveWhere(item => item == null);
        }
    }
}
