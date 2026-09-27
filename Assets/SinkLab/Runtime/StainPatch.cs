using System.Collections.Generic;
using UnityEngine;

namespace SinkLab
{
    /// <summary>Progressively scrubbed by a visible water footprint, with no click interaction.</summary>
    public sealed class StainPatch : MonoBehaviour
    {
        public static readonly HashSet<StainPatch> Active = new HashSet<StainPatch>();
        public float radius = .25f;
        public float Remaining { get; private set; } = 1f;
        public bool IsClean => Remaining <= 0f;

        Vector3 originalScale;
        Renderer stainRenderer;
        Color originalColor;
        MaterialPropertyBlock properties;
        bool initialized;
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int ColorProperty = Shader.PropertyToID("_Color");

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);
        void Start() => Initialize();

        void Initialize()
        {
            if (initialized)
            {
                return;
            }

            originalScale = transform.localScale;
            stainRenderer = GetComponentInChildren<Renderer>();
            properties = new MaterialPropertyBlock();
            originalColor = new Color(.33f, .18f, .055f);
            if (stainRenderer != null && stainRenderer.sharedMaterial != null)
            {
                Material material = stainRenderer.sharedMaterial;
                if (material.HasProperty(BaseColor))
                {
                    originalColor = material.GetColor(BaseColor);
                }
                else if (material.HasProperty(ColorProperty))
                {
                    originalColor = material.GetColor(ColorProperty);
                }
            }

            initialized = true;
        }

        public void Wash(float amount)
        {
            if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount) || IsClean)
            {
                return;
            }

            Initialize();
            Remaining = Mathf.Max(0f, Remaining - amount);
            if (Remaining < .005f)
            {
                Remaining = 0f;
            }

            UpdateAppearance();
        }

        public void ResetStain()
        {
            Initialize();
            Remaining = 1f;
            UpdateAppearance();
        }

        void UpdateAppearance()
        {
            float scale = Mathf.Sqrt(Remaining);
            transform.localScale = new Vector3(originalScale.x * scale, originalScale.y, originalScale.z * scale);
            if (stainRenderer == null)
            {
                return;
            }

            stainRenderer.enabled = !IsClean;
            Color color = Color.Lerp(new Color(.56f, .58f, .56f, originalColor.a), originalColor, Remaining);
            stainRenderer.GetPropertyBlock(properties);
            properties.SetColor(BaseColor, color);
            properties.SetColor(ColorProperty, color);
            stainRenderer.SetPropertyBlock(properties);
        }
    }
}
