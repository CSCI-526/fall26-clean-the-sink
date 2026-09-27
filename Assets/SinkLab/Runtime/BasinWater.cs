using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SinkLab
{
    /// <summary>
    /// Spray adds water. Higher pressure fills faster, and a smaller drain empties slower.
    /// Overflow reflects the current water level at the rim.
    /// </summary>
    public sealed class BasinWater : MonoBehaviour
    {
        // Basin dimensions are local to its unit-scale, upright sink assembly.
        public const float FloorY = 0.80f;
        public const float OverflowY = 1.035f;
        const float FormerDepth = 1.195f - FloorY;

        public WaterJet water;
        public Drain drain;
        public SinkWorld world;

        public float NormalizedLevel { get; private set; }
        public bool IsOverflowed { get; private set; }
        public float LocalSurfaceY => FloorY + Mathf.Clamp01(NormalizedLevel) * (OverflowY - FloorY);

        /// <summary>World-space surface height for translated and yaw-rotated sink assemblies.</summary>
        public float SurfaceY => BasinFrame.TransformPoint(new Vector3(0f, LocalSurfaceY, 0f)).y;
        public float JetEfficiency => Mathf.Lerp(1f, 0.38f, Mathf.Clamp01(NormalizedLevel));

        Transform BasinFrame => world != null && world.sink != null ? world.sink.transform : transform;
        float LocalDepth => Mathf.Max(0f, LocalSurfaceY - FloorY);

        Transform volume;
        Material volumeMaterial;
        Transform vortex;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static PhysicsMaterial wetSlide;
        readonly Dictionary<Collider, PhysicsMaterial> dryMaterials = new Dictionary<Collider, PhysicsMaterial>();

        public void Configure(SinkWorld owner)
        {
            world = owner;
            if (owner == null) return;
            water = owner.water;
            drain = owner.drain;
            if (water != null) water.basin = this;
            EnsureVolume();
            UpdateVolume();
        }

        public void ResetWater()
        {
            NormalizedLevel = 0f;
            IsOverflowed = false;
            UpdateVolume();
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            float inflow = 0f;
            if (water != null && water.IsSpraying)
            {
                float pressure = water.Pressure * water.Pressure;
                inflow = Mathf.Lerp(0.02f, 0.22f, pressure) / 2f;
                if (water.WideSpray) inflow *= 1.25f;
            }

            float opening = 1f;
            if (drain != null && drain.StartRadius > 0.001f)
            {
                float ratio = Mathf.Clamp01(drain.radius / drain.StartRadius);
                opening = drain.IsOpen ? ratio * ratio : 0f;
            }

            // Area of the hole. A full opening outruns even a wide, high-pressure spray,
            // so the level stays down until the drain has actually shrunk.
            float head = Mathf.Lerp(0.16f, 0.40f, NormalizedLevel);
            float outflow = opening * head;
            float levelBefore = NormalizedLevel;
            // A shallower bowl holds less, so the same stream fills and drains it sooner.
            float volume = FormerDepth / Mathf.Max(0.05f, OverflowY - FloorY);
            NormalizedLevel = Mathf.Clamp01(NormalizedLevel + (inflow - outflow) * volume * dt);
            bool rising = NormalizedLevel > levelBefore + 0.00001f && NormalizedLevel >= 0.08f;
            if (NormalizedLevel >= 1f) NormalizedLevel = 1f;
            bool wasOverflowed = IsOverflowed;
            IsOverflowed = NormalizedLevel >= 1f;
            if (IsOverflowed && !wasOverflowed && water != null) water.SetSpraying(false);

            UpdateVolume();
            ApplyBuoyancy(rising);
            ApplyWetFriction();
            PullIntoDrain();
            UpdateVortex();
        }

        void ApplyBuoyancy(bool rising)
        {
            if (NormalizedLevel <= 0.001f) return;
            float surface = SurfaceY;
            foreach (FoodScrap food in FoodScrap.Active)
            {
                if (food == null || food.IsDrained || food.Body == null || food.Body.isKinematic) continue;
                if (world != null && food.GetComponentInParent<SinkWorld>() != world) continue;
                Collider collider = food.GetComponent<Collider>();
                if (collider == null || !collider.enabled) continue;
                Bounds bounds = collider.bounds;
                float height = Mathf.Max(0.02f, bounds.size.y);
                float submerged = Mathf.Clamp01((surface - bounds.min.y) / height);
                if (submerged <= 0f) continue;
                submerged *= BuoyancyOverDrain(food.Body.worldCenterOfMass);
                if (submerged <= 0f) continue;
                Rigidbody body = food.Body;
                body.AddForce(Vector3.up * submerged * body.mass * 16f, ForceMode.Force);
                if (submerged > 0.3f)
                {
                    body.AddForce(-body.linearVelocity * submerged * body.mass * 1.6f, ForceMode.Force);
                    body.AddForce(Vector3.down * body.linearVelocity.y * submerged * body.mass * 2.2f, ForceMode.Force);
                }
                // The rise itself is the shuffle. Once the level stops climbing, scraps settle where they drifted.
                if (rising && submerged > 0.35f)
                    body.AddForce(ShuffleCurrent(food, body.worldCenterOfMass) * submerged * body.mass, ForceMode.Force);
            }
        }

        static readonly Vector2[] CornerCenters =
        {
            new Vector2(-1.18f, -0.73f),
            new Vector2(1.18f, -0.73f),
            new Vector2(1.18f, 0.73f),
            new Vector2(-1.18f, 0.73f)
        };

        Vector3 ShuffleCurrent(FoodScrap food, Vector3 worldPoint)
        {
            Transform sink = world != null && world.sink != null ? world.sink.transform : null;
            if (sink == null) return Vector3.zero;
            Vector3 local = sink.InverseTransformPoint(worldPoint);
            Vector3 fromCenter = new Vector3(local.x, 0f, local.z);
            Vector3 swirl = new Vector3(fromCenter.z, 0f, -fromCenter.x);
            if (swirl.sqrMagnitude < 0.04f) swirl = Vector3.right;
            else swirl.Normalize();

            float phase = (Mathf.Abs(food.GetInstanceID()) % 997) * 0.013f;
            float turn = Time.time * 0.55f + phase;
            Vector3 personal = new Vector3(Mathf.Cos(turn), 0f, Mathf.Sin(turn));
            Vector3 push = swirl * 0.65f + personal * 0.7f;

            float best = 0.52f;
            Vector2 corner = default;
            bool inCorner = false;
            for (int i = 0; i < CornerCenters.Length; i++)
            {
                float distance = Vector2.Distance(new Vector2(local.x, local.z), CornerCenters[i]);
                if (distance >= best) continue;
                best = distance;
                corner = CornerCenters[i];
                inCorner = true;
            }
            if (inCorner)
            {
                Vector3 exit = new Vector3(-corner.x, 0f, -corner.y);
                if (exit.sqrMagnitude > 0.001f) push += exit.normalized * 1.35f;
            }
            if (Mathf.Abs(local.x) > 1.05f) push.x -= Mathf.Sign(local.x) * 0.55f;
            if (Mathf.Abs(local.z) > 0.7f) push.z -= Mathf.Sign(local.z) * 0.55f;
            if (push.sqrMagnitude < 0.0001f) return Vector3.zero;
            return sink.TransformDirection(push.normalized * 1.05f);
        }

        void ApplyWetFriction()
        {
            if (wetSlide == null)
            {
                wetSlide = new PhysicsMaterial("Standing water")
                {
                    dynamicFriction = 0.02f,
                    staticFriction = 0.015f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine = PhysicsMaterialCombine.Minimum,
                    bounciness = 0f
                };
                wetSlide.hideFlags = HideFlags.DontSave;
            }
            float surface = SurfaceY;
            bool slippery = NormalizedLevel >= 0.05f;
            foreach (FoodScrap food in FoodScrap.Active)
            {
                if (food == null || food.IsDrained) continue;
                if (world != null && food.GetComponentInParent<SinkWorld>() != world) continue;
                Collider collider = food.GetComponent<Collider>();
                if (collider == null) continue;
                bool wet = false;
                if (slippery)
                {
                    Bounds bounds = collider.bounds;
                    float height = Mathf.Max(0.02f, bounds.size.y);
                    wet = (surface - bounds.min.y) / height > 0.12f;
                }
                if (wet)
                {
                    if (!dryMaterials.ContainsKey(collider))
                        dryMaterials.Add(collider, collider.sharedMaterial);
                    collider.sharedMaterial = wetSlide;
                }
                else if (dryMaterials.TryGetValue(collider, out PhysicsMaterial dry))
                {
                    collider.sharedMaterial = dry;
                    dryMaterials.Remove(collider);
                }
            }
        }

        float BuoyancyOverDrain(Vector3 worldPoint)
        {
            if (drain == null || !drain.IsOpen || NormalizedLevel < 0.08f) return 1f;
            Vector3 mouth = drain.transform.position;
            Vector3 flat = worldPoint - mouth;
            flat.y = 0f;
            float hole = Mathf.Max(0.05f, drain.radius);
            float dist = flat.magnitude;
            if (dist >= hole) return 1f;
            return Mathf.Lerp(0.55f, 1f, dist / hole);
        }

        void PullIntoDrain()
        {
            // A dry opening does not pull. The swirl is standing water moving toward the mouth.
            if (drain == null || !drain.IsOpen || drain.IsSealed || NormalizedLevel < 0.08f) return;
            Vector3 mouth = drain.transform.position;
            float surface = SurfaceY;
            float hole = Mathf.Max(0.05f, drain.radius);
            float reach = hole * Mathf.Lerp(1.2f, 1.65f, NormalizedLevel);
            float strength = Mathf.Lerp(0.25f, 1f, NormalizedLevel);
            foreach (FoodScrap food in FoodScrap.Active)
            {
                if (food == null || food.IsDrained || food.Body == null || food.Body.isKinematic) continue;
                if (world != null && food.GetComponentInParent<SinkWorld>() != world) continue;
                Collider collider = food.GetComponent<Collider>();
                if (collider == null || !collider.enabled) continue;
                Bounds bounds = collider.bounds;
                float height = Mathf.Max(0.02f, bounds.size.y);
                float submerged = Mathf.Clamp01((surface - bounds.min.y) / height);
                if (submerged < 0.2f) continue;
                Vector3 pos = food.Body.worldCenterOfMass;
                if (pos.y < mouth.y - 0.04f) continue;
                Vector3 flat = pos - mouth;
                flat.y = 0f;
                float dist = flat.magnitude;
                if (dist > reach || dist < 0.0001f) continue;
                float closeness = 1f - dist / reach;
                Vector3 inward = -flat / dist;
                Vector3 swirl = Vector3.Cross(Vector3.up, inward);
                float swirlFade = Mathf.Clamp01((dist - hole * 0.35f) / hole);
                float down = dist < hole ? 2.5f * closeness : 0f;
                Vector3 accel = inward * (3.2f * closeness) + swirl * (1.4f * closeness * swirlFade) + Vector3.down * down;
                food.Body.AddForce(accel * strength * submerged * food.Body.mass, ForceMode.Force);
            }
        }

        void EnsureVortex()
        {
            if (vortex != null || volumeMaterial == null) return;
            var root = new GameObject("Drain vortex");
            root.transform.SetParent(volume != null ? volume.parent : transform, false);
            vortex = root.transform;
            SpiralArm("Vortex arm a", 0f);
            SpiralArm("Vortex arm b", Mathf.PI);
            root.SetActive(false);
        }

        void SpiralArm(string name, float phase)
        {
            var go = new GameObject(name);
            go.transform.SetParent(vortex, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = false;
            line.positionCount = 32;
            line.numCapVertices = 3;
            line.startWidth = 0.045f;
            line.endWidth = 0.012f;
            line.sharedMaterial = volumeMaterial;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.alignment = LineAlignment.View;
            for (int i = 0; i < 32; i++)
            {
                float t = i / 31f;
                float angle = phase + t * Mathf.PI * 3.4f;
                float radius = Mathf.Lerp(0.96f, 0.08f, t);
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }

        void UpdateVortex()
        {
            const float minimumVisibleDepth = 0.012f;
            const float surfaceOffset = 0.012f;
            const float minimumVortexRadius = 0.05f;
            const float rotationDegreesPerSecond = 140f;

            EnsureVortex();
            if (vortex == null || drain == null)
            {
                return;
            }

            bool show = LocalDepth > minimumVisibleDepth && drain.IsOpen;
            if (vortex.gameObject.activeSelf != show)
            {
                vortex.gameObject.SetActive(show);
            }
            if (!show)
            {
                return;
            }

            Vector3 worldDrainCenter = drain.transform.position;
            float vortexRadius = Mathf.Max(minimumVortexRadius, drain.radius);
            vortex.position = new Vector3(worldDrainCenter.x, SurfaceY + surfaceOffset, worldDrainCenter.z);
            vortex.rotation = Quaternion.Euler(0f, Time.time * rotationDegreesPerSecond, 0f);
            vortex.localScale = new Vector3(vortexRadius, 1f, vortexRadius);
        }

        void EnsureVolume()
        {
            if (volume != null)
            {
                return;
            }

            var volumeObject = new GameObject("Basin water");
            volumeObject.layer = 2;
            volumeObject.transform.SetParent(BasinFrame, false);
            volumeObject.transform.localPosition = new Vector3(0f, FloorY, 0f);
            volumeObject.transform.localRotation = Quaternion.identity;
            Mesh mesh = BasinRounding.BuildWaterSurface(1.47f, 1.02f, 0.30f, 1f);
            mesh.hideFlags = HideFlags.DontSave;
            volumeObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = volumeObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            volumeMaterial = CreateWaterMaterial();
            renderer.sharedMaterial = volumeMaterial;
            volume = volumeObject.transform;
        }

        void UpdateVolume()
        {
            const float minimumVisibleDepth = 0.004f;

            if (volume == null)
            {
                return;
            }

            float localDepth = LocalDepth;
            bool visible = localDepth > minimumVisibleDepth;
            volume.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            volume.localScale = new Vector3(1f, localDepth, 1f);
            if (volumeMaterial != null && volumeMaterial.HasProperty(BaseColor))
            {
                Color calm = new Color(0.25f, 0.74f, 0.95f, 0.5f);
                Color danger = new Color(0.86f, 0.28f, 0.18f, 0.62f);
                volumeMaterial.SetColor(BaseColor, Color.Lerp(calm, danger, NormalizedLevel));
            }
        }

        void OnDestroy()
        {
            if (volume != null)
            {
                MeshFilter filter = volume.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    if (Application.isPlaying) Destroy(filter.sharedMesh);
                    else DestroyImmediate(filter.sharedMesh);
                }
            }
            if (volumeMaterial != null)
            {
                if (Application.isPlaying) Destroy(volumeMaterial);
                else DestroyImmediate(volumeMaterial);
            }
        }

        static Material CreateWaterMaterial()
        {
            Shader shader = Shader.Find("SinkLab/Water");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader) { name = "Basin water" };
            material.hideFlags = HideFlags.DontSave;
            Color tint = new Color(0.25f, 0.74f, 0.95f, 0.5f);
            material.SetColor("_BaseColor", tint);
            material.color = tint;
            if (material.HasProperty("_Surface")) material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }
    }
}
