using UnityEngine;
using UnityEngine.UI;

namespace Yard
{
    /// <summary>A flag pinned on a marked marker: a pole, a cloth that is raised, and the flag's name.</summary>
    [DisallowMultipleComponent]
    public class FlagView : MonoBehaviour
    {
        [SerializeField] Transform cloth;
        [SerializeField] Transform label;
        [SerializeField] Text labelText;
        [Tooltip("Cloth local Y at the bottom of the pole.")]
        [SerializeField] float loweredY = 0.25f;
        [Tooltip("Cloth local Y at the top of the pole.")]
        [SerializeField] float raisedY = 0.92f;
        [Tooltip("How far the name sits above the cloth.")]
        [SerializeField] float labelAbove = 0.32f;

        void Awake() => LabelOnTop.Apply(label);

        public void SetName(string flagName) => labelText.text = string.IsNullOrEmpty(flagName) ? "flag" : flagName;

        /// <summary>0 = not there yet, 1 = full size. The flag grows out of its mount instead of appearing.</summary>
        public void SetGrow(float p) => transform.localScale = Vector3.one * Mathf.Max(0f, p);

        /// <summary>0 = cloth at the bottom of the pole, 1 = at the top (a little past 1 overshoots). The name rides with it.</summary>
        public void SetRaise(float p)
        {
            float y = Mathf.LerpUnclamped(loweredY, raisedY, p);
            var c = cloth.localPosition;
            c.y = y;
            cloth.localPosition = c;
            var l = label.localPosition;
            l.y = y + labelAbove;
            label.localPosition = l;
        }
    }
}
