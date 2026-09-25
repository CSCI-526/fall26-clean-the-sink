using UnityEngine;

namespace SinkLab
{
    /// <summary>Compact, resolution-independent guidance and honest completion feedback.</summary>
    public sealed class SinkHUD : MonoBehaviour
    {
        public SinkWorld world;
        public SinkPlayer player;
        public WaterJet water;

        GUIStyle _title;
        GUIStyle _body;
        GUIStyle _small;
        GUIStyle _center;
        GUIStyle _complete;
        readonly Color _panel = new Color(0.025f, 0.055f, 0.075f, 0.88f);
        readonly Color _muted = new Color(0.66f, 0.77f, 0.81f, 1f);
        readonly Color _accent = new Color(0.40f, 0.90f, 0.94f, 1f);

        void OnGUI()
        {
            EnsureStyles();
            if (world != null)
            {
                if (player == null) player = world.player;
                if (water == null) water = world.water;
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            float scale = Mathf.Clamp(Screen.width / 1100f, 0.65f, 1.2f);
            _small.fontSize = _center.fontSize = Mathf.Max(14, Mathf.CeilToInt(11f / scale));
            GUI.matrix = Matrix4x4.Scale(Vector3.one * scale);
            float width = Screen.width / scale;
            float height = Screen.height / scale;
            bool compact = width < 650f;

            float leftWidth = compact ? 200f : 230f;
            Panel(new Rect(16f, 16f, leftWidth, 72f));
            GUI.Label(new Rect(30f, 24f, leftWidth - 28f, 26f), "SINK", _title);
            if (world != null)
                GUI.Label(new Rect(30f, 52f, leftWidth - 28f, 24f),
                    $"Food {world.FoodRemaining}    Stains {world.StainsRemaining}", _body);

            float rightWidth = compact ? 210f : 250f;
            float rightX = width - rightWidth - 16f;
            BasinWater basin = world != null ? world.basin : null;
            bool warn = basin != null && !basin.IsOverflowed && basin.NormalizedLevel >= 0.62f;
            float rightHeight = warn ? 162f : 140f;
            Panel(new Rect(rightX, 16f, rightWidth, rightHeight));
            string mode = water != null && water.WideSpray ? "SHOWER" : "JET";
            GUI.Label(new Rect(rightX + 14f, 24f, rightWidth - 28f, 22f), mode, _body);
            int pressure = water == null ? 0 : Mathf.RoundToInt(Mathf.Lerp(26f, 100f, water.Pressure));
            GUI.Label(new Rect(rightX + 14f, 48f, rightWidth - 28f, 18f), $"Pressure {pressure}%", _small);
            DrawMeter(new Rect(rightX + 14f, 68f, rightWidth - 28f, 5f), pressure / 100f, _accent);
            float waterLevel = basin == null ? 0f : basin.NormalizedLevel;
            Color waterColor = Color.Lerp(_accent, new Color(0.95f, 0.28f, 0.22f), waterLevel);
            GUI.Label(new Rect(rightX + 14f, 80f, rightWidth - 28f, 18f), $"Water {Mathf.RoundToInt(waterLevel * 100f)}%", _small);
            DrawMeter(new Rect(rightX + 14f, 100f, rightWidth - 28f, 5f), waterLevel, waterColor);
            bool holeOpen = world == null || world.drain == null || world.drain.IsOpen;
            float opening = 1f;
            if (world != null && world.drain != null && world.drain.StartRadius > 0.001f)
                opening = Mathf.Clamp01(world.drain.radius / world.drain.StartRadius);
            if (!holeOpen) opening = 0f;
            GUI.Label(new Rect(rightX + 14f, 112f, rightWidth - 28f, 18f),
                holeOpen ? $"Drain {Mathf.RoundToInt(opening * 100f)}%" : "Drain shut", _small);
            DrawMeter(new Rect(rightX + 14f, 132f, rightWidth - 28f, 5f), opening, new Color(0.78f, 0.86f, 0.88f));
            if (warn)
                GUI.Label(new Rect(rightX + 14f, 142f, rightWidth - 28f, 18f), "High water", _small);

            if (player != null && player.HasControl)
                DrawReticle(width * 0.5f, height * 0.5f, water != null && water.IsSpraying);
            else
            {
                Rect capture = new Rect(width * 0.5f - 175f, height * 0.5f - 37f, 350f, 74f);
                Panel(capture);
                GUI.Label(new Rect(capture.x + 10f, capture.y + 9f, 330f, 28f), "Click to aim", _center);
                GUI.Label(new Rect(capture.x + 10f, capture.y + 38f, 330f, 25f), "Hold LMB to spray", _center);
            }

            if (world != null && world.IsComplete)
            {
                float bannerWidth = Mathf.Min(440f, width - 32f);
                Rect banner = new Rect((width - bannerWidth) * 0.5f, 188f, bannerWidth, 95f);
                Panel(banner);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 9f, banner.width - 24f, 35f), "Success", _complete);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 48f, banner.width - 24f, 22f), "Clean", _center);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 70f, banner.width - 24f, 20f), "R reset", _center);
            }
            else if (world != null && world.IsDrainSealed)
            {
                float bannerWidth = Mathf.Min(460f, width - 32f);
                Rect banner = new Rect((width - bannerWidth) * 0.5f, 188f, bannerWidth, 95f);
                Panel(banner);
                Color previousComplete = _complete.normal.textColor;
                _complete.normal.textColor = new Color(0.95f, 0.34f, 0.28f);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 9f, banner.width - 24f, 35f), "Fail", _complete);
                _complete.normal.textColor = previousComplete;
                GUI.Label(new Rect(banner.x + 12f, banner.y + 48f, banner.width - 24f, 22f), "Drain closed", _center);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 70f, banner.width - 24f, 20f), "R reset", _center);
            }

            float toolWidth = compact ? 176f : 210f;
            float controlsWidth = Mathf.Min(560f, width - toolWidth - 44f);
            Rect controls = new Rect(16f, height - 75f, controlsWidth, 59f);
            Panel(controls);
            GUI.Label(new Rect(controls.x + 8f, controls.y + 6f, controls.width - 16f, 25f),
                "WASD move  /  Mouse aim  /  Hold LMB spray  /  Q or RMB mode", _center);
            GUI.Label(new Rect(controls.x + 8f, controls.y + 30f, controls.width - 16f, 24f),
                "Scroll pressure  /  E hold water  /  R reset", _center);
            DrawDrainTool(new Rect(width - toolWidth - 16f, height - 92f, toolWidth, 76f));

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        void DrawDrainTool(Rect panel)
        {
            Panel(panel);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 6f, panel.width - 20f, 18f), "Once", _small);
            if (world == null || world.drain == null) return;
            bool ready = world.drain.HasFullOpenCharge;
            GUI.Label(new Rect(panel.x + 10f, panel.y + 24f, panel.width - 20f, 22f),
                ready ? "F  Open drain" : "Used", _body);
            Rect button = new Rect(panel.x + 10f, panel.y + 48f, panel.width - 20f, 22f);
            if (ready)
            {
                if (GUI.Button(button, "Use"))
                    world.drain.TryOpenFully();
            }
            else GUI.Label(button, "Spent", _center);
        }

        void EnsureStyles()
        {
            if (_title != null) return;
            _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold };
            _title.normal.textColor = Color.white;
            _body = new GUIStyle(GUI.skin.label) { fontSize = 16 };
            _body.normal.textColor = Color.white;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _small.normal.textColor = _muted;
            _center = new GUIStyle(_small) { alignment = TextAnchor.MiddleCenter };
            _complete = new GUIStyle(_title) { fontSize = 27, alignment = TextAnchor.MiddleCenter };
            _complete.normal.textColor = _accent;
        }

        void Panel(Rect rect) => Solid(rect, _panel);

        void DrawMeter(Rect rect, float amount, Color color)
        {
            Solid(rect, new Color(0.20f, 0.29f, 0.33f));
            Solid(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(amount), rect.height), color);
        }

        static void Solid(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }

        void DrawReticle(float x, float y, bool spraying)
        {
            Color color = spraying ? _accent : new Color(1f, 1f, 1f, 0.9f);
            Solid(new Rect(x - 2f, y - 2f, 5f, 5f), new Color(0f, 0f, 0f, 0.65f));
            Solid(new Rect(x - 1f, y - 1f, 3f, 3f), color);
            Solid(new Rect(x - 11f, y, 5f, 1f), color);
            Solid(new Rect(x + 7f, y, 5f, 1f), color);
            Solid(new Rect(x, y - 11f, 1f, 5f), color);
            Solid(new Rect(x, y + 7f, 1f, 5f), color);
        }
    }
}
