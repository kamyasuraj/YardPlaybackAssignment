using System.IO;
using UnityEditor;
using UnityEngine;
using Yard.Core;

namespace Yard.EditorTools
{
    /// <summary>
    /// The page's six controls inside Unity (Load, Start, Pause, Resume, Speed, Skip), calling the scene the
    /// way index.html does: <c>SendMessage</c> to the GameObject named <c>YardPlayer</c>, with the same method
    /// names and values. Shows the finished payload when playback ends. Works in Play mode.
    /// Menu: Yard > Page Controls.
    /// </summary>
    public class YardPageWindow : EditorWindow
    {
        const string ObjectName = ScriptParser.ReservedId;   // "YardPlayer"
        const string DefaultScriptPath = "Assets/_Yard/Data/shift-04.json";
        static readonly string[] SpeedChoices = { "Speed", "1×", "2×" };

        string script;
        string lastCall = "—";
        string payload;
        Vector2 scroll;

        [MenuItem("Yard/Page Controls")]
        static void Open() => GetWindow<YardPageWindow>("Yard page");

        void OnEnable()
        {
            if (string.IsNullOrEmpty(script) && File.Exists(DefaultScriptPath)) script = File.ReadAllText(DefaultScriptPath);
            PageLink.Finished += OnFinished;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        void OnDisable()
        {
            PageLink.Finished -= OnFinished;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        }

        void OnFinished(string json)
        {
            payload = json;
            Repaint();
        }

        void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            payload = null;
            lastCall = "—";
            Repaint();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("Calls the object named", ObjectName, EditorStyles.boldLabel);
            if (!Application.isPlaying)
                EditorGUILayout.HelpBox("Enter Play mode to use the controls.", MessageType.Info);

            EditorGUILayout.LabelField("Script (sent as one string)");
            scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(120f), GUILayout.MaxHeight(260f));
            script = EditorGUILayout.TextArea(script ?? "", GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();

            using (new EditorGUI.DisabledScope(!Application.isPlaying))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Load")) Send("Load", script);
                if (GUILayout.Button("Start")) Send("Play");
                if (GUILayout.Button("Pause")) Send("Pause");
                if (GUILayout.Button("Resume")) Send("Resume");
                // One Speed control, as on the page: pick 1× or 2× and that value is sent. It keeps no state
                // (it shows "Speed" again), so it never goes out of step with the in-scene 1×/2×.
                int pick = EditorGUILayout.Popup(0, SpeedChoices, GUILayout.Width(80f));
                if (pick == 1) Send("SetSpeed", 1f);
                else if (pick == 2) Send("SetSpeed", 2f);
                if (GUILayout.Button("Skip")) Send("Skip");
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Last call", lastCall);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Finished: who is left, and each load", EditorStyles.boldLabel);
            if (string.IsNullOrEmpty(payload))
            {
                EditorGUILayout.LabelField("(not finished yet)");
                return;
            }
            var finished = JsonUtility.FromJson<FinishedPayload>(payload);
            EditorGUILayout.LabelField("ticket", finished.ticket ?? "");
            foreach (var entry in finished.left ?? new MarkerEntry[0])
                EditorGUILayout.LabelField(entry.id, $"{entry.lane} lane · load {entry.load}");
            EditorGUILayout.SelectableLabel(payload, EditorStyles.wordWrappedMiniLabel, GUILayout.MinHeight(32f));
        }

        void Send(string method, object value = null)
        {
            var target = GameObject.Find(ObjectName);
            if (target == null)
            {
                lastCall = $"no object named '{ObjectName}' in the scene";
                return;
            }
            if (method == "Load") payload = null;
            if (value == null) target.SendMessage(method, SendMessageOptions.RequireReceiver);
            else target.SendMessage(method, value, SendMessageOptions.RequireReceiver);
            lastCall = value is string text
                ? $"SendMessage('{ObjectName}', '{method}', <script, {text.Length} characters>)"
                : value != null ? $"SendMessage('{ObjectName}', '{method}', {value})" : $"SendMessage('{ObjectName}', '{method}')";
        }
    }
}
