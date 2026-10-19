using System.Text;
using Microsoft.CodeAnalysis;
namespace DotnetDide.Tests;

public sealed class DotnetCommandTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotnetdide-build-" + Guid.NewGuid());
    private static readonly MetadataReference[] References = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Select(path => MetadataReference.CreateFromFile(path)).ToArray();
    private Runspace Create() => new(new WorkspaceFileSystem(_root), References);
    private static void Write(Runspace r, string path, string code) => r.Files.WriteFile(path, Encoding.UTF8.GetBytes(code));
    private static async Task<RunResult> Execute(Runspace r, string code)
    {
        var result = await r.RunAsync(code);
        Assert.True(result.Success, string.Join("\n", result.Diagnostics.Select(d => d.Message)) + result.Error);
        return result;
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }

    [Fact]
    public async Task BuildDiscoversSourcesAndEmitsDllAndPortablePdbWithoutExecuting()
    {
        using var r = Create(); Write(r, "Program.cs", "Console.WriteLine(\"hello\"); File.WriteAllText(\"ran.txt\", \"yes\");");
        var build = await Execute(r, "var build = Dotnet.Build(); build.Success");
        Assert.Equal("True", build.Value); Assert.Contains("Build succeeded", build.Stdout);
        Assert.Contains(".dll", (await Execute(r, "build.AssemblyPath")).Value);
        Assert.False(File.Exists(Path.Combine(_root, "ran.txt")));
        Assert.Contains(r.Files.ReadDirectory("bin"), f => f.Name.EndsWith(".dll") && f.Size > 0);
        var pdb = r.Files.ReadDirectory("bin").Single(f => f.Name.EndsWith(".pdb"));
        Assert.Equal("BSJB", Encoding.ASCII.GetString(r.Files.ReadFile("bin/" + pdb.Name)[..4]));
    }
    [Fact]
    public async Task RunFileAwaitsAsyncEntryPointPassesArgumentsAndKeepsScriptState()
    {
        using var r = Create(); Write(r, "main.cs", "await Task.Delay(1); Console.Write(args[0]); Console.Error.Write(\"warning\"); File.WriteAllText(\"made.txt\", \"saved\"); return 7;");
        await Execute(r, "int answer = 42;");
        var run = await Execute(r, "var run = await Dotnet.Run(\"main.cs\", new[] { \"hello\" }); run.ExitCode");
        Assert.Equal("7", run.Value); Assert.Contains("hello", run.Stdout); Assert.Equal("warning", run.Stderr);
        Assert.Equal("False", (await Execute(r, "run.Success")).Value);
        Assert.Equal("42", (await Execute(r, "answer")).Value);
        Assert.Equal("saved", Encoding.UTF8.GetString(r.Files.ReadFile("made.txt")));
        r.Reset(); Assert.Equal("7", (await Execute(r, "(await Dotnet.Run(\"main.cs\", new[] { \"again\" })).ExitCode")).Value);
    }
    [Fact]
    public async Task SdkProjectUsesMultipleFilesAndProjectWorkingDirectory()
    {
        using var r = Create(); r.Files.Mkdir("app"); r.Files.Mkdir("app/nested"); r.Files.Mkdir("app/obj");
        Write(r, "app/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>");
        Write(r, "app/Program.cs", "Console.Write(Helper.Value); File.WriteAllText(\"result.txt\", Helper.Value.ToString());");
        Write(r, "app/nested/Helper.cs", "public static class Helper { public static int Value => 42; }");
        Write(r, "app/obj/ignored.cs", "this is invalid");
        var run = await Execute(r, "var run = await Dotnet.Run(\"app/App.csproj\"); run.Success");
        Assert.Equal("True", run.Value); Assert.Contains("42", run.Stdout);
        Assert.Equal("42", Encoding.UTF8.GetString(r.Files.ReadFile("app/result.txt")));
        Assert.Equal(_root, (await Execute(r, "Directory.GetCurrentDirectory()")).Value);
        Assert.Equal("True", (await Execute(r, "Directory.SetCurrentDirectory(\"app\"); Dotnet.Build().Success")).Value);
    }
    [Fact]
    public async Task CompilerErrorsIncludeFileAndLocationAndNeverRunStaleOutput()
    {
        using var r = Create(); Write(r, "main.cs", "Console.Write(\"first\");");
        Assert.Contains("first", (await Execute(r, "await Dotnet.Run(\"main.cs\");")).Stdout);
        Write(r, "main.cs", "\nunknownName();");
        var failed = await Execute(r, "var failed = await Dotnet.Run(\"main.cs\"); failed.Success");
        Assert.Equal("False", failed.Value); Assert.DoesNotContain("first", failed.Stdout);
        Assert.Contains("main.cs(2,1)", failed.Stdout); Assert.Contains("CS0103", failed.Stdout);
        Assert.Equal("main.cs", (await Execute(r, "failed.Build.Diagnostics.First(d => d.Id == \"CS0103\").FilePath")).Value);
        Assert.Equal("True", (await Execute(r, "failed.Build.AssemblyPath == null")).Value);
    }
    [Fact]
    public async Task RepeatedRunsUseRebuiltProgramAndRuntimeErrorsRestoreOutput()
    {
        using var r = Create(); Write(r, "main.cs", "Console.Write(\"one\");");
        Assert.Contains("one", (await Execute(r, "await Dotnet.Run(\"main.cs\");")).Stdout);
        Write(r, "main.cs", "Console.Write(\"two\");");
        Assert.Contains("two", (await Execute(r, "await Dotnet.Run(\"main.cs\");")).Stdout);
        Write(r, "main.cs", "Console.Write(\"before\"); throw new Exception(\"boom\");");
        var result = await Execute(r, "var failedRun = await Dotnet.Run(\"main.cs\"); Console.Write(\"after\"); failedRun.Error.Message");
        Assert.Equal("boom", result.Value); Assert.Contains("before", result.Stdout); Assert.EndsWith("after", result.Stdout);
    }
    [Fact]
    public async Task ProjectLibraryAndExplicitCompileItemsAreSupported()
    {
        using var r = Create(); r.Files.Mkdir("src");
        Write(r, "Lib.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net11.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems><DefineConstants>FEATURE</DefineConstants><AssemblyName>Example</AssemblyName></PropertyGroup><ItemGroup><Compile Include=\"src/**/*.cs\" Exclude=\"src/Bad.cs\"/><Using Include=\"System.Text\"/></ItemGroup></Project>");
        Write(r, "src/Good.cs", "#if FEATURE\npublic class Good { public string Value => new StringBuilder(\"yes\").ToString(); }\n#else\ninvalid\n#endif");
        Write(r, "src/Bad.cs", "invalid");
        Assert.Equal("True", (await Execute(r, "Dotnet.Build().Success")).Value);
        Assert.True(r.Files.Stat("bin/Example.dll").Size > 0);
        Assert.Equal("False", (await Execute(r, "(await Dotnet.Run(\"Lib.csproj\")).Success")).Value);
    }
    [Theory]
    [InlineData("<ItemGroup><PackageReference Include=\"Other\" Version=\"1.0\"/></ItemGroup>", "PackageReference")]
    [InlineData("<ItemGroup><ProjectReference Include=\"Other.csproj\"/></ItemGroup>", "ProjectReference")]
    [InlineData("<Target Name=\"Custom\"/>", "Target")]
    [InlineData("<PropertyGroup Condition=\"'$(Configuration)' == 'Debug'\"><OutputType>Exe</OutputType></PropertyGroup>", "Condition")]
    public async Task UnsupportedProjectFeaturesFailClearly(string body, string expected)
    {
        using var r = Create(); Write(r, "App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\">" + body + "</Project>");
        Write(r, "Program.cs", "Console.WriteLine(1);");
        var result = await Execute(r, "var build = Dotnet.Build(); build.Success");
        Assert.Equal("False", result.Value); Assert.Contains(expected, result.Stdout); Assert.Contains("not supported", result.Stdout);
    }
    [Fact]
    public async Task MissingAmbiguousAndEscapingInputsReturnBuildDiagnostics()
    {
        using var r = Create();
        Assert.Equal("False", (await Execute(r, "Dotnet.Build().Success")).Value);
        Assert.Equal("False", (await Execute(r, "Dotnet.Build(\"../outside.cs\").Success")).Value);
        Write(r, "A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>"); Write(r, "B.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"/>");
        Assert.Contains("Multiple project files", (await Execute(r, "Dotnet.Build();")).Stdout);
    }
    [Fact]
    public async Task DefaultImportsAreScopedAndProjectImplicitUsingsAreOptIn()
    {
        using var r = Create();
        Assert.False((await r.RunAsync("new StringBuilder()")).Success);
        await Execute(r, "using System.Text; new StringBuilder();");
        Write(r, "Program.cs", "Console.WriteLine(42);");
        Write(r, "App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType></PropertyGroup></Project>");
        Assert.Equal("False", (await Execute(r, "Dotnet.Build().Success")).Value);
        Write(r, "App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings></PropertyGroup></Project>");
        Assert.Equal("True", (await Execute(r, "Dotnet.Build().Success")).Value);
        Write(r, "Program.cs", "Console.WriteLine(new StringBuilder(\"no shared imports\"));");
        Assert.Equal("False", (await Execute(r, "Dotnet.Build(\"Program.cs\").Success")).Value);
    }
    [Fact]
    public async Task WarningPolicyIsReflectedInBuildOutput()
    {
        using var r = Create();
        Write(r, "Program.cs", "#warning example warning\nSystem.Console.WriteLine(42);");
        Write(r, "App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><OutputType>Exe</OutputType><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup></Project>");
        var result = await Execute(r, "var built = Dotnet.Build(); built.Success");
        Assert.Equal("False", result.Value); Assert.Contains("error CS1030", result.Stdout);
    }
}
