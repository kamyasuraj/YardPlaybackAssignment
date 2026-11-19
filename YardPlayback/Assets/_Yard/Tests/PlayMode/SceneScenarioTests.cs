using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Yard.Core;
using Object = UnityEngine.Object;

namespace Yard.Tests
{
    /// <summary>
    /// Plays every scenario script in the real Yard scene (sped up with Time.timeScale), to the end and with Skip
    /// midway, and checks what a viewer sees at the end: exactly the <c>closing</c> markers, each showing its
    /// closing load, resting in its own lane and on the lane, and nothing else in the yard. Also drives the scene
    /// the way the page does, by name. Any error logged fails the test.
    /// </summary>
    public class SceneScenarioTests
    {
        const string Folder = "Assets/_Yard/Tests/Scenarios";
        const string PageObject = "YardPlayer";
        const float LaneHalfWidth = 1.6f;
        const float LaneHalfLength = 8.5f;

        YardPlayer player;

        static IEnumerable<string> Scenarios() =>
            Directory.GetFiles(Folder, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => path.Replace('\\', '/'));

        [UnitySetUp]
        public IEnumerator OpenTheYard()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Yard");
            yield return null;
            yield return null;
            player = Object.FindAnyObjectByType<YardPlayer>();
            Assert.NotNull(player, "the Yard scene has no YardPlayer");
            Assert.NotNull(player.Playback, "YardPlayer did not start (is the scene set up?)");
        }

        [TearDown]
        public void RestoreTime() => Time.timeScale = 1f;

        [UnityTest]
        public IEnumerator PlayedToTheEndTheYardShowsClosing([ValueSource(nameof(Scenarios))] string file)
        {
            player.Load(File.ReadAllText(file));
            Time.timeScale = 8f;
            player.Play();
            yield return WaitFor(() => player.Playback.State == PlaybackState.Finished, 90f);
            yield return null;
            AssertTheYardShowsClosing();
        }

        [UnityTest]
        public IEnumerator SkippedMidwayTheYardShowsClosing([ValueSource(nameof(Scenarios))] string file)
        {
            player.Load(File.ReadAllText(file));
            int half = player.Playback.Script.cues.Length / 2;
            Time.timeScale = 4f;
            player.Play();
            yield return WaitFor(() => player.Playback.CurrentCue >= half || player.Playback.State == PlaybackState.Finished, 90f);
            yield return null;

            player.Skip();
            yield return null;
            AssertTheYardShowsClosing();

            player.Skip();
            yield return null;
            AssertTheYardShowsClosing();
        }

        [Test]
        public void TheObjectThePageCallsCarriesOnlyThePageControls()
        {
            var page = GameObject.Find(PageObject);
            Assert.NotNull(page, $"no GameObject named '{PageObject}'");
            var components = page.GetComponents<Component>().Where(c => !(c is Transform)).ToList();
            Assert.AreEqual(1, components.Count, "components: " + string.Join(", ", components.Select(c => c.GetType().Name)));
            Assert.IsInstanceOf<YardPage>(components[0]);
            Assert.AreEqual(0, page.transform.childCount, "the page's object has no children");
            Assert.AreEqual(1, Object.FindObjectsByType<YardPage>(FindObjectsSortMode.None).Length);

            var methods = typeof(YardPage).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly).Select(m => m.Name);
            CollectionAssert.AreEquivalent(new[] { "Load", "Play", "Pause", "Resume", "SetSpeed", "Skip" }, methods);
        }

        [UnityTest]
        public IEnumerator ThePageDrivesThePlayerByNameAndHearsTheFinish()
        {
            var page = GameObject.Find(PageObject);
            string heard = null;
            Action<string> listen = payload => heard = payload;
            PageLink.Finished += listen;
            try
            {
                page.SendMessage("Load", File.ReadAllText(Folder + "/01_brief_shift04.json"));
                Assert.AreEqual(PlaybackState.Ready, player.Playback.State);

                page.SendMessage("Play");
                yield return WaitFor(() => player.Playback.CurrentCue >= 1, 30f);
                page.SendMessage("Pause");
                Assert.AreEqual(PlaybackState.Paused, player.Playback.State);
                int cue = player.Playback.CurrentCue;
                for (int i = 0; i < 30; i++) yield return null;
                Assert.AreEqual(cue, player.Playback.CurrentCue, "paused: the same cue");

                page.SendMessage("Resume");
                Assert.AreEqual(PlaybackState.Playing, player.Playback.State);
                page.SendMessage("SetSpeed", 2f);
                Assert.AreEqual(2f, player.Playback.Clock.Speed);
                LogAssert.Expect(LogType.Warning, "[Yard] speed 3 ignored; use 1 or 2");
                page.SendMessage("SetSpeed", 3f);
                Assert.AreEqual(2f, player.Playback.Clock.Speed, "only 1 and 2 are speeds");

                page.SendMessage("Skip");
                Assert.AreEqual(PlaybackState.Finished, player.Playback.State);
                Assert.IsNotNull(heard, "the page heard the finish");
                var finished = JsonUtility.FromJson<FinishedPayload>(heard);
                Assert.AreEqual("shift-04", finished.ticket);
                CollectionAssert.AreEquivalent(new[] { "fork-1:north:2", "fork-2:south:0" },
                    finished.left.Select(m => $"{m.id}:{m.lane}:{m.load}"));

                heard = null;
                page.SendMessage("Skip");
                Assert.IsNull(heard, "a second Skip stays on closing and does not finish again");
                yield return null;
                AssertTheYardShowsClosing();
            }
            finally
            {
                PageLink.Finished -= listen;
            }
        }

        void AssertTheYardShowsClosing()
        {
            var script = player.Playback.Script;
            Assert.AreEqual(PlaybackState.Finished, player.Playback.State);

            var markers = GameObject.Find("Yard/World/Markers").transform;
            var shown = markers.GetComponentsInChildren<MarkerView>(true).Where(m => m.gameObject.activeInHierarchy).ToList();
            CollectionAssert.AreEquivalent(script.closing.Select(entry => entry.id), shown.Select(m => m.Id), "who is in the yard");

            float northZ = GameObject.Find("Yard/World/Lane north").transform.position.z;
            float southZ = GameObject.Find("Yard/World/Lane south").transform.position.z;
            foreach (var entry in script.closing)
            {
                var view = shown.First(m => m.Id == entry.id);
                Assert.AreEqual(entry.load.ToString(), view.LoadText, $"{entry.id} shows its closing load");
                // The scenarios name crates crate-* and forklifts fork-*: each must be drawn as what it is.
                Assert.AreEqual(!entry.id.StartsWith("crate"), view.HasForks, $"{entry.id} is drawn as a {(view.HasForks ? "forklift" : "crate")}");
                if (view.CarriedBy != null) continue;   // on a forklift's forks: it rides with the forklift
                var at = view.transform.position;
                float laneZ = entry.lane == Lanes.South ? southZ : northZ;
                Assert.Less(Mathf.Abs(at.z - laneZ), LaneHalfWidth, $"{entry.id} rests in the {entry.lane} lane (z = {at.z:F2})");
                Assert.LessOrEqual(Mathf.Abs(at.x), LaneHalfLength, $"{entry.id} rests on the lane (x = {at.x:F2})");
            }
        }

        static IEnumerator WaitFor(Func<bool> done, float seconds)
        {
            float waited = 0f;
            while (!done())
            {
                waited += Time.unscaledDeltaTime;
                Assert.Less(waited, seconds, "timed out");
                yield return null;
            }
        }
    }
}
