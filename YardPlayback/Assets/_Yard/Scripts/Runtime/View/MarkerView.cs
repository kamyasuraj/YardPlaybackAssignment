using UnityEngine;
using UnityEngine.UI;

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
        static readonly Color FlashColor = new Color(1f, 0.98f, 0.8f);
        static readonly Color PulseColor = new Color(1f, 0.85f, 0.2f);

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
        Color[] flashBase;
        Transform yardRoot;
        FlagView flagTemplate;
        FlagView flag;
        bool captured;

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
            block = new MaterialPropertyBlock();
            int count = flashRenderers != null ? flashRenderers.Length : 0;
            flashBase = new Color[count];
            for (int i = 0; i < count; i++)
            {
                var material = flashRenderers[i] != null ? flashRenderers[i].sharedMaterial : null;
                flashBase[i] = material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId) : Color.white;
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

        public void SetRest(Vector3 position)
        {
            Rest = position;
            transform.position = position;
        }

        public void SetLoad(int load) => loadText.text = load.ToString();

        /// <summary>Draws the eye to a load that just changed: 1 = big and yellow, 0 = normal.</summary>
        public void SetLoadPulse(float k)
        {
            loadText.transform.localScale = Vector3.one * (1f + 0.7f * k);
            loadText.color = Color.Lerp(Color.white, PulseColor, k);
        }

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

        public void ShowFlag(string flagName)
        {
            if (flag == null)
            {
                if (flagTemplate == null || flagMount == null) return;
                flag = Instantiate(flagTemplate, flagMount, false);
            }
            flag.SetName(flagName);
        }

        /// <summary>Raises the flag up its pole: 0 = bottom, 1 = top.</summary>
        public void SetFlagRaise(float p)
        {
            if (flag != null) flag.SetRaise(p);
        }

        /// <summary>Brightens the flashing parts: 0 = normal, 1 = full flash. Per instance; shared materials are untouched.</summary>
        public void SetFlash(float k)
        {
            if (flashRenderers == null) return;
            for (int i = 0; i < flashRenderers.Length; i++)
            {
                var renderer = flashRenderers[i];
                if (renderer == null) continue;
                if (k <= 0f)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }
                block.SetColor(BaseColorId, Color.Lerp(flashBase[i], FlashColor, k));
                renderer.SetPropertyBlock(block);
            }
        }

        public void SetFlashLabel(string text)
        {
            flashText.text = text ?? "";
            flashText.enabled = text != null;
        }

        /// <summary>Shrinks and fades the marker as it leaves: 1 = whole, 0 = gone.</summary>
        public void SetFade(float s)
        {
            body.localScale = Vector3.one * s;
            if (labelGroup != null) labelGroup.alpha = s;
        }

        /// <summary>Removes all short-lived motion: offsets, wobble, squash, fade, flash, pulses.</summary>
        public void ClearTransients()
        {
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.identity;
            SetFade(1f);
            SetFlash(0f);
            SetFlashLabel(null);
            SetLoadPulse(0f);
        }

        void RemoveFlag()
        {
            if (flag == null) return;
            Destroy(flag.gameObject);
            flag = null;
        }
    }
}
