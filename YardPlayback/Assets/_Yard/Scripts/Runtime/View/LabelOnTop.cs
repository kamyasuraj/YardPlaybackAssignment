using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Yard
{
    /// <summary>
    /// World-space labels drawn over the yard's geometry (no depth test), so a load number or an action
    /// word is never hidden behind a gate post or another marker. Rich text is turned off on the way:
    /// ids, flags and the ticket come from the script and show exactly as written, tags and all.
    /// </summary>
    public static class LabelOnTop
    {
        static readonly int ZTestId = Shader.PropertyToID("unity_GUIZTestMode");
        static Material material;

        public static void Apply(Component root)
        {
            if (root == null) return;
            if (material == null)
            {
                material = new Material(Graphic.defaultGraphicMaterial) { name = "Label (on top)" };
                material.SetInt(ZTestId, (int)CompareFunction.Always);
            }
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.material = material;
                if (graphic is Text text) text.supportRichText = false;
            }
        }

        /// <summary>Turns rich text off on every Text under <paramref name="root"/> (screen-space HUD text).</summary>
        public static void PlainText(Component root)
        {
            if (root == null) return;
            foreach (var text in root.GetComponentsInChildren<Text>(true)) text.supportRichText = false;
        }
    }
}
