using UnityEngine;
using UnityEngine.UI;

namespace Yard
{
    /// <summary>The current action's word (CARRY, SLIP, …) by the marker doing it, in the action's colour.</summary>
    [DisallowMultipleComponent]
    public class ActionChip : MonoBehaviour
    {
        const float MinWidth = 320f;
        const float Padding = 70f;   // room either side of the word, in chip-canvas units

        [SerializeField] CanvasGroup group;
        [SerializeField] RectTransform content;
        [SerializeField] Image back;
        [SerializeField] Text word;

        public bool IsSetUp => group != null && content != null && back != null && word != null;

        void Awake() => LabelOnTop.Apply(content);

        /// <summary>Shows <paramref name="text"/> on a chip of <paramref name="color"/>, wide enough that the word never touches its edges.</summary>
        public void Set(string text, Color color)
        {
            word.text = text;
            back.color = color;
            float width = Mathf.Max(MinWidth, word.preferredWidth + Padding);
            back.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }

        /// <summary>0 = hidden, 1 = shown; it grows a little as it appears.</summary>
        public void SetVisible(float k)
        {
            group.alpha = k;
            content.localScale = Vector3.one * (0.8f + 0.2f * k);
        }
    }
}
