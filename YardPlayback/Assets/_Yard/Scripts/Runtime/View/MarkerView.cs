using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Yard.Cues;

namespace Yard
{
    /// <summary>
    /// One marker, a forklift or a crate, from a prefab. It is either a placeholder already in the
    /// scene or spawned from its prefab when the script names a marker the scene doesn't have; either
    /// way the script takes it over on Load (<see cref="Bind"/>). The root sits where the marker rests;
    /// <see cref="Body"/> holds the model and any short-lived motion, so clearing Body leaves nothing moving.
    /// </summary>
    [DisallowMultipleComponent]
    public class MarkerView : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color DefaultFlashColor = new Color(1f, 0.98f, 0.8f);
        static readonly Color PulseColor = new Color(1f, 0.85f, 0.2f);
        // A carried crate's label moves aside and up, so it never sits on top of its forklift's.
        static readonly Vector3 CarriedLabelShift = new Vector3(0.65f, 0.35f, 0f);
        const float DarkestDim = 0.35f;     // body colour at full dim, as a fraction of normal
        const float DimmedLabelAlpha = 0.7f; // labels never fade further when dimmed, so loads stay readable

        [Tooltip("The model. Stumbles, squashes and fades move this, never the marker's own transform.")]
        [SerializeField] Transform body;
        [Tooltip("Forklifts only. Local X = how far ahead the forks reach; local Y as saved = forks down.")]
        [SerializeField] Transform forkAnchor;
        [Tooltip("Forklifts only: the forks' local Y while carrying.")]
        [SerializeField] float forkLiftHeight = 0.6f;
        [Tooltip("Where a flag is pinned when this marker is marked.")]
        [SerializeField] Transform flagMount;
        [Tooltip("Parts that light up when a tick flashes this marker.")]
        [SerializeField] Renderer[] flashRenderers;
        [SerializeField] CanvasGroup labelGroup;
        [SerializeField] Text loadText;
        [SerializeField] Text nameText;
        [SerializeField] Text flashText;

        MaterialPropertyBlock block;
        Renderer[] parts;
        Color[] partColors;
        bool[] partFlashes;
        Transform yardRoot;
        Transform label;
        Vector3 labelHome;
        FlagView flagTemplate;
        FlagView flag;
        bool captured;
        float flashK;
        float dimK;
        float fade = 1f;
        Color flashColor = DefaultFlashColor;

        public string Id { get; private set; }
        public string Lane { get; set; }
        public Transform Body => body;
        public bool HasForks => forkAnchor != null;
        public float ForkReach => forkAnchor != null ? forkAnchor.localPosition.x : 0f;
        /// <summary>The forks' height as saved in the prefab: forks down.</summary>
        public float ForkLow { get; private set; }
        public float ForkHigh => forkLiftHeight;
        public float ForkHeight => forkAnchor != null ? forkAnchor.localPosition.y : 0f;
        public MarkerView CarriedBy { get; private set; }
        public string LoadText => loadText.text;

        /// <summary>Where the marker rests between cues. Cues change it only when they complete.</summary>
        public Vector3 Rest { get; private set; }

        /// <summary>Where it stood in the scene before any playback (meaningful for placeholders).</summary>
        public Vector3 AuthoredPosition { get; private set; }

        void Awake() => Capture();

        // Records the saved state once, before anything moves.
        void Capture()
        {
            if (captured) return;
            captured = true;
            yardRoot = transform.parent;
            AuthoredPosition = transform.position;
            ForkLow = ForkHeight;
            label = labelGroup != null ? labelGroup.transform : null;
            labelHome = label != null ? label.localPosition : Vector3.zero;
            LabelOnTop.Apply(label);
            block = new MaterialPropertyBlock();

            parts = body.GetComponentsInChildren<Renderer>(true);
            partColors = new Color[parts.Length];
            partFlashes = new bool[parts.Length];
            var flashing = new HashSet<Renderer>(flashRenderers ?? new Renderer[0]);
            for (int i = 0; i < parts.Length; i++)
            {
                var material = parts[i].sharedMaterial;
                partColors[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
                partFlashes[i] = flashing.Contains(parts[i]);
            }
        }

        /// <summary>The script takes this marker over: shown, named, in its lane at <paramref name="rest"/>, with its saved look restored.</summary>
        public void Bind(string id, string lane, Vector3 rest, FlagView flagPrefab)
        {
            Capture();
            Id = id;
            Lane = lane;
            flagTemplate = flagPrefab;
            name = id;
            gameObject.SetActive(true);
            ReleaseCargo();
            CarriedBy = null;
            transform.SetParent(yardRoot, true);
            transform.localRotation = Quaternion.identity;
            SetForkHeight(ForkLow);
            RemoveFlag();
            SetLabelShift(0f);
            dimK = 0f;
            ClearTransients();
            SetRest(rest);
            nameText.text = id;
        }

        /// <summary>Takes the marker out of the yard without destroying it, so a later Load can use it again.</summary>
        public void Hide()
        {
            Capture();
            ReleaseCargo();
            CarriedBy = null;
            transform.SetParent(yardRoot, true);
            gameObject.SetActive(false);
        }

        /// <summary>Where the marker rests from now on. With <paramref name="moveNow"/> false it stays put, for a move that eases there.</summary>
        public void SetRest(Vector3 position, bool moveNow = true)
        {
            Rest = position;
            if (moveNow) transform.position = position;
        }

        public void SnapToRest() => transform.position = Rest;

        // ---- the load number ----

        public void SetLoad(int load) => loadText.text = load.ToString();

        public void SetLoadText(string load) => loadText.text = load;

        /// <summary>
        /// Rolls the load from <paramref name="from"/> to <paramref name="to"/> as p goes 0 → 1: the old number
        /// fades out, swaps while invisible, and the new one fades in with a yellow bump. Nothing pops.
        /// </summary>
        public void SetLoadRoll(string from, string to, float p)
        {
            loadText.text = p < 0.5f ? from : to;
            float bump = Easing.Pulse(p);
            var color = Color.Lerp(Color.white, PulseColor, bump);
            color.a = Easing.InOut(Mathf.Abs(1f - 2f * p));
            loadText.color = color;
            loadText.transform.localScale = Vector3.one * (1f + 0.35f * bump);
        }

        public void ResetLoadLook()
        {
            loadText.color = Color.white;
            loadText.transform.localScale = Vector3.one;
        }

        /// <summary>A point above the load number, in label-canvas units, that follows the label wherever it goes.</summary>
        public Vector3 LabelPoint(float canvasHeight) =>
            label != null ? label.TransformPoint(new Vector3(0f, canvasHeight, 0f)) : transform.position + Vector3.up * 2.5f;

        /// <summary>0 = label in its own place, 1 = moved aside (while this marker is being carried).</summary>
        public void SetLabelShift(float k)
        {
            if (label != null) label.localPosition = labelHome + CarriedLabelShift * k;
        }

        // ---- forks and carrying ----

        public void SetForkHeight(float height)
        {
            if (forkAnchor == null) return;
            var p = forkAnchor.localPosition;
            p.y = height;
            forkAnchor.localPosition = p;
        }

        /// <summary>Puts this marker on <paramref name="carrier"/>'s forks.</summary>
        public void AttachTo(MarkerView carrier)
        {
            CarriedBy = carrier;
            transform.SetParent(carrier.forkAnchor, true);
            // On lowered forks a crate sits exactly on the ground, so pick-up and set-down never make it jump.
            transform.localPosition = new Vector3(0f, -carrier.ForkLow, 0f);
            transform.localRotation = Quaternion.identity;
        }

        /// <summary>Takes this marker off whatever carries it and rests it at <paramref name="position"/>.</summary>
        public void DetachTo(Vector3 position)
        {
            CarriedBy = null;
            transform.SetParent(yardRoot, true);
            transform.localRotation = Quaternion.identity;
            SetRest(position);
        }

        /// <summary>Drops anything on this marker's forks where it is.</summary>
        public void ReleaseCargo()
        {
            if (forkAnchor == null) return;
            foreach (var cargo in forkAnchor.GetComponentsInChildren<MarkerView>(true))
                cargo.DetachTo(cargo.transform.position);
        }

        // ---- flag ----

        public void ShowFlag(string flagName)
        {
            if (flag == null)
            {
                if (flagTemplate == null || flagMount == null) return;
                flag = Instantiate(flagTemplate, flagMount, false);
            }
            flag.SetName(flagName);
        }

        /// <summary>Grows the flag in from nothing: 0 = not there, 1 = full size.</summary>
        public void SetFlagGrow(float p)
        {
            if (flag != null) flag.SetGrow(p);
        }

        /// <summary>Raises the flag up its pole: 0 = bottom, 1 = top (a little past 1 for a bounce).</summary>
        public void SetFlagRaise(float p)
        {
            if (flag != null) flag.SetRaise(p);
        }

        // ---- colour: flash and dim ----

        /// <summary>Brightens the flashing parts toward the default flash colour: 0 = normal, 1 = full flash.</summary>
        public void SetFlash(float k) => SetFlash(k, DefaultFlashColor);

        /// <summary>Brightens the flashing parts toward <paramref name="color"/>. Per instance; shared materials are untouched.</summary>
        public void SetFlash(float k, Color color)
        {
            flashK = k;
            flashColor = color;
            ApplyColors();
        }

        /// <summary>Darkens the whole marker while another one has the focus: 0 = normal, 1 = fully dimmed.</summary>
        public void SetDim(float k)
        {
            if (Mathf.Approximately(k, dimK)) return;
            dimK = k;
            ApplyColors();
            ApplyLabelAlpha();
        }

        public void SetFlashLabel(string text)
        {
            flashText.text = text ?? "";
            flashText.enabled = text != null;
            SetFlashLabelAlpha(0f);
        }

        public void SetFlashLabelAlpha(float alpha)
        {
            var color = flashText.color;
            color.a = alpha;
            flashText.color = color;
        }

        /// <summary>Shrinks and fades the marker as it leaves: 1 = whole, 0 = gone.</summary>
        public void SetFade(float s)
        {
            fade = s;
            body.localScale = Vector3.one * s;
            ApplyLabelAlpha();
        }

        /// <summary>Removes all short-lived motion: offsets, wobble, squash, fade, flash, number roll.</summary>
        public void ClearTransients()
        {
            Capture();
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            SetFade(1f);
            SetFlash(0f);
            SetFlashLabel(null);
            ResetLoadLook();
        }

        void ApplyColors()
        {
            if (parts == null) return;
            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (part == null) continue;
                bool flashing = partFlashes[i] && flashK > 0f;
                if (!flashing && dimK <= 0f)
                {
                    part.SetPropertyBlock(null);
                    continue;
                }
                var color = partColors[i];
                if (flashing) color = Color.Lerp(color, flashColor, flashK);
                if (dimK > 0f)
                {
                    var dark = color * DarkestDim;
                    dark.a = color.a;
                    color = Color.Lerp(color, dark, dimK);
                }
                block.SetColor(BaseColorId, color);
                part.SetPropertyBlock(block);
            }
        }

        void ApplyLabelAlpha()
        {
            if (labelGroup != null) labelGroup.alpha = fade * Mathf.Lerp(1f, DimmedLabelAlpha, dimK);
        }

        void RemoveFlag()
        {
            if (flag == null) return;
            Destroy(flag.gameObject);
            flag = null;
        }
    }
}
