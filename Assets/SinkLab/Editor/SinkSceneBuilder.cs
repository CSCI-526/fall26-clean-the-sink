using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace SinkLab.Editor
{
    public static class SinkSceneBuilder
    {
        public const string ScenePath="Assets/SinkLab/Scenes/SinkLab.unity";
        [InitializeOnLoadMethod]
        static void RegisterAudit()
        {
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        static void OnPlayMode(PlayModeStateChange state)
        {
            if(state==PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("SinkLab.RunAudit",false))
            {
                SessionState.SetBool("SinkLab.RunAudit",false);
                EditorApplication.delayCall += AttachAudit;
            }
        }
        static void AttachAudit()
        {
            Application.runInBackground=true;
            Time.timeScale=2;
            var world=Object.FindFirstObjectByType<SinkWorld>();
            if(world&&!world.GetComponent<SinkGameplayAudit>())world.gameObject.AddComponent<SinkGameplayAudit>();
        }
        [MenuItem("Sink Lab/Run complete gameplay audit")]
        public static void RunAudit()
        {
            if(EditorApplication.isPlaying){AttachAudit();return;}
            SessionState.SetBool("SinkLab.RunAudit",true);
            EditorApplication.isPlaying=true;
        }
        public const string LevelPrefabPath = "Assets/SinkLab/Prefabs/Levels/SinkLevel.prefab";

        [MenuItem("Sink Lab/Create level from prefabs")]
        public static void Build()
        {
            if (EditorApplication.isPlaying)
                throw new System.InvalidOperationException("Stop Play mode before creating a level.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new System.InvalidOperationException("Save existing scene changes first.");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(LevelPrefabPath);
            if (!prefab) throw new System.InvalidOperationException("The SinkLevel prefab is missing.");

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            root.name = "Sink Level";
            // The prefab owns component defaults and layout. Creating a level never
            // regenerates primitives or overwrites edits to the reusable assets.
            RenderSettings.ambientLight = new Color(.53f, .61f, .66f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Directory.CreateDirectory("Assets/SinkLab/Scenes");
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (SceneView.lastActiveSceneView)
                SceneView.lastActiveSceneView.LookAt(new Vector3(0, .9f, 0), Quaternion.Euler(50, 0, 0), 5);
            Debug.Log("Level assembled from prefab instances: " + ScenePath);
        }
    }
}
