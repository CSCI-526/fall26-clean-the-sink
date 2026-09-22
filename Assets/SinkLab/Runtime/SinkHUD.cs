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
                GUI.Label(new Rect(30f, 83f, leftWidth - 28f, 19f), "Spray behind food to steer it.", _small);
            }

            float rightWidth = compact ? 180f : 230f;
            float rightX = width - rightWidth - 16f;
            Panel(new Rect(rightX, 16f, rightWidth, 94f));
            string mode = water != null && water.WideSpray ? "SHOWER / WIDE" : "JET / FOCUSED";
            GUI.Label(new Rect(rightX + 14f, 25f, rightWidth - 28f, 25f), mode, _body);
            int pressure = water == null ? 0 : Mathf.RoundToInt(Mathf.Lerp(26f, 100f, water.Pressure));
            GUI.Label(new Rect(rightX + 14f, 53f, rightWidth - 28f, 23f), $"Pressure {pressure}%", _small);
            Solid(new Rect(rightX + 14f, 83f, rightWidth - 28f, 5f), new Color(0.20f, 0.29f, 0.33f));
            Solid(new Rect(rightX + 14f, 83f, (rightWidth - 28f) * pressure / 100f, 5f), _accent);

            if (player != null && player.HasControl)
                DrawReticle(width * 0.5f, height * 0.5f, water != null && water.IsSpraying);
            else
            {
                Rect capture = new Rect(width * 0.5f - 175f, height * 0.5f - 37f, 350f, 74f);
                Panel(capture);
                GUI.Label(new Rect(capture.x + 10f, capture.y + 9f, 330f, 28f), "Click to aim", _center);
                GUI.Label(new Rect(capture.x + 10f, capture.y + 38f, 330f, 25f), "Then hold left mouse to spray.", _center);
            }

            if (world != null && world.IsComplete)
            {
                float bannerWidth = Mathf.Min(440f, width - 32f);
                Rect banner = new Rect((width - bannerWidth) * 0.5f, 127f, bannerWidth, 95f);
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
                "Scroll pressure  /  1 jet, 2 shower  /  R reset  /  Esc release mouse", _center);

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
