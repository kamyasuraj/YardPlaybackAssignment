using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Yard.Core;

namespace Yard.Tests
{
    public class YardStateTests
    {
        [Test]
        public void OpeningPlacesEveryMarkerInItsLaneWithItsLoad()
        {
            var yard = YardState.FromOpening(Shift04.Parse());
            Assert.AreEqual(3, yard.Markers.Count);
            AssertMarker(yard, "fork-1", "north", 3);
            AssertMarker(yard, "fork-2", "south", 0);
            AssertMarker(yard, "crate-a", "north", 1);
        }

        [Test]
        public void LoadsComeOnlyFromPatchSet()
        {
            var script = Shift04.Parse();
            var yard = YardState.FromOpening(script);
            var fork1AfterEachCue = new List<int>();
            foreach (var cue in script.cues)
            {
                yard.ApplyPatch(cue.patch);
                fork1AfterEachCue.Add(yard.Get("fork-1").Load);
            }
            // clock, carry, slip, mark, clock, tick, setdown, shift_over
            CollectionAssert.AreEqual(new[] { 3, 6, 6, 6, 6, 5, 2, 2 }, fork1AfterEachCue);
        }

        [Test]
        public void AmountIsNeverAddedOrSubtracted()
        {
            var script = Shift04.Parse();
            var tick = script.cues[Shift04.IndexOf(script, "tick")];
            tick.amount = 99;
            var yard = YardState.FromOpening(script);
            foreach (var cue in script.cues)
            {
                yard.ApplyPatch(cue.patch);
                if (cue == tick) Assert.AreEqual(5, yard.Get("fork-1").Load);
            }
        }

        [Test]
        public void TheEndOfTheScriptMatchesClosing()
        {
            var script = Shift04.Parse();
            var yard = YardState.FromOpening(script);
            foreach (var cue in script.cues) yard.ApplyPatch(cue.patch);

            Assert.IsTrue(yard.Matches(script.closing));
            Assert.IsFalse(yard.Has("crate-a"));
            AssertMarker(yard, "fork-1", "north", 2);
            AssertMarker(yard, "fork-2", "south", 0);
        }

        [Test]
        public void EnforceClosingIsIdempotent()
        {
            var script = Shift04.Parse();
            var yard = YardState.FromOpening(script);
            yard.EnforceClosing(script.closing);
            string once = yard.ToPayloadJson(script.ticket);
            yard.EnforceClosing(script.closing);

            Assert.IsTrue(yard.Matches(script.closing));
            Assert.AreEqual(once, yard.ToPayloadJson(script.ticket));
        }

        [Test]
        public void PayloadListsWhoIsLeftAndEachLoad()
        {
            var script = Shift04.Parse();
            var yard = YardState.FromOpening(script);
            foreach (var cue in script.cues) yard.ApplyPatch(cue.patch);

            var payload = JsonUtility.FromJson<FinishedPayload>(yard.ToPayloadJson(script.ticket));
            Assert.AreEqual("shift-04", payload.ticket);
            Assert.AreEqual(2, payload.left.Length);
            Assert.AreEqual("fork-1", payload.left[0].id);
            Assert.AreEqual(2, payload.left[0].load);
            Assert.AreEqual("fork-2", payload.left[1].id);
            Assert.AreEqual(0, payload.left[1].load);
        }

        [Test]
        public void PatchingAnUnknownMarkerIsReportedAndIgnored()
        {
            var yard = YardState.FromOpening(Shift04.Parse());
            var result = yard.ApplyPatch(new Patch { set = new[] { new LoadSet { id = "ghost", load = 9 } }, gone = new[] { "nobody" } });
            CollectionAssert.AreEquivalent(new[] { "ghost", "nobody" }, result.UnknownIds);
            Assert.AreEqual(3, yard.Markers.Count);
        }

        static void AssertMarker(YardState yard, string id, string lane, int load)
        {
            var marker = yard.Get(id);
            Assert.NotNull(marker, id);
            Assert.AreEqual(lane, marker.Lane, id);
            Assert.AreEqual(load, marker.Load, id);
        }
    }
}
