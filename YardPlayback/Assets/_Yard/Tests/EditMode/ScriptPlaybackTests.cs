using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Yard.Core;

namespace Yard.Tests
{
    /// <summary>
    /// Plays the brief's script against a fake animator, one simulated frame at a time, to check the
    /// player obeys it: order, pause, speed and skip.
    /// </summary>
    public class ScriptPlaybackTests
    {
        const float Frame = 0.05f;

        FakeAnimator animator;
        ScriptPlayback playback;
        string[] says;
        int finishedCount;

        [SetUp]
        public void SetUp()
        {
            var clock = new PlaybackClock();
            animator = new FakeAnimator { Clock = clock };
            playback = new ScriptPlayback(animator, clock);
            Assert.IsTrue(playback.Load(Shift04.Json, out var error), error);
            says = playback.Script.cues.Select(c => c.say).ToArray();
            finishedCount = 0;
            playback.Finished += _ => finishedCount++;
        }

        [Test]
        public void PlaysEveryCueOnceInOrderAndEndsOnClosing()
        {
            playback.Play();
            RunUntil(() => playback.State == PlaybackState.Finished);

            CollectionAssert.AreEqual(says, Started);
            CollectionAssert.AreEqual(says, Ended);
            Assert.IsTrue(playback.Yard.Matches(playback.Script.closing));
            Assert.AreEqual(1, finishedCount);
            StringAssert.Contains("\"left\"", playback.Payload);
        }

        [Test]
        public void EachCueFinishesBeforeTheNextStarts()
        {
            playback.Play();
            RunUntil(() => playback.State == PlaybackState.Finished);

            var sequence = animator.Log.Where(l => l.StartsWith("start ") || l.StartsWith("end ")).ToList();
            Assert.AreEqual(says.Length * 2, sequence.Count);
            for (int i = 0; i < says.Length; i++)
            {
                Assert.AreEqual("start " + says[i], sequence[2 * i]);
                Assert.AreEqual("end " + says[i], sequence[2 * i + 1]);
            }
        }

        [Test]
        public void LoadsAreTheValuesThePatchSets()
        {
            var fork1 = new Dictionary<int, int>();
            playback.CueFinished += (index, cue) => fork1[index] = playback.Yard.Get("fork-1").Load;
            playback.Play();
            RunUntil(() => playback.State == PlaybackState.Finished);

            Assert.AreEqual(6, fork1[Shift04.IndexOf(playback.Script, "carry")]);
            Assert.AreEqual(5, fork1[Shift04.IndexOf(playback.Script, "tick")]);
            Assert.AreEqual(2, fork1[Shift04.IndexOf(playback.Script, "setdown")]);
        }

        [Test]
        public void PauseFreezesTheCueAndResumeContinuesTheSameCue()
        {
            playback.Play();
            RunUntil(() => animator.Log.Contains("start carry"));
            playback.Tick(Frame);
            playback.Tick(Frame);   // 0.1 s into a 1 s cue
            playback.Pause();

            int cue = playback.CurrentCue;
            int logged = animator.Log.Count;
            for (int i = 0; i < 500; i++) playback.Tick(Frame);
            Assert.AreEqual(PlaybackState.Paused, playback.State);
            Assert.AreEqual(cue, playback.CurrentCue);
            Assert.AreEqual(logged, animator.Log.Count);

            playback.Resume();
            int framesToFinishCarry = RunUntil(() => animator.Log.Contains("end carry"));
            Assert.That(framesToFinishCarry, Is.InRange(16, 20), "carry should resume 0.1 s in, not restart or jump ahead");

            RunUntil(() => playback.State == PlaybackState.Finished);
            CollectionAssert.AreEqual(says, Started);
            CollectionAssert.AreEqual(says, Ended);
        }

        [Test]
        public void DoubleSpeedMidCueNeitherSkipsNorRepeatsACue()
        {
            playback.Play();
            RunUntil(() => animator.Log.Contains("start slip"));
            playback.SetSpeed(2f);
            RunUntil(() => playback.State == PlaybackState.Finished);

            CollectionAssert.AreEqual(says, Started);
            CollectionAssert.AreEqual(says, Ended);
            Assert.IsTrue(playback.Yard.Matches(playback.Script.closing));
        }

        [Test]
        public void DoubleSpeedPlaysFaster()
        {
            playback.Play();
            int normal = RunUntil(() => playback.State == PlaybackState.Finished);

            SetUp();
            playback.SetSpeed(2f);
            playback.Play();
            int fast = RunUntil(() => playback.State == PlaybackState.Finished);

            Assert.Less(fast, normal * 0.6f);
        }

        [Test]
        public void SkipMidCueSnapsToClosingWithoutRedoingFinishedCues()
        {
            playback.Play();
            RunUntil(() => animator.Log.Contains("start mark"));
            playback.Tick(Frame);
            playback.Skip();

            Assert.AreEqual(PlaybackState.Finished, playback.State);
            Assert.IsTrue(playback.Yard.Matches(playback.Script.closing));
            Assert.IsFalse(playback.Yard.Has("crate-a"));
            Assert.AreEqual(2, playback.Yard.Get("fork-1").Load);
            CollectionAssert.AreEqual(
                new[] { "halt", "snap mark", "snap clock", "snap tick", "snap setdown", "snap shift_over", "sync" },
                animator.Log.SkipWhile(l => l != "halt").ToArray());
            Assert.AreEqual(1, finishedCount);
        }

        [Test]
        public void SkipTwiceStaysOnClosing()
        {
            playback.Play();
            RunUntil(() => animator.Log.Contains("start carry"));
            playback.Skip();
            string payload = playback.Payload;
            int logged = animator.Log.Count;

            playback.Skip();

            Assert.AreEqual(PlaybackState.Finished, playback.State);
            Assert.IsTrue(playback.Yard.Matches(playback.Script.closing));
            Assert.AreEqual(payload, playback.Payload);
            Assert.AreEqual(1, finishedCount);
            CollectionAssert.AreEqual(new[] { "halt", "sync" }, animator.Log.Skip(logged).ToArray());
        }

        [Test]
        public void SkipBeforePlayGoesStraightToClosing()
        {
            playback.Skip();
            Assert.AreEqual(PlaybackState.Finished, playback.State);
            Assert.IsTrue(playback.Yard.Matches(playback.Script.closing));
        }

        [Test]
        public void NothingMovesAfterSkip()
        {
            playback.Play();
            RunUntil(() => animator.Log.Contains("start setdown"));
            playback.Skip();
            int logged = animator.Log.Count;
            for (int i = 0; i < 500; i++) playback.Tick(Frame);
            Assert.AreEqual(logged, animator.Log.Count);
        }

        [Test]
        public void ABadLoadKeepsTheCurrentScript()
        {
            Assert.IsFalse(playback.Load("{ oops", out var error));
            Assert.IsNotEmpty(error);
            Assert.AreEqual("shift-04", playback.Script.ticket);
            Assert.AreEqual(PlaybackState.Ready, playback.State);
        }

        string[] Started => animator.Log.Where(l => l.StartsWith("start ")).Select(l => l.Substring(6)).ToArray();
        string[] Ended => animator.Log.Where(l => l.StartsWith("end ")).Select(l => l.Substring(4)).ToArray();

        int RunUntil(Func<bool> done, int maxFrames = 20000)
        {
            int frames = 0;
            while (!done() && frames < maxFrames)
            {
                playback.Tick(Frame);
                frames++;
            }
            Assert.Less(frames, maxFrames, "playback never got there");
            return frames;
        }

        /// <summary>Each cue takes 1 s of playback time and logs when it starts, ends or is snapped.</summary>
        class FakeAnimator : ICueAnimator
        {
            public PlaybackClock Clock;
            public readonly List<string> Log = new List<string>();

            public void Reset(YardScript script, YardState state) => Log.Add("reset");

            public bool CanPlay(string say) => true;

            public IEnumerator Play(Cue cue)
            {
                Log.Add("start " + cue.say);
                for (float t = 0f; t < 1f; t += Clock.Delta) yield return null;
                Log.Add("end " + cue.say);
            }

            public IEnumerator AnimatePatch(Cue cue, PatchResult result, YardState state)
            {
                yield return null;
            }

            public void Snap(Cue cue) => Log.Add("snap " + cue.say);

            public void HaltMotion() => Log.Add("halt");

            public void SyncInstant(YardState state) => Log.Add("sync");
        }
    }
}
