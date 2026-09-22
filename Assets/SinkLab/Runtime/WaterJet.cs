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
            if (basin != null && basin.IsOverflowed) return;
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
            SinkWorld owner = GetComponentInParent<SinkWorld>();
            FoodScrap directFood = hit.collider.GetComponentInParent<FoodScrap>();
            foreach (FoodScrap food in FoodScrap.Active)
            {
                if (food == null || food.IsDrained || food.Body == null || food.Body.isKinematic) continue;
                if (food.GetComponentInParent<SinkWorld>() != owner) continue;
                bool direct = food == directFood;
                // Splash is ground flow, not an area attack through walls or through another scrap.
                if (!direct && (directFood != null || hit.normal.y < .65f)) continue;
                Vector3 contact = food.ClosestPoint(hit.point);
                float distance = Vector3.Distance(contact, hit.point);
                if (!direct && distance > EffectiveRadius) continue;
                if (!direct && !CanReachFootprint(hit.point, contact, food)) continue;
                // The jet shoves food downstream, away from the nozzle. A hit just
                // past a scrap must not drag it back toward the hose.
                Vector3 push = GuidePush(direction, food.Body.worldCenterOfMass - hit.point, food.Body.worldCenterOfMass, direct);
                float coverage = direct ? 1f : Mathf.Lerp(.28f, 1f, 1f - distance / EffectiveRadius);
                float modeForce = WideSpray ? .57f : 1f;
                Vector3 force = (push + Vector3.down * .035f) * (waterForce * pressureFactor * coverage * modeForce);
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

        public Vector3 GuidePush(Vector3 streamDirection, Vector3 impactToBody, Vector3 bodyPosition, bool direct)
        {
            Vector3 downstream = Vector3.ProjectOnPlane(streamDirection, Vector3.up);
            if (downstream.sqrMagnitude < .0001f && aimCamera != null)
                downstream = Vector3.ProjectOnPlane(aimCamera.transform.forward, Vector3.up);
            if (downstream.sqrMagnitude < .0001f) return Vector3.zero;
            downstream.Normalize();

            Vector3 push = downstream;
            Vector3 lateral = Vector3.ProjectOnPlane(impactToBody, downstream);
            if (!direct && lateral.sqrMagnitude > .0001f)
                push = (downstream + lateral.normalized * .55f).normalized;

            Drain mouth = basin != null ? basin.drain : null;
            if (mouth == null)
            {
                SinkWorld owner = GetComponentInParent<SinkWorld>();
                if (owner != null) mouth = owner.drain;
            }
            if (mouth == null) return push;
            Vector3 toDrain = mouth.transform.position - bodyPosition;
            toDrain.y = 0f;
            if (toDrain.sqrMagnitude < .0001f) return push;
            float steer = Mathf.Lerp(.2f, .62f, 1f - Mathf.Clamp01(toDrain.magnitude / 1.5f));
            return (push * (1f - steer) + toDrain.normalized * steer).normalized;
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
