using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Yard.Core;

namespace Yard.EditorTools
{
    /// <summary>
    /// Builds the Yard scene's content and the marker prefabs from Unity primitives: colour materials,
    /// the Forklift, Crate and Flag prefabs, the World (ground, lanes, signs, marker placeholders) and
    /// the Screen (HUD and controls) under YardPlayer, with every reference wired.
    /// Creates only what is missing: assets and scene objects that already exist are kept, so hand
    /// edits survive a re-run. Menu: Yard > Build Scene and Prefabs.
    /// </summary>
    public static class YardSceneBuilder
    {
        const string Root = "Assets/_Yard";
        const string MaterialsFolder = Root + "/Materials";
        const string PrefabsFolder = Root + "/Prefabs";
        const string ScenePath = "Assets/Scenes/Yard.unity";
        const string ScriptPath = Root + "/Data/shift-04.json";

        const float LaneLength = 17f;
        const float LaneWidth = 3.2f;
        const float NorthZ = 2.2f;
        const float SouthZ = -2.2f;
        const float FirstSlotX = -5f;
        const float SlotSpacing = 3.5f;
        const float LabelScale = 0.006f;   // world units per label-canvas unit

        static readonly Dictionary<string, Color> Colors = new Dictionary<string, Color>
        {
            ["Forklift"] = new Color(1f, 0.76f, 0.12f),
            ["Mast"] = new Color(0.28f, 0.29f, 0.32f),
            ["Tine"] = new Color(0.6f, 0.61f, 0.65f),
            ["Crate"] = new Color(0.66f, 0.45f, 0.24f),
            ["CrateBand"] = new Color(0.42f, 0.27f, 0.13f),
            ["Flag"] = new Color(0.92f, 0.18f, 0.16f),
            ["Pole"] = new Color(0.9f, 0.9f, 0.9f),
            ["Ground"] = new Color(0.16f, 0.17f, 0.19f),
            ["LaneNorth"] = new Color(0.2f, 0.33f, 0.58f),
            ["LaneSouth"] = new Color(0.18f, 0.45f, 0.36f),
            ["Divider"] = new Color(0.85f, 0.85f, 0.8f),
        };

        [MenuItem("Yard/Build Scene and Prefabs")]
        static void BuildFromMenu() => Debug.Log("[Yard] build: " + Build());

        public static string Build()
        {
            var scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                if (scene.isDirty) return "not built: the open scene has unsaved changes. Save it and run again.";
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            }

            var player = FindRoot(scene, "YardPlayer");
            if (player == null) return "not built: Yard.unity has no YardPlayer object.";
            var yardPlayer = player.GetComponent<YardPlayer>();
            if (yardPlayer == null) return "not built: YardPlayer has no YardPlayer component.";

            var cam = FindCamera(scene);
            var facing = cam != null ? cam.transform.rotation : Quaternion.Euler(45f, 0f, 0f);
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var script = LoadScript();
            var made = new List<string>();

            var materials = Materials(made);
            var flag = FlagPrefab(materials, font, facing, made);
            var forklift = ForkliftPrefab(materials, font, facing, made);
            var crate = CratePrefab(materials, font, facing, made);

            var world = player.transform.Find("World");
            if (world == null) world = BuildWorld(player.transform, materials, font, facing, forklift, crate, script, made);
            var screen = player.transform.Find("Screen");
            if (screen == null) screen = BuildScreen(player.transform, yardPlayer, font, script, made);

            WireView(player, world, forklift, crate, flag);
            WireHud(player, screen);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            return made.Count == 0 ? "nothing missing; references re-wired" : "made " + string.Join(", ", made);
        }

        // ---------- materials ----------

        static Dictionary<string, Material> Materials(List<string> made)
        {
            EnsureFolder(MaterialsFolder);
            var probe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var template = probe.GetComponent<Renderer>().sharedMaterial;   // the pipeline's default Lit material
            var result = new Dictionary<string, Material>();
            foreach (var entry in Colors)
            {
                string path = $"{MaterialsFolder}/{entry.Key}.mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(template) { name = entry.Key };
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", entry.Value);
                    if (material.HasProperty("_Color")) material.SetColor("_Color", entry.Value);
                    AssetDatabase.CreateAsset(material, path);
                    made.Add("material " + entry.Key);
                }
                result[entry.Key] = material;
            }
            Object.DestroyImmediate(probe);
            return result;
        }

        // ---------- prefabs ----------

        static FlagView FlagPrefab(Dictionary<string, Material> m, Font font, Quaternion facing, List<string> made)
        {
            string path = PrefabsFolder + "/Flag.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<FlagView>(path);
            if (existing != null) return existing;

            var root = new GameObject("Flag");
            Part(PrimitiveType.Cylinder, root.transform, "Pole", new Vector3(0f, 0.55f, 0f), new Vector3(0.05f, 0.55f, 0.05f), m["Pole"]);
            var cloth = Part(PrimitiveType.Cube, root.transform, "Cloth", new Vector3(-0.3f, 0.92f, 0f), new Vector3(0.55f, 0.32f, 0.04f), m["Flag"]);
            var label = LabelCanvas(root.transform, "Label", new Vector3(-0.3f, 1.24f, 0f), facing, false);
            var text = LabelText(label, "Name", "flag", 52, new Color(1f, 0.55f, 0.5f), Vector2.zero, FontStyle.Bold, font);

            var view = root.AddComponent<FlagView>();
            Set(view, "cloth", cloth.transform);
            Set(view, "label", label);
            Set(view, "labelText", text);
            return SavePrefab<FlagView>(root, path, made);
        }

        static MarkerView ForkliftPrefab(Dictionary<string, Material> m, Font font, Quaternion facing, List<string> made)
        {
            string path = PrefabsFolder + "/Forklift.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<MarkerView>(path);
            if (existing != null) return existing;

            var root = new GameObject("Forklift");
            var body = Empty(root.transform, "Body", Vector3.zero);
            var chassis = Part(PrimitiveType.Cube, body, "Chassis", new Vector3(0f, 0.35f, 0f), new Vector3(1.5f, 0.55f, 1.05f), m["Forklift"]);
            var cab = Part(PrimitiveType.Cube, body, "Cab", new Vector3(-0.3f, 0.95f, 0f), new Vector3(0.7f, 0.65f, 0.9f), m["Forklift"]);
            Part(PrimitiveType.Cube, body, "Mast", new Vector3(0.8f, 0.78f, 0f), new Vector3(0.12f, 1.45f, 0.85f), m["Mast"]);
            var forks = Empty(body, "Forks", new Vector3(1.25f, -0.03f, 0f));   // local Y = forks down
            Part(PrimitiveType.Cube, forks, "Tine L", new Vector3(-0.1f, 0.03f, 0.28f), new Vector3(0.95f, 0.05f, 0.14f), m["Tine"]);
            Part(PrimitiveType.Cube, forks, "Tine R", new Vector3(-0.1f, 0.03f, -0.28f), new Vector3(0.95f, 0.05f, 0.14f), m["Tine"]);
            var mount = Empty(body, "FlagMount", new Vector3(-0.5f, 1.27f, 0f));

            var view = MarkerParts(root, body, mount, font, facing, 1.85f, "fork");
            Set(view, "forkAnchor", forks);
            SetArray(view, "flashRenderers", chassis.GetComponent<Renderer>(), cab.GetComponent<Renderer>());
            return SavePrefab<MarkerView>(root, path, made);
        }

        static MarkerView CratePrefab(Dictionary<string, Material> m, Font font, Quaternion facing, List<string> made)
        {
            string path = PrefabsFolder + "/Crate.prefab";
            var existing = AssetDatabase.LoadAssetAtPath<MarkerView>(path);
            if (existing != null) return existing;

            var root = new GameObject("Crate");
            var body = Empty(root.transform, "Body", Vector3.zero);
            var box = Part(PrimitiveType.Cube, body, "Box", new Vector3(0f, 0.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), m["Crate"]);
            Part(PrimitiveType.Cube, body, "Band", new Vector3(0f, 0.4f, 0f), new Vector3(0.82f, 0.12f, 0.82f), m["CrateBand"]);
            var mount = Empty(body, "FlagMount", new Vector3(0f, 0.8f, 0f));

            var view = MarkerParts(root, body, mount, font, facing, 1.2f, "crate");
            SetArray(view, "flashRenderers", box.GetComponent<Renderer>());
            return SavePrefab<MarkerView>(root, path, made);
        }

        // The parts every marker has: body, flag mount, and a label (load number, name, tick flash).
        static MarkerView MarkerParts(GameObject root, Transform body, Transform mount, Font font, Quaternion facing, float labelHeight, string name)
        {
            var label = LabelCanvas(root.transform, "Label", new Vector3(0f, labelHeight, 0f), facing, true);
            var load = LabelText(label, "Load", "0", 100, Color.white, Vector2.zero, FontStyle.Bold, font);
            var nameText = LabelText(label, "Name", name, 44, new Color(1f, 1f, 1f, 0.85f), new Vector2(0f, -75f), FontStyle.Normal, font);
            var flash = LabelText(label, "Flash", "", 80, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 185f), FontStyle.Bold, font);
            flash.enabled = false;

            var view = root.AddComponent<MarkerView>();
            Set(view, "body", body);
            Set(view, "flagMount", mount);
            Set(view, "labelGroup", label.GetComponent<CanvasGroup>());
            Set(view, "loadText", load);
            Set(view, "nameText", nameText);
            Set(view, "flashText", flash);
            return view;
        }

        static T SavePrefab<T>(GameObject root, string path, List<string> made) where T : Component
        {
            EnsureFolder(PrefabsFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            made.Add("prefab " + Path.GetFileNameWithoutExtension(path));
            return prefab.GetComponent<T>();
        }

        // ---------- scene: World ----------

        static Transform BuildWorld(Transform player, Dictionary<string, Material> m, Font font, Quaternion facing,
            MarkerView forklift, MarkerView crate, YardScript script, List<string> made)
        {
            var world = Empty(player, "World", Vector3.zero);
            Part(PrimitiveType.Cube, world, "Ground", new Vector3(0f, -0.05f, 0f), new Vector3(LaneLength + 4f, 0.1f, 2f * LaneWidth + 4f), m["Ground"]);
            Part(PrimitiveType.Cube, world, "Lane north", new Vector3(0f, YardView.GroundY * 0.5f, NorthZ), new Vector3(LaneLength, YardView.GroundY, LaneWidth), m["LaneNorth"]);
            Part(PrimitiveType.Cube, world, "Lane south", new Vector3(0f, YardView.GroundY * 0.5f, SouthZ), new Vector3(LaneLength, YardView.GroundY, LaneWidth), m["LaneSouth"]);
            Part(PrimitiveType.Cube, world, "Divider", new Vector3(0f, 0.005f, 0f), new Vector3(LaneLength, 0.01f, 0.08f), m["Divider"]);
            Sign(world, "NORTH", NorthZ, Colors["LaneNorth"], font, facing);
            Sign(world, "SOUTH", SouthZ, Colors["LaneSouth"], font, facing);

            var markers = Empty(world, "Markers", Vector3.zero);
            if (script != null) Placeholders(markers, forklift, crate, script);
            made.Add("World (ground, lanes, signs, marker placeholders)");
            return world;
        }

        static void Sign(Transform world, string text, float z, Color laneColor, Font font, Quaternion facing)
        {
            var canvas = LabelCanvas(world, text + " sign", new Vector3(-LaneLength * 0.5f + 1.1f, 0.3f, z), facing, false);
            LabelText(canvas, "Text", text, 90, Color.Lerp(laneColor, Color.white, 0.55f), Vector2.zero, FontStyle.Bold, font);
        }

        // One placeholder per opening entry of the brief's script, in the same slots playback uses.
        // Preview only: on Load the file decides who is shown, where, and with what load.
        static void Placeholders(Transform markers, MarkerView forklift, MarkerView crate, YardScript script)
        {
            var cargo = new HashSet<string>();
            foreach (var cue in script.cues)
                if (!string.IsNullOrEmpty(cue.what)) cargo.Add(cue.what);

            var slots = new Dictionary<string, int>();
            foreach (var entry in script.opening)
            {
                slots.TryGetValue(entry.lane, out int slot);
                slots[entry.lane] = slot + 1;
                var prefab = cargo.Contains(entry.id) ? crate : forklift;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, markers);
                go.name = entry.id;
                go.transform.position = new Vector3(FirstSlotX + slot * SlotSpacing, YardView.GroundY, entry.lane == Lanes.South ? SouthZ : NorthZ);
                var load = go.transform.Find("Label/Load").GetComponent<Text>();
                var name = go.transform.Find("Label/Name").GetComponent<Text>();
                load.text = entry.load.ToString();
                name.text = entry.id;
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(load);
                PrefabUtility.RecordPrefabInstancePropertyModifications(name);
            }
        }

        // ---------- scene: Screen ----------

        static Transform BuildScreen(Transform player, YardPlayer yardPlayer, Font font, YardScript script, List<string> made)
        {
            var go = new GameObject("Screen", typeof(RectTransform));
            go.transform.SetParent(player, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            // The freeze tint sits first, under the HUD.
            var freeze = Panel(go.transform, "Freeze", Color.clear);
            Stretch(freeze.rectTransform);
            freeze.raycastTarget = false;

            var hud = new GameObject("Hud", typeof(RectTransform)).GetComponent<RectTransform>();
            hud.SetParent(go.transform, false);
            Stretch(hud);

            var minuteBox = Panel(hud, "Minute", YardHud.PanelColor);
            Place(minuteBox.rectTransform, new Vector2(0f, 1f), new Vector2(32f, -32f), new Vector2(210f, 140f));
            var caption = HudText(minuteBox.transform, "Caption", "MINUTE", 22, YardHud.MutedColor, TextAnchor.UpperCenter, font);
            Place(caption.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(200f, 30f));
            var minute = HudText(minuteBox.transform, "Value", "–", 80, Color.white, TextAnchor.MiddleCenter, font);
            Place(minute.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 10f), new Vector2(200f, 96f));

            string ticketText = script != null ? "TICKET  " + script.ticket : "TICKET";
            string cueText = script != null ? $"cue 0/{script.cues.Length}  ·  ready" : "cue 0/0";
            var ticket = HudText(hud, "Ticket", ticketText, 24, YardHud.MutedColor, TextAnchor.MiddleCenter, font);
            Place(ticket.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(900f, 34f));
            var cue = HudText(hud, "Cue", cueText, 30, Color.white, TextAnchor.MiddleCenter, font);
            Place(cue.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -68f), new Vector2(1100f, 42f));
            var status = HudText(hud, "Status", "READY  ·  1×", 28, Color.white, TextAnchor.MiddleRight, font);
            Place(status.rectTransform, new Vector2(1f, 1f), new Vector2(-36f, -36f), new Vector2(420f, 40f));

            var shiftOver = HudText(hud, "Shift over", "SHIFT OVER", 72, Color.clear, TextAnchor.MiddleCenter, font);
            Place(shiftOver.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 330f), new Vector2(900f, 90f));
            var error = HudText(hud, "Error", "", 24, YardHud.ErrorColor, TextAnchor.MiddleCenter, font);
            Place(error.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(1400f, 34f));

            var bar = Panel(hud, "Controls", YardHud.PanelColor);
            Place(bar.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(866f, 90f));
            const float step = 166f;
            var play = HudButton(bar.transform, "Play", -2f * step, font);
            var pause = HudButton(bar.transform, "Pause", -step, font);
            var speed1 = HudButton(bar.transform, "1×", 0f, font);
            var speed2 = HudButton(bar.transform, "2×", step, font);
            var skip = HudButton(bar.transform, "Skip", 2f * step, font);

            // Wired in each button's On Click, so the Inspector shows exactly what it calls.
            UnityEventTools.AddPersistentListener(play.onClick, yardPlayer.Play);
            UnityEventTools.AddPersistentListener(pause.onClick, yardPlayer.Pause);
            UnityEventTools.AddFloatPersistentListener(speed1.onClick, yardPlayer.SetSpeed, 1f);
            UnityEventTools.AddFloatPersistentListener(speed2.onClick, yardPlayer.SetSpeed, 2f);
            UnityEventTools.AddPersistentListener(skip.onClick, yardPlayer.Skip);

            // Look like the Ready state until Play mode takes over.
            YardHud.Style(play, true, false);
            YardHud.Style(pause, false, false);
            YardHud.Style(speed1, true, true);
            YardHud.Style(speed2, true, false);
            YardHud.Style(skip, true, false);

            made.Add("Screen (HUD and the five controls)");
            return go.transform;
        }

        // ---------- wiring ----------

        static void WireView(GameObject player, Transform world, MarkerView forklift, MarkerView crate, FlagView flag)
        {
            var view = GetOrAdd<YardView>(player);
            Set(view, "markersRoot", world.Find("Markers"));
            Set(view, "northLane", world.Find("Lane north"));
            Set(view, "southLane", world.Find("Lane south"));
            Set(view, "forkliftPrefab", forklift);
            Set(view, "cratePrefab", crate);
            Set(view, "flagPrefab", flag);
        }

        static void WireHud(GameObject player, Transform screen)
        {
            var hud = GetOrAdd<YardHud>(player);
            Set(hud, "minute", Find<Text>(screen, "Hud/Minute/Value"));
            Set(hud, "ticket", Find<Text>(screen, "Hud/Ticket"));
            Set(hud, "cueLine", Find<Text>(screen, "Hud/Cue"));
            Set(hud, "status", Find<Text>(screen, "Hud/Status"));
            Set(hud, "error", Find<Text>(screen, "Hud/Error"));
            Set(hud, "freezeTitle", Find<Text>(screen, "Hud/Shift over"));
            Set(hud, "freeze", Find<Image>(screen, "Freeze"));
            Set(hud, "play", Find<Button>(screen, "Hud/Controls/Play"));
            Set(hud, "pause", Find<Button>(screen, "Hud/Controls/Pause"));
            Set(hud, "speed1", Find<Button>(screen, "Hud/Controls/1×"));
            Set(hud, "speed2", Find<Button>(screen, "Hud/Controls/2×"));
            Set(hud, "skip", Find<Button>(screen, "Hud/Controls/Skip"));
        }

        // ---------- helpers ----------

        static GameObject Part(PrimitiveType type, Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        static Transform Empty(Transform parent, string name, Vector3 position)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = position;
            return t;
        }

        // A world-space canvas facing the camera; text on it stays square to the view in Play mode.
        static RectTransform LabelCanvas(Transform parent, string name, Vector3 position, Quaternion facing, bool fades)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.localPosition = position;
            rect.rotation = facing;
            rect.localScale = Vector3.one * LabelScale;
            rect.sizeDelta = new Vector2(400f, 300f);
            go.AddComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            if (fades)
            {
                var group = go.AddComponent<CanvasGroup>();
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            go.AddComponent<FaceCamera>();
            return rect;
        }

        static Text LabelText(RectTransform canvas, string name, string value, int size, Color color, Vector2 position, FontStyle style, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(600f, size * 1.3f);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(4f, -4f);
            return text;
        }

        static Text HudText(Transform parent, string name, string value, int size, Color color, TextAnchor alignment, Font font)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        static Button HudButton(Transform parent, string label, float x, Font font)
        {
            var image = Panel(parent, label, YardHud.ButtonColor);
            Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(152f, 60f));
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.disabledColor = new Color(1f, 1f, 1f, 0.3f);
            button.colors = colors;
            var text = HudText(image.transform, "Text", label, 28, Color.white, TextAnchor.MiddleCenter, font);
            Stretch(text.rectTransform);
            return button;
        }

        static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }

        static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetArray(Object target, string field, params Object[] values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            if (property == null) throw new System.ArgumentException($"{target.GetType().Name} has no serialized field '{field}'.");
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static T Find<T>(Transform root, string path) where T : Component
        {
            var t = root != null ? root.Find(path) : null;
            if (t == null) Debug.LogWarning($"[Yard] build: '{path}' not found under {(root != null ? root.name : "(none)")}.");
            return t != null ? t.GetComponent<T>() : null;
        }

        static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == name) return root;
            return null;
        }

        static Camera FindCamera(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var cam = root.GetComponentInChildren<Camera>();
                if (cam != null) return cam;
            }
            return null;
        }

        static YardScript LoadScript()
        {
            if (!File.Exists(ScriptPath)) return null;
            return ScriptParser.TryParse(File.ReadAllText(ScriptPath), out var script, out _) ? script : null;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
