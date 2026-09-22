using UnityEngine;

namespace SinkLab
{
    /// <summary>Collection happens only after food has physically fallen into the opening.</summary>
    public sealed class Drain : MonoBehaviour
    {
        public Transform drainCenter;
        public float radius = .22f;
        public bool squareOpening;
        [Min(0f)]
        [Tooltip("Distance below the drain opening, in its local frame, that food must cross before collection. Moves and rotates with this sink.")]
        public float captureDepth = .07f;
        public float captureHeight => CaptureFrame.TransformPoint(new Vector3(0f, -captureDepth, 0f)).y;
        public int DrainedCount { get; private set; }

        Transform CaptureFrame => drainCenter != null ? drainCenter : transform;

        void FixedUpdate()
        {
            foreach (FoodScrap food in FoodScrap.Active) TryConsume(food);
        }

        public bool TryConsume(FoodScrap food)
        {
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
            return true;
        }

        public void ResetCount() => DrainedCount = 0;
    }
}
