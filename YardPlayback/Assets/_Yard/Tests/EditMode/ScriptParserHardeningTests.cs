using System.Linq;
using NUnit.Framework;
using Yard.Core;

namespace Yard.Tests
{
    /// <summary>
    /// The parser rejects rather than guesses: no value the file doesn't hold reaches the yard, and input
    /// that could hurt the player (deep nesting, huge counts, a marker named like the player) is refused.
    /// </summary>
    public class ScriptParserHardeningTests
    {
        const string EmptyLists = "\"opening\":[],\"cues\":[],\"closing\":[]";

        static string Script(string opening, string cues = "[]", string closing = "[]") =>
            "{\"opening\":" + opening + ",\"cues\":" + cues + ",\"closing\":" + closing + "}";

        // ---- whole numbers ----

        [TestCase("\"three\"", TestName = "RejectsALoadThatIsNotANumber(\"three\")")]
        [TestCase("2.5", TestName = "RejectsALoadThatIsNotANumber(2.5)")]
        [TestCase("99999999999", TestName = "RejectsALoadThatIsNotANumber(too big for int)")]
        [TestCase("true", TestName = "RejectsALoadThatIsNotANumber(true)")]
        public void RejectsALoadThatIsNotAWholeNumber(string value)
        {
            Assert.IsFalse(ScriptParser.TryParse(Script("[{\"id\":\"a\",\"lane\":\"north\",\"load\":" + value + "}]"), out _, out var error));
            StringAssert.Contains("whole number", error);
        }

        [Test]
        public void RejectsMinuteAndAmountThatAreNotWholeNumbers()
        {
            Assert.IsFalse(ScriptParser.TryParse(Script("[]", "[{\"say\":\"clock\",\"minute\":1.5,\"patch\":{}}]"), out _, out _));
            Assert.IsFalse(ScriptParser.TryParse(Script("[]", "[{\"say\":\"tick\",\"amount\":\"2\",\"patch\":{}}]"), out _, out _));
        }

        [Test]
        public void AcceptsNegativeAndZeroWholeNumbers()
        {
            Assert.IsTrue(ScriptParser.TryParse(Script("[{\"id\":\"a\",\"lane\":\"north\",\"load\":-3},{\"id\":\"b\",\"lane\":\"south\",\"load\":0}]"), out var script, out var error), error);
            Assert.AreEqual(-3, script.opening[0].load);
        }

        [Test]
        public void ALoadWordInsideATextValueIsNotMistakenForTheField()
        {
            string json = "{\"ticket\":\"load: three\",\"opening\":[{\"id\":\"load\",\"lane\":\"north\",\"load\":1}],\"cues\":[],\"closing\":[]}";
            Assert.IsTrue(ScriptParser.TryParse(json, out _, out var error), error);
        }

        // ---- ids ----

        [Test]
        public void RejectsADuplicateIdInOpening()
        {
            Assert.IsFalse(ScriptParser.TryParse(Script("[{\"id\":\"a\",\"lane\":\"north\",\"load\":1},{\"id\":\"a\",\"lane\":\"south\",\"load\":2}]"), out _, out var error));
            StringAssert.Contains("twice", error);
        }

        [Test]
        public void RejectsTheReservedIdInOpeningAndInClosing()
        {
            string marker = "[{\"id\":\"YardPlayer\",\"lane\":\"north\",\"load\":1}]";
            Assert.IsFalse(ScriptParser.TryParse(Script(marker), out _, out var openingError));
            StringAssert.Contains("reserved", openingError);
            Assert.IsFalse(ScriptParser.TryParse(Script("[]", "[]", marker), out _, out var closingError));
            StringAssert.Contains("reserved", closingError);
        }

        [Test]
        public void TheReservedIdIsCaseSensitive()
        {
            Assert.IsTrue(ScriptParser.TryParse(Script("[{\"id\":\"yardplayer\",\"lane\":\"north\",\"load\":1}]"), out _, out var error), error);
        }

        // ---- nesting ----

        [Test]
        public void AcceptsNestingOfExactly32AndRejects33()
        {
            Assert.IsTrue(ScriptParser.TryParse(Nested(ScriptParser.MaxDepth - 1), out _, out var error), error);
            Assert.IsFalse(ScriptParser.TryParse(Nested(ScriptParser.MaxDepth), out _, out var deep));
            StringAssert.Contains("nests deeper", deep);
        }

        [Test]
        public void BracketsInsideAStringDoNotCountAsNesting()
        {
            string json = "{\"ticket\":\"" + new string('[', 40) + "\"," + EmptyLists + "}";
            Assert.IsTrue(ScriptParser.TryParse(json, out _, out var error), error);
        }

        [Test]
        public void AnEscapedQuoteDoesNotEndTheString()
        {
            string json = "{\"ticket\":\"a\\\"[[[\"," + EmptyLists + "}";
            Assert.IsTrue(ScriptParser.TryParse(json, out _, out var error), error);
        }

        [Test]
        public void RejectsAComment()
        {
            string json = "{/*x*/\"ticket\":\"t\"," + EmptyLists + "}";
            Assert.IsFalse(ScriptParser.TryParse(json, out _, out var error));
            StringAssert.Contains("comments", error);
        }

        [Test]
        public void StrayClosingBracketsCannotBuyExtraNesting()
        {
            string json = "{\"x\":" + new string(']', 40) + new string('[', ScriptParser.MaxDepth + 1) + "}";
            Assert.IsFalse(ScriptParser.TryParse(json, out _, out var error));
            StringAssert.Contains("nests deeper", error);
        }

        [Test]
        public void RejectsAKeyWrittenWithAnEscape()
        {
            Assert.IsFalse(ScriptParser.TryParse(Script("[{\"id\":\"a\",\"lane\":\"north\",\"\\u006coad\":2.5}]"), out _, out var error));
            StringAssert.Contains("escape", error);
        }

        [Test]
        public void RejectsSingleQuotedText()
        {
            string json = "{'ticket':'t'," + EmptyLists + "}";
            Assert.IsFalse(ScriptParser.TryParse(json, out _, out var error));
            StringAssert.Contains("plain JSON", error);
        }

        [Test]
        public void PlainJsonValuesOfEveryKindAreFine()
        {
            string json = "{\"x\":[true, false, null, -1.5e3, 2E+2],\r\n\t" + EmptyLists + "}";
            Assert.IsTrue(ScriptParser.TryParse(json, out _, out var error), error);
        }

        [Test]
        public void ASlashInsideAStringIsFine()
        {
            string json = "{\"ticket\":\"shift/04\",\"opening\":[{\"id\":\"a\",\"lane\":\"north\",\"load\":1}],"
                + "\"cues\":[{\"say\":\"mark\",\"who\":\"a\",\"flag\":\"a/b\",\"patch\":{}}],\"closing\":[{\"id\":\"a\",\"lane\":\"north\",\"load\":1}]}";
            Assert.IsTrue(ScriptParser.TryParse(json, out var script, out var error), error);
            Assert.AreEqual("shift/04", script.ticket);
        }

        [Test]
        public void ThousandsOfNestedBracketsAreRejectedBeforeTheJsonReader()
        {
            Assert.IsFalse(ScriptParser.TryParse(Nested(5000), out _, out var error));
            StringAssert.Contains("nests deeper", error);
        }

        // ---- sizes ----

        [Test]
        public void RejectsTooManyMarkers()
        {
            string markers = "[" + string.Join(",", Enumerable.Range(0, ScriptParser.MaxMarkers + 1).Select(i => $"{{\"id\":\"m{i}\",\"lane\":\"north\",\"load\":0}}")) + "]";
            Assert.IsFalse(ScriptParser.TryParse(Script(markers), out _, out var error));
            StringAssert.Contains("limit", error);
        }

        [Test]
        public void RejectsTooManyCues()
        {
            string cues = "[" + string.Join(",", Enumerable.Repeat("{\"say\":\"clock\",\"minute\":1,\"patch\":{}}", ScriptParser.MaxCues + 1)) + "]";
            Assert.IsFalse(ScriptParser.TryParse(Script("[]", cues), out _, out var error));
            StringAssert.Contains("limit", error);
        }

        [Test]
        public void RejectsAPatchThatNamesTooManyMarkers()
        {
            string gone = "[" + string.Join(",", Enumerable.Range(0, ScriptParser.MaxPatchEntries + 1).Select(i => $"\"g{i}\"")) + "]";
            Assert.IsFalse(ScriptParser.TryParse(Script("[]", "[{\"say\":\"clock\",\"minute\":1,\"patch\":{\"gone\":" + gone + "}}]"), out _, out var error));
            StringAssert.Contains("more than", error);
        }

        [TestCase("ticket")]
        [TestCase("id")]
        [TestCase("say")]
        [TestCase("flag")]
        public void RejectsTextLongerThanTheLimit(string field)
        {
            string longText = new string('x', ScriptParser.MaxText + 1);
            string json;
            switch (field)
            {
                case "ticket": json = "{\"ticket\":\"" + longText + "\"," + EmptyLists + "}"; break;
                case "id": json = Script("[{\"id\":\"" + longText + "\",\"lane\":\"north\",\"load\":1}]"); break;
                case "say": json = Script("[]", "[{\"say\":\"" + longText + "\",\"patch\":{}}]"); break;
                default: json = Script("[]", "[{\"say\":\"mark\",\"who\":\"a\",\"flag\":\"" + longText + "\",\"patch\":{}}]"); break;
            }
            Assert.IsFalse(ScriptParser.TryParse(json, out _, out var error));
            StringAssert.Contains("limit", error);
        }

        // {"x": [[...]], "opening":[],...} — the root object plus `arrays` levels of nesting.
        static string Nested(int arrays) =>
            "{\"x\":" + new string('[', arrays) + new string(']', arrays) + "," + EmptyLists + "}";
    }
}
