using UnityEngine;

namespace SinkLab
{
    /// <summary>Collection happens only after food has physically fallen into the opening.</summary>
    public sealed class Drain : MonoBehaviour
    {
        public Transform drainCenter;
        public float radius = .22f;
        public bool squareOpening;
        [Min(0.01f)] public float minRadius = 0.04f;
        [Min(0f)] public float shrinkPerSecond = 0.0005f;
        [Min(0f)] public float shrinkPerScrap = 0.006f;
        [Min(0f)]
        [Tooltip("Distance below the drain opening, in its local frame, that food must cross before collection. Moves and rotates with this sink.")]
        public float captureDepth = .07f;
        public float captureHeight => CaptureFrame.TransformPoint(new Vector3(0f, -captureDepth, 0f)).y;
        public int DrainedCount { get; private set; }
        public bool IsOpen { get; private set; } = true;
        public bool IsSealed { get; private set; }
        public bool HasFullOpenCharge { get; private set; } = true;
        public float StartRadius => originReady ? startRadius : radius;

        Transform CaptureFrame => drainCenter != null ? drainCenter : transform;
        float startRadius;
        bool originReady;
        Transform[] lips;
        Transform darkInterior;
        Transform iris;
        MeshFilter irisFilter;
        Transform collar;
        Mesh irisMesh;
        Transform plug;
        Material plugMaterial;
        readonly System.Collections.Generic.List<Mesh> segmentMeshes = new System.Collections.Generic.List<Mesh>();
        float builtRadius = -1f;
        bool fastShrink;
        float fastShrinkTarget;

        void Awake()
        {
            startRadius = Mathf.Max(radius, minRadius);
            originReady = true;
        }

        void Start()
        {
            if (Application.isPlaying) EnsureCollar();
        }

        void FixedUpdate()
        {
            if (originReady && Application.isPlaying && !IsSealed)
            {
                float rate = shrinkPerSecond * (fastShrink ? 30f : 1f);
                radius = Mathf.Max(minRadius, radius - rate * Time.fixedDeltaTime);
                if (fastShrink && radius <= fastShrinkTarget)
                {
                    radius = Mathf.Max(minRadius, fastShrinkTarget);
                    fastShrink = false;
                }
                if (radius <= minRadius + 0.0001f) Seal();
            }
            UpdateCollar();
            foreach (FoodScrap food in FoodScrap.Active) TryConsume(food);
        }

        public void SetOpen(bool open)
        {
            IsOpen = open;
            ApplyPlug();
        }

        public void ToggleOpen() => SetOpen(!IsOpen);

        /// <summary>One use per run. Snaps the opening back to its largest size and leaves it unplugged.</summary>
        public bool TryOpenFully()
        {
            if (!HasFullOpenCharge) return false;
            HasFullOpenCharge = false;
            fastShrinkTarget = radius;
            fastShrink = true;
            IsSealed = false;
            IsOpen = true;
            radius = originReady ? startRadius : Mathf.Max(radius, minRadius);
            if (radius <= fastShrinkTarget) fastShrink = false;
            UpdateCollar();
            ApplyPlug();
            return true;
        }

        void Seal()
        {
            if (IsSealed) return;
            IsSealed = true;
            radius = minRadius;
            SinkWorld owner = GetComponentInParent<SinkWorld>();
            if (owner != null && owner.water != null) owner.water.SetSpraying(false);
            UpdateCollar();
            ApplyPlug();
        }

        public bool TryConsume(FoodScrap food)
        {
            if (!IsOpen || IsSealed) return false;
            if (food == null || food.IsDrained || !food.isActiveAndEnabled || food.Body == null) return false;
            if (food.GetComponentInParent<SinkWorld>() != GetComponentInParent<SinkWorld>()) return false;
            Vector3 position = CaptureFrame.InverseTransformPoint(food.Body.worldCenterOfMass);
            Vector2 offset = new Vector2(position.x, position.z);
            // No magnet or distance shortcut: the center must cross below the actual floor opening.
            bool inside = squareOpening
                ? Mathf.Abs(offset.x) < radius && Mathf.Abs(offset.y) < radius
                : offset.sqrMagnitude < radius * radius;
            if (position.y >= -captureDepth || !inside) return false;
            food.MarkDrained();
            DrainedCount++;
            radius = Mathf.Max(minRadius, radius - shrinkPerScrap);
            if (Application.isPlaying && radius <= minRadius + 0.0001f) Seal();
            else UpdateCollar();
            return true;
        }

        public void ResetCount()
        {
            DrainedCount = 0;
            IsOpen = true;
            IsSealed = false;
            HasFullOpenCharge = true;
            fastShrink = false;
            if (originReady) radius = startRadius;
            UpdateCollar();
            ApplyPlug();
        }

        void EnsureCollar()
        {
            if (iris != null) return;
            var lipList = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in transform)
            {
                if (child.name.StartsWith("Drain lip")) lipList.Add(child);
                else if (child.name == "Dark drain interior") darkInterior = child;
            }
            lips = lipList.ToArray();
            for (int i = 0; i < lips.Length; i++)
            {
                if (lips[i] == null) continue;
                Renderer lipRenderer = lips[i].GetComponent<Renderer>();
                if (lipRenderer != null) lipRenderer.enabled = false;
                Collider lipCollider = lips[i].GetComponent<Collider>();
                if (lipCollider != null) lipCollider.enabled = false;
            }

            // The authored floor gap is square. A flush ring closes its corners so the
            // opening the player sees and walks food into is a circle at floor height.
            squareOpening = false;
            var ring = new GameObject("Drain iris");
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.identity;
            iris = ring.transform;
            irisFilter = ring.AddComponent<MeshFilter>();
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FloorMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var blocks = new GameObject("Drain collar");
            blocks.transform.SetParent(ring.transform, false);
            collar = blocks.transform;
            EnsurePlug();
            UpdateCollar();
        }

        Material FloorMaterial()
        {
            SinkAssembly assembly = GetComponentInParent<SinkAssembly>();
            Transform floor = assembly != null ? assembly.transform.Find("Floor/Basin floor left") : null;
            Renderer floorRenderer = floor != null ? floor.GetComponent<Renderer>() : null;
            if (floorRenderer != null && floorRenderer.sharedMaterial != null)
            {
                Material copy = new Material(floorRenderer.sharedMaterial) { name = "Drain floor" };
                copy.hideFlags = HideFlags.DontSave;
                if (copy.HasProperty("_Cull")) copy.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
                return copy;
            }
            Renderer interiorRenderer = darkInterior != null ? darkInterior.GetComponent<Renderer>() : null;
            return interiorRenderer != null ? interiorRenderer.sharedMaterial : null;
        }

        void UpdateCollar()
        {
            if (!originReady || iris == null) return;
            float hole = Mathf.Clamp(radius, minRadius, startRadius);
            if (irisMesh == null || Mathf.Abs(builtRadius - hole) > 0.0015f)
            {
                float outer = startRadius + 0.02f;
                Mesh next = BuildFlushRing(outer, hole, 0.12f, 36);
                next.hideFlags = HideFlags.DontSave;
                if (irisMesh != null) Destroy(irisMesh);
                irisMesh = next;
                irisFilter.sharedMesh = next;
                RebuildCollar(outer, hole, 0.12f, 24);
                builtRadius = hole;
            }
            ScaleInterior(hole);
            ApplyPlug();
        }

        void EnsurePlug()
        {
            if (plug != null) return;
            var stopper = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stopper.name = "Drain plug";
            stopper.transform.SetParent(transform, false);
            Collider capsule = stopper.GetComponent<Collider>();
            if (capsule != null)
            {
                capsule.enabled = false;
                Destroy(capsule);
            }
            MeshFilter filter = stopper.GetComponent<MeshFilter>();
            MeshCollider meshCollider = stopper.AddComponent<MeshCollider>();
            meshCollider.convex = true;
            meshCollider.sharedMesh = filter.sharedMesh;
            Renderer renderer = stopper.GetComponent<Renderer>();
            plugMaterial = new Material(renderer.sharedMaterial) { name = "Drain plug" };
            plugMaterial.hideFlags = HideFlags.DontSave;
            Color color = new Color(0.18f, 0.2f, 0.22f);
            plugMaterial.color = color;
            if (plugMaterial.HasProperty("_BaseColor")) plugMaterial.SetColor("_BaseColor", color);
            renderer.sharedMaterial = plugMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            plug = stopper.transform;
            ApplyPlug();
        }

        void ApplyPlug()
        {
            if (plug == null) return;
            float hole = Mathf.Max(minRadius, radius);
            plug.localScale = new Vector3(hole * 2f, 0.012f, hole * 2f);
            plug.localPosition = new Vector3(0f, -0.008f, 0f);
            plug.gameObject.SetActive(!IsOpen);
        }

        void RebuildCollar(float squareHalf, float holeRadius, float thickness, int segments)
        {
            if (collar == null) return;
            for (int i = collar.childCount - 1; i >= 0; i--)
            {
                GameObject child = collar.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
            for (int i = 0; i < segmentMeshes.Count; i++)
            {
                Mesh old = segmentMeshes[i];
                if (old == null) continue;
                if (Application.isPlaying) Destroy(old);
                else DestroyImmediate(old);
            }
            segmentMeshes.Clear();

            holeRadius = Mathf.Min(holeRadius, squareHalf - 0.004f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Mesh piece = SegmentMesh(a0, a1, holeRadius, squareHalf, thickness);
                piece.hideFlags = HideFlags.DontSave;
                segmentMeshes.Add(piece);
                var block = new GameObject("Drain collar block");
                block.transform.SetParent(collar, false);
                MeshCollider collider = block.AddComponent<MeshCollider>();
                collider.convex = true;
                collider.contactOffset = 0.001f;
                collider.sharedMesh = piece;
            }
        }

        static Mesh SegmentMesh(float a0, float a1, float holeRadius, float squareHalf, float thickness)
        {
            float lip = Mathf.Max(0.02f, holeRadius - 0.035f);
            Vector2 inner0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * lip;
            Vector2 inner1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * lip;
            Vector2 outer0 = SquareEdge(a0, squareHalf);
            Vector2 outer1 = SquareEdge(a1, squareHalf);
            var mesh = new Mesh { name = "Drain collar block" };
            mesh.vertices = new[]
            {
                new Vector3(inner0.x, -0.055f, inner0.y),
                new Vector3(inner1.x, -0.055f, inner1.y),
                new Vector3(outer1.x, -0.004f, outer1.y),
                new Vector3(outer0.x, -0.004f, outer0.y),
                new Vector3(inner0.x, -thickness, inner0.y),
                new Vector3(inner1.x, -thickness, inner1.y),
                new Vector3(outer1.x, -thickness, outer1.y),
                new Vector3(outer0.x, -thickness, outer0.y)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 6, 2, 3, 7, 6,
                0, 4, 7, 0, 7, 3,
                1, 2, 6, 1, 6, 5
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh BuildFlushRing(float squareHalf, float holeRadius, float thickness, int segments)
        {
            holeRadius = Mathf.Min(holeRadius, squareHalf - 0.004f);
            var vertices = new System.Collections.Generic.List<Vector3>(segments * 8);
            var triangles = new System.Collections.Generic.List<int>(segments * 36);
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector2 inner0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * holeRadius;
                Vector2 inner1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * holeRadius;
                Vector2 outer0 = SquareEdge(a0, squareHalf);
                Vector2 outer1 = SquareEdge(a1, squareHalf);
                int v = vertices.Count;
                vertices.Add(new Vector3(inner0.x, 0f, inner0.y));
                vertices.Add(new Vector3(inner1.x, 0f, inner1.y));
                vertices.Add(new Vector3(outer1.x, 0f, outer1.y));
                vertices.Add(new Vector3(outer0.x, 0f, outer0.y));
                vertices.Add(new Vector3(inner0.x, -thickness, inner0.y));
                vertices.Add(new Vector3(inner1.x, -thickness, inner1.y));
                vertices.Add(new Vector3(outer1.x, -thickness, outer1.y));
                vertices.Add(new Vector3(outer0.x, -thickness, outer0.y));
                AddQuad(triangles, v, v + 3, v + 2, v + 1);
                AddQuad(triangles, v + 4, v + 5, v + 6, v + 7);
                AddQuad(triangles, v, v + 1, v + 5, v + 4);
                AddQuad(triangles, v + 3, v + 7, v + 6, v + 2);
                AddQuad(triangles, v, v + 4, v + 7, v + 3);
                AddQuad(triangles, v + 1, v + 2, v + 6, v + 5);
            }
            var mesh = new Mesh { name = "Flush drain ring" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Vector2 SquareEdge(float angle, float half)
        {
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            float reachX = Mathf.Abs(cosine) > 0.0001f ? half / Mathf.Abs(cosine) : 1000f;
            float reachZ = Mathf.Abs(sine) > 0.0001f ? half / Mathf.Abs(sine) : 1000f;
            float reach = Mathf.Min(reachX, reachZ);
            return new Vector2(cosine * reach, sine * reach);
        }

        static void AddQuad(System.Collections.Generic.List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        void ScaleInterior(float opening)
        {
            if (darkInterior == null) return;
            Vector3 scale = darkInterior.localScale;
            scale.x = opening * 2f;
            scale.z = opening * 2f;
            darkInterior.localScale = scale;
        }

        void OnDestroy()
        {
            if (irisMesh != null)
            {
                if (Application.isPlaying) Destroy(irisMesh);
                else DestroyImmediate(irisMesh);
            }
            for (int i = 0; i < segmentMeshes.Count; i++)
            {
                Mesh old = segmentMeshes[i];
                if (old == null) continue;
                if (Application.isPlaying) Destroy(old);
                else DestroyImmediate(old);
            }
            if (plugMaterial != null)
            {
                if (Application.isPlaying) Destroy(plugMaterial);
                else DestroyImmediate(plugMaterial);
            }
        }
    }
}
