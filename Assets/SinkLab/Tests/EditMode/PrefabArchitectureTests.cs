using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SinkLab.Tests
{
    public sealed class PrefabArchitectureTests
    {
        SinkWorld world;
        SceneSetup[] previousScenes;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;
        bool restoreEnvironment;

        [SetUp]
        public void SetUp()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False,
                    "Save loaded scenes before running the prefab architecture tests.");
            }

            previousScenes = EditorSceneManager.GetSceneManagerSetup();
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            restoreEnvironment = true;
            try
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                world = EditModePrefabFactory.InstantiateLevel();
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
            if (!restoreEnvironment)
            {
                return;
            }

            try
            {
                // Discard only the test instances; never apply overrides or save assets.
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                FoodScrap.Active.RemoveWhere(item => item == null);
                StainPatch.Active.RemoveWhere(item => item == null);
                if (previousScenes != null && previousScenes.Length > 0)
                {
                    EditorSceneManager.RestoreSceneManagerSetup(previousScenes);
                }
            }
            finally
            {
                Cursor.lockState = previousCursorLock;
                Cursor.visible = previousCursorVisible;
                restoreEnvironment = false;
            }
        }

        [Test]
        public void ReusablePartsAndLevelExistAsPrefabAssets()
        {
            string[] required =
            {
                "Player/Player.prefab", "Sink/Sink.prefab", "Sink/Parts/SinkWall.prefab",
                "Mess/FoodCube.prefab", "Mess/FoodSphere.prefab", "Mess/Stain.prefab",
                "Levels/SinkLevel.prefab"
            };
            foreach (string path in required)
            {
                GameObject asset = EditModePrefabFactory.Load(path);
                Assert.That(PrefabUtility.IsPartOfPrefabAsset(asset), Is.True, path);
                Assert.That(PrefabUtility.GetPrefabAssetType(asset),
                    Is.Not.EqualTo(PrefabAssetType.MissingAsset), path);
            }
        }

        [Test]
        public void LevelUsesGroupedHierarchyAndNestedReusableParts()
        {
            AssertChildren(world.transform, "Player", "Sink", "Mess", "Environment");
            AssertChildren(RequirePath(world.transform, "Mess"), "Food", "Stains");
            AssertChildren(RequirePath(world.transform, "Environment"), "Room", "Lighting");
            Transform sink = RequirePath(world.transform, "Sink");
            AssertChildren(sink, "Floor", "Walls", "Rim", "Counter", "Cabinet", "Drain", "Faucet", "Rounded corners");
            AssertPrefabSource(RequirePath(world.transform, "Player"), "Player/Player.prefab");
            AssertPrefabSource(sink, "Sink/Sink.prefab");
            AssertPrefabSource(RequirePath(sink, "Rounded corners"), "Sink/Parts/RoundedBasinCorners.prefab");

            Transform walls = RequirePath(sink, "Walls");
            Assert.That(walls.childCount, Is.EqualTo(4));
            foreach (Transform wall in walls)
            {
                AssertPrefabSource(wall, "Sink/Parts/SinkWall.prefab");
            }

            Transform foodGroup = RequirePath(world.transform, "Mess/Food");
            Assert.That(foodGroup.childCount, Is.EqualTo(8));
            foreach (Transform food in foodGroup)
            {
                Assert.That(food.GetComponent<FoodScrap>(), Is.Not.Null, food.name);
                AssertPrefabSource(food, food.GetComponent<SphereCollider>() != null
                    ? "Mess/FoodSphere.prefab" : "Mess/FoodCube.prefab");
            }
            Transform stains = RequirePath(world.transform, "Mess/Stains");
            Assert.That(stains.childCount, Is.EqualTo(6));
            foreach (Transform stain in stains)
            {
                AssertPrefabSource(stain, "Mess/Stain.prefab");
            }

            foreach (string group in new[] { "Floor", "Rim", "Counter", "Cabinet", "Drain", "Faucet" })
            {
                Assert.That(RequirePath(sink, group).childCount, Is.GreaterThan(0), group + " must contain its parts.");
            }
            Assert.That(RequirePath(world.transform, "Environment/Room").childCount, Is.GreaterThan(0));
            Assert.That(RequirePath(world.transform, "Environment/Lighting").childCount, Is.GreaterThan(0));
        }

        [Test]
        public void NestedLevelContainsNoMissingPrefabAssetsOrScripts()
        {
            foreach (Transform item in world.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(PrefabUtility.GetPrefabInstanceStatus(item.gameObject),
                    Is.Not.EqualTo(PrefabInstanceStatus.MissingAsset), item.name);
                foreach (Component component in item.GetComponents<Component>())
                {
                    Assert.That(component, Is.Not.Null, "Missing script on " + item.name);
                }
            }
        }

        [Test]
        public void StandalonePlayerAssetKeepsInternalReferencesAndNoLevelReferences()
        {
            GameObject asset = EditModePrefabFactory.Load("Player/Player.prefab");
            SinkPlayer player = asset.GetComponent<SinkPlayer>();
            WaterJet water = asset.GetComponent<WaterJet>();
            SinkHUD hud = asset.GetComponent<SinkHUD>();
            WaterVisuals visuals = asset.GetComponent<WaterVisuals>();
            Assert.That(player, Is.Not.Null);
            Assert.That(water, Is.Not.Null);
            Assert.That(hud, Is.Not.Null);
            Assert.That(visuals, Is.Not.Null);
            Assert.That(player.world, Is.Null, "A reusable player must not retain one particular level.");
            Assert.That(hud.world, Is.Null);
            Assert.That(visuals.hoseAnchor, Is.Null, "The anchor belongs to the level's sink, not the player asset.");
            Assert.That(player.viewCamera, Is.Not.Null);
            Assert.That(player.viewCamera.transform.IsChildOf(asset.transform), Is.True);
            Assert.That(water.aimCamera, Is.EqualTo(player.viewCamera));
            Assert.That(water.nozzle, Is.Not.Null);
            Assert.That(water.nozzle.IsChildOf(asset.transform), Is.True);
            Assert.That(player.water, Is.EqualTo(water));
            Assert.That(hud.player, Is.EqualTo(player));
            Assert.That(hud.water, Is.EqualTo(water));
            Assert.That(visuals.water, Is.EqualTo(water));
        }

        [Test]
        public void LevelAssetPersistsItsOwnPlayerDrainAndHoseBindings()
        {
            // Read the asset itself before Awake/Refresh can repair missing bindings.
            GameObject asset = EditModePrefabFactory.Load("Levels/SinkLevel.prefab");
            SinkWorld authored = asset.GetComponent<SinkWorld>();
            SinkPlayer player = RequirePath(asset.transform, "Player").GetComponent<SinkPlayer>();
            SinkAssembly sink = RequirePath(asset.transform, "Sink").GetComponent<SinkAssembly>();
            Assert.That(authored, Is.Not.Null);
            Assert.That(sink, Is.Not.Null);
            Assert.That(authored.player, Is.EqualTo(player));
            Assert.That(authored.sink, Is.EqualTo(sink));
            Assert.That(authored.water, Is.EqualTo(player.GetComponent<WaterJet>()));
            Assert.That(authored.drain, Is.Not.Null);
            Assert.That(authored.drain, Is.EqualTo(sink.drain));
            Assert.That(authored.drain.transform.IsChildOf(RequirePath(sink.transform, "Drain")), Is.True);
            Assert.That(sink.hoseAnchor, Is.Not.Null);
            Assert.That(sink.hoseAnchor.IsChildOf(RequirePath(sink.transform, "Faucet")), Is.True);
            Assert.That(player.world, Is.EqualTo(authored));
            Assert.That(player.GetComponent<WaterVisuals>().hoseAnchor, Is.EqualTo(sink.hoseAnchor));
            SinkHUD hud = player.GetComponent<SinkHUD>();
            Assert.That(hud.world, Is.EqualTo(authored));
            Assert.That(hud.player, Is.EqualTo(player));
            Assert.That(hud.water, Is.EqualTo(authored.water));
        }

        [Test]
        public void RefreshIncludesAddedMessPrefabsAndExcludesANestedIndependentLevel()
        {
            int foodBefore = world.FoodRemaining;
            int stainsBefore = world.StainsRemaining;
            FoodScrap addedFood = EditModePrefabFactory.Instantiate("Mess/FoodCube.prefab",
                RequirePath(world.transform, "Mess/Food")).GetComponent<FoodScrap>();
            StainPatch addedStain = EditModePrefabFactory.Instantiate("Mess/Stain.prefab",
                RequirePath(world.transform, "Mess/Stains")).GetComponent<StainPatch>();
            world.RefreshLevelReferences();
            Assert.That(world.foods, Does.Contain(addedFood));
            Assert.That(world.stains, Does.Contain(addedStain));
            Assert.That(world.FoodRemaining, Is.EqualTo(foodBefore + 1));
            Assert.That(world.StainsRemaining, Is.EqualTo(stainsBefore + 1));

            SinkWorld nested = EditModePrefabFactory.InstantiateLevel();
            nested.transform.SetParent(world.transform, false);
            nested.transform.localPosition = new Vector3(12f, 0f, 0f);
            nested.RefreshLevelReferences();
            world.RefreshLevelReferences();

            Assert.That(world.FoodRemaining, Is.EqualTo(foodBefore + 1));
            Assert.That(world.StainsRemaining, Is.EqualTo(stainsBefore + 1));
            foreach (FoodScrap food in nested.foods)
            {
                Assert.That(world.foods.Contains(food), Is.False);
            }
            foreach (StainPatch stain in nested.stains)
            {
                Assert.That(world.stains.Contains(stain), Is.False);
            }
            Assert.That(world.player.world, Is.EqualTo(world));
            Assert.That(nested.player.world, Is.EqualTo(nested));
            Assert.That(world.player, Is.Not.EqualTo(nested.player));
            Assert.That(world.drain, Is.Not.EqualTo(nested.drain));
            Assert.That(world.player.GetComponent<WaterVisuals>().hoseAnchor, Is.EqualTo(world.sink.hoseAnchor));
            Assert.That(nested.player.GetComponent<WaterVisuals>().hoseAnchor, Is.EqualTo(nested.sink.hoseAnchor));
        }

        [Test]
        public void SharedWallColliderAndPlayerSpeedRemainInheritedFromTheirPartAssets()
        {
            BoxCollider sourceWall = EditModePrefabFactory.Load("Sink/Parts/SinkWall.prefab")
                .GetComponent<BoxCollider>();
            Assert.That(sourceWall, Is.Not.Null);
            foreach (Transform wall in RequirePath(world.transform, "Sink/Walls"))
            {
                BoxCollider collider = wall.GetComponent<BoxCollider>();
                Assert.That(collider, Is.Not.Null);
                Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(collider), Is.EqualTo(sourceWall));
                Assert.That(collider.size, Is.EqualTo(sourceWall.size));
                Assert.That(collider.center, Is.EqualTo(sourceWall.center));
                Assert.That(collider.enabled, Is.EqualTo(sourceWall.enabled));
                Assert.That(collider.isTrigger, Is.EqualTo(sourceWall.isTrigger));
                Assert.That(collider.sharedMaterial, Is.EqualTo(sourceWall.sharedMaterial));
                AssertNoOverride(collider, "m_Size", "m_Center", "m_Enabled", "m_IsTrigger", "m_Material");
            }
            SinkPlayer sourcePlayer = EditModePrefabFactory.Load("Player/Player.prefab").GetComponent<SinkPlayer>();
            Assert.That(sourcePlayer, Is.Not.Null);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(world.player), Is.EqualTo(sourcePlayer));
            Assert.That(world.player.moveSpeed, Is.EqualTo(sourcePlayer.moveSpeed));
            AssertNoOverride(world.player, "moveSpeed");
        }

        static Transform RequirePath(Transform root, string path)
        {
            Transform found = root.Find(path);
            Assert.That(found, Is.Not.Null, "Required hierarchy group is missing: " + root.name + "/" + path);
            return found;
        }

        static void AssertChildren(Transform parent, params string[] expected)
        {
            string[] actual = parent.Cast<Transform>().Select(child => child.name).ToArray();
            Assert.That(actual, Is.EquivalentTo(expected), "Unexpected hierarchy beneath " + parent.name);
        }

        static void AssertPrefabSource(Transform instance, string relativePath)
        {
            Assert.That(PrefabUtility.GetPrefabInstanceStatus(instance.gameObject),
                Is.EqualTo(PrefabInstanceStatus.Connected), instance.name);
            Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(instance.gameObject),
                Is.EqualTo(EditModePrefabFactory.Root + relativePath), instance.name);
        }

        static void AssertNoOverride(Component component, params string[] names)
        {
            var serialized = new SerializedObject(component);
            foreach (string name in names)
            {
                SerializedProperty property = serialized.FindProperty(name);
                Assert.That(property, Is.Not.Null, "Shared property must exist: " + name);
                Assert.That(property.prefabOverride, Is.False,
                    component.name + " overrides " + name + ", so future shared-prefab edits would not propagate.");
            }
        }
    }
}
