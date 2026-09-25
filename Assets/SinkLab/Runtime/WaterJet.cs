using UnityEngine;

namespace SinkLab
{
    /// <summary>Ray-directed water transfers momentum into rigidbodies and washes exposed stains.</summary>
    public sealed class WaterJet : MonoBehaviour
    {
        public Camera aimCamera;
        public Transform nozzle;
        public LayerMask waterMask = Physics.DefaultRaycastLayers;
        public float maxDistance = 8f;
        public float focusedRadius = .26f;
        public float sprayRadius = .34f;
        public float waterForce = .20f;
        public float cleaningRate = .48f;

        public bool IsSpraying { get; private set; }
        public bool WideSpray { get; set; }
        public BasinWater basin;
        float pressure = .6f;
        readonly Collider[] muzzleOverlaps = new Collider[16];
        public float Pressure
        {
            get => pressure;
            set => pressure = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
        }
        public Vector3 LastHitPoint { get; private set; }
        public Vector3 LastHitNormal { get; private set; } = Vector3.up;
        public bool HasHit { get; private set; }
        public float EffectiveRadius => WideSpray ? sprayRadius : focusedRadius;

        public void SetSpraying(bool spraying)
        {
            IsSpraying = spraying;
            if (!spraying) HasHit = false;
        }

        void OnDisable() => SetSpraying(false);

        void FixedUpdate()
        {
            if (IsSpraying) SimulateSpray(Time.fixedDeltaTime);
        }

        public void SimulateSpray(float dt)
        {
            HasHit = false;
            if (!IsSpraying || aimCamera == null || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            SinkWorld ownerWorld = GetComponentInParent<SinkWorld>();
            if (ownerWorld != null && (ownerWorld.IsDrainSealed || ownerWorld.IsOverflowed)) return;
            Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            Vector3 desiredPoint = aimRay.GetPoint(maxDistance);
            if (Physics.Raycast(aimRay, out RaycastHit aimHit, maxDistance, waterMask, QueryTriggerInteraction.Ignore))
                desiredPoint = aimHit.point;

            Vector3 origin = nozzle != null ? nozzle.position : aimRay.origin;
            Vector3 travel = desiredPoint - origin;
            float length = Mathf.Min(travel.magnitude + .035f, maxDistance);
            if (travel.sqrMagnitude < .0001f) return;
            Vector3 direction = travel.normalized;
            LastHitPoint = origin + direction * length;
            // Unity raycasts skip colliders containing the ray origin. A clipped nozzle must not
            // use that behaviour to spray through a solid wall.
            int overlapCount = Physics.OverlapSphereNonAlloc(origin, .008f, muzzleOverlaps, waterMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider blocker = muzzleOverlaps[i];
                if ((blocker.ClosestPoint(origin) - origin).sqrMagnitude > .000001f) continue;
                LastHitPoint = origin;
                LastHitNormal = -direction;
                HasHit = true;
                return;
            }
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, length, waterMask, QueryTriggerInteraction.Ignore)) return;
            HasHit = true;
            LastHitPoint = hit.point;
            LastHitNormal = hit.normal;

            float pressureFactor = Mathf.Lerp(.26f, 1f, Pressure);
            float wet = basin != null ? basin.JetEfficiency : 1f;
            pressureFactor *= wet;
            SinkWorld owner = ownerWorld;
            FoodScrap directFood = hit.collider.GetComponentInParent<FoodScrap>();
            foreach (FoodScrap food in FoodScrap.Active)
            {
                if (food == null || food.IsDrained || food.Body == null || food.Body.isKinematic) continue;
                if (food.GetComponentInParent<SinkWorld>() != owner) continue;
                bool direct = food == directFood;
                Vector3 exit = CornerExit(owner, food.Body.worldCenterOfMass);
                bool inCorner = exit.sqrMagnitude > .001f;
                // Splash is ground flow, not an area attack through walls or through another scrap.
                // Water that hits a corner has to flow back into the basin, so it can still move a scrap trapped there.
                if (!direct && directFood != null) continue;
                if (!direct && hit.normal.y < .65f && !inCorner) continue;
                Vector3 contact = food.ClosestPoint(hit.point);
                float distance = Vector3.Distance(contact, hit.point);
                float reach = inCorner && hit.normal.y < .65f ? .42f : EffectiveRadius;
                if (!direct && distance > reach) continue;
                if (!direct && !inCorner && !CanReachFootprint(hit.point, contact, food)) continue;
                Vector3 push = GuidePush(direction, food.Body.worldCenterOfMass - hit.point, direct);
                float cornerBoost = 1f;
                if (inCorner)
                {
                    push = (exit * 1.35f + push * .2f).normalized;
                    cornerBoost = 2.6f;
                }
                float coverage = direct ? 1f : Mathf.Lerp(.28f, 1f, 1f - distance / Mathf.Max(.05f, reach));
                float modeForce = WideSpray ? .57f : 1f;
                Vector3 force = (push + Vector3.down * .035f) * (waterForce * pressureFactor * coverage * modeForce * cornerBoost);
                food.Body.AddForce(force * dt, ForceMode.Impulse);
            }

            // A wall hit cannot wash the floor on its other side.
            foreach (StainPatch stain in StainPatch.Active)
            {
                if (stain == null || stain.IsClean) continue;
                if (stain.GetComponentInParent<SinkWorld>() != owner) continue;
                Vector3 normal = stain.transform.up;
                Vector3 delta = stain.transform.position - hit.point;
                if (Mathf.Abs(Vector3.Dot(delta, normal)) > .085f || Vector3.Dot(hit.normal, normal) < .7f) continue;
                float distance = Vector3.ProjectOnPlane(delta, normal).magnitude;
                float remainingRadius = stain.radius * Mathf.Sqrt(stain.Remaining);
                if (distance > EffectiveRadius + remainingRadius) continue;
                Vector3 exposurePoint = stain.transform.position + normal * .035f;
                if (!VisibleFromNozzle(origin, exposurePoint, stain)) continue;
                float edge = Mathf.Clamp01((EffectiveRadius + remainingRadius - distance) / Mathf.Max(.06f, remainingRadius));
                float focus = WideSpray ? .67f : 1f;
                stain.Wash(cleaningRate * pressureFactor * focus * edge * dt);
            }
        }

        public Vector3 GuidePush(Vector3 streamDirection, Vector3 impactToBody, bool direct)
        {
            Vector3 downstream = Vector3.ProjectOnPlane(streamDirection, Vector3.up);
            if (downstream.sqrMagnitude < .0001f && aimCamera != null)
                downstream = Vector3.ProjectOnPlane(aimCamera.transform.forward, Vector3.up);
            if (downstream.sqrMagnitude < .0001f) return Vector3.zero;
            downstream.Normalize();

            Vector3 lateral = Vector3.ProjectOnPlane(impactToBody, downstream);
            if (direct || lateral.sqrMagnitude < .0001f) return downstream;
            return (downstream + lateral.normalized * .55f).normalized;
        }

        static readonly Vector2[] CornerCenters =
        {
            new Vector2(-1.18f, -0.73f),
            new Vector2(1.18f, -0.73f),
            new Vector2(1.18f, 0.73f),
            new Vector2(-1.18f, 0.73f)
        };

        static Vector3 CornerExit(SinkWorld owner, Vector3 worldPos)
        {
            if (owner == null || owner.sink == null) return Vector3.zero;
            Transform sink = owner.sink.transform;
            Vector3 local = sink.InverseTransformPoint(worldPos);
            float best = .52f;
            Vector2 found = default;
            bool hit = false;
            for (int i = 0; i < CornerCenters.Length; i++)
            {
                float distance = Vector2.Distance(new Vector2(local.x, local.z), CornerCenters[i]);
                if (distance >= best) continue;
                best = distance;
                found = CornerCenters[i];
                hit = true;
            }
            if (!hit) return Vector3.zero;
            Vector3 exit = new Vector3(-found.x, 0f, -found.y);
            if (exit.sqrMagnitude < .001f) return Vector3.zero;
            return sink.TransformDirection(exit.normalized);
        }

        bool CanReachFootprint(Vector3 impact, Vector3 contact, FoodScrap target)
        {
            Vector3 start = impact + Vector3.up * .045f;
            Vector3 end = contact + Vector3.up * .045f;
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < .0001f) return true;
            if (!Physics.Raycast(start, delta.normalized, out RaycastHit blocker, delta.magnitude,
                waterMask, QueryTriggerInteraction.Ignore)) return true;
            return blocker.collider.GetComponentInParent<FoodScrap>() == target;
        }

        bool VisibleFromNozzle(Vector3 origin, Vector3 destination, StainPatch target)
        {
            Vector3 delta = destination - origin;
            if (!Physics.Raycast(origin, delta.normalized, out RaycastHit blocker, Mathf.Max(0f, delta.magnitude - .012f),
                waterMask, QueryTriggerInteraction.Ignore)) return true;
            return blocker.collider.GetComponentInParent<StainPatch>() == target;
        }
    }
}
