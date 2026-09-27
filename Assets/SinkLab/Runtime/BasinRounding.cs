using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SinkLab
{
    /// <summary>
    /// Tracks temporary resources during the one-time Editor authoring operation.
    /// It is removed before the rounded basin prefab is saved.
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
        const float WallThickness = 0.12f;
        const float RimBottom = 0.965f;
        // The corner rim continues the same inner wall surface above this height.
        const float CornerWallHeight = RimBottom - FloorTop;
        const float RimHeight = 0.07f;
        const float RimThickness = 0.195f;
        const int Segments = 12;

        public static void Apply(SinkAssembly sink)
        {
            if (Application.isPlaying)
            {
                throw new System.InvalidOperationException(
                    "Author rounded basin geometry before Play mode using SinkGeometryAuthoring.");
            }

            if (sink == null)
            {
                return;
            }

            Transform root = sink.transform;
            BasinCorners state = sink.GetComponent<BasinCorners>();
            if (state != null && state.shaped && root.Find("Rounded corners") != null)
            {
                return;
            }
            if (state == null)
            {
                state = sink.gameObject.AddComponent<BasinCorners>();
            }

            if (!state.shaped)
            {
                if (!ReshapeStraightSections(root))
                {
                    return;
                }
                state.shaped = true;
            }
            if (root.Find("Rounded corners") == null)
            {
                BuildCorners(root, state);
            }

            Physics.SyncTransforms();
        }

        static bool ReshapeStraightSections(Transform sink)
        {
            // Meet the quarter arcs exactly; overlapping coplanar faces flicker at these joins.
            float straightZ = (InnerHalfZ * 2f) - (CornerRadius * 2f);
            float straightX = (InnerHalfX * 2f) - (CornerRadius * 2f);
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
            if (walls == null || floor == null || rim == null)
            {
                return;
            }

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
                Vector3 center = new Vector3(
                    sx * (InnerHalfX - CornerRadius), 0f, sz * (InnerHalfZ - CornerRadius));
                float start = StartAngle(sx, sz);
                Place(holder, "Basin corner wall " + i, wallMaterial,
                    QuarterRing(CornerRadius, CornerRadius + WallThickness, CornerWallHeight, start, Segments),
                    center + Vector3.up * FloorTop, state);
                Place(holder, "Basin corner rim " + i, rimMaterial,
                    QuarterRing(CornerRadius, CornerRadius + RimThickness, RimHeight, start, Segments),
                    center + Vector3.up * RimBottom, state);
                Place(holder, "Basin corner floor " + i, floorMaterial,
                    QuarterDisk(CornerRadius, FloorThickness, start, Segments),
                    center + Vector3.up * (FloorTop - FloorThickness), state, true);
                BoxFill(holder, floorMaterial, sx, sz);
            }
        }

        static void BoxFill(Transform holder, Material material, float sx, float sz)
        {
            float innerX = InnerHalfX - CornerRadius;
            float innerZ = InnerHalfZ - CornerRadius;
            const float drain = 0.28f;
            float x0 = sx < 0f ? -innerX : drain;
            float x1 = sx < 0f ? -drain : innerX;
            float z0 = sz < 0f ? -InnerHalfZ : innerZ;
            float z1 = sz < 0f ? -innerZ : InnerHalfZ;
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Basin floor fillet";
            box.transform.SetParent(holder, false);
            box.transform.localPosition = new Vector3((x0 + x1) * 0.5f, 0.74f, (z0 + z1) * 0.5f);
            box.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), FloorThickness, Mathf.Abs(z1 - z0));
            Collider boxCollider = box.GetComponent<Collider>();
            if (boxCollider != null)
            {
                boxCollider.contactOffset = 0.001f;
            }
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
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 top = Vector3.up * height;

            for (int i = 0; i < segments; i++)
            {
                float angle0 = start + Mathf.PI * 0.5f * (i / (float)segments);
                float angle1 = start + Mathf.PI * 0.5f * ((i + 1) / (float)segments);
                Vector3 direction0 = ArcDirection(angle0);
                Vector3 direction1 = ArcDirection(angle1);
                Vector3 inner0 = direction0 * inner;
                Vector3 inner1 = direction1 * inner;
                Vector3 outer0 = direction0 * outer;
                Vector3 outer1 = direction1 * outer;

                // Curved faces have radial normals. Caps use separate vertices so their
                // perpendicular normals cannot bend the lighting along the vertical wall.
                AddSurfaceQuad(vertices, normals, triangles,
                    inner0, inner1, inner1 + top, inner0 + top,
                    -direction0, -direction1, -direction1, -direction0);
                AddSurfaceQuad(vertices, normals, triangles,
                    outer0, outer0 + top, outer1 + top, outer1,
                    direction0, direction0, direction1, direction1);
                AddSurfaceQuad(vertices, normals, triangles,
                    inner0 + top, inner1 + top, outer1 + top, outer0 + top, Vector3.up);
                AddSurfaceQuad(vertices, normals, triangles,
                    inner0, outer0, outer1, inner1, Vector3.down);
            }

            Vector3 first = ArcDirection(start);
            Vector3 last = ArcDirection(start + Mathf.PI * 0.5f);
            AddSurfaceQuad(vertices, normals, triangles,
                first * inner, first * inner + top, first * outer + top, first * outer,
                Vector3.Cross(Vector3.up, first));
            AddSurfaceQuad(vertices, normals, triangles,
                last * inner, last * outer, last * outer + top, last * inner + top,
                Vector3.Cross(last, Vector3.up));
            return FinishCornerMesh(vertices, normals, triangles, "Basin corner ring");
        }

        static Mesh QuarterDisk(float radius, float height, float start, int segments)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var triangles = new List<int>();
            Vector3 top = Vector3.up * height;

            for (int i = 0; i < segments; i++)
            {
                float angle0 = start + Mathf.PI * 0.5f * (i / (float)segments);
                float angle1 = start + Mathf.PI * 0.5f * ((i + 1) / (float)segments);
                Vector3 direction0 = ArcDirection(angle0);
                Vector3 direction1 = ArcDirection(angle1);
                Vector3 outer0 = direction0 * radius;
                Vector3 outer1 = direction1 * radius;

                AddSurfaceTriangle(vertices, normals, triangles,
                    Vector3.zero, outer0, outer1, Vector3.down);
                AddSurfaceTriangle(vertices, normals, triangles,
                    top, outer1 + top, outer0 + top, Vector3.up);
                AddSurfaceQuad(vertices, normals, triangles,
                    outer0, outer0 + top, outer1 + top, outer1,
                    direction0, direction0, direction1, direction1);
            }

            Vector3 first = ArcDirection(start);
            Vector3 last = ArcDirection(start + Mathf.PI * 0.5f);
            AddSurfaceQuad(vertices, normals, triangles,
                Vector3.zero, top, first * radius + top, first * radius,
                Vector3.Cross(Vector3.up, first));
            AddSurfaceQuad(vertices, normals, triangles,
                Vector3.zero, last * radius, last * radius + top, top,
                Vector3.Cross(last, Vector3.up));
            return FinishCornerMesh(vertices, normals, triangles, "Basin corner floor");
        }

        static Vector3 ArcDirection(float angle)
        {
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
        }

        static void AddSurfaceTriangle(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
        {
            int first = vertices.Count;
            vertices.AddRange(new[] { a, b, c });
            normals.AddRange(new[] { normal, normal, normal });
            triangles.Add(first);
            triangles.Add(first + 1);
            triangles.Add(first + 2);
        }

        static void AddSurfaceQuad(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            AddSurfaceQuad(vertices, normals, triangles, a, b, c, d, normal, normal, normal, normal);
        }

        static void AddSurfaceQuad(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 normalA, Vector3 normalB, Vector3 normalC, Vector3 normalD)
        {
            int first = vertices.Count;
            vertices.AddRange(new[] { a, b, c, d });
            normals.AddRange(new[] { normalA, normalB, normalC, normalD });
            AddQuad(triangles, first, first + 1, first + 2, first + 3);
        }

        static Mesh FinishCornerMesh(
            List<Vector3> vertices, List<Vector3> normals, List<int> triangles, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
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
