using System.Collections.Generic;
using UnityEngine;

namespace SinkLab
{
    /// <summary>A physical piece of food. Only the drain can mark it as collected.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class FoodScrap : MonoBehaviour
    {
        public static readonly HashSet<FoodScrap> Active = new HashSet<FoodScrap>();

        public Rigidbody Body;
        public bool IsDrained { get; private set; }

        Vector3 spawnPosition;
        Quaternion spawnRotation;
        bool hasSpawn;
        Renderer[] renderers;
        Collider[] colliders;
        bool[] rendererStates;
        bool[] colliderStates;
        bool spawnKinematic;
        bool spawnGravity;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Start()
        {
            if (!hasSpawn)
            {
                CaptureSpawn();
            }
        }

        public void CaptureSpawn()
        {
            if (Body == null)
            {
                Body = GetComponent<Rigidbody>();
            }

            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            spawnKinematic = Body.isKinematic;
            spawnGravity = Body.useGravity;

            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            rendererStates = new bool[renderers.Length];
            colliderStates = new bool[colliders.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                rendererStates[i] = renderers[i].enabled;
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                colliderStates[i] = colliders[i].enabled;
            }

            hasSpawn = true;
        }

        internal void MarkDrained()
        {
            if (IsDrained)
            {
                return;
            }

            if (!hasSpawn)
            {
                CaptureSpawn();
            }

            IsDrained = true;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.isKinematic = true;
            foreach (Renderer item in renderers)
            {
                if (item != null)
                {
                    item.enabled = false;
                }
            }

            foreach (Collider item in colliders)
            {
                if (item != null)
                {
                    item.enabled = false;
                }
            }
        }

        /// <summary>The scrap the audit should keep pushing.</summary>
        public FoodScrap GuidanceTarget => this;

        public void ResetScrap()
        {
            if (!hasSpawn)
            {
                CaptureSpawn();
            }

            IsDrained = false;
            gameObject.SetActive(true);
            Body.isKinematic = false;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.position = spawnPosition;
            Body.rotation = spawnRotation;
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Body.useGravity = spawnGravity;
            Body.isKinematic = spawnKinematic;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled = rendererStates[i];
                }
            }

            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled = colliderStates[i];
                }
            }

            if (!Body.isKinematic)
            {
                Body.WakeUp();
            }
        }

        public Vector3 ClosestPoint(Vector3 position)
        {
            if (colliders == null)
            {
                colliders = GetComponentsInChildren<Collider>();
            }

            Vector3 closest = Body != null ? Body.worldCenterOfMass : transform.position;
            float distance = float.PositiveInfinity;
            foreach (Collider item in colliders)
            {
                if (item == null || !item.enabled || item.isTrigger)
                {
                    continue;
                }

                Vector3 point = item.ClosestPoint(position);
                float candidate = (position - point).sqrMagnitude;
                if (candidate < distance)
                {
                    distance = candidate;
                    closest = point;
                }
            }

            return closest;
        }
    }
}
