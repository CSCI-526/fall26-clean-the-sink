using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SinkLab
{
    /// <summary>
    /// Replaces the basin's sharp inner corners with fillets at play time.
    /// Straight authored pieces stay in place; only their overlapping ends are shortened.
    /// </summary>
    public sealed class BasinCorners : MonoBehaviour
    {
        public bool shaped;
        readonly List<Object> generated = new List<Object>();

        public void Track(Object asset)
        {
            if (asset == null) return;
            asset.hideFlags = HideFlags.DontSave;
            generated.Add(asset);
        }

        void OnDestroy()
        {
            for (int i = 0; i < generated.Count; i++)
            {
                Object asset = generated[i];
                if (asset == null) continue;
                if (Application.isPlaying) Destroy(asset);
                else DestroyImmediate(asset);
            }
        }
    }

    public static class BasinRounding
    {
        public const float CornerRadius = 0.32f;
        const float InnerHalfX = 1.50f;
        const float InnerHalfZ = 1.05f;
        const float FloorTop = 0.80f;
        const float FloorThickness = 0.12f;
        const float WallHeight = 0.20f;
        const float WallThickness = 0.12f;
        const float RimBottom = 0.965f;
        const float RimHeight = 0.07f;
        const float RimThickness = 0.195f;
        const int Segments = 12;

        public static void Apply(SinkAssembly sink)
        {
            if (sink == null) return;
            Transform root = sink.transform;
            BasinCorners state = sink.GetComponent<BasinCorners>();
            if (state != null && state.shaped && root.Find("Rounded corners") != null) return;
            if (state == null) state = sink.gameObject.AddComponent<BasinCorners>();
            if (!state.shaped)
            {
                if (!ReshapeStraightSections(root)) return;
                state.shaped = true;
            }
            if (root.Find("Rounded corners") == null) BuildCorners(root, state);
            Physics.SyncTransforms();
        }

        static bool ReshapeStraightSections(Transform sink)
        {
            float straightZ = (InnerHalfZ * 2f) - (CornerRadius * 2f) + 0.04f;
            float straightX = (InnerHalfX * 2f) - (CornerRadius * 2f) + 0.04f;
            float floorZ = (InnerHalfZ * 2f) - (CornerRadius * 2f);
            bool found = SetAxis(sink, "Walls/Basin wall left", 2, straightZ);
            found &= SetAxis(sink, "Walls/Basin wall right", 2, straightZ);
            found &= SetAxis(sink, "Walls/Basin wall front", 0, straightX);
            found &= SetAxis(sink, "Walls/Basin wall back", 0, straightX);
            found &= SetAxis(sink, "Rim/Left rim", 2, straightZ);
            found &= SetAxis(sink, "Rim/Right rim", 2, straightZ);
            found &= SetAxis(sink, "Rim/Front rim", 0, straightX);
            found &= SetAxis(sink, "Rim/Back rim", 0, straightX);
            found &= SetAxis(sink, "Floor/Basin floor left", 2, floorZ);
            found &= SetAxis(sink, "Floor/Basin floor right", 2, floorZ);
            return found;
        }

        static bool SetAxis(Transform sink, string path, int axis, float size)
        {
            Transform piece = sink.Find(path);
            if (piece == null) return false;
            Vector3 scale = piece.localScale;
            scale[axis] = size;
            piece.localScale = scale;
            return true;
        }

        static void BuildCorners(Transform sink, BasinCorners state)
        {
            Transform walls = sink.Find("Walls/Basin wall left");
            Transform floor = sink.Find("Floor/Basin floor left");
            Transform rim = sink.Find("Rim/Left rim");
            if (walls == null || floor == null || rim == null) return;

            Material wallMaterial = DoubleSided(walls.GetComponent<Renderer>().sharedMaterial);
            Material floorMaterial = DoubleSided(floor.GetComponent<Renderer>().sharedMaterial);
            Material rimMaterial = DoubleSided(rim.GetComponent<Renderer>().sharedMaterial);
            state.Track(wallMaterial);
            state.Track(floorMaterial);
            state.Track(rimMaterial);

            Transform holder = new GameObject("Rounded corners").transform;
            holder.SetParent(sink, false);

            Vector2[] corners =
            {
                new Vector2(-1f, -1f),
                new Vector2(1f, -1f),
                new Vector2(1f, 1f),
                new Vector2(-1f, 1f)
            };
            for (int i = 0; i < corners.Length; i++)
            {
                float sx = corners[i].x;
                float sz = corners[i].y;
                Vector3 center = new Vector3(sx * (InnerHalfX - CornerRadius), 0f, sz * (InnerHalfZ - CornerRadius));
                float start = StartAngle(sx, sz);
                Place(holder, "Basin corner wall " + i, wallMaterial,
                    QuarterRing(CornerRadius, CornerRadius + WallThickness, WallHeight, start, Segments),
                    center + Vector3.up * FloorTop, state);
                Place(holder, "Basin corner rim " + i, rimMaterial,
                    QuarterRing(CornerRadius, CornerRadius + RimThickness, RimHeight, start, Segments),
                    center + Vector3.up * RimBottom, state);
                Place(holder, "Basin corner floor " + i, floorMaterial,
                    QuarterDisk(CornerRadius + 0.012f, FloorThickness, start, Segments),
                    center + Vector3.up * (FloorTop - FloorThickness), state, true);
                BoxFill(holder, floorMaterial, sx, sz);
            }
        }

        static void BoxFill(Transform holder, Material material, float sx, float sz)
        {
            float innerX = InnerHalfX - CornerRadius;
            float innerZ = InnerHalfZ - CornerRadius;
            const float drain = 0.28f;
            float x0 = sx < 0f ? -(innerX + 0.012f) : drain;
            float x1 = sx < 0f ? -drain : innerX + 0.012f;
            float z0 = sz < 0f ? -InnerHalfZ : innerZ - 0.012f;
            float z1 = sz < 0f ? -innerZ + 0.012f : InnerHalfZ;
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Basin floor fillet";
            box.transform.SetParent(holder, false);
            box.transform.localPosition = new Vector3((x0 + x1) * 0.5f, 0.74f, (z0 + z1) * 0.5f);
            box.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), FloorThickness, Mathf.Abs(z1 - z0));
            Collider boxCollider = box.GetComponent<Collider>();
            if (boxCollider != null) boxCollider.contactOffset = 0.001f;
            Renderer renderer = box.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void Place(Transform parent, string name, Material material, Mesh mesh, Vector3 localPosition, BasinCorners state, bool convex = false)
        {
            state.Track(mesh);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
            collider.convex = convex;
            collider.contactOffset = 0.001f;
        }

        static Material DoubleSided(Material source)
        {
            if (source == null) return null;
            Material copy = new Material(source) { name = source.name + " rounded" };
            copy.hideFlags = HideFlags.DontSave;
            if (copy.HasProperty("_Cull")) copy.SetInt("_Cull", (int)CullMode.Off);
            return copy;
        }

        static float StartAngle(float sx, float sz)
        {
            if (sx < 0f && sz < 0f) return Mathf.PI;
            if (sx > 0f && sz < 0f) return -Mathf.PI * 0.5f;
            if (sx > 0f && sz > 0f) return 0f;
            return Mathf.PI * 0.5f;
        }

        public static Mesh BuildWaterSurface(float halfX, float halfZ, float radius, float height)
        {
            Mesh mesh = RoundedPrism(halfX, halfZ, radius, height, 10);
            mesh.name = "Basin water";
            return mesh;
        }

        static Mesh QuarterRing(float inner, float outer, float height, float start, int segments)
        {
            int count = segments + 1;
            var vertices = new List<Vector3>(count * 4);
            for (int i = 0; i < count; i++)
            {
                float angle = start + (Mathf.PI * 0.5f) * (i / (float)segments);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(direction * inner);
                vertices.Add(direction * outer);
                vertices.Add(direction * inner + Vector3.up * height);
                vertices.Add(direction * outer + Vector3.up * height);
            }

            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                int a = i * 4;
                int b = (i + 1) * 4;
                AddQuad(triangles, a, b, b + 2, a + 2);
                AddQuad(triangles, a + 1, a + 3, b + 3, b + 1);
                AddQuad(triangles, a + 2, b + 2, b + 3, a + 3);
                AddQuad(triangles, a, a + 1, b + 1, b);
            }
            AddQuad(triangles, 0, 2, 3, 1);
            int last = (count - 1) * 4;
            AddQuad(triangles, last, last + 1, last + 3, last + 2);
            return Finish(vertices, triangles, "Basin corner ring");
        }

        static Mesh QuarterDisk(float radius, float height, float start, int segments)
        {
            var vertices = new List<Vector3> { Vector3.zero, Vector3.up * height };
            for (int i = 0; i <= segments; i++)
            {
                float angle = start + (Mathf.PI * 0.5f) * (i / (float)segments);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                vertices.Add(direction);
                vertices.Add(direction + Vector3.up * height);
            }

            var triangles = new List<int>();
            for (int i = 0; i < segments; i++)
            {
                int bottom = 2 + i * 2;
                int next = bottom + 2;
                triangles.Add(0); triangles.Add(next); triangles.Add(bottom);
                triangles.Add(1); triangles.Add(bottom + 1); triangles.Add(next + 1);
                AddQuad(triangles, bottom, next, next + 1, bottom + 1);
            }
            AddQuad(triangles, 0, 1, 3, 2);
            int end = vertices.Count - 2;
            AddQuad(triangles, 0, end, end + 1, 1);
            return Finish(vertices, triangles, "Basin corner floor");
        }

        static Mesh RoundedPrism(float halfX, float halfZ, float radius, float height, int segments)
        {
            radius = Mathf.Min(radius, halfX - 0.02f, halfZ - 0.02f);
            var ring = new List<Vector2>();
            AddArc(ring, new Vector2(halfX - radius, halfZ - radius), radius, 0f, segments);
            AddArc(ring, new Vector2(-halfX + radius, halfZ - radius), radius, Mathf.PI * 0.5f, segments);
            AddArc(ring, new Vector2(-halfX + radius, -halfZ + radius), radius, Mathf.PI, segments);
            AddArc(ring, new Vector2(halfX - radius, -halfZ + radius), radius, Mathf.PI * 1.5f, segments);

            int count = ring.Count;
            var vertices = new List<Vector3>(count * 2 + 2);
            var uvs = new List<Vector2>(count * 2 + 2);
            var colors = new List<Color>(count * 2 + 2);
            for (int i = 0; i < count; i++)
            {
                vertices.Add(new Vector3(ring[i].x, 0f, ring[i].y));
                vertices.Add(new Vector3(ring[i].x, height, ring[i].y));
                uvs.Add(new Vector2(0.5f, 0.5f));
                uvs.Add(new Vector2(0.5f, 0.5f));
                colors.Add(Color.white);
                colors.Add(Color.white);
            }
            vertices.Add(new Vector3(0f, height, 0f));
            uvs.Add(new Vector2(0.5f, 0.5f));
            colors.Add(Color.white);
            int topCenter = vertices.Count - 1;

            var triangles = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int bottom = i * 2;
                int next = ((i + 1) % count) * 2;
                AddQuad(triangles, bottom, next, next + 1, bottom + 1);
                triangles.Add(topCenter);
                triangles.Add(bottom + 1);
                triangles.Add(next + 1);
            }

            Mesh mesh = Finish(vertices, triangles, "Basin water");
            mesh.colors = colors.ToArray();
            mesh.uv = uvs.ToArray();
            return mesh;
        }

        static void AddArc(List<Vector2> ring, Vector2 center, float radius, float start, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle = start + (Mathf.PI * 0.5f) * (i / (float)segments);
                ring.Add(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }
        }

        static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(a); triangles.Add(c); triangles.Add(d);
        }

        static Mesh Finish(List<Vector3> vertices, List<int> triangles, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
