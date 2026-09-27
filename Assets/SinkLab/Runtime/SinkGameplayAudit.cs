#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SinkLab.Editor
{
    /// <summary>
    /// Drives the saved level through movement, aim, spray and the normal drain tools.
    /// Never relocates food, applies forces, or directly washes or collects an objective.
    /// </summary>
    public sealed class SinkGameplayAudit : MonoBehaviour
    {
        [Serializable]
        public class AuditResult
        {
            public bool finished;
            public bool passed;
            public string status;
            public int foodRemaining;
            public int stainsRemaining;
            public float seconds;
            public List<string> observations = new List<string>();
        }

        public AuditResult result = new AuditResult();
        [Tooltip("Optional folder beneath Verification. Set before the audit starts.")]
        public string outputSubdirectory = string.Empty;

        const float AuditTimeoutSeconds = 360f;
        const float FoodTimeoutSeconds = 80f;
        const float StainAttemptSeconds = 9f;
        const float RouteStepTimeoutSeconds = 10f;
        const float DrainPauseTimeoutSeconds = 22f;
        const float FoodEdgeClearance = 0.008f;
        const float MinimumSteeringClearance = 0.035f;
        const float MouthApproachRadius = 0.45f;
        const float ReopenStallSeconds = 3f;
        const float RerouteStallSeconds = 5f;
        const float ProgressDistance = 0.045f;
        const float ShuffleTargetLevel = 0.5f;
        const float ShuffleFillTimeoutSeconds = 5f;
        const float ShuffleSettleSeconds = 1.5f;

        readonly Vector3[] _localStations =
        {
            new Vector3(0f, 0f, -1.72f),
            new Vector3(2.4f, 0f, -1.72f),
            new Vector3(2.35f, 0f, 0f),
            new Vector3(2.4f, 0f, 1.72f),
            new Vector3(0f, 0f, 1.72f),
            new Vector3(-2.4f, 0f, 1.72f),
            new Vector3(-2.35f, 0f, 0f),
            new Vector3(-2.4f, 0f, -1.72f)
        };

        SinkWorld _world;
        int _station;
        float _started;
        float _auditDeadline;
        string _outputDirectory;
        bool _environmentCaptured;
        bool _previousRunInBackground;
        bool _previousPlayerEnabled;
        float _previousTimeScale;
        bool _usedWaterShuffle;

        Transform DrainFrame => _world.drain.drainCenter != null
            ? _world.drain.drainCenter
            : _world.drain.transform;

        IEnumerator Start()
        {
            if (!InitializeAudit())
            {
                yield break;
            }

            Capture("01-start");
            yield return new WaitForSeconds(0.5f);
            yield return WashStains();
            if (result.finished)
            {
                yield break;
            }

            Capture("02-stains-washed");
            _world.water.WideSpray = false;
            yield return GuideFood();
            if (result.finished)
            {
                yield break;
            }

            yield return RouteTo(0);
            if (result.finished)
            {
                yield break;
            }

            Aim(_world.sink.transform.TransformPoint(new Vector3(0f, BasinWater.FloorY, 0f)));
            _world.water.SetSpraying(false);
            yield return new WaitForSeconds(0.25f);
            Capture("04-complete");
            Finish(_world.IsComplete, _world.IsComplete
                ? "All stains washed and food drained through movement, aim, spray and available drain tools"
                : "Counts did not complete");
        }

        bool InitializeAudit()
        {
            _started = Time.time;
            _auditDeadline = _started + AuditTimeoutSeconds;
            string verificationRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "../Verification"));
            _outputDirectory = verificationRoot;
            if (!string.IsNullOrWhiteSpace(outputSubdirectory))
            {
                string requestedDirectory = Path.GetFullPath(Path.Combine(verificationRoot, outputSubdirectory));
                if (Path.IsPathRooted(outputSubdirectory) ||
                    !requestedDirectory.StartsWith(verificationRoot + Path.DirectorySeparatorChar,
                        StringComparison.Ordinal))
                {
                    Finish(false, "Audit output must be a subfolder of Verification");
                    return false;
                }

                _outputDirectory = requestedDirectory;
            }
            Directory.CreateDirectory(_outputDirectory);

            _world = FindFirstObjectByType<SinkWorld>();
            if (_world == null || _world.player == null || _world.water == null ||
                _world.drain == null || _world.sink == null || _world.player.viewCamera == null ||
                _world.water.aimCamera == null || _world.water.nozzle == null ||
                _world.foods == null || _world.foods.Length == 0 ||
                _world.stains == null || _world.stains.Length == 0)
            {
                Finish(false, "No complete playable world found");
                return false;
            }

            _previousRunInBackground = Application.runInBackground;
            _previousTimeScale = Time.timeScale;
            _previousPlayerEnabled = _world.player.enabled;
            _environmentCaptured = true;
            _world.ResetRun();
            _world.player.enabled = false;
            _world.player.SetControl(true);
            _world.water.Pressure = 0.8f;
            Application.runInBackground = true;
            return true;
        }

        IEnumerator WashStains()
        {
            result.status = "Washing stains through the actual nozzle";
            Save();
            foreach (StainPatch stain in _world.stains)
            {
                if (stain == null)
                {
                    Finish(false, "A required stain is missing");
                    yield break;
                }

                AlignStations(stain.transform.position);
                int firstStation = StationFor(stain.transform.position);
                for (int attempt = 0; attempt < 4 && !stain.IsClean; attempt++)
                {
                    yield return RouteTo((firstStation + attempt * 2) % _localStations.Length);
                    if (result.finished)
                    {
                        yield break;
                    }

                    _world.water.WideSpray = true;
                    float attemptStarted = Time.time;
                    float deadline = attemptStarted + StainAttemptSeconds;
                    float startingRemaining = stain.Remaining;
                    while (!stain.IsClean && Time.time < deadline)
                    {
                        if (!CanContinueAudit())
                        {
                            yield break;
                        }

                        yield return DrainPause();
                        if (result.finished)
                        {
                            yield break;
                        }
                        if (Time.time >= deadline)
                        {
                            break;
                        }

                        Aim(stain.transform.position);
                        _world.water.SetSpraying(true);
                        yield return new WaitForFixedUpdate();
                        if (Time.time - attemptStarted > 2f &&
                            Mathf.Abs(stain.Remaining - startingRemaining) < 0.001f)
                        {
                            break;
                        }
                    }

                    _world.water.SetSpraying(false);
                }

                result.observations.Add(stain.name + ": " +
                    (stain.IsClean ? "washed" : "FAILED, remaining " + stain.Remaining));
                Save();
                if (!stain.IsClean)
                {
                    Capture("failed-stain");
                    Finish(false, "Could not wash stain within bounded attempts: " + stain.name);
                    yield break;
                }
            }
        }

        IEnumerator GuideFood()
        {
            foreach (FoodScrap food in _world.foods)
            {
                if (food == null || food.Body == null || food.GetComponent<Collider>() == null)
                {
                    Finish(false, "A required physical food scrap is missing");
                    yield break;
                }
                if (food.IsDrained)
                {
                    continue;
                }

                result.status = "Guiding " + food.name + " with water";
                Save();
                AlignStations(food.transform.position);
                yield return RouteTo(StationFor(food.transform.position));
                if (result.finished)
                {
                    yield break;
                }

                float deadline = Time.time + FoodTimeoutSeconds;
                float lastProgressTime = Time.time;
                float bestDistance = HorizontalDistanceToDrain(food.Body.worldCenterOfMass);
                int sprayFrames = 0;
                int routeChangesWithoutProgress = 0;
                while (!food.IsDrained && Time.time < deadline)
                {
                    if (!CanContinueAudit())
                    {
                        yield break;
                    }

                    yield return DrainPause();
                    if (result.finished)
                    {
                        yield break;
                    }
                    if (Time.time >= deadline)
                    {
                        break;
                    }

                    FoodScrap subject = food.GuidanceTarget;
                    Vector3 worldCenter = subject.Body.worldCenterOfMass;
                    Vector3 localCenter = DrainFrame.InverseTransformPoint(worldCenter);
                    float distance = new Vector2(localCenter.x, localCenter.z).magnitude;
                    float footprintRadius = FoodFootprintRadius(subject);
                    if (TryRestoreTightOpening(food, distance, footprintRadius, Time.time - lastProgressTime))
                    {
                        lastProgressTime = Time.time;
                    }

                    // The center and the whole horizontal footprint need room in the current circle.
                    // A fixed square stopping zone can strand food on a shrinking collar.
                    float clearance = _world.drain.radius - footprintRadius - FoodEdgeClearance;
                    bool overOpening = clearance > 0f && distance < clearance;
                    if (localCenter.y < -_world.drain.captureDepth)
                    {
                        _world.water.SetSpraying(false);
                        yield return new WaitForFixedUpdate();
                        continue;
                    }

                    Vector3 toDrain = DrainFrame.position - worldCenter;
                    toDrain.y = 0f;
                    Vector3 velocity = subject.Body.linearVelocity;
                    velocity.y = 0f;
                    Vector3 desired = (toDrain.normalized - velocity * 0.65f).normalized;
                    float aimScore = ChooseAim(subject, desired);
                    if (distance < bestDistance - ProgressDistance)
                    {
                        bestDistance = distance;
                        lastProgressTime = Time.time;
                        routeChangesWithoutProgress = 0;
                    }

                    bool stalled = Time.time - lastProgressTime > RerouteStallSeconds;
                    if (!overOpening && !_usedWaterShuffle && IsNearBasinWall(worldCenter) &&
                        (stalled || routeChangesWithoutProgress >= 2))
                    {
                        yield return ShuffleWithStandingWater(subject, deadline);
                        if (result.finished)
                        {
                            yield break;
                        }

                        lastProgressTime = Time.time;
                        bestDistance = HorizontalDistanceToDrain(subject.Body.worldCenterOfMass);
                        routeChangesWithoutProgress = 0;
                        continue;
                    }

                    if (!overOpening && (aimScore < 0.25f || stalled))
                    {
                        _world.water.SetSpraying(false);
                        AlignStations(worldCenter);
                        yield return RouteTo((_station + 2) % _localStations.Length);
                        if (result.finished)
                        {
                            yield break;
                        }

                        lastProgressTime = Time.time;
                        routeChangesWithoutProgress++;
                        continue;
                    }

                    _world.water.Pressure = _world.water.WideSpray ? 1f : 0.85f;
                    if (!_world.water.WideSpray && distance < MouthApproachRadius)
                    {
                        _world.water.Pressure = 0.6f;
                    }
                    _world.water.SetSpraying(!overOpening);
                    if (sprayFrames++ == 60)
                    {
                        Capture("03-guiding-food");
                    }
                    yield return new WaitForFixedUpdate();
                }

                _world.water.SetSpraying(false);
                result.observations.Add(food.name + ": " + (food.IsDrained
                    ? "physically drained"
                    : "FAILED at " + food.Body.position.ToString("F3")));
                Save();
                if (!food.IsDrained)
                {
                    Capture("failed-food");
                    Finish(false, "Could not guide " + food.name + " within the food time budget");
                    yield break;
                }
            }
        }

        bool TryRestoreTightOpening(FoodScrap food, float distance, float footprintRadius, float stalledSeconds)
        {
            Drain drain = _world.drain;
            if (!drain.HasFullOpenCharge || drain.radius >= drain.StartRadius - 0.01f)
            {
                return false;
            }

            bool nearMouth = distance < Mathf.Max(MouthApproachRadius, drain.radius + footprintRadius);
            bool tooTight = drain.radius - footprintRadius - FoodEdgeClearance < MinimumSteeringClearance;
            bool stalledAtMouth = stalledSeconds >= ReopenStallSeconds && distance < drain.radius + footprintRadius;
            return nearMouth && (tooTight || stalledAtMouth) &&
                UseFullOpenTool("opening clearance for " + food.name);
        }

        bool IsNearBasinWall(Vector3 worldPoint)
        {
            Vector3 localPoint = _world.sink.transform.InverseTransformPoint(worldPoint);
            return Mathf.Abs(localPoint.x) > 1.05f || Mathf.Abs(localPoint.z) > 0.7f;
        }

        IEnumerator ShuffleWithStandingWater(FoodScrap food, float foodDeadline)
        {
            BasinWater basin = _world.basin;
            if (basin == null)
            {
                yield break;
            }

            _usedWaterShuffle = true;
            float previousPressure = _world.water.Pressure;
            bool previousWideSpray = _world.water.WideSpray;
            result.observations.Add("Used E to float and reshuffle the stalled " + food.name);
            Save();
            try
            {
                _world.drain.SetOpen(false);
                _world.water.Pressure = 1f;
                _world.water.WideSpray = true;
                float fillDeadline = Mathf.Min(foodDeadline, Time.time + ShuffleFillTimeoutSeconds);
                while (basin.NormalizedLevel < ShuffleTargetLevel && Time.time < fillDeadline)
                {
                    if (!CanContinueAudit())
                    {
                        yield break;
                    }

                    Aim(food.Body.worldCenterOfMass);
                    _world.water.SetSpraying(true);
                    yield return new WaitForFixedUpdate();
                }

                _world.water.SetSpraying(false);
                _world.drain.SetOpen(true);
                float settleDeadline = Mathf.Min(foodDeadline, Time.time + ShuffleSettleSeconds);
                while (Time.time < settleDeadline)
                {
                    if (!CanContinueAudit())
                    {
                        yield break;
                    }

                    yield return new WaitForFixedUpdate();
                }
            }
            finally
            {
                // Always reopen the plug, even if an audit timeout interrupts this recovery.
                if (_world != null)
                {
                    if (_world.water != null)
                    {
                        _world.water.SetSpraying(false);
                        _world.water.Pressure = previousPressure;
                        _world.water.WideSpray = previousWideSpray;
                    }
                    if (_world.drain != null)
                    {
                        _world.drain.SetOpen(true);
                    }
                }
            }
        }

        bool UseFullOpenTool(string reason)
        {
            float previousRadius = _world.drain.radius;
            if (!_world.drain.TryOpenFully())
            {
                return false;
            }

            result.observations.Add("Used F for " + reason + "; radius " + previousRadius.ToString("F3") +
                " -> " + _world.drain.radius.ToString("F3"));
            Save();
            return true;
        }

        bool CanContinueAudit()
        {
            if (result.finished)
            {
                return false;
            }
            if (Time.time >= _auditDeadline)
            {
                Finish(false, "Audit reached its " + AuditTimeoutSeconds + " second total time budget");
                return false;
            }
            if (_world.IsComplete)
            {
                return true;
            }
            if (_world.IsOverflowed)
            {
                Finish(false, "Audit stopped when water spilled over the rim");
                return false;
            }
            if (_world.IsDrainSealed && !UseFullOpenTool("reopening the sealed drain"))
            {
                Finish(false, "The drain sealed and the full-open charge was already used");
                return false;
            }

            return true;
        }

        float FoodFootprintRadius(FoodScrap food)
        {
            Vector3 localBodyCenter = DrainFrame.InverseTransformPoint(food.Body.worldCenterOfMass);
            Collider collider = food.GetComponent<Collider>();
            if (collider is SphereCollider sphere)
            {
                Vector3 scale = sphere.transform.lossyScale;
                float sphereRadius = sphere.radius *
                    Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
                Vector3 localSphereCenter =
                    DrainFrame.InverseTransformPoint(sphere.transform.TransformPoint(sphere.center));
                Vector2 offset = new Vector2(localSphereCenter.x - localBodyCenter.x,
                    localSphereCenter.z - localBodyCenter.z);
                return sphereRadius + offset.magnitude;
            }

            BoxCollider box = collider as BoxCollider;
            Vector3 center = box != null ? box.center : collider.bounds.center;
            Vector3 halfSize = box != null ? box.size * 0.5f : collider.bounds.extents;
            float maximumSquaredRadius = 0f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = center + Vector3.Scale(halfSize, new Vector3(x, y, z));
                        Vector3 worldCorner = box != null ? box.transform.TransformPoint(corner) : corner;
                        Vector3 localCorner = DrainFrame.InverseTransformPoint(worldCorner) - localBodyCenter;
                        float squaredRadius = localCorner.x * localCorner.x + localCorner.z * localCorner.z;
                        maximumSquaredRadius = Mathf.Max(maximumSquaredRadius, squaredRadius);
                    }
                }
            }

            return Mathf.Sqrt(maximumSquaredRadius);
        }

        float HorizontalDistanceToDrain(Vector3 worldPoint)
        {
            Vector3 local = DrainFrame.InverseTransformPoint(worldPoint);
            return new Vector2(local.x, local.z).magnitude;
        }

        int StationFor(Vector3 worldPoint)
        {
            Vector3 localDirection = _world.sink.transform.InverseTransformDirection(worldPoint - DrainFrame.position);
            if (Mathf.Abs(localDirection.x) > Mathf.Abs(localDirection.z))
            {
                return localDirection.x > 0f ? 2 : 6;
            }

            return localDirection.z > 0f ? 4 : 0;
        }

        void AlignStations(Vector3 worldTarget)
        {
            Vector3 localTarget = _world.sink.transform.InverseTransformPoint(worldTarget);
            float sideX = Mathf.Clamp(localTarget.x, -1.2f, 1.2f);
            float sideZ = Mathf.Clamp(localTarget.z, -0.7f, 0.7f);
            _localStations[0].x = sideX;
            _localStations[4].x = sideX;
            _localStations[2].z = sideZ;
            _localStations[6].z = sideZ;
        }

        float ChooseAim(FoodScrap food, Vector3 desired)
        {
            float bestScore = -2f;
            Vector3 bestPoint = food.Body.worldCenterOfMass;
            bool bestWide = false;
            for (int mode = 0; mode < 2; mode++)
            {
                for (int sample = 0; sample < 12; sample++)
                {
                    Vector3 direction = Quaternion.Euler(0f, sample * 30f, 0f) * desired;
                    Vector3 candidate = food.Body.worldCenterOfMass - direction * (mode == 0 ? 0.14f : 0.24f);
                    Vector3 localCandidate = _world.sink.transform.InverseTransformPoint(candidate);
                    localCandidate.y = BasinWater.FloorY + 0.001f;
                    candidate = _world.sink.transform.TransformPoint(localCandidate);
                    Aim(candidate);

                    Ray ray = _world.water.aimCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                    if (!Physics.Raycast(ray, out RaycastHit eyeHit, _world.water.maxDistance,
                        _world.water.waterMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    Vector3 nozzleOrigin = _world.water.nozzle.position;
                    Vector3 travel = eyeHit.point - nozzleOrigin;
                    if (!Physics.Raycast(nozzleOrigin, travel.normalized, out RaycastHit hit,
                        travel.magnitude + 0.035f, _world.water.waterMask, QueryTriggerInteraction.Ignore))
                    {
                        continue;
                    }

                    FoodScrap direct = hit.collider.GetComponentInParent<FoodScrap>();
                    Vector3 push;
                    float coverage = 1f;
                    if (direct == food)
                    {
                        push = _world.water.GuidePush(travel, food.Body.worldCenterOfMass - hit.point, true);
                    }
                    else
                    {
                        if (direct != null || hit.normal.y < 0.65f)
                        {
                            continue;
                        }

                        float radius = mode == 0 ? _world.water.focusedRadius : _world.water.sprayRadius;
                        Vector3 contact = food.ClosestPoint(hit.point);
                        float distance = Vector3.Distance(contact, hit.point);
                        if (distance > radius || !CanReachFoodFromImpact(hit.point, contact, food))
                        {
                            continue;
                        }

                        push = _world.water.GuidePush(travel, food.Body.worldCenterOfMass - hit.point, false);
                        coverage = Mathf.Lerp(0.28f, 1f, 1f - distance / radius);
                    }

                    float modeForce = mode == 0 ? 1f : 0.57f;
                    float score = Vector3.Dot(push, desired) * coverage * modeForce;
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPoint = candidate;
                        bestWide = mode == 1;
                    }
                }
            }

            _world.water.WideSpray = bestWide;
            Aim(bestPoint);
            return bestScore;
        }

        bool CanReachFoodFromImpact(Vector3 impact, Vector3 contact, FoodScrap food)
        {
            // Match the physical splash path so the controller does not prefer a blocked corner shot.
            Vector3 start = impact + Vector3.up * 0.045f;
            Vector3 delta = contact + Vector3.up * 0.045f - start;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return true;
            }

            if (!Physics.Raycast(start, delta.normalized, out RaycastHit blocker, delta.magnitude,
                _world.water.waterMask, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            return blocker.collider.GetComponentInParent<FoodScrap>() == food;
        }

        IEnumerator RouteTo(int destination)
        {
            _world.water.SetSpraying(false);
            int stationCount = _localStations.Length;
            int forwardSteps = (destination - _station + stationCount) % stationCount;
            int backwardSteps = (_station - destination + stationCount) % stationCount;
            int direction = forwardSteps <= backwardSteps ? 1 : -1;
            do
            {
                if (_station != destination)
                {
                    _station = (_station + direction + stationCount) % stationCount;
                }

                float deadline = Time.time + RouteStepTimeoutSeconds;
                bool reachedStation = false;
                while (Time.time < deadline)
                {
                    if (!CanContinueAudit())
                    {
                        yield break;
                    }

                    Vector3 destinationPoint = _world.sink.transform.TransformPoint(_localStations[_station]);
                    Vector3 delta = destinationPoint - _world.player.transform.position;
                    delta.y = 0f;
                    if (delta.magnitude < 0.06f)
                    {
                        reachedStation = true;
                        break;
                    }

                    Transform player = _world.player.transform;
                    Vector2 input = new Vector2(Vector3.Dot(delta.normalized, player.right),
                        Vector3.Dot(delta.normalized, player.forward));
                    _world.player.Move(input, Time.fixedDeltaTime);
                    yield return new WaitForFixedUpdate();
                }

                if (!reachedStation)
                {
                    Finish(false, "Player could not navigate around counter within the route time budget");
                    yield break;
                }
            }
            while (_station != destination);
        }

        void Aim(Vector3 point)
        {
            Vector3 direction = point - _world.player.viewCamera.transform.position;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = Mathf.Atan2(-direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            _world.player.SetView(yaw, pitch);
        }

        IEnumerator DrainPause()
        {
            BasinWater basin = _world.basin;
            if (basin == null || basin.NormalizedLevel < 0.68f)
            {
                yield break;
            }

            _world.water.SetSpraying(false);
            float deadline = Time.time + DrainPauseTimeoutSeconds;
            while (basin.NormalizedLevel > 0.3f && Time.time < deadline)
            {
                if (!CanContinueAudit())
                {
                    yield break;
                }

                yield return new WaitForFixedUpdate();
            }
        }

        void Finish(bool passed, string message)
        {
            result.finished = true;
            result.passed = passed;
            result.status = message;
            UpdateResultCounts();
            RestoreEnvironment();
            Save();
            Debug.Log("Sink gameplay audit: " + message);
        }

        void RestoreEnvironment()
        {
            if (!_environmentCaptured)
            {
                return;
            }

            if (_world != null)
            {
                if (_world.water != null)
                {
                    _world.water.SetSpraying(false);
                }
                if (_world.player != null)
                {
                    _world.player.enabled = _previousPlayerEnabled;
                    _world.player.SetControl(false);
                }
            }
            Application.runInBackground = _previousRunInBackground;
            Time.timeScale = _previousTimeScale;
            _environmentCaptured = false;
        }

        void OnDestroy()
        {
            RestoreEnvironment();
        }

        void UpdateResultCounts()
        {
            result.seconds = Time.time - _started;
            if (_world != null)
            {
                result.foodRemaining = _world.FoodRemaining;
                result.stainsRemaining = _world.StainsRemaining;
            }
        }

        void Save()
        {
            UpdateResultCounts();
            if (string.IsNullOrEmpty(_outputDirectory))
            {
                return;
            }

            Directory.CreateDirectory(_outputDirectory);
            File.WriteAllText(Path.Combine(_outputDirectory, "gameplay-audit.json"), JsonUtility.ToJson(result, true));
        }

        void Capture(string name)
        {
            // Camera renders show the actual scene; IMGUI evidence must be captured separately.
            Camera camera = _world.player.viewCamera;
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var renderTexture = new RenderTexture(1280, 800, 24);
            var texture = new Texture2D(1280, 800, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0f, 0f, 1280f, 800f), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(_outputDirectory, name + ".png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Destroy(texture);
                renderTexture.Release();
                Destroy(renderTexture);
            }
        }
    }
}
#endif
