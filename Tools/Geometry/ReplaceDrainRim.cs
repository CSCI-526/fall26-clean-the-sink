// Run through the live Editor: unity command eval_file --file Tools/Geometry/ReplaceDrainRim.cs
// One reusable annular mesh replaces the original individual lip objects.
const string drainPath = "Assets/SinkLab/Prefabs/Sink/Parts/Drain.prefab";
const string ringPath = "Assets/SinkLab/Prefabs/Sink/Parts/DrainRim.prefab";
const string meshPath = "Assets/SinkLab/Meshes/DrainRim.asset";
if (EditorApplication.isPlaying)
{
    throw new InvalidOperationException("Stop Play mode before changing the drain prefab.");
}

var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
if (stage != null && stage.scene.isDirty)
{
    throw new InvalidOperationException("Save changes in Prefab Mode before updating the drain.");
}

var contents = PrefabUtility.LoadPrefabContents(drainPath);
try
{
    var lips = contents.GetComponentsInChildren<Transform>(true)
        .Where(t => t.name.StartsWith("Drain lip")).ToArray();
    if (lips.Length != 24)
    {
        throw new InvalidOperationException(
            "Expected the original 24 drain lips; found " + lips.Length + ". No change applied.");
    }

    if (AssetDatabase.LoadAssetAtPath<Mesh>(meshPath) || AssetDatabase.LoadAssetAtPath<GameObject>(ringPath))
    {
        throw new InvalidOperationException("A ring asset already exists; inspect it before replacing it.");
    }

    var material = lips[0].GetComponent<Renderer>().sharedMaterial;
    if (!material)
    {
        throw new InvalidOperationException("The existing chrome material is missing.");
    }

    foreach (var lip in lips)
    {
        if (lip.GetComponent<Collider>())
        {
            throw new InvalidOperationException("A lip has custom physics; inspect it before changing it.");
        }
    }

    const int segments = 96;
    const float inner = .2945f;
    const float outer = .3218f;
    const float top = .006f;
    const float bottom = -.006f;
    const float bevel = .0015f;
    var profile = new Vector2[]
    {
        new Vector2(inner + bevel, top),
        new Vector2(outer - bevel, top),
        new Vector2(outer, top - bevel),
        new Vector2(outer, bottom + bevel),
        new Vector2(outer - bevel, bottom),
        new Vector2(inner + bevel, bottom),
        new Vector2(inner, bottom + bevel),
        new Vector2(inner, top - bevel)
    };
    var vertices = new List<Vector3>();
    var normals = new List<Vector3>();
    var uv = new List<Vector2>();
    var triangles = new List<int>();
    for (int edge = 0; edge < profile.Length; edge++)
    {
        Vector2 a = profile[edge];
        Vector2 b = profile[(edge + 1) % profile.Length];
        Vector2 normal = new Vector2(-(b.y - a.y), b.x - a.x).normalized;
        int start = vertices.Count;
        for (int step = 0; step <= segments; step++)
        {
            float angle = step * Mathf.PI * 2f / segments;
            float c = Mathf.Cos(angle);
            float s = Mathf.Sin(angle);
            vertices.Add(new Vector3(c * a.x, a.y, s * a.x));
            vertices.Add(new Vector3(c * b.x, b.y, s * b.x));
            normals.Add(new Vector3(c * normal.x, normal.y, s * normal.x));
            normals.Add(new Vector3(c * normal.x, normal.y, s * normal.x));
            uv.Add(new Vector2((float)step / segments, 0));
            uv.Add(new Vector2((float)step / segments, 1));
            if (step == segments)
            {
                continue;
            }

            int i = start + step * 2;
            triangles.Add(i);
            triangles.Add(i + 3);
            triangles.Add(i + 1);
            triangles.Add(i);
            triangles.Add(i + 2);
            triangles.Add(i + 3);
        }
    }

    var mesh = new Mesh { name = "DrainRim" };
    mesh.SetVertices(vertices);
    mesh.SetNormals(normals);
    mesh.SetUVs(0, uv);
    mesh.SetTriangles(triangles, 0);
    mesh.RecalculateBounds();
    if (!AssetDatabase.IsValidFolder("Assets/SinkLab/Meshes"))
    {
        AssetDatabase.CreateFolder("Assets/SinkLab", "Meshes");
    }

    AssetDatabase.CreateAsset(mesh, meshPath);

    var ring = new GameObject("DrainRim");
    ring.transform.SetParent(contents.transform, false);
    ring.layer = 2;
    ring.AddComponent<MeshFilter>().sharedMesh = mesh;
    ring.AddComponent<MeshRenderer>().sharedMaterial = material;
    PrefabUtility.SaveAsPrefabAssetAndConnect(ring, ringPath, InteractionMode.AutomatedAction);
    ring.name = "Rim";
    ring.transform.localPosition = new Vector3(0, .005f, 0);
    PrefabUtility.RecordPrefabInstancePropertyModifications(ring);
    PrefabUtility.RecordPrefabInstancePropertyModifications(ring.transform);
    foreach (var lip in lips)
    {
        UnityEngine.Object.DestroyImmediate(lip.gameObject);
    }

    PrefabUtility.SaveAsPrefabAsset(contents, drainPath);
    AssetDatabase.SaveAssets();
    return new
    {
        changed = drainPath,
        ringPrefab = ringPath,
        mesh = meshPath,
        removedLipObjects = lips.Length,
        ringObjects = 1,
        segments,
        vertices = vertices.Count,
        triangles = triangles.Count / 3,
        innerRadius = inner,
        outerRadius = outer,
        colliderAdded = false
    };
}
finally
{
    PrefabUtility.UnloadPrefabContents(contents);
}
