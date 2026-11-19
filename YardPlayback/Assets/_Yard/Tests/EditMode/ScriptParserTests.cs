using NUnit.Framework;
using Yard.Core;

namespace Yard.Tests
{
    public class ScriptParserTests
    {
        [Test]
        public void ParsesTheBriefScript()
        {
            var script = Shift04.Parse();
            Assert.AreEqual("shift-04", script.ticket);
            Assert.AreEqual(3, script.opening.Length);
            Assert.AreEqual(8, script.cues.Length);
            Assert.AreEqual(2, script.closing.Length);
        }

        [Test]
        public void EmptyPatchesBecomeEmptyLists()
        {
            foreach (var cue in Shift04.Parse().cues)
            {
                Assert.NotNull(cue.patch.set, cue.say);
                Assert.NotNull(cue.patch.gone, cue.say);
            }
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("not json")]
        [TestCase("{\"opening\":[{\"lane\":\"north\",\"load\":1}],\"cues\":[],\"closing\":[]}")]
        [TestCase("{\"opening\":[],\"cues\":[{\"patch\":{}}],\"closing\":[]}")]
        public void RejectsBadScripts(string json)
        {
            Assert.IsFalse(ScriptParser.TryParse(json, out var script, out var error));
            Assert.IsNull(script);
            Assert.IsNotEmpty(error);
        }

        [Test]
        public void RejectsAnUnknownLane()
        {
            string json = Shift04.Json.Replace("\"south\"", "\"east\"");
            Assert.IsFalse(ScriptParser.TryParse(json, out _, out var error));
            StringAssert.Contains("east", error);
        }

        [Test]
        public void RejectsAnOversizedScript()
        {
            Assert.IsFalse(ScriptParser.TryParse(new string(' ', ScriptParser.MaxLength) + "{}", out _, out var error));
            StringAssert.Contains("limit", error);
        }
    }
}
