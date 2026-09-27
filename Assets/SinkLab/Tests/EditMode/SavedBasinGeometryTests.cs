using NUnit.Framework;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkLab.Tests
{
    public sealed class SavedBasinGeometryTests
    {
        SceneSetup[] _previousScenes;
        CursorLockMode _previousCursorLock;
        bool _previousCursorVisible;
        bool _restoreEnvironment;

        [SetUp]
        public void SetUp()
        {
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
            {
                Assert.That(SceneManager.GetSceneAt(sceneIndex).isDirty, Is.False,
                    "Save loaded scenes before running the saved basin geometry tests.");
            }

            _previousScenes = EditorSceneManager.GetSceneManagerSetup();
            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _restoreEnvironment = true;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
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
                // Discard only test instances; never save the temporary scene or apply prefab overrides.
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
                Cursor.lockState = _previousCursorLock;
                Cursor.visible = _previousCursorVisible;
                _restoreEnvironment = false;
            }
        }

        [Test]
        public void RoundedBasinExistsInSourcePrefab_WithPersistentMeshesAndWallMaterials()
        {
            GameObject sink = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SinkLab/Prefabs/Sink/Sink.prefab");
            Assert.That(sink, Is.Not.Null);
            Transform corners = sink.transform.Find("Rounded corners");
            Assert.That(corners, Is.Not.Null,
                "The playable rounded geometry must already exist before entering Play mode.");
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(corners.gameObject),
                Is.EqualTo("Assets/SinkLab/Prefabs/Sink/Parts/RoundedBasinCorners.prefab"));
            Assert.That(sink.GetComponent<BasinCorners>(), Is.Null,
                "Temporary mesh ownership must not be serialized onto the reusable sink.");

            MeshFilter[] meshes = corners.GetComponentsInChildren<MeshFilter>();
            Assert.That(meshes.Length, Is.GreaterThan(0));
            int wallCount = 0;
            foreach (MeshFilter filter in meshes)
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
                Assert.That(EditorUtility.IsPersistent(filter.sharedMesh), Is.True, filter.name);
                Renderer renderer = filter.GetComponent<Renderer>();
                Assert.That(renderer.sharedMaterial, Is.Not.Null, filter.name);
                Assert.That(EditorUtility.IsPersistent(renderer.sharedMaterial), Is.True, filter.name);

                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider != null)
                {
                    Assert.That(collider.sharedMesh, Is.SameAs(filter.sharedMesh), filter.name);
                }

                if (filter.name.StartsWith("Basin corner wall"))
                {
                    wallCount++;
                    PhysicsMaterial material = filter.GetComponent<Collider>().sharedMaterial;
                    Assert.That(material, Is.Not.Null);
                    Assert.That(material.bounciness, Is.GreaterThan(0f).And.LessThan(1f));
                    Assert.That(material.bounceCombine, Is.EqualTo(PhysicsMaterialCombine.Maximum));
                }
            }

            Assert.That(wallCount, Is.EqualTo(4));
        }

        [Test]
        public void DrainHasOneContinuousTrim_AndAuthoredCircularVisualAndCollider()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SinkLab/Prefabs/Sink/Parts/Drain.prefab");
            Drain drain = root.GetComponent<Drain>();
            Assert.That(drain.squareOpening, Is.False);
            Assert.That(drain.openingMesh, Is.Not.Null);
            Assert.That(drain.openingCollider, Is.Not.Null);
            Assert.That(drain.openingCollider.convex, Is.False,
                "A convex hull would fill the hole instead of preserving its open center.");
            Assert.That(drain.openingCollider.sharedMesh, Is.SameAs(drain.openingMesh.sharedMesh));
            Assert.That(EditorUtility.IsPersistent(drain.openingMesh.sharedMesh), Is.True);
            Assert.That(drain.plug, Is.Not.Null);
            Assert.That(drain.plug.gameObject.activeSelf, Is.False);
            Assert.That(drain.darkInterior, Is.Not.Null);

            Transform rim = root.transform.Find("Rim");
            Assert.That(rim, Is.Not.Null);
            Assert.That(rim.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(rim.gameObject),
                Is.EqualTo("Assets/SinkLab/Prefabs/Sink/Parts/DrainRim.prefab"));

            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(child.name.StartsWith("Drain lip"), Is.False, child.name);
                Assert.That(child.name.StartsWith("Drain collar block"), Is.False, child.name);
            }
        }

        [Test]
        public void SmallerStartingDrainRadius_PreservesFloorFootprintAcrossShrinkAndReset()
        {
            GameObject drainAsset = EditModePrefabFactory.Load("Sink/Parts/Drain.prefab");
            Drain savedDrain = drainAsset.GetComponent<Drain>();
            Mesh savedMesh = savedDrain.openingMesh.sharedMesh;
            Vector3[] savedVertices = savedMesh.vertices;
            int[] savedTriangles = savedMesh.triangles;
            Bounds savedBounds = savedMesh.bounds;

            GameObject drainObject = null;
            GameObject foodObject = null;
            try
            {
                drainObject = (GameObject)PrefabUtility.InstantiatePrefab(drainAsset);
                Drain drain = drainObject.GetComponent<Drain>();
                float configuredRadius = savedDrain.radius * 0.7f;
                drain.radius = configuredRadius;

                // EditMode does not run these lifecycle callbacks for this ordinary component.
                // Initialize the real prefab after applying the same override a variant would save.
                const BindingFlags privateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
                typeof(Drain).GetMethod("Awake", privateInstance).Invoke(drain, null);
                typeof(Drain).GetMethod("Start", privateInstance).Invoke(drain, null);
                AssertOpeningGeometry(drain, configuredRadius, savedBounds);
                Assert.That(drain.openingMesh.sharedMesh, Is.Not.SameAs(savedMesh));

                foodObject = EditModePrefabFactory.Instantiate("Mess/FoodCube.prefab");
                FoodScrap food = foodObject.GetComponent<FoodScrap>();
                Vector3 capturePoint = drain.transform.TransformPoint(
                    new Vector3(0f, -drain.captureDepth - 0.1f, 0f));
                food.Body.position += capturePoint - food.Body.worldCenterOfMass;
                food.transform.position = food.Body.position;
                Physics.SyncTransforms();
                Assert.That(drain.TryConsume(food), Is.True);
                AssertOpeningGeometry(drain, configuredRadius - drain.shrinkPerScrap, savedBounds);

                drain.ResetCount();
                Assert.That(drain.radius, Is.EqualTo(configuredRadius).Within(0.00001f));
                AssertOpeningGeometry(drain, configuredRadius, savedBounds);
                Assert.That(drain.openingMesh.sharedMesh, Is.Not.SameAs(savedMesh),
                    "Reset must restore the configured aperture, not the wider source-asset aperture.");
            }
            finally
            {
                if (foodObject != null)
                {
                    Object.DestroyImmediate(foodObject);
                }

                if (drainObject != null)
                {
                    Object.DestroyImmediate(drainObject);
                }
            }

            Assert.That(savedMesh, Is.Not.Null);
            Assert.That(EditorUtility.IsPersistent(savedMesh), Is.True);
            Assert.That(savedDrain.openingMesh.sharedMesh, Is.SameAs(savedMesh));
            Assert.That(savedDrain.openingCollider.sharedMesh, Is.SameAs(savedMesh));
            CollectionAssert.AreEqual(savedVertices, savedMesh.vertices);
            CollectionAssert.AreEqual(savedTriangles, savedMesh.triangles);
        }

        static void AssertOpeningGeometry(Drain drain, float expectedRadius, Bounds expectedOuterBounds)
        {
            Mesh mesh = drain.openingMesh.sharedMesh;
            Assert.That(drain.openingCollider.sharedMesh, Is.SameAs(mesh));
            Assert.That(mesh.bounds.min.x, Is.EqualTo(expectedOuterBounds.min.x).Within(0.00001f));
            Assert.That(mesh.bounds.max.x, Is.EqualTo(expectedOuterBounds.max.x).Within(0.00001f));
            Assert.That(mesh.bounds.min.z, Is.EqualTo(expectedOuterBounds.min.z).Within(0.00001f));
            Assert.That(mesh.bounds.max.z, Is.EqualTo(expectedOuterBounds.max.z).Within(0.00001f));

            float actualRadius = float.PositiveInfinity;
            foreach (Vector3 vertex in mesh.vertices)
            {
                actualRadius = Mathf.Min(actualRadius, new Vector2(vertex.x, vertex.z).magnitude);
            }

            Assert.That(actualRadius, Is.EqualTo(expectedRadius).Within(0.00001f));
        }
    }
}
