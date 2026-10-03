using System;
using LivingWorld.Simulation.Core;
using NUnit.Framework;

namespace LivingWorld.Simulation.Tests.Core.Randomness
{
    /// <summary>Protects reproducible draws, saved-state continuation and bounded sampling.</summary>
    public sealed class SimRngTests
    {
        [Test]
        public void ZeroSeedMatchesSplitMix64ReferenceVector()
        {
            // Fixed SplitMix64 reference outputs, independent of the implementation under test.
            ulong[] expected = { 0xE220A8397B1DCDAFUL, 0x6E789E6AA1B965F4UL,
                0x06C45D188009454FUL, 0xF88BB8A8724C81ECUL, 0x1B39896A51A8749BUL };
            var rng = new SimRng(0);
            Assert.That(rng.State, Is.Zero);
            foreach (ulong value in expected)
                Assert.That(rng.NextUInt64(), Is.EqualTo(value));
        }

        [TestCase(0UL)]
        [TestCase(42UL)]
        [TestCase(ulong.MaxValue)]
        public void SameSeedReproducesMixedDraws(ulong seed)
        {
            var first = new SimRng(seed);
            var second = new SimRng(seed);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(first.NextUInt64(), Is.EqualTo(second.NextUInt64()));
                Assert.That(first.NextInt(97), Is.EqualTo(second.NextInt(97)));
            }
            Assert.That(first.State, Is.EqualTo(second.State));
        }

        [TestCase(0UL)]
        [TestCase(42UL)]
        [TestCase(ulong.MaxValue)]
        public void SavedStateResumesExactContinuation(ulong seed)
        {
            var original = new SimRng(seed);
            Assert.That(SimRng.FromState(seed).State, Is.EqualTo(seed));
            for (int i = 0; i < 31; i++) original.NextUInt64();
            var resumed = SimRng.FromState(original.State);
            Assert.That(resumed.State, Is.EqualTo(original.State));
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(resumed.NextInt(int.MaxValue), Is.EqualTo(original.NextInt(int.MaxValue)));
                Assert.That(resumed.NextUInt64(), Is.EqualTo(original.NextUInt64()));
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(97)]
        [TestCase(int.MaxValue)]
        public void BoundedDrawsStayWithinExclusiveRange(int bound)
        {
            var rng = new SimRng(ulong.MaxValue);
            for (int i = 0; i < 1000; i++)
                Assert.That(rng.NextInt(bound), Is.InRange(0, bound - 1));
        }

        [Test]
        public void BoundedDrawRejectsSurplusValueRatherThanBiasingZero()
        {
            // This state produces zero first, then reference vector[0]. For bound 3,
            // zero is the one surplus value in the 2^64 possible raw outputs.
            var rng = SimRng.FromState(0x61C8864680B583EBUL);
            Assert.That(rng.NextInt(3), Is.EqualTo(1));
            Assert.That(rng.State, Is.EqualTo(0x9E3779B97F4A7C15UL));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void InvalidBoundThrowsWithoutAdvancingState(int bound)
        {
            var rng = new SimRng(42);
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextInt(bound));
            Assert.That(error.ParamName, Is.EqualTo("exclusiveMax"));
            Assert.That(rng.State, Is.EqualTo(42UL));
        }
    }
}
