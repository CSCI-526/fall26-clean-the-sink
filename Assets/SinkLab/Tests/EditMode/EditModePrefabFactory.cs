using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SinkLab.Tests
{
    internal static class EditModePrefabFactory
    {
        internal const string Root = "Assets/SinkLab/Prefabs/";
        internal const string LevelPath = Root + "Levels/SinkLevel.prefab";

        internal static GameObject Load(string relativePath)
        {
            string path = Root + relativePath;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, "Required prefab asset is missing: " + path);
            return asset;
        }

        internal static GameObject Instantiate(string relativePath, Transform parent = null)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(Load(relativePath));
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }
            return instance;
        }

        internal static SinkWorld InstantiateLevel()
        {
            GameObject instance = Instantiate("Levels/SinkLevel.prefab");
            SinkWorld world = instance.GetComponent<SinkWorld>();
            Assert.That(world, Is.Not.Null, "The level prefab must own the SinkWorld coordinator.");
            world.RefreshLevelReferences();
            return world;
        }
    }
}
