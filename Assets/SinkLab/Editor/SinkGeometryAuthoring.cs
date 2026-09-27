using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SinkLab.Editor
{
    /// <summary>
    /// Saves the prototype's previously generated geometry into reusable source assets.
    /// Existing authored geometry is reused, so rerunning this does not replace manual edits.
    /// </summary>
    public static class SinkGeometryAuthoring
    {
        const string SinkPath = "Assets/SinkLab/Prefabs/Sink/Sink.prefab";
        const string WallPath = "Assets/SinkLab/Prefabs/Sink/Parts/SinkWall.prefab";
        const string DrainPath = "Assets/SinkLab/Prefabs/Sink/Parts/Drain.prefab";
        const string CornersPath = "Assets/SinkLab/Prefabs/Sink/Parts/RoundedBasinCorners.prefab";
        const string MeshDirectory = "Assets/SinkLab/Meshes/RoundedBasin";
        const string MaterialDirectory = "Assets/SinkLab/Materials";
        const string WallMaterialPath = MaterialDirectory + "/Basin wall rebound.physicMaterial";
        const float OpeningRecessMeters = 0.001f;

        static readonly string[] StraightSectionPaths =
        {
            "Walls/Basin wall left",
            "Walls/Basin wall right",
            "Walls/Basin wall front",
            "Walls/Basin wall back",
            "Rim/Left rim",
            "Rim/Right rim",
            "Rim/Front rim",
            "Rim/Back rim",
            "Floor/Basin floor left",
            "Floor/Basin floor right"
        };

        [MenuItem("Sink Lab/Author saved basin geometry")]
        public static void Bake()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Stop Play mode before authoring sink geometry.");
            }

            if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                throw new InvalidOperationException("Close Prefab mode before updating source prefabs.");
            }

            foreach (string path in new[] { SinkPath, WallPath, DrainPath })
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                {
                    throw new InvalidOperationException("Required source prefab is missing: " + path);
                }
            }

            EnsureFolder(MeshDirectory);
            PhysicsMaterial wallMaterial = GetWallMaterial();
            AuthorStraightWall(wallMaterial);
            AuthorRoundedBasin(wallMaterial);
            AuthorDrainOpening();
            AssetDatabase.SaveAssets();
            Debug.Log("Saved rounded basin, continuous drain opening, and contact-only wall rebound.");
        }

        /// <summary>
        /// Repairs the known overlap and winding defects in this integration's generated assets.
        /// Existing mesh assets are updated in place; their GUIDs, material assignments and prefab links stay intact.
        /// This is a bounded migration for the authored basin, not a general prefab regeneration command.
        /// </summary>
        [MenuItem("Sink Lab/Repair generated basin seams")]
        public static void RepairGeneratedSeams()
        {
            if (EditorApplication.isPlaying ||
                UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                throw new InvalidOperationException("Stop Play mode and close Prefab mode before repairing seams.");
            }

            GameObject templateRoot = null;
            GameObject sinkRoot = null;
            GameObject cornersRoot = null;
            GameObject drainRoot = null;
            try
            {
                templateRoot = PrefabUtility.LoadPrefabContents(SinkPath);
                sinkRoot = PrefabUtility.LoadPrefabContents(SinkPath);
                cornersRoot = PrefabUtility.LoadPrefabContents(CornersPath);
                drainRoot = PrefabUtility.LoadPrefabContents(DrainPath);

                Transform oldCorners = templateRoot.transform.Find("Rounded corners");
                Transform savedCorners = sinkRoot.transform.Find("Rounded corners");
                if (oldCorners == null || savedCorners == null ||
                    PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(savedCorners.gameObject) != CornersPath)
                {
                    throw new InvalidOperationException(
                        "The expected generated rounded-corner prefab is not connected.");
                }

                Object.DestroyImmediate(oldCorners.gameObject);
                BasinRounding.Apply(templateRoot.GetComponent<SinkAssembly>());
                Transform correctedCorners = templateRoot.transform.Find("Rounded corners");
                if (correctedCorners == null || correctedCorners.childCount != 16 ||
                    cornersRoot.transform.childCount != 16)
                {
                    throw new InvalidOperationException(
                        "The generated corner layout differs from the known repair layout.");
                }

                // Validate the complete repair set before changing any persistent asset.
                foreach (Transform part in correctedCorners)
                {
                    Transform savedPart = FindMatchingCorner(cornersRoot.transform, part);
                    MeshFilter generatedFilter = part.GetComponent<MeshFilter>();
                    MeshFilter savedFilter = savedPart.GetComponent<MeshFilter>();
                    if (generatedFilter == null || savedFilter == null)
                    {
                        throw new InvalidOperationException("A generated corner part has no mesh: " + part.name);
                    }

                    if (part.name != "Basin floor fillet")
                    {
                        string expectedPath = MeshDirectory + "/" + part.name + ".asset";
                        MeshCollider savedCollider = savedPart.GetComponent<MeshCollider>();
                        if (AssetDatabase.GetAssetPath(savedFilter.sharedMesh) != expectedPath ||
                            savedCollider == null || savedCollider.sharedMesh != savedFilter.sharedMesh)
                        {
                            throw new InvalidOperationException("The generated mesh has been replaced: " + part.name);
                        }
                    }
                }

                foreach (string path in StraightSectionPaths)
                {
                    if (templateRoot.transform.Find(path) == null || sinkRoot.transform.Find(path) == null)
                    {
                        throw new InvalidOperationException("A required straight basin part is missing: " + path);
                    }
                }

                Drain drain = drainRoot.GetComponent<Drain>();
                if (drain.openingMesh == null || drain.openingCollider == null ||
                    drain.openingMesh.transform != drain.openingCollider.transform ||
                    drain.openingMesh.sharedMesh != drain.openingCollider.sharedMesh ||
                    AssetDatabase.GetAssetPath(drain.openingMesh.sharedMesh) !=
                        "Assets/SinkLab/Meshes/DrainOpening.asset")
                {
                    throw new InvalidOperationException("The expected shared drain opening has been replaced.");
                }

                foreach (Transform part in correctedCorners)
                {
                    Transform savedPart = FindMatchingCorner(cornersRoot.transform, part);
                    if (part.name != "Basin floor fillet")
                    {
                        Mesh generatedMesh = part.GetComponent<MeshFilter>().sharedMesh;
                        Mesh savedMesh = savedPart.GetComponent<MeshFilter>().sharedMesh;
                        EditorUtility.CopySerialized(generatedMesh, savedMesh);
                        savedMesh.name = part.name;
                        savedMesh.hideFlags = HideFlags.None;
                        EditorUtility.SetDirty(savedMesh);
                    }

                    CopyLocalTransform(part, savedPart);
                }

                foreach (string path in StraightSectionPaths)
                {
                    Transform savedPart = sinkRoot.transform.Find(path);
                    CopyLocalTransform(templateRoot.transform.Find(path), savedPart);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(savedPart);
                }

                RecessOpening(drain);
                SavePrefab(cornersRoot, CornersPath);
                SavePrefab(sinkRoot, SinkPath);
                SavePrefab(drainRoot, DrainPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Repaired generated basin seams without replacing mesh assets or prefab connections.");
            }
            finally
            {
                foreach (GameObject root in new[] { drainRoot, cornersRoot, sinkRoot, templateRoot })
                {
                    if (root != null)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                }
            }
        }

        static Transform FindMatchingCorner(Transform savedRoot, Transform generatedPart)
        {
            Transform match = null;
            foreach (Transform candidate in savedRoot)
            {
                if (candidate.name != generatedPart.name)
                {
                    continue;
                }

                // The four rectangular floor fills deliberately share one name.
                if (candidate.name == "Basin floor fillet" &&
                    (Mathf.Sign(candidate.localPosition.x) != Mathf.Sign(generatedPart.localPosition.x) ||
                     Mathf.Sign(candidate.localPosition.z) != Mathf.Sign(generatedPart.localPosition.z)))
                {
                    continue;
                }

                if (match != null)
                {
                    throw new InvalidOperationException("Duplicate generated corner part: " + generatedPart.name);
                }

                match = candidate;
            }

            if (match == null)
            {
                throw new InvalidOperationException("Missing generated corner part: " + generatedPart.name);
            }

            return match;
        }

        static void CopyLocalTransform(Transform source, Transform target)
        {
            target.localPosition = source.localPosition;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        static void RecessOpening(Drain drain)
        {
            // The opening overlaps the square floor aperture slightly. Recess both the
            // renderer and its shared collider together so their surfaces cannot z-fight.
            Vector3 position = drain.openingMesh.transform.localPosition;
            position.y = -OpeningRecessMeters;
            drain.openingMesh.transform.localPosition = position;
        }

        static PhysicsMaterial GetWallMaterial()
        {
            PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(WallMaterialPath);
            if (material != null)
            {
                return material;
            }

            material = new PhysicsMaterial("Basin wall rebound")
            {
                // Match Unity's previous default wall friction; only restitution changes.
                dynamicFriction = 0.6f,
                staticFriction = 0.6f,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounciness = 0.4f,
                bounceCombine = PhysicsMaterialCombine.Maximum
            };
            AssetDatabase.CreateAsset(material, WallMaterialPath);
            return material;
        }

        static void AuthorStraightWall(PhysicsMaterial wallMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(WallPath);
            try
            {
                root.GetComponent<Collider>().sharedMaterial = wallMaterial;
                SavePrefab(root, WallPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void AuthorRoundedBasin(PhysicsMaterial wallMaterial)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(SinkPath);
            try
            {
                SinkAssembly sink = root.GetComponent<SinkAssembly>();
                if (root.transform.Find("Rounded corners") == null)
                {
                    BasinRounding.Apply(sink);
                    Transform corners = root.transform.Find("Rounded corners");
                    if (corners == null)
                    {
                        throw new InvalidOperationException("The expected basin parts were not found.");
                    }

                    PersistCornerResources(corners, wallMaterial);

                    // The transient authoring component owns only the original in-memory
                    // resources. Saved clones are rebound first so cleanup cannot delete them.
                    Object.DestroyImmediate(root.GetComponent<BasinCorners>());
                    GameObject cornerAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CornersPath);
                    if (cornerAsset == null)
                    {
                        cornerAsset = PrefabUtility.SaveAsPrefabAsset(corners.gameObject, CornersPath);
                    }

                    if (cornerAsset == null)
                    {
                        throw new InvalidOperationException("Could not save the rounded-corner prefab.");
                    }

                    Object.DestroyImmediate(corners.gameObject);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(cornerAsset, root.scene);
                    instance.transform.SetParent(root.transform, false);
                    instance.name = "Rounded corners";

                    foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(child))
                        {
                            PrefabUtility.RecordPrefabInstancePropertyModifications(child);
                        }
                    }
                }

                foreach (MeshFilter filter in
                    root.transform.Find("Rounded corners").GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null || !EditorUtility.IsPersistent(filter.sharedMesh) ||
                        filter.GetComponent<Renderer>().sharedMaterial == null ||
                        !EditorUtility.IsPersistent(filter.GetComponent<Renderer>().sharedMaterial))
                    {
                        throw new InvalidOperationException(
                            "Existing rounded geometry has unsaved resources: " + filter.name);
                    }
                }

                SavePrefab(root, SinkPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static void PersistCornerResources(Transform corners, PhysicsMaterial wallMaterial)
        {
            var materials = new Dictionary<Material, Material>();
            foreach (MeshFilter filter in corners.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh source = filter.sharedMesh;
                // Built-in cube meshes already have durable references.
                if (!AssetDatabase.Contains(source))
                {
                    string meshPath = MeshDirectory + "/" + filter.name + ".asset";
                    Mesh savedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                    if (savedMesh == null)
                    {
                        savedMesh = Object.Instantiate(source);
                        savedMesh.name = filter.name;
                        savedMesh.hideFlags = HideFlags.None;
                        AssetDatabase.CreateAsset(savedMesh, meshPath);
                    }

                    filter.sharedMesh = savedMesh;
                    MeshCollider collider = filter.GetComponent<MeshCollider>();
                    if (collider != null)
                    {
                        collider.sharedMesh = savedMesh;
                    }
                }

                Renderer renderer = filter.GetComponent<Renderer>();
                Material sourceMaterial = renderer.sharedMaterial;
                if (!AssetDatabase.Contains(sourceMaterial))
                {
                    if (!materials.TryGetValue(sourceMaterial, out Material savedMaterial))
                    {
                        string materialPath = MaterialDirectory + "/" + sourceMaterial.name + ".mat";
                        savedMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                        if (savedMaterial == null)
                        {
                            savedMaterial = new Material(sourceMaterial)
                            {
                                name = sourceMaterial.name,
                                hideFlags = HideFlags.None
                            };
                            AssetDatabase.CreateAsset(savedMaterial, materialPath);
                        }

                        materials.Add(sourceMaterial, savedMaterial);
                    }

                    renderer.sharedMaterial = savedMaterial;
                }

                if (filter.name.StartsWith("Basin corner wall", StringComparison.Ordinal))
                {
                    filter.GetComponent<Collider>().sharedMaterial = wallMaterial;
                }
            }
        }

        static void AuthorDrainOpening()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DrainPath);
            try
            {
                Drain drain = root.GetComponent<Drain>();
                if (drain.openingMesh == null)
                {
                    const string openingMeshPath = "Assets/SinkLab/Meshes/DrainOpening.asset";
                    Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(openingMeshPath);
                    if (mesh == null)
                    {
                        mesh = Drain.BuildOpeningMesh(drain.radius + 0.02f, drain.radius, 0.12f, 96);
                        mesh.name = "Circular drain opening";
                        AssetDatabase.CreateAsset(mesh, openingMeshPath);
                    }

                    var opening = new GameObject("Drain opening");
                    opening.transform.SetParent(root.transform, false);
                    drain.openingMesh = opening.AddComponent<MeshFilter>();
                    drain.openingMesh.sharedMesh = mesh;
                    MeshRenderer renderer = opening.AddComponent<MeshRenderer>();
                    renderer.sharedMaterial = FindFloorMaterial();
                    renderer.shadowCastingMode = ShadowCastingMode.Off;

                    drain.openingCollider = opening.AddComponent<MeshCollider>();
                    drain.openingCollider.convex = false;
                    drain.openingCollider.contactOffset = 0.001f;
                    drain.openingCollider.sharedMesh = mesh;
                    RecessOpening(drain);

                    GameObject stopper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    stopper.name = "Drain plug";
                    stopper.transform.SetParent(root.transform, false);
                    Object.DestroyImmediate(stopper.GetComponent<Collider>());
                    MeshCollider plugCollider = stopper.AddComponent<MeshCollider>();
                    plugCollider.sharedMesh = stopper.GetComponent<MeshFilter>().sharedMesh;
                    plugCollider.convex = true;
                    plugCollider.contactOffset = 0.001f;
                    Renderer plugRenderer = stopper.GetComponent<Renderer>();
                    plugRenderer.sharedMaterial = CreatePlugMaterial();
                    plugRenderer.shadowCastingMode = ShadowCastingMode.Off;
                    drain.plug = stopper.transform;
                    drain.plug.localScale = new Vector3(drain.radius * 2f, 0.012f, drain.radius * 2f);
                    drain.plug.localPosition = new Vector3(0f, -0.008f, 0f);
                    stopper.SetActive(false);
                }

                if (drain.openingMesh.sharedMesh == null ||
                    !EditorUtility.IsPersistent(drain.openingMesh.sharedMesh) ||
                    drain.openingCollider == null || drain.openingCollider.convex ||
                    drain.openingCollider.sharedMesh != drain.openingMesh.sharedMesh || drain.plug == null)
                {
                    throw new InvalidOperationException(
                        "The existing drain opening has incomplete authored references; inspect it before rebaking.");
                }

                drain.darkInterior = root.transform.Find("Dark drain interior");
                drain.squareOpening = false;
                SavePrefab(root, DrainPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        static Material FindFloorMaterial()
        {
            GameObject sink = AssetDatabase.LoadAssetAtPath<GameObject>(SinkPath);
            return sink.transform.Find("Floor/Basin floor left").GetComponent<Renderer>().sharedMaterial;
        }

        static Material CreatePlugMaterial()
        {
            const string path = MaterialDirectory + "/Drain plug.mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(FindFloorMaterial()) { name = "Drain plug" };
            Color color = new Color(0.18f, 0.20f, 0.22f);
            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        static void SavePrefab(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path, out bool succeeded);
            if (!succeeded)
            {
                throw new InvalidOperationException("Could not save prefab: " + path);
            }
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }
    }
}
