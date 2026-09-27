using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SinkLab.Editor
{
    /// <summary>
    /// One-time conversion of the authored prototype. Subsequent editing happens
    /// in prefab assets and the level; this does not generate geometry or materials.
    /// </summary>
    public static class SinkPrefabMigration
    {
        const string Prefabs = "Assets/SinkLab/Prefabs/";
        const string Backup = "Assets/SinkLab/Scenes/Backups/BeforePrefabRefactor.unity";

        [MenuItem("Sink Lab/Migrate original prototype to prefabs")]
        public static void Migrate()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Stop Play mode first.");
            }

            if (AssetDatabase.LoadAssetAtPath<GameObject>(SinkSceneBuilder.LevelPrefabPath))
            {
                throw new InvalidOperationException(
                    "Migration is already complete. Edit the existing prefabs instead.");
            }

            var scene = SceneManager.GetActiveScene();
            var source = scene.GetRootGameObjects().Select(root => root.GetComponent<SinkWorld>())
                .FirstOrDefault(world => world != null);
            if (!source || !source.transform.Find("Sink and room"))
            {
                throw new InvalidOperationException("Open the original prototype scene before migration.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Backup));
            if (File.Exists(Backup))
            {
                throw new InvalidOperationException("A migration backup already exists; it will not be overwritten.");
            }

            if (!EditorSceneManager.SaveScene(scene, Backup, true))
            {
                throw new IOException("Could not preserve the current scene, including unsaved changes.");
            }

            foreach (string folder in new[] { "Player", "Sink/Parts", "Mess", "Environment", "Levels" })
            {
                Directory.CreateDirectory(Prefabs + folder);
            }

            AssetDatabase.Refresh();

            Vector3 position = source.transform.position;
            Quaternion rotation = source.transform.rotation;
            Vector3 scale = source.transform.localScale;
            Scene preview = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = Object.Instantiate(source.gameObject);
                SceneManager.MoveGameObjectToScene(root, preview);
                root.name = "Sink Level";
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                ConvertHierarchy(root.GetComponent<SinkWorld>(), preview);
                if (!PrefabUtility.SaveAsPrefabAsset(root, SinkSceneBuilder.LevelPrefabPath))
                {
                    throw new IOException("Could not save the composed level prefab.");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
            }

            // Replace the original only after all reusable assets have been saved.
            var levelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(SinkSceneBuilder.LevelPrefabPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(levelAsset, scene);
            instance.name = "Sink Level";
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.transform.localScale = scale;
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
            Object.DestroyImmediate(source.gameObject);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, SinkSceneBuilder.ScenePath))
            {
                throw new IOException("Could not save the level scene. The original backup is intact.");
            }

            AssetDatabase.SaveAssets();
            Selection.activeGameObject = instance;
            Debug.Log("Prefab migration complete. Original scene preserved at " + Backup);
        }

        static void ConvertHierarchy(SinkWorld world, Scene preview)
        {
            Transform root = world.transform;
            Transform originalEnvironment = root.Find("Sink and room");
            Transform sink = Group("Sink", root);
            Transform mess = Group("Mess", root);
            Transform environment = Group("Environment", root);
            Transform room = Group("Room", environment);
            Transform lighting = Group("Lighting", environment);
            Transform floor = Group("Floor", sink);
            Transform walls = Group("Walls", sink);
            Transform rim = Group("Rim", sink);
            Transform counter = Group("Counter", sink);
            Transform cabinet = Group("Cabinet", sink);
            Transform roomWalls = Group("Walls", room);

            MoveMatching(originalEnvironment, floor, name => name.StartsWith("Basin floor "));
            MoveMatching(originalEnvironment, walls, name => name.StartsWith("Basin wall "));
            MoveMatching(originalEnvironment, rim, name => name.EndsWith(" rim"));
            MoveMatching(originalEnvironment, counter, name => name.StartsWith("Counter "));
            MoveMatching(originalEnvironment, cabinet, name => name.StartsWith("Cabinet "));
            MoveMatching(originalEnvironment, roomWalls, name => name == "Back wall" || name.EndsWith("room boundary"));
            MoveMatching(originalEnvironment, room, name => name == "Room floor");

            ReplaceGroupParts(floor, "Sink/Parts/SinkFloor.prefab", preview);
            ReplaceGroupParts(walls, "Sink/Parts/SinkWall.prefab", preview);
            ReplaceGroupParts(rim, "Sink/Parts/SinkRim.prefab", preview);
            ReplaceGroupParts(counter, "Sink/Parts/CounterPanel.prefab", preview);
            ReplaceGroupParts(cabinet, "Sink/Parts/CabinetPanel.prefab", preview);
            ReplaceGroupParts(roomWalls, "Environment/RoomWall.prefab", preview);
            Transform roomFloor = room.Find("Room floor");
            var roomFloorAsset = CreatePart(roomFloor.gameObject, "Environment/RoomFloor.prefab", preview, true);
            ReplacePart(roomFloor.gameObject, roomFloorAsset);

            Transform drain = world.drain.transform;
            drain.SetParent(sink, true);
            drain.name = "Drain";
            MoveMatching(originalEnvironment, drain, name => name == "Dark drain interior" || name == "Drain lip");
            int lip = 0;
            foreach (Transform child in drain)
            {
                if (child.name == "Drain lip")
                {
                    child.name = "Drain lip " + (++lip).ToString("D2");
                }
            }

            Connect(drain.gameObject, "Sink/Parts/Drain.prefab");

            Transform faucet = Group("Faucet", sink);
            MoveMatching(originalEnvironment, faucet, name => name.StartsWith("Faucet ") || name == "Hose anchor");
            Connect(faucet.gameObject, "Sink/Parts/Faucet.prefab");
            var assembly = sink.gameObject.AddComponent<SinkAssembly>();
            assembly.drain = drain.GetComponent<Drain>();
            assembly.hoseAnchor = faucet.Find("Hose anchor");
            Connect(sink.gameObject, "Sink/Sink.prefab");

            Transform food = root.Find("Food - physical scraps");
            food.SetParent(mess, true);
            food.name = "Food";
            var cubeAsset = CreatePart(food.Cast<Transform>().First(t => t.GetComponent<BoxCollider>()).gameObject,
                "Mess/FoodCube.prefab", preview, false);
            var sphereAsset = CreatePart(food.Cast<Transform>().First(t => t.GetComponent<SphereCollider>()).gameObject,
                "Mess/FoodSphere.prefab", preview, false);
            foreach (Transform old in food.Cast<Transform>().ToArray())
            {
                var asset = old.GetComponent<BoxCollider>() ? cubeAsset : sphereAsset;
                Material material = old.GetComponent<Renderer>().sharedMaterial;
                float mass = old.GetComponent<Rigidbody>().mass;
                var replacement = ReplacePart(old.gameObject, asset);
                replacement.GetComponent<Renderer>().sharedMaterial = material;
                replacement.GetComponent<Rigidbody>().mass = mass;
                Record(replacement.GetComponent<Renderer>(), replacement.GetComponent<Rigidbody>());
            }

            Transform stains = root.Find("Stains - wash with water");
            stains.SetParent(mess, true);
            stains.name = "Stains";
            var stainAsset = CreatePart(stains.GetChild(0).gameObject, "Mess/Stain.prefab", preview, false);
            foreach (Transform old in stains.Cast<Transform>().ToArray())
            {
                ReplacePart(old.gameObject, stainAsset);
            }

            Transform player = world.player.transform;
            player.name = "Player";
            world.player.world = null;
            var hud = player.GetComponent<SinkHUD>();
            hud.world = null;
            hud.player = world.player;
            hud.water = world.water;
            player.GetComponent<WaterVisuals>().hoseAnchor = null;
            Connect(player.gameObject, "Player/Player.prefab");

            MoveMatching(root, lighting, name => name == "Soft kitchen light" || name == "Sink fill light");
            // Preserve any manually added environment children rather than dropping them.
            foreach (Transform child in originalEnvironment.Cast<Transform>().ToArray())
            {
                child.SetParent(room, true);
            }

            Object.DestroyImmediate(originalEnvironment.gameObject);
            Connect(room.gameObject, "Environment/Room.prefab");
            Connect(lighting.gameObject, "Environment/Lighting.prefab");

            player.SetSiblingIndex(0);
            sink.SetSiblingIndex(1);
            mess.SetSiblingIndex(2);
            environment.SetSiblingIndex(3);
            world.RefreshLevelReferences();
            Record(world.player, world.player.GetComponent<SinkHUD>(), world.player.GetComponent<WaterVisuals>());
        }

        static Transform Group(string name, Transform parent)
        {
            var group = new GameObject(name).transform;
            group.SetParent(parent, false);
            return group;
        }

        static void MoveMatching(Transform source, Transform destination, Func<string, bool> matches)
        {
            foreach (Transform child in source.Cast<Transform>().Where(t => matches(t.name)).ToArray())
            {
                child.SetParent(destination, true);
            }
        }

        static void ReplaceGroupParts(Transform group, string path, Scene preview)
        {
            if (group.childCount == 0)
            {
                throw new InvalidOperationException("Expected parts in " + group.name);
            }

            var prefab = CreatePart(group.GetChild(0).gameObject, path, preview, true);
            foreach (Transform old in group.Cast<Transform>().ToArray())
            {
                ReplacePart(old.gameObject, prefab);
            }
        }

        static GameObject CreatePart(GameObject source, string path, Scene preview, bool unitScale)
        {
            var template = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(template, preview);
            template.name = Path.GetFileNameWithoutExtension(path);
            template.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            if (unitScale)
            {
                template.transform.localScale = Vector3.one;
            }

            try
            {
                return PrefabUtility.SaveAsPrefabAsset(template, Prefabs + path);
            }
            finally
            {
                Object.DestroyImmediate(template);
            }
        }

        static GameObject ReplacePart(GameObject original, GameObject prefab)
        {
            if (!prefab)
            {
                throw new InvalidOperationException("A part prefab could not be saved.");
            }

            Transform old = original.transform;
            int index = old.GetSiblingIndex();
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, original.scene);
            instance.name = original.name;
            instance.transform.SetParent(old.parent, false);
            instance.transform.SetPositionAndRotation(old.position, old.rotation);
            instance.transform.localScale = old.localScale;
            instance.transform.SetSiblingIndex(index);
            Record(instance, instance.transform);
            Object.DestroyImmediate(original);
            return instance;
        }

        static void Connect(GameObject root, string path)
        {
            if (!PrefabUtility.SaveAsPrefabAssetAndConnect(root, Prefabs + path, InteractionMode.AutomatedAction))
            {
                throw new IOException("Could not save prefab " + path);
            }
        }

        static void Record(params Object[] objects)
        {
            foreach (Object item in objects)
            {
                PrefabUtility.RecordPrefabInstancePropertyModifications(item);
            }
        }
    }
}
