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

            float leftWidth = compact ? 235f : 285f;
            Panel(new Rect(16f, 16f, leftWidth, 94f));
            GUI.Label(new Rect(30f, 26f, leftWidth - 28f, 29f), "SINK / WATER ONLY", _title);
            if (world != null)
            {
                GUI.Label(new Rect(30f, 59f, leftWidth - 28f, 25f),
                    $"Food: {world.FoodRemaining} left     Stains: {world.StainsRemaining} left", _body);
                GUI.Label(new Rect(30f, 83f, leftWidth - 28f, 19f), "Spray food. Water pushes it away.", _small);
            }

            float rightWidth = compact ? 210f : 250f;
            float rightX = width - rightWidth - 16f;
            BasinWater basin = world != null ? world.basin : null;
            bool warn = basin != null && !basin.IsOverflowed && basin.NormalizedLevel >= 0.62f;
            float rightHeight = warn ? 176f : 156f;
            Panel(new Rect(rightX, 16f, rightWidth, rightHeight));
            string mode = water != null && water.WideSpray ? "SHOWER / WIDE" : "JET / FOCUSED";
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
                holeOpen ? $"Drain {Mathf.RoundToInt(opening * 100f)}% open" : "Drain closed", _small);
            DrawMeter(new Rect(rightX + 14f, 132f, rightWidth - 28f, 5f), opening, new Color(0.78f, 0.86f, 0.88f));
            GUI.Label(new Rect(rightX + 14f, 142f, rightWidth - 28f, 18f),
                holeOpen ? "E closes the hole to hold water." : "E opens it. Water pulls food in.", _small);
            if (warn)
                GUI.Label(new Rect(rightX + 14f, 162f, rightWidth - 28f, 22f), "Release spray. Let it drop.", _small);

            if (player != null && player.HasControl)
                DrawReticle(width * 0.5f, height * 0.5f, water != null && water.IsSpraying);
            else
            {
                Rect capture = new Rect(width * 0.5f - 175f, height * 0.5f - 37f, 350f, 74f);
                Panel(capture);
                GUI.Label(new Rect(capture.x + 10f, capture.y + 9f, 330f, 28f), "Click to aim", _center);
                GUI.Label(new Rect(capture.x + 10f, capture.y + 38f, 330f, 25f), "Then hold left mouse to spray.", _center);
            }

            if (world != null && world.IsOverflowed)
            {
                float bannerWidth = Mathf.Min(460f, width - 32f);
                Rect banner = new Rect((width - bannerWidth) * 0.5f, 188f, bannerWidth, 95f);
                Panel(banner);
                Color previousComplete = _complete.normal.textColor;
                _complete.normal.textColor = new Color(0.95f, 0.34f, 0.28f);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 9f, banner.width - 24f, 35f), "Sink overflowed.", _complete);
                _complete.normal.textColor = previousComplete;
                GUI.Label(new Rect(banner.x + 12f, banner.y + 45f, banner.width - 24f, 24f), "The water crossed the rim.", _center);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 70f, banner.width - 24f, 20f), "Press R for a fresh sink", _center);
            }
            else if (world != null && world.IsComplete)
            {
                float bannerWidth = Mathf.Min(440f, width - 32f);
                Rect banner = new Rect((width - bannerWidth) * 0.5f, 188f, bannerWidth, 95f);
                Panel(banner);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 9f, banner.width - 24f, 35f), "Sink clear.", _complete);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 45f, banner.width - 24f, 24f), "All food drained. All stains washed away.", _center);
                GUI.Label(new Rect(banner.x + 12f, banner.y + 70f, banner.width - 24f, 20f), "Press R for a fresh sink", _center);
            }

            float controlsWidth = Mathf.Min(750f, width - 32f);
            Rect controls = new Rect((width - controlsWidth) * 0.5f, height - 75f, controlsWidth, 59f);
            Panel(controls);
            GUI.Label(new Rect(controls.x + 8f, controls.y + 6f, controls.width - 16f, 25f),
                "WASD move  /  Mouse aim  /  Hold LMB spray  /  Q or RMB mode", _center);
            GUI.Label(new Rect(controls.x + 8f, controls.y + 30f, controls.width - 16f, 24f),
                "Scroll pressure  /  E open or close hole  /  R reset", _center);

            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
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
