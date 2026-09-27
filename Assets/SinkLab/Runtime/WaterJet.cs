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
            if (!spraying)
            {
                HasHit = false;
            }
        }

        void OnDisable() => SetSpraying(false);

        void FixedUpdate()
        {
            if (IsSpraying)
            {
                SimulateSpray(Time.fixedDeltaTime);
            }
        }

        public void SimulateSpray(float dt)
        {
            HasHit = false;
            if (!IsSpraying || aimCamera == null || dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt))
            {
                return;
            }

            SinkWorld owner = GetComponentInParent<SinkWorld>();
            if (owner != null && (owner.IsDrainSealed || owner.IsOverflowed))
            {
                return;
            }

            Ray aimRay = aimCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            Vector3 desiredPoint = aimRay.GetPoint(maxDistance);
            if (Physics.Raycast(aimRay, out RaycastHit aimHit, maxDistance, waterMask, QueryTriggerInteraction.Ignore))
            {
                desiredPoint = aimHit.point;
            }

            Vector3 nozzleOrigin = nozzle != null ? nozzle.position : aimRay.origin;
            Vector3 travel = desiredPoint - nozzleOrigin;
            // Extend slightly beyond the camera's impact so the nozzle ray reaches that surface.
            const float impactAllowanceMeters = .035f;
            float travelDistance = Mathf.Min(travel.magnitude + impactAllowanceMeters, maxDistance);
            if (travel.sqrMagnitude < .0001f)
            {
                return;
            }

            Vector3 streamDirection = travel.normalized;
            LastHitPoint = nozzleOrigin + streamDirection * travelDistance;

            // Unity raycasts skip colliders containing the ray origin. A clipped nozzle must not
            // use that behaviour to spray through a solid wall.
            const float nozzleOverlapRadiusMeters = .008f;
            int overlapCount = Physics.OverlapSphereNonAlloc(
                nozzleOrigin, nozzleOverlapRadiusMeters, muzzleOverlaps, waterMask,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlapCount; i++)
            {
                Collider blocker = muzzleOverlaps[i];
                if ((blocker.ClosestPoint(nozzleOrigin) - nozzleOrigin).sqrMagnitude > .000001f)
                {
                    continue;
                }

                LastHitPoint = nozzleOrigin;
                LastHitNormal = -streamDirection;
                HasHit = true;
                return;
            }

            if (!Physics.Raycast(nozzleOrigin, streamDirection, out RaycastHit hit, travelDistance,
                waterMask, QueryTriggerInteraction.Ignore))
            {
                return;
            }

            HasHit = true;
            LastHitPoint = hit.point;
            LastHitNormal = hit.normal;

            float pressureFactor = Mathf.Lerp(.26f, 1f, Pressure);
            float jetEfficiency = basin != null ? basin.JetEfficiency : 1f;
            pressureFactor *= jetEfficiency;

            PushExposedFood(owner, hit, streamDirection, pressureFactor, dt);
            WashExposedStains(owner, hit, nozzleOrigin, pressureFactor, dt);
        }

        void PushExposedFood(SinkWorld owner, RaycastHit hit, Vector3 streamDirection, float pressureFactor, float dt)
        {
            const float minimumFloorNormalY = .65f;
            const float minimumSplashCoverage = .28f;
            const float minimumCoverageRadiusMeters = .05f;
            const float wideSprayForceScale = .57f;
            const float downwardForceBias = .035f;

            FoodScrap directFood = hit.collider.GetComponentInParent<FoodScrap>();
            foreach (FoodScrap food in FoodScrap.Active)
            {
                if (food == null || food.IsDrained || food.Body == null || food.Body.isKinematic)
                {
                    continue;
                }

                if (food.GetComponentInParent<SinkWorld>() != owner)
                {
                    continue;
                }

                bool direct = food == directFood;
                // Splash is ground flow, not an area attack through walls or through another scrap.
                if (!direct && (directFood != null || hit.normal.y < minimumFloorNormalY))
                {
                    continue;
                }

                Vector3 contact = food.ClosestPoint(hit.point);
                float distance = Vector3.Distance(contact, hit.point);
                if (!direct && (distance > EffectiveRadius || !CanReachFootprint(hit.point, contact, food)))
                {
                    continue;
                }

                Vector3 push = GuidePush(streamDirection, food.Body.worldCenterOfMass - hit.point, direct);
                float coverage = direct
                    ? 1f
                    : Mathf.Lerp(minimumSplashCoverage, 1f,
                        1f - distance / Mathf.Max(minimumCoverageRadiusMeters, EffectiveRadius));
                float modeForce = WideSpray ? wideSprayForceScale : 1f;
                Vector3 force = (push + Vector3.down * downwardForceBias)
                    * (waterForce * pressureFactor * coverage * modeForce);
                food.Body.AddForce(force * dt, ForceMode.Impulse);
            }
        }

        void WashExposedStains(SinkWorld owner, RaycastHit hit, Vector3 nozzleOrigin, float pressureFactor, float dt)
        {
            const float surfaceHeightToleranceMeters = .085f;
            const float minimumSurfaceAlignment = .7f;
            const float exposureOffsetMeters = .035f;
            const float minimumEdgeRadiusMeters = .06f;
            const float wideSprayCleaningScale = .67f;

            // A wall hit cannot wash the floor on its other side.
            foreach (StainPatch stain in StainPatch.Active)
            {
                if (stain == null || stain.IsClean)
                {
                    continue;
                }

                if (stain.GetComponentInParent<SinkWorld>() != owner)
                {
                    continue;
                }

                Vector3 normal = stain.transform.up;
                Vector3 delta = stain.transform.position - hit.point;
                if (Mathf.Abs(Vector3.Dot(delta, normal)) > surfaceHeightToleranceMeters
                    || Vector3.Dot(hit.normal, normal) < minimumSurfaceAlignment)
                {
                    continue;
                }

                float distance = Vector3.ProjectOnPlane(delta, normal).magnitude;
                float remainingRadius = stain.radius * Mathf.Sqrt(stain.Remaining);
                if (distance > EffectiveRadius + remainingRadius)
                {
                    continue;
                }

                Vector3 exposurePoint = stain.transform.position + normal * exposureOffsetMeters;
                if (!VisibleFromNozzle(nozzleOrigin, exposurePoint, stain))
                {
                    continue;
                }

                float edgeCoverage = Mathf.Clamp01(
                    (EffectiveRadius + remainingRadius - distance)
                    / Mathf.Max(minimumEdgeRadiusMeters, remainingRadius));
                float modeCleaningScale = WideSpray ? wideSprayCleaningScale : 1f;
                stain.Wash(cleaningRate * pressureFactor * modeCleaningScale * edgeCoverage * dt);
            }
        }

        public Vector3 GuidePush(Vector3 streamDirection, Vector3 impactToBody, bool direct)
        {
            const float minimumDirectionSquared = .0001f;
            const float lateralFlowScale = .55f;

            Vector3 downstream = Vector3.ProjectOnPlane(streamDirection, Vector3.up);
            if (downstream.sqrMagnitude < minimumDirectionSquared && aimCamera != null)
            {
                downstream = Vector3.ProjectOnPlane(aimCamera.transform.forward, Vector3.up);
            }

            if (downstream.sqrMagnitude < minimumDirectionSquared)
            {
                return Vector3.zero;
            }

            downstream.Normalize();

            // Surface flow stays horizontal; the scrap's height must not introduce lift.
            Vector3 floorDisplacement = Vector3.ProjectOnPlane(impactToBody, Vector3.up);
            Vector3 lateral = Vector3.ProjectOnPlane(floorDisplacement, downstream);
            if (direct || lateral.sqrMagnitude < minimumDirectionSquared)
            {
                return downstream;
            }

            return (downstream + lateral.normalized * lateralFlowScale).normalized;
        }

        bool CanReachFootprint(Vector3 impact, Vector3 contact, FoodScrap target)
        {
            Vector3 start = impact + Vector3.up * .045f;
            Vector3 end = contact + Vector3.up * .045f;
            Vector3 delta = end - start;
            if (delta.sqrMagnitude < .0001f)
            {
                return true;
            }

            if (!Physics.Raycast(start, delta.normalized, out RaycastHit blocker, delta.magnitude,
                waterMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return blocker.collider.GetComponentInParent<FoodScrap>() == target;
        }

        bool VisibleFromNozzle(Vector3 origin, Vector3 destination, StainPatch target)
        {
            Vector3 delta = destination - origin;
            if (!Physics.Raycast(
                origin, delta.normalized, out RaycastHit blocker, Mathf.Max(0f, delta.magnitude - .012f),
                waterMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return blocker.collider.GetComponentInParent<StainPatch>() == target;
        }
    }
}
