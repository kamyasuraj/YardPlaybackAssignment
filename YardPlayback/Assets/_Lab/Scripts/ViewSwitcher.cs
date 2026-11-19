using UnityEngine;
using UnityEngine.InputSystem;

namespace Lab
{
    /// <summary>
    /// Shows one of the View_1..View_4 cameras under this object at a time, so the four fixed views can be
    /// checked in the Game view: press 1–4 in Play mode, or use the menu Lab › View 1–4 (Alt+1–4) in Edit or
    /// Play mode. When Play starts, a short hint explains this until it is closed.
    /// </summary>
    [DisallowMultipleComponent]
    public class ViewSwitcher : MonoBehaviour
    {
        const string HintTitle = "Lab lighting: four fixed views";
        const string HintBody =
            "Press 1, 2, 3 or 4 to switch between View_1 to View_4.\n" +
            "Or use the menu Lab › View 1–4 (Alt+1 to Alt+4), also in Edit mode.\n\n" +
            "The grey sphere is not static: it takes its light from the light probes. " +
            "Watch it get darker as it moves into the west corners.";

        Camera[] views;
        bool hintOpen = true;
        GUIStyle titleStyle, bodyStyle, buttonStyle;
        Texture2D panel;

        /// <summary>Index of the view showing (0 = View_1).</summary>
        public int Current { get; private set; }

        Camera[] Views => views != null && views.Length > 0 ? views : views = GetComponentsInChildren<Camera>(true);

        void Awake()
        {
            // Keep whichever view was showing in the Editor; View_1 if none.
            int showing = 0;
            for (int i = 0; i < Views.Length; i++)
                if (Views[i].enabled) { showing = i; break; }
            Show(showing);
        }

        /// <summary>Shows view <paramref name="index"/> (0-based) and turns the others off.</summary>
        public void Show(int index)
        {
            if (Views.Length == 0) return;
            Current = Mathf.Clamp(index, 0, Views.Length - 1);
            for (int i = 0; i < Views.Length; i++) Views[i].enabled = i == Current;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.digit1Key.wasPressedThisFrame || keyboard.numpad1Key.wasPressedThisFrame) Show(0);
            if (keyboard.digit2Key.wasPressedThisFrame || keyboard.numpad2Key.wasPressedThisFrame) Show(1);
            if (keyboard.digit3Key.wasPressedThisFrame || keyboard.numpad3Key.wasPressedThisFrame) Show(2);
            if (keyboard.digit4Key.wasPressedThisFrame || keyboard.numpad4Key.wasPressedThisFrame) Show(3);
        }

        // The hint: a centred panel, laid out at 720 px high and scaled to the Game view, with a Close button.
        void OnGUI()
        {
            if (!hintOpen) return;
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, wordWrap = true };
                titleStyle.normal.textColor = new Color(1f, 0.8f, 0.2f);
                bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 17, wordWrap = true };
                bodyStyle.normal.textColor = Color.white;
                buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 17, fontStyle = FontStyle.Bold };
                panel = new Texture2D(1, 1);
                panel.SetPixel(0, 0, new Color(0.05f, 0.06f, 0.08f, 0.88f));
                panel.Apply();
            }

            var oldMatrix = GUI.matrix;
            float scale = Mathf.Max(0.75f, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            const float w = 600f, h = 250f, pad = 22f;
            var box = new Rect((Screen.width / scale - w) * 0.5f, (720f - h) * 0.5f, w, h);
            GUI.DrawTexture(box, panel);
            GUI.Label(new Rect(box.x + pad, box.y + pad, w - 2f * pad, 32f), HintTitle, titleStyle);
            GUI.Label(new Rect(box.x + pad, box.y + pad + 42f, w - 2f * pad, h - 2f * pad - 90f), HintBody, bodyStyle);
            if (GUI.Button(new Rect(box.xMax - pad - 120f, box.yMax - pad - 40f, 120f, 40f), "Close", buttonStyle)) hintOpen = false;

            GUI.matrix = oldMatrix;
        }

        void OnDestroy()
        {
            if (panel != null) Destroy(panel);
        }
    }
}
