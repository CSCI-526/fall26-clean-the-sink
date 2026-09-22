using UnityEngine;

namespace SinkLab
{
    /// <summary>Authored connection points owned by a reusable sink prefab.</summary>
    public sealed class SinkAssembly : MonoBehaviour
    {
        public Drain drain;
        public Transform hoseAnchor;
    }
}
