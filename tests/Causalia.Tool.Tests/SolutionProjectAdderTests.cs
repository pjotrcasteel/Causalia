using System.Xml.Linq;
using Causalia.Tool.Initialization;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class SolutionProjectAdderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task TryAddAsync_WhenCalledTwice_AddsProjectOnceAndLeavesNoTemporaryFile()
    {
        using var directory = new TemporaryDirectory();
        var solutionPath = await directory.WriteFileAsync("App.slnx", "<Solution />", TestContext.CancellationToken);
        var projectPath = Path.Combine(directory.Path, "tests/App.Tests/App.Tests.csproj");
        var adder = new SolutionProjectAdder();

        Assert.IsTrue(await adder.TryAddAsync(solutionPath, directory.Path, projectPath, TestContext.CancellationToken));
        Assert.IsTrue(await adder.TryAddAsync(solutionPath, directory.Path, projectPath, TestContext.CancellationToken));

        Assert.AreEqual(1, XDocument.Load(solutionPath).Descendants("Project").Count());
        Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp").Length);
        using var exclusive = File.Open(solutionPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.IsTrue(exclusive.Length > 0);
    }

    [TestMethod]
    public async Task TryAddAsync_WhenCancelled_PreservesOriginalSolution()
    {
        using var directory = new TemporaryDirectory();
        const string original = "<Solution><Project Path=\"App.csproj\" /></Solution>";
        var solutionPath = await directory.WriteFileAsync("App.slnx", original, TestContext.CancellationToken);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await new SolutionProjectAdder().TryAddAsync(solutionPath, directory.Path, "App.Tests.csproj", cancellation.Token));

        Assert.AreEqual(original, await File.ReadAllTextAsync(solutionPath, TestContext.CancellationToken));
        Assert.AreEqual(0, Directory.GetFiles(directory.Path, "*.tmp").Length);
    }
}
