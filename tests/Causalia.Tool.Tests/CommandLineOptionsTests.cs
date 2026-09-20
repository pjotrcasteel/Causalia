namespace Causalia.Tool.Tests;

[TestClass]
public sealed class CommandLineOptionsTests
{
    [TestMethod]
    public void Parse_WhenInspectHasNoPath_UsesCurrentDirectory()
    {
        IReadOnlyList<string> arguments = new List<string> { "inspect" };

        var options = CommandLineOptions.Parse(arguments);

        Assert.AreEqual("inspect", options.Command);
        Assert.AreEqual(".", options.TargetPath);
        Assert.AreEqual(OutputFormat.Text, options.OutputFormat);
        Assert.IsNull(options.ProjectPath);
        Assert.IsFalse(options.Force);
        Assert.IsFalse(options.ShowHelp);
    }

    [TestMethod]
    public void Parse_WhenJsonFormatIsSpecified_UsesJson()
    {
        IReadOnlyList<string> arguments = new List<string>
        {
            "inspect",
            "Service.slnx",
            "--format",
            "json"
        };

        var options = CommandLineOptions.Parse(arguments);

        Assert.AreEqual("Service.slnx", options.TargetPath);
        Assert.AreEqual(OutputFormat.Json, options.OutputFormat);
    }

    [TestMethod]
    public void Parse_WhenInitOptionsAreSpecified_ReturnsProjectAndForce()
    {
        IReadOnlyList<string> arguments = new List<string>
        {
            "init",
            "Service.slnx",
            "--project",
            "src/Orders/Orders.csproj",
            "--force"
        };

        var options = CommandLineOptions.Parse(arguments);

        Assert.AreEqual("init", options.Command);
        Assert.AreEqual("Service.slnx", options.TargetPath);
        Assert.AreEqual("src/Orders/Orders.csproj", options.ProjectPath);
        Assert.IsTrue(options.Force);
    }

    [TestMethod]
    public void Parse_WhenInspectUsesForce_Throws()
    {
        IReadOnlyList<string> arguments = new List<string> { "inspect", "--force" };

        Assert.ThrowsExactly<ArgumentException>(() => CommandLineOptions.Parse(arguments));
    }

    [TestMethod]
    public void Parse_WhenCommandIsUnknown_Throws()
    {
        IReadOnlyList<string> arguments = new List<string> { "unknown" };

        Assert.ThrowsExactly<ArgumentException>(() => CommandLineOptions.Parse(arguments));
    }
}
