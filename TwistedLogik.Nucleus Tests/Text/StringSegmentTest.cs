﻿using NUnit.Framework;
using TwistedLogik.Nucleus.Testing;
using TwistedLogik.Nucleus.Text;

namespace TwistedLogik.Nucleus.Tests.Text
{
    [TestFixture]
    public class StringSegmentTest : NucleusTestFramework
    {
        [Test]
        public void StringSegment_CanBeCreatedFromString()
        {
            var source = "Hello, world!";
            var segment = new StringSegment(source, 2, 4);

            TheResultingString(segment.ToString())
                .ShouldBe("llo,");
        }

        [Test]
        public void StringSegment_IndexOfCharacter_ReturnsCorrectValueForExistingCharacter()
        {
            var segment = new StringSegment("Hello, world!");
            var result = segment.IndexOf('w');

            TheResultingValue(result).ShouldBe(7);
        }

        [Test]
        public void StringSegment_IndexOfCharacter_ReturnsNegativeOneForNonExistingCharacter()
        {
            var segment = new StringSegment("Hello, world!");
            var result = segment.IndexOf('z');

            TheResultingValue(result).ShouldBe(-1);
        }

        [Test]
        public void StringSegment_IndexOfString_ReturnsCorrectValueForExistingCharacter()
        {
            var segment = new StringSegment("Hello, world!");
            var result = segment.IndexOf("world");

            TheResultingValue(result).ShouldBe(7);
        }

        [Test]
        public void StringSegment_IndexOfString_ReturnsNegativeOneForNonExistingCharacter()
        {
            var segment = new StringSegment("Hello, world!");
            var result = segment.IndexOf("zorld");

            TheResultingValue(result).ShouldBe(-1);
        }
    }
}
