using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Yard.Core;

namespace Yard.Tests
{
    /// <summary>
    /// Plays every scenario script in Assets/_Yard/Tests/Scenarios through the real player core and checks the
    /// brief's rules on each: cues once and in order, loads and exits at the cues the scenario names, the end on
    /// <c>closing</c>, Skip from any cue, Pause and 2× without dropping or repeating, and <c>amount</c> never
    /// changing a load. Each scenario file is a script in the brief's format plus an "expect" block the player ignores.
    /// </summary>
    public class ScenarioTests
    {
        const string Folder = "Assets/_Yard/Tests/Scenarios";
        const float Frame = 0.05f;

        // The cue types the player has handlers for; anything else is "unknown" (warned about, patch still applied).
        static readonly HashSet<string> KnownCues = new HashSet<string> { "clock", "carry", "slip", "mark", "tick", "setdown", "shift_over" };

        public static IEnumerable<TestCaseData> Scenarios() =>
            Directory.GetFiles(Folder, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new TestCaseData(path.Replace('\\', '/')).SetName("{m}(" + Path.GetFileNameWithoutExtension(path) + ")"));

        [TestCaseSource(nameof(Scenarios))]
        public void IsAValidScriptWithExpectations(string file)
        {
            string json = File.ReadAllText(file);
            Assert.IsTrue(ScriptParser.TryParse(json, out _, out var error), error);
            var expect = ExpectOf(json);
            Assert.IsNotNull(expect, "the scenario has no expect block");
            Assert.IsFalse(string.IsNullOrEmpty(expect.about), "say what the scenario checks");
        }

        [TestCaseSource(nameof(Scenarios))]
        public void PlaysEveryCueOnceInOrder(string file)
        {
            var run = Begin(File.ReadAllText(file));
            run.Playback.Play();
            RunToEnd(run);

            var all = Enumerable.Range(0, run.Playback.Script.cues.Length).ToList();
            CollectionAssert.AreEqual(all, run.Started, "cues started");
            CollectionAssert.AreEqual(all, run.Finished, "cues finished");
            Assert.AreEqual(1, run.FinishedEvents);
        }

        [TestCaseSource(nameof(Scenarios))]
        public void LoadsAndExitsHappenAtTheExpectedCues(string file)
        {
            string json = File.ReadAllText(file);
            var expect = ExpectOf(json);
            var run = Begin(json);
            run.Playback.Play();
            RunToEnd(run);

            foreach (var at in expect.loads ?? new LoadAt[0])
            {
                var after = run.AfterCue[at.cue - 1];
                Assert.IsTrue(after.ContainsKey(at.id), $"{at.id} should be in the yard after cue {at.cue}");
                Assert.AreEqual(at.load, after[at.id], $"{at.id}'s load after cue {at.cue}");
            }
            foreach (var at in expect.gone ?? new GoneAt[0])
            {
                Assert.IsFalse(run.AfterCue[at.cue - 1].ContainsKey(at.id), $"{at.id} should have left the yard at cue {at.cue}");
                bool before = at.cue == 1
                    ? run.Playback.Script.opening.Any(entry => entry.id == at.id)
                    : run.AfterCue[at.cue - 2].ContainsKey(at.id);
                Assert.IsTrue(before, $"{at.id} should still be in the yard before cue {at.cue}");
            }
        }

        [TestCaseSource(nameof(Scenarios))]
        public void EndsOnClosing(string file)
        {
            string json = File.ReadAllText(file);
            var expect = ExpectOf(json);
            var run = Begin(json);
            run.Playback.Play();
            RunToEnd(run);

            Assert.IsTrue(run.Playback.Yard.Matches(run.Playback.Script.closing), "the end matches closing");
            bool snapped = run.Warnings.Any(w => w.Contains("differs from 'closing'"));
            Assert.AreEqual(!expect.closingFromCues, snapped,
                expect.closingFromCues ? "the cues alone should reach closing" : "the end should be held to closing");
            Assert.AreEqual(expect.warnings, run.Warnings.Count, "warnings: " + string.Join(" | ", run.Warnings));
            StringAssert.Contains(run.Playback.Script.ticket, run.Playback.Payload);
        }

        [TestCaseSource(nameof(Scenarios))]
        public void SkipFromAnyCueLandsOnClosingAndStaysThere(string file)
        {
            string json = File.ReadAllText(file);
            int cues = Begin(json).Playback.Script.cues.Length;
            for (int k = -1; k < cues; k++)   // -1 = Skip before Play
            {
                var run = Begin(json);
                if (k >= 0)
                {
                    run.Playback.Play();
                    TickUntil(run, () => run.Started.Count > k || run.Playback.State == PlaybackState.Finished);
                    run.Playback.Tick(Frame);
                }
                string at = k < 0 ? "before Play" : $"during cue {k + 1}";

                run.Playback.Skip();
                Assert.AreEqual(PlaybackState.Finished, run.Playback.State, "Skip " + at);
                Assert.IsTrue(run.Playback.Yard.Matches(run.Playback.Script.closing), "Skip " + at);

                string payload = run.Playback.Payload;
                run.Playback.Skip();
                Assert.AreEqual(payload, run.Playback.Payload, "second Skip " + at);
                Assert.AreEqual(1, run.FinishedEvents, "finished once, Skip " + at);

                int started = run.Started.Count;
                for (int i = 0; i < 100; i++) run.Playback.Tick(Frame);
                Assert.AreEqual(started, run.Started.Count, "nothing plays after Skip " + at);
            }
        }

        [TestCaseSource(nameof(Scenarios))]
        public void PauseAndDoubleSpeedNeitherDropNorRepeatACue(string file)
        {
            var run = Begin(File.ReadAllText(file));
            int cues = run.Playback.Script.cues.Length;
            run.Playback.Play();
            if (cues > 0)
            {
                TickUntil(run, () => run.Started.Count > cues / 2 || run.Playback.State == PlaybackState.Finished);
                run.Playback.Tick(Frame);
                if (run.Playback.State == PlaybackState.Playing)
                {
                    run.Playback.Pause();
                    int started = run.Started.Count;
                    int finished = run.Finished.Count;
                    int current = run.Playback.CurrentCue;
                    for (int i = 0; i < 200; i++) run.Playback.Tick(Frame);
                    Assert.AreEqual(started, run.Started.Count, "no cue starts while paused");
                    Assert.AreEqual(finished, run.Finished.Count, "no cue finishes while paused");
                    Assert.AreEqual(current, run.Playback.CurrentCue, "the same cue is on screen after the pause");
                    run.Playback.Resume();
                }
                run.Playback.SetSpeed(2f);
            }
            RunToEnd(run);

            var all = Enumerable.Range(0, cues).ToList();
            CollectionAssert.AreEqual(all, run.Started, "cues started");
            CollectionAssert.AreEqual(all, run.Finished, "cues finished");
            Assert.IsTrue(run.Playback.Yard.Matches(run.Playback.Script.closing));
        }

        [TestCaseSource(nameof(Scenarios))]
        public void AmountNeverChangesALoad(string file)
        {
            string json = File.ReadAllText(file);
            Assert.IsTrue(ScriptParser.TryParse(json, out var script, out var error), error);
            foreach (var cue in script.cues) cue.amount += 1000;
            string bumpedJson = JsonUtility.ToJson(script);

            var plain = Begin(json);
            plain.Playback.Play();
            RunToEnd(plain);
            var bumped = Begin(bumpedJson);
            bumped.Playback.Play();
            RunToEnd(bumped);

            Assert.AreEqual(plain.AfterCue.Count, bumped.AfterCue.Count);
            for (int i = 0; i < plain.AfterCue.Count; i++)
                CollectionAssert.AreEquivalent(plain.AfterCue[i], bumped.AfterCue[i], $"loads after cue {i + 1}");
        }

        [Test]
        public void ALongShiftPlaysEveryCueInOrder()
        {
            var cues = new List<Cue>();
            int fork1 = 0, fork2 = 0;
            for (int i = 0; i < 60; i++)
            {
                switch (i % 4)
                {
                    case 0: cues.Add(new Cue { say = "clock", minute = i / 4 + 1, patch = new Patch() }); break;
                    case 1: fork1 = i; cues.Add(Tick("fork-1", i)); break;
                    case 2: fork2 = i * 2; cues.Add(Tick("fork-2", i * 2)); break;
                    default: cues.Add(new Cue { say = "slip", who = i % 8 == 3 ? "fork-1" : "fork-2", patch = new Patch() }); break;
                }
            }
            cues.Add(new Cue { say = "shift_over", patch = new Patch() });
            var script = new YardScript
            {
                ticket = "long-shift",
                opening = new[] { new MarkerEntry { id = "fork-1", lane = "north", load = 0 }, new MarkerEntry { id = "fork-2", lane = "south", load = 0 } },
                cues = cues.ToArray(),
                closing = new[] { new MarkerEntry { id = "fork-1", lane = "north", load = fork1 }, new MarkerEntry { id = "fork-2", lane = "south", load = fork2 } },
            };

            var run = Begin(JsonUtility.ToJson(script));
            run.Playback.SetSpeed(2f);
            run.Playback.Play();
            RunToEnd(run);

            CollectionAssert.AreEqual(Enumerable.Range(0, cues.Count).ToList(), run.Started);
            Assert.IsTrue(run.Playback.Yard.Matches(script.closing));
            Assert.AreEqual(0, run.Warnings.Count, string.Join(" | ", run.Warnings));
        }

        [TestCase("{\"opening\":[{\"id\":\"a\",\"lane\":\"North\",\"load\":1}],\"cues\":[],\"closing\":[]}", TestName = "Rejects(lane in capitals)")]
        [TestCase("{\"opening\":[],\"cues\":[{\"say\":\"\",\"patch\":{}}],\"closing\":[]}", TestName = "Rejects(empty say)")]
        [TestCase("{\"opening\":[],\"cues\":[],\"closing\":[{\"lane\":\"south\",\"load\":1}]}", TestName = "Rejects(closing entry without id)")]
        [TestCase("{ \"opening\": [ ", TestName = "Rejects(cut-off JSON)")]
        [TestCase("[]", TestName = "Rejects(array instead of object)")]
        public void RejectsAMalformedScriptAndKeepsTheCurrentOne(string json)
        {
            var run = Begin(File.ReadAllText(Folder + "/01_brief_shift04.json"));
            Assert.IsFalse(run.Playback.Load(json, out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual("shift-04", run.Playback.Script.ticket, "the current script stays");
            Assert.AreEqual(PlaybackState.Ready, run.Playback.State);
        }

        // ---------- helpers ----------

        static Cue Tick(string who, int load) => new Cue
        {
            say = "tick", who = who, flag = "count", amount = 1,
            patch = new Patch { set = new[] { new LoadSet { id = who, load = load } } },
        };

        sealed class Run
        {
            public ScriptPlayback Playback;
            public readonly List<int> Started = new List<int>();
            public readonly List<int> Finished = new List<int>();
            public readonly List<string> Warnings = new List<string>();
            public readonly List<Dictionary<string, int>> AfterCue = new List<Dictionary<string, int>>();
            public int FinishedEvents;
        }

        static Run Begin(string json)
        {
            var clock = new PlaybackClock();
            var run = new Run();
            run.Playback = new ScriptPlayback(new FakeAnimator(clock), clock);
            run.Playback.Warning += message => run.Warnings.Add(message);
            run.Playback.CueStarted += (index, cue) => run.Started.Add(index);
            run.Playback.CueFinished += (index, cue) =>
            {
                run.Finished.Add(index);
                run.AfterCue.Add(run.Playback.Yard.Markers.ToDictionary(m => m.Id, m => m.Load));
            };
            run.Playback.Finished += _ => run.FinishedEvents++;
            Assert.IsTrue(run.Playback.Load(json, out var error), error);
            return run;
        }

        static void RunToEnd(Run run) => TickUntil(run, () => run.Playback.State == PlaybackState.Finished);

        static void TickUntil(Run run, Func<bool> done)
        {
            for (int frames = 0; !done(); frames++)
            {
                Assert.Less(frames, 100000, "playback never got there");
                run.Playback.Tick(Frame);
            }
        }

        static Expect ExpectOf(string json) => JsonUtility.FromJson<Fixture>(json).expect;

        [Serializable] class Fixture { public Expect expect; }

        [Serializable]
        class Expect
        {
            public string about;
            public LoadAt[] loads;
            public GoneAt[] gone;
            public bool closingFromCues;
            public int warnings;
        }

        [Serializable] class LoadAt { public int cue; public string id; public int load; }

        [Serializable] class GoneAt { public int cue; public string id; }

        /// <summary>Each known cue takes 0.5 s of playback time; unknown cue types have no handler, like in the scene.</summary>
        sealed class FakeAnimator : ICueAnimator
        {
            readonly PlaybackClock clock;

            public FakeAnimator(PlaybackClock clock) { this.clock = clock; }

            public void Reset(YardScript script, YardState state) { }

            public bool CanPlay(string say) => KnownCues.Contains(say);

            public IEnumerator Play(Cue cue)
            {
                for (float t = 0f; t < 0.5f; t += clock.Delta) yield return null;
            }

            public IEnumerator AnimatePatch(Cue cue, PatchResult result, YardState state)
            {
                yield return null;
            }

            public void Snap(Cue cue) { }

            public void HaltMotion() { }

            public void SyncInstant(YardState state) { }

            public IEnumerator AnimateClosing(YardState state)
            {
                for (float t = 0f; t < 0.5f; t += clock.Delta) yield return null;
            }
        }
    }
}
