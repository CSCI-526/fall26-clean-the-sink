using System.Collections.Generic;
using UnityEngine;

namespace SinkLab
{
    /// <summary>Coordinates the authored sink, player, and mess below one level root.</summary>
    public sealed class SinkWorld : MonoBehaviour
    {
        public FoodScrap[] foods;
        public StainPatch[] stains;
        public WaterJet water;
        public SinkPlayer player;
        public Drain drain;
        public SinkAssembly sink;
        public BasinWater basin;

        public int FoodRemaining
        {
            get
            {
                int count = 0;
                if (foods != null)
                    foreach (FoodScrap food in foods)
                        if (food == null || !food.IsDrained) count++;
                return count;
            }
        }

        public int StainsRemaining
        {
            get
            {
                int count = 0;
                if (stains != null)
                    foreach (StainPatch stain in stains)
                        if (stain == null || !stain.IsClean) count++;
                return count;
            }
        }

        public bool IsOverflowed => basin != null && basin.IsOverflowed;
        public bool IsDrainSealed => drain != null && drain.IsSealed;

        // Missing required parts or deleted objective references must not award completion.
        public bool IsComplete => player != null && water != null && drain != null &&
            foods != null && foods.Length > 0 && stains != null && stains.Length > 0 &&
            FoodRemaining == 0 && StainsRemaining == 0;

        void Awake()
        {
            RefreshLevelReferences();

            if (!Application.isPlaying)
            {
                return;
            }

            if (basin == null)
            {
                basin = GetComponent<BasinWater>();
            }

            if (basin == null)
            {
                basin = gameObject.AddComponent<BasinWater>();
            }

            basin.Configure(this);
        }

        /// <summary>
        /// Rebinds reusable child prefabs and includes newly placed or inactive mess.
        /// A nested level owns its own children and is deliberately excluded.
        /// </summary>
        [ContextMenu("Refresh Level References")]
        public void RefreshLevelReferences()
        {
            foods = GatherLevelComponents<FoodScrap>();
            stains = GatherLevelComponents<StainPatch>();
            sink = FindLevelComponent<SinkAssembly>();
            player = FindLevelComponent<SinkPlayer>();
            water = FindLevelComponent<WaterJet>();
            drain = sink != null && BelongsToThisLevel(sink.drain)
                ? sink.drain
                : FindLevelComponent<Drain>();

            if (player != null)
            {
                player.world = this;
                player.water = water;
            }

            foreach (SinkHUD hud in GatherLevelComponents<SinkHUD>())
            {
                hud.world = this;
                hud.player = player;
                hud.water = water;
            }

            Transform hoseAnchor = sink != null && BelongsToThisLevel(sink.hoseAnchor)
                ? sink.hoseAnchor
                : null;
            foreach (WaterVisuals visuals in GatherLevelComponents<WaterVisuals>())
            {
                visuals.water = water;
                visuals.hoseAnchor = hoseAnchor;
            }
        }

        public void ResetRun()
        {
            if (water != null) water.SetSpraying(false);
            if (foods != null)
                foreach (FoodScrap food in foods)
                    if (food != null) food.ResetScrap();
            if (stains != null)
                foreach (StainPatch stain in stains)
                    if (stain != null) stain.ResetStain();
            if (drain != null) drain.ResetCount();
            if (basin != null) basin.ResetWater();
            if (player != null) player.ResetPlayer();
            Physics.SyncTransforms();
        }

        T FindLevelComponent<T>() where T : Component
        {
            foreach (T candidate in GetComponentsInChildren<T>(true))
                if (BelongsToThisLevel(candidate)) return candidate;
            return null;
        }

        T[] GatherLevelComponents<T>() where T : Component
        {
            var result = new List<T>();
            foreach (T candidate in GetComponentsInChildren<T>(true))
                if (BelongsToThisLevel(candidate)) result.Add(candidate);
            return result.ToArray();
        }

        bool BelongsToThisLevel(Component candidate)
        {
            return candidate != null && candidate.GetComponentInParent<SinkWorld>(true) == this;
        }
    }
}
