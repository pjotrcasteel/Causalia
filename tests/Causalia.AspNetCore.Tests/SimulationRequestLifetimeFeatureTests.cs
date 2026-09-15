using Causalia.AspNetCore.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Causalia.AspNetCore.Tests;

[TestClass]
public sealed class SimulationRequestLifetimeFeatureTests
{
    [TestMethod]
    public void RequestAborted_WhenApplicationReplacesToken_PreservesNodeCancellation()
    {
        using var nodeCancellation = new CancellationTokenSource();
        using var applicationCancellation = new CancellationTokenSource();
        using var lifetime = new SimulationRequestLifetimeFeature(nodeCancellation.Token)
        {
            RequestAborted = applicationCancellation.Token
        };

        nodeCancellation.Cancel();

        Assert.IsTrue(lifetime.RequestAborted.IsCancellationRequested);
    }
}
