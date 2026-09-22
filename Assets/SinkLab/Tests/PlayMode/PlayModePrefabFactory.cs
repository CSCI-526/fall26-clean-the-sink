using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SinkLab.Tests
{
    internal static class PlayModePrefabFactory
    {
        internal static GameObject Instantiate(string relativePath)
        {
#if UNITY_EDITOR
            string path = "Assets/SinkLab/Prefabs/" + relativePath;
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, "Required prefab asset is missing: " + path);
            return (GameObject)PrefabUtility.InstantiatePrefab(asset);
#else
            Assert.Ignore("These prefab integration tests run in the Unity Editor.");
            return null;
#endif
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
