using UnityEngine;
using UnityEngine.Rendering;

namespace SinkLab
{
    /// <summary>
    /// Spray adds water. Higher pressure fills faster, and a smaller drain empties slower.
    /// Crossing the rim is an immediate loss until the sink is reset.
    /// </summary>
    public sealed class BasinWater : MonoBehaviour
    {
        public const float FloorY = 0.80f;
        public const float OverflowY = 1.195f;

        public WaterJet water;
        public Drain drain;
        public SinkWorld world;

        public float NormalizedLevel { get; private set; }
        public bool IsOverflowed { get; private set; }
        public float SurfaceY => FloorY + Mathf.Clamp01(NormalizedLevel) * (OverflowY - FloorY);
        public float JetEfficiency => Mathf.Lerp(1f, 0.38f, Mathf.Clamp01(NormalizedLevel));

        Transform volume;
        Material volumeMaterial;
        Transform vortex;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

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
            if (IsOverflowed) return;
            float dt = Time.fixedDeltaTime;
            if (dt <= 0f) return;

            float inflow = 0f;
            if (water != null && water.IsSpraying)
            {
                float pressure = water.Pressure * water.Pressure;
                inflow = Mathf.Lerp(0.02f, 0.22f, pressure) / 3f;
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
            NormalizedLevel = Mathf.Clamp01(NormalizedLevel + (inflow - outflow) * dt);
            if (NormalizedLevel >= 1f)
            {
                NormalizedLevel = 1f;
                IsOverflowed = true;
                if (water != null) water.SetSpraying(false);
            }

            UpdateVolume();
            ApplyBuoyancy();
            PullIntoDrain();
            UpdateVortex();
        }

        void ApplyBuoyancy()
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
            }
        }

        float BuoyancyOverDrain(Vector3 worldPoint)
        {
            if (drain == null) return 1f;
            Vector3 mouth = drain.transform.position;
            Vector3 flat = worldPoint - mouth;
            flat.y = 0f;
            float reach = Mathf.Max(0.05f, drain.radius) * 1.15f;
            float dist = flat.magnitude;
            if (dist >= reach) return 1f;
            return Mathf.Lerp(0.04f, 1f, dist / reach);
        }

        void PullIntoDrain()
        {
            if (drain == null || !drain.IsOpen) return;
            Vector3 mouth = drain.transform.position;
            float hole = Mathf.Max(0.05f, drain.radius);
            float reach = hole * 3.2f;
            float wet = Mathf.Lerp(0.7f, 1.45f, NormalizedLevel);
            foreach (FoodScrap food in FoodScrap.Active)
            {
                if (food == null || food.IsDrained || food.Body == null || food.Body.isKinematic) continue;
                if (world != null && food.GetComponentInParent<SinkWorld>() != world) continue;
                Vector3 pos = food.Body.worldCenterOfMass;
                float height = pos.y - mouth.y;
                if (height < -0.04f || height > 0.4f) continue;
                Vector3 flat = pos - mouth;
                flat.y = 0f;
                float dist = flat.magnitude;
                if (dist > reach || dist < 0.0001f)
                {
                    if (dist <= hole)
                        food.Body.AddForce(Vector3.down * 24f * wet * food.Body.mass, ForceMode.Force);
                    continue;
                }
                float closeness = 1f - dist / reach;
                Vector3 inward = -flat / dist;
                Vector3 swirl = Vector3.Cross(Vector3.up, inward);
                float swirlFade = Mathf.Clamp01((dist - hole * 0.45f) / hole);
                float down = dist < hole * 1.25f ? Mathf.Lerp(34f, 12f, Mathf.Clamp01(dist / hole)) : 4f * closeness;
                Vector3 accel = inward * Mathf.Lerp(14f, 28f, closeness) + swirl * (4f * closeness * swirlFade) + Vector3.down * down;
                food.Body.AddForce(accel * wet * food.Body.mass, ForceMode.Force);
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
            EnsureVortex();
            if (vortex == null || drain == null) return;
            float depth = Mathf.Max(0f, SurfaceY - FloorY);
            bool show = depth > 0.012f && drain.IsOpen;
            if (vortex.gameObject.activeSelf != show) vortex.gameObject.SetActive(show);
            if (!show) return;
            Vector3 mouth = drain.transform.position;
            float hole = Mathf.Max(0.05f, drain.radius);
            vortex.position = new Vector3(mouth.x, SurfaceY + 0.012f, mouth.z);
            vortex.rotation = Quaternion.Euler(0f, Time.time * 140f, 0f);
            vortex.localScale = new Vector3(hole, 1f, hole);
        }

        void EnsureVolume()
        {
            if (volume != null) return;
            Transform parent = world != null && world.sink != null ? world.sink.transform : transform;
            var go = new GameObject("Basin water");
            go.layer = 2;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, FloorY, 0f);
            go.transform.localRotation = Quaternion.identity;
            Mesh mesh = BasinRounding.BuildWaterSurface(1.47f, 1.02f, 0.30f, 1f);
            mesh.hideFlags = HideFlags.DontSave;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            volumeMaterial = CreateWaterMaterial();
            renderer.sharedMaterial = volumeMaterial;
            volume = go.transform;
        }

        void UpdateVolume()
        {
            if (volume == null) return;
            float depth = Mathf.Max(0f, SurfaceY - FloorY);
            bool visible = depth > 0.004f;
            volume.gameObject.SetActive(visible);
            if (!visible) return;
            volume.localScale = new Vector3(1f, depth, 1f);
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
