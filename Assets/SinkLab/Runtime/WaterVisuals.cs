using UnityEngine;

namespace SinkLab
{
    public sealed class WaterVisuals : MonoBehaviour
    {
        const int StreamCount = 7;
        const int WideStreamCount = 6;
        const int HosePointCount = 24;

        public WaterJet water;
        public Transform hoseAnchor;
        public Material waterMaterial;
        public Material hoseMaterial;

        [Tooltip("Procedural water sound is disabled for the class prototype. Set before entering Play mode.")]
        public bool enableWaterAudio = false;

        [Header("Drawn thickness. Stream 0 is the focused jet. Streams 1-6 appear only in wide spray.")]
        public float focusedStartWidth = .034f;
        public float focusedEndWidth = .05f;
        public float wideStartWidth = .016f;
        public float wideEndWidth = .028f;

        LineRenderer[] streams;
        LineRenderer hose;
        ParticleSystem splash;
        AudioSource flow;
        float emitRemainder;

        void Start()
        {
            InitializeStreams();
            hose = CreateLine("Flexible faucet hose", hoseMaterial, .035f, HosePointCount);
            InitializeSplash();
            if (enableWaterAudio)
            {
                InitializeFlowAudio();
            }
        }

        void InitializeStreams()
        {
            streams = new LineRenderer[StreamCount];
            for (int streamIndex = 0; streamIndex < streams.Length; streamIndex++)
            {
                streams[streamIndex] = CreateLine("Water stream " + streamIndex, waterMaterial, .013f, 3);
            }
        }

        void InitializeSplash()
        {
            GameObject splashObject = new GameObject("Water splash droplets");
            splashObject.transform.SetParent(transform);
            splash = splashObject.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = splash.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = .24f;
            main.startSpeed = 0;
            main.startSize = .027f;
            main.gravityModifier = .8f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 500;

            ParticleSystem.EmissionModule emission = splash.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = splash.shape;
            shape.enabled = false;
            splash.GetComponent<ParticleSystemRenderer>().sharedMaterial = waterMaterial;
            splash.Play();
        }

        void InitializeFlowAudio()
        {
            flow = gameObject.AddComponent<AudioSource>();
            flow.loop = true;
            flow.playOnAwake = false;
            flow.volume = 0;
            flow.spatialBlend = 0;

            const int sampleRate = 24000;
            const int noiseSeed = 178;
            const float noiseSmoothing = .32f;
            const float noiseAmplitude = .38f;

            // One second of seeded, smoothed noise keeps the procedural loop reproducible.
            AudioClip clip = AudioClip.Create("Procedural running water", sampleRate, 1, sampleRate, false);
            float[] samples = new float[sampleRate];
            System.Random random = new System.Random(noiseSeed);
            float smoothedNoise = 0;
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                float noise = (float)random.NextDouble() * 2 - 1;
                smoothedNoise = Mathf.Lerp(smoothedNoise, noise, noiseSmoothing);
                samples[sampleIndex] = smoothedNoise * noiseAmplitude;
            }

            clip.SetData(samples, 0);
            flow.clip = clip;
            flow.Play();
        }

        LineRenderer CreateLine(string name, Material material, float width, int positionCount)
        {
            GameObject lineObject = new GameObject(name);
            lineObject.transform.SetParent(transform);
            LineRenderer lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = material;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
            lineRenderer.positionCount = positionCount;
            lineRenderer.useWorldSpace = true;
            lineRenderer.numCapVertices = 3;
            return lineRenderer;
        }

        void LateUpdate()
        {
            if (streams == null)
            {
                return;
            }

            if (!water || !water.aimCamera)
            {
                HideVisualsAndMute();
                return;
            }

            const float unblockedStreamLengthMeters = 3f;
            Vector3 nozzlePosition = water.nozzle ? water.nozzle.position : water.aimCamera.transform.position;
            Vector3 endPosition = water.HasHit
                ? water.LastHitPoint
                : nozzlePosition + water.aimCamera.transform.forward * unblockedStreamLengthMeters;

            UpdateStreams(nozzlePosition, endPosition);
            UpdateHose(nozzlePosition);
            EmitSplash(endPosition);
            UpdateFlowAudio();
        }

        void HideVisualsAndMute()
        {
            foreach (LineRenderer stream in streams)
            {
                if (stream)
                {
                    stream.enabled = false;
                }
            }
            if (hose)
            {
                hose.enabled = false;
            }
            if (flow)
            {
                flow.volume = 0;
            }
        }

        void UpdateStreams(Vector3 nozzlePosition, Vector3 endPosition)
        {
            const float spreadRadiusScale = .65f;
            const float waveRate = 38f;
            const float waveAmplitudeMeters = .006f;

            Vector3 streamAxis = (endPosition - nozzlePosition).normalized;
            Vector3 streamSide = Vector3.Cross(streamAxis, Vector3.up).normalized;
            Vector3 streamUp = Vector3.Cross(streamSide, streamAxis);
            for (int streamIndex = 0; streamIndex < streams.Length; streamIndex++)
            {
                LineRenderer streamRenderer = streams[streamIndex];
                streamRenderer.enabled = water.IsSpraying && (streamIndex == 0 || water.WideSpray);
                if (!streamRenderer.enabled)
                {
                    continue;
                }

                float angle = streamIndex * Mathf.PI * 2 / WideStreamCount;
                Vector3 spread = streamIndex == 0
                    ? Vector3.zero
                    : (streamSide * Mathf.Cos(angle) + streamUp * Mathf.Sin(angle))
                        * water.EffectiveRadius * spreadRadiusScale;
                float pressureWidthScale = Mathf.Lerp(.7f, 1.45f, water.Pressure);
                streamRenderer.startWidth = (water.WideSpray ? wideStartWidth : focusedStartWidth) * pressureWidthScale;
                streamRenderer.endWidth = (water.WideSpray ? wideEndWidth : focusedEndWidth) * pressureWidthScale;

                streamRenderer.SetPosition(0, nozzlePosition);
                Vector3 midpoint = Vector3.Lerp(nozzlePosition, endPosition + spread, .5f)
                    + streamSide * Mathf.Sin(Time.time * waveRate + streamIndex) * waveAmplitudeMeters;
                streamRenderer.SetPosition(1, midpoint);
                streamRenderer.SetPosition(2, endPosition + spread);
            }
        }

        void UpdateHose(Vector3 nozzlePosition)
        {
            const float hoseSagMeters = .26f;
            hose.enabled = hoseAnchor != null;
            if (!hoseAnchor)
            {
                return;
            }

            for (int pointIndex = 0; pointIndex < HosePointCount; pointIndex++)
            {
                float progress = pointIndex / (float)(HosePointCount - 1);
                Vector3 position = Vector3.Lerp(hoseAnchor.position, nozzlePosition, progress)
                    + Vector3.down * Mathf.Sin(progress * Mathf.PI) * hoseSagMeters;
                hose.SetPosition(pointIndex, position);
            }
        }

        void EmitSplash(Vector3 impactPoint)
        {
            if (!water.IsSpraying || !water.HasHit)
            {
                return;
            }

            const float dropletsPerSecond = 110f;
            const float impactOffsetMeters = .025f;
            const float scatterRadiusMeters = .025f;
            const float velocityScatterScale = .9f;

            emitRemainder += Time.deltaTime * dropletsPerSecond;
            int dropletCount = Mathf.FloorToInt(emitRemainder);
            emitRemainder -= dropletCount;
            for (int dropletIndex = 0; dropletIndex < dropletCount; dropletIndex++)
            {
                // Keep random sampling ordered by position, velocity, then size for each droplet.
                ParticleSystem.EmitParams droplet = new ParticleSystem.EmitParams();
                droplet.position = impactPoint + water.LastHitNormal * impactOffsetMeters
                    + Random.insideUnitSphere * scatterRadiusMeters;
                droplet.velocity = (water.LastHitNormal + Random.insideUnitSphere * velocityScatterScale)
                    * Random.Range(.4f, 1.4f);
                droplet.startSize = Random.Range(.009f, .03f);
                droplet.startColor = new Color(.7f, .9f, 1, .7f);
                splash.Emit(droplet, 1);
            }
        }

        void UpdateFlowAudio()
        {
            if (!flow)
            {
                return;
            }

            if (enableWaterAudio)
            {
                float targetVolume = water.IsSpraying ? .12f + water.Pressure * .09f : 0;
                flow.volume = Mathf.MoveTowards(flow.volume, targetVolume, Time.unscaledDeltaTime * .8f);
            }
            else
            {
                flow.volume = 0;
            }
            flow.pitch = water.WideSpray ? 1.15f : .88f + water.Pressure * .15f;
        }

        void OnDestroy()
        {
            if (flow && flow.clip)
            {
                Destroy(flow.clip);
            }
        }
    }
}
