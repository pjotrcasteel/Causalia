using System.Xml.Linq;
using Causalia.Tool.Initialization;

namespace Causalia.Tool.Tests;

[TestClass]
public sealed class CentralPackageManagementTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task InitializeAsync_WhenDirectProjectInheritsCentralVersions_GeneratesMatchingScopedProps()
    {
        using var directory = new TemporaryDirectory();
        await directory.WriteFileAsync("Directory.Packages.props",
            """
            <Project>
              <PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup>
              <ItemGroup><PackageVersion Include="MSTest.TestFramework" Version="4.4.0" /></ItemGroup>
            </Project>
            """, TestContext.CancellationToken);
        var projectPath = await directory.WriteFileAsync("src/Orders/Orders.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>",
            TestContext.CancellationToken);

        var result = await new ProjectInitializer().InitializeAsync(projectPath, null, false, TestContext.CancellationToken);
        var generatedPath = Path.Combine(Path.GetDirectoryName(projectPath)!, result.TestProject);
        var generated = XDocument.Load(generatedPath);
        var propsPath = Path.Combine(Path.GetDirectoryName(generatedPath)!, "Directory.Packages.props");

        Assert.IsTrue(File.Exists(propsPath));
        Assert.IsTrue(generated.Descendants("PackageReference").All(reference => reference.Attribute("Version") is null));
        var props = XDocument.Load(propsPath);
        Assert.AreEqual(0, props.Descendants("PackageVersion").Count(version => version.Attribute("Include")?.Value == "MSTest.TestFramework"));
        var importedPath = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(propsPath)!, props.Root!.Element("Import")!.Attribute("Project")!.Value));
        Assert.AreEqual(Path.Combine(directory.Path, "Directory.Packages.props"), importedPath);
    }

    [TestMethod]
    public async Task FindAsync_WhenNearestPropsDisablesCentralManagement_DoesNotUseAncestor()
    {
        using var directory = new TemporaryDirectory();
        await directory.WriteFileAsync("Directory.Packages.props",
            "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup></Project>",
            TestContext.CancellationToken);
        await directory.WriteFileAsync("src/Orders/Directory.Packages.props",
            "<Project><PropertyGroup><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup></Project>",
            TestContext.CancellationToken);

        var management = await CentralPackageManagement.FindAsync(Path.Combine(directory.Path, "src/Orders"), TestContext.CancellationToken);

        Assert.IsNull(management);
    }
}
