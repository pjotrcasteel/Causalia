using Causalia.Randomness;

namespace Causalia.Tests.Randomness;

[TestClass]
public sealed class DeterministicRandomTests
{
    [TestMethod]
    public void NextUInt64_WithKnownSeed_ProducesStableSequence()
    {
        var random = new DeterministicRandom(42);
        IReadOnlyList<ulong> expected =
        [
            1546998764402558742UL,
            6990951692964543102UL,
            12544586762248559009UL,
            17057574109182124193UL,
            18295552978065317476UL
        ];

        foreach (var expectedValue in expected)
        {
            Assert.AreEqual(expectedValue, random.NextUInt64());
        }
    }

    [TestMethod]
    public void NextInt32_WithInvalidMaximum_ThrowsExactly()
    {
        var random = new DeterministicRandom(42);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => random.NextInt32(0));
    }
}
