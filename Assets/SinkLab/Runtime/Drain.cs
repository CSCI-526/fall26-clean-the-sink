using System;
using System.Collections.Generic;
using UnityEngine;

namespace SinkLab
{
    /// <summary>Collection happens only after food has physically fallen into the opening.</summary>
    public sealed class Drain : MonoBehaviour
    {
        public Transform drainCenter;
        public float radius = .22f;
        public bool squareOpening;
        [Min(0.01f)]
        public float minRadius = 0.04f;
        [Min(0f)]
        public float shrinkPerSecond = 0.0005f;
        [Min(0f)]
        public float shrinkPerScrap = 0.006f;
        [Min(0f)]
        [Tooltip("Distance below the drain opening, in its local frame, that food must cross before collection.")]
        public float captureDepth = .07f;

        [Header("Authored drain parts")]
        [Tooltip("The saved circular opening. Its renderer and collider share the same mesh.")]
        public MeshFilter openingMesh;
        [Tooltip("A non-convex collider on the static opening, so the center remains hollow.")]
        public MeshCollider openingCollider;
        public Transform plug;
        public Transform darkInterior;

        public float captureHeight => CaptureFrame.TransformPoint(new Vector3(0f, -captureDepth, 0f)).y;
        public int DrainedCount { get; private set; }
        public bool IsOpen { get; private set; } = true;
        public bool IsSealed { get; private set; }
        public bool HasFullOpenCharge { get; private set; } = true;
        public float StartRadius => _originReady ? _startRadius : radius;

        // Rebuild only after a visible change in radius, measured in sink-local meters.
        const float OpeningRebuildStep = 0.0015f;
        const float OpeningThickness = 0.12f;
        const float OpeningOuterMargin = 0.02f;
        const int OpeningSegments = 96;

        Transform CaptureFrame => drainCenter != null ? drainCenter : transform;
        float _startRadius;
        bool _originReady;
        Mesh _authoredOpeningMesh;
        Mesh _runtimeOpeningMesh;
        float _authoredHoleRadius;
        float _openingOuterHalfWidth;
        float _builtRadius = -1f;
        bool _fastShrink;
        float _fastShrinkTarget;

        void Awake()
        {
            _startRadius = Mathf.Max(radius, minRadius);
            _originReady = true;
            _openingOuterHalfWidth = _startRadius + OpeningOuterMargin;
            _authoredOpeningMesh = openingMesh != null ? openingMesh.sharedMesh : null;
            if (_authoredOpeningMesh != null)
            {
                // The saved centered annulus defines the fixed floor footprint independently
                // of a prefab variant's configured starting aperture.
                Bounds bounds = _authoredOpeningMesh.bounds;
                _openingOuterHalfWidth = Mathf.Min(bounds.extents.x, bounds.extents.z);
                _authoredHoleRadius = float.PositiveInfinity;
                foreach (Vector3 vertex in _authoredOpeningMesh.vertices)
                {
                    float radialDistance = new Vector2(vertex.x, vertex.z).magnitude;
                    _authoredHoleRadius = Mathf.Min(_authoredHoleRadius, radialDistance);
                }

                // An overridden aperture must be built on Start, even if the difference
                // is smaller than the normal animation update threshold.
                if (Mathf.Approximately(_authoredHoleRadius, _startRadius))
                {
                    _builtRadius = _authoredHoleRadius;
                }
            }
        }

        void Start()
        {
            UpdateOpening();
        }

        void FixedUpdate()
        {
            if (_originReady && Application.isPlaying && !IsSealed)
            {
                float rate = shrinkPerSecond * (_fastShrink ? 40f : 1f);
                radius = Mathf.Max(minRadius, radius - rate * Time.fixedDeltaTime);
                if (_fastShrink && radius <= _fastShrinkTarget)
                {
                    radius = Mathf.Max(minRadius, _fastShrinkTarget);
                    _fastShrink = false;
                }

                if (radius <= minRadius + 0.0001f)
                {
                    Seal();
                }
            }

            UpdateOpening();
            foreach (FoodScrap food in FoodScrap.Active)
            {
                TryConsume(food);
            }
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
            if (!HasFullOpenCharge)
            {
                return false;
            }

            HasFullOpenCharge = false;
            _fastShrinkTarget = radius;
            _fastShrink = true;
            IsSealed = false;
            IsOpen = true;
            radius = _originReady ? _startRadius : Mathf.Max(radius, minRadius);
            if (radius <= _fastShrinkTarget)
            {
                _fastShrink = false;
            }

            UpdateOpening();
            ApplyPlug();
            return true;
        }

        void Seal()
        {
            if (IsSealed)
            {
                return;
            }

            IsSealed = true;
            radius = minRadius;
            SinkWorld owner = GetComponentInParent<SinkWorld>();
            if (owner != null && owner.water != null)
            {
                owner.water.SetSpraying(false);
            }

            UpdateOpening();
            ApplyPlug();
        }

        public bool TryConsume(FoodScrap food)
        {
            if (!IsOpen || IsSealed)
            {
                return false;
            }

            if (food == null || food.IsDrained || !food.isActiveAndEnabled || food.Body == null)
            {
                return false;
            }

            if (food.GetComponentInParent<SinkWorld>() != GetComponentInParent<SinkWorld>())
            {
                return false;
            }

            Vector3 position = CaptureFrame.InverseTransformPoint(food.Body.worldCenterOfMass);
            Vector2 offset = new Vector2(position.x, position.z);
            // No magnet or distance shortcut: the center must cross below the actual floor opening.
            bool inside = squareOpening
                ? Mathf.Abs(offset.x) < radius && Mathf.Abs(offset.y) < radius
                : offset.sqrMagnitude < radius * radius;
            if (position.y >= -captureDepth || !inside)
            {
                return false;
            }

            food.MarkDrained();
            DrainedCount++;
            radius = Mathf.Max(minRadius, radius - shrinkPerScrap);
            if (Application.isPlaying && radius <= minRadius + 0.0001f)
            {
                Seal();
            }
            else
            {
                UpdateOpening();
            }

            return true;
        }

        public void ResetCount()
        {
            DrainedCount = 0;
            IsOpen = true;
            IsSealed = false;
            HasFullOpenCharge = true;
            _fastShrink = false;
            if (_originReady)
            {
                radius = _startRadius;
            }

            UpdateOpening();
            ApplyPlug();
        }

        void UpdateOpening()
        {
            if (!_originReady)
            {
                return;
            }

            float holeRadius = Mathf.Clamp(radius, minRadius, _startRadius);
            if (openingMesh != null && openingCollider != null)
            {
                UpdateOpeningMesh(holeRadius);
            }

            ScaleInterior(holeRadius);
            ApplyPlug();
        }

        void UpdateOpeningMesh(float holeRadius)
        {
            if (_authoredOpeningMesh != null && Mathf.Approximately(holeRadius, _authoredHoleRadius))
            {
                // Reuse the asset only when its actual aperture matches the requested one.
                // A variant's reset radius may require a runtime mesh instead.
                AssignOpeningMesh(_authoredOpeningMesh);
                ReleaseRuntimeMesh();
                _builtRadius = _authoredHoleRadius;
                return;
            }

            bool radiusChanged = Mathf.Abs(_builtRadius - holeRadius) > OpeningRebuildStep;
            bool reachedMinimum = holeRadius <= minRadius && _builtRadius != holeRadius;
            bool reachedStart = Mathf.Approximately(holeRadius, _startRadius) &&
                !Mathf.Approximately(holeRadius, _builtRadius);
            if (!radiusChanged && !reachedMinimum && !reachedStart)
            {
                return;
            }

            Mesh next = BuildOpeningMesh(
                _openingOuterHalfWidth,
                holeRadius,
                OpeningThickness,
                OpeningSegments);
            next.hideFlags = HideFlags.DontSave;
            AssignOpeningMesh(next);
            ReleaseRuntimeMesh();
            _runtimeOpeningMesh = next;
            _builtRadius = holeRadius;
        }

        void AssignOpeningMesh(Mesh mesh)
        {
            if (openingMesh.sharedMesh != mesh)
            {
                openingMesh.sharedMesh = mesh;
            }

            if (openingCollider.sharedMesh != mesh)
            {
                openingCollider.sharedMesh = mesh;
            }
        }

        void ApplyPlug()
        {
            if (plug == null)
            {
                return;
            }

            float holeRadius = Mathf.Max(minRadius, radius);
            plug.localScale = new Vector3(holeRadius * 2f, 0.012f, holeRadius * 2f);
            plug.localPosition = new Vector3(0f, -0.008f, 0f);
            plug.gameObject.SetActive(!IsOpen);
        }

        /// <summary>
        /// Creates a closed square-to-circle annulus at local floor height for rendering and static collision.
        /// The caller owns the returned mesh. Segment count is rounded up to a multiple of eight so every
        /// square corner has a vertex. Adjacent sections share vertices and have no internal dividing faces.
        /// </summary>
        public static Mesh BuildOpeningMesh(float squareHalf, float holeRadius, float thickness, int segments)
        {
            ValidatePositiveDimension(squareHalf, nameof(squareHalf));
            ValidatePositiveDimension(holeRadius, nameof(holeRadius));
            ValidatePositiveDimension(thickness, nameof(thickness));
            if (holeRadius >= squareHalf)
            {
                throw new ArgumentOutOfRangeException(nameof(holeRadius), "The opening must fit inside the square.");
            }

            segments = Mathf.Max(8, Mathf.CeilToInt(segments / 8f) * 8);
            var vertices = new Vector3[segments * 4];
            var uv = new Vector2[vertices.Length];
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                Vector2 inner = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * holeRadius;
                Vector2 outer = SquareEdge(angle, squareHalf);
                int vertex = i * 4;
                vertices[vertex] = new Vector3(inner.x, 0f, inner.y);
                vertices[vertex + 1] = new Vector3(outer.x, 0f, outer.y);
                vertices[vertex + 2] = new Vector3(inner.x, -thickness, inner.y);
                vertices[vertex + 3] = new Vector3(outer.x, -thickness, outer.y);
                for (int offset = 0; offset < 4; offset++)
                {
                    Vector3 point = vertices[vertex + offset];
                    uv[vertex + offset] = new Vector2(point.x, point.z) / (squareHalf * 2f) + Vector2.one * 0.5f;
                }
            }

            var triangles = new List<int>(segments * 24);
            for (int i = 0; i < segments; i++)
            {
                int current = i * 4;
                int next = ((i + 1) % segments) * 4;
                AddQuad(triangles, current, next, next + 1, current + 1); // Top faces upward.
                AddQuad(triangles, current + 2, current + 3, next + 3, next + 2);
                AddQuad(triangles, current, current + 2, next + 2, next); // Inner wall faces the hole.
                AddQuad(triangles, current + 1, next + 1, next + 3, current + 3);
            }

            var mesh = new Mesh
            {
                name = "Drain opening"
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static void ValidatePositiveDimension(float value, string parameterName)
        {
            if (value <= 0f || float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, "Mesh dimensions must be finite and positive.");
            }
        }

        static Vector2 SquareEdge(float angle, float half)
        {
            float cosine = Mathf.Cos(angle);
            float sine = Mathf.Sin(angle);
            float reach = half / Mathf.Max(Mathf.Abs(cosine), Mathf.Abs(sine));
            return new Vector2(cosine * reach, sine * reach);
        }

        static void AddQuad(List<int> triangles, int a, int b, int c, int d)
        {
            triangles.Add(a);
            triangles.Add(b);
            triangles.Add(c);
            triangles.Add(a);
            triangles.Add(c);
            triangles.Add(d);
        }

        void ScaleInterior(float openingRadius)
        {
            if (darkInterior == null)
            {
                return;
            }

            Vector3 scale = darkInterior.localScale;
            scale.x = openingRadius * 2f;
            scale.z = openingRadius * 2f;
            darkInterior.localScale = scale;
        }

        void ReleaseRuntimeMesh()
        {
            if (_runtimeOpeningMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_runtimeOpeningMesh);
            }
            else
            {
                DestroyImmediate(_runtimeOpeningMesh);
            }

            _runtimeOpeningMesh = null;
        }

        void OnDestroy()
        {
            ReleaseRuntimeMesh();
        }
    }
}
