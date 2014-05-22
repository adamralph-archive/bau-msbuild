// parameters
var versionSuffix = Environment.GetEnvironmentVariable("VERSION_SUFFIX") ?? "-adhoc";
var msBuildFileVerbosity = Environment.GetEnvironmentVariable("MSBUILD_FILE_VERBOSITY") ?? "normal";
var nugetVerbosity = Environment.GetEnvironmentVariable("NUGET_VERBOSITY") ?? "quiet";

// solution specific variables
var version = File.ReadAllText("src/CommonAssemblyInfo.cs").Split(new[] { "AssemblyInformationalVersion(\"" }, 2, StringSplitOptions.None).ElementAt(1).Split(new[] { '"' }).First();
var nugetCommand = "packages/NuGet.CommandLine.2.8.1/tools/NuGet.exe";
var xunitCommand = "packages/xunit.runners.1.9.2/tools/xunit.console.clr4.exe";
var solution = "src/Bau.MSBuild.sln";
var output = "artifacts/output";
var tests = "artifacts/tests";
var logs = "artifacts/logs";
var unit = "src/test/Bau.MSBuild.Test.Unit/bin/Release/Bau.MSBuild.Test.Unit.dll";
var packs = new[] { "src/Bau.MSBuild/Bau.MSBuild", };

// solution agnostic tasks
Require<Bau>()

.Task("default").DependsOn("unit", "pack")

.Task("logs").Do(() =>
{
    if (!Directory.Exists(logs))
    {
        Directory.CreateDirectory(logs);
        System.Threading.Thread.Sleep(100); // HACK (adamralph): wait for the directory to be created
    }
})

.MSBuild("clean").DependsOn("logs").Do(msbuild =>
{
    msbuild.MSBuildVersion = "net45";
    msbuild.Solution = solution;
    msbuild.Targets = new[] { "Clean", };
    msbuild.Properties = new { Configuration = "Release" };
    msbuild.MaxCpuCount = -1;
    msbuild.NodeReuse = false;
    msbuild.Verbosity = Verbosity.Minimal;
    msbuild.NoLogo = true;
    msbuild.Args = "/fileLogger /fileloggerparameters:PerformanceSummary;Summary;Verbosity=" + msBuildFileVerbosity + ";LogFile=" + logs + "/clean.log";
})

.Task("clobber").DependsOn("clean").Do(() =>
{
    if (Directory.Exists(output))
    {
        Directory.Delete(output, true);
    }
})

.Exec("restore").Do(exec => exec
    .Run(nugetCommand)
    .With("restore", solution))

.MSBuild("build").DependsOn("clean", "restore", "logs").Do(msbuild =>
{
    msbuild.MSBuildVersion = "net45";
    msbuild.Solution = solution;
    msbuild.Targets = new[] { "Build", };
    msbuild.Properties = new { Configuration = "Release" };
    msbuild.MaxCpuCount = -1;
    msbuild.NodeReuse = false;
    msbuild.Verbosity = Verbosity.Minimal;
    msbuild.NoLogo = true;
    msbuild.Args = "/fileLogger /fileloggerparameters:PerformanceSummary;Summary;Verbosity=" + msBuildFileVerbosity + ";LogFile=" + logs + "/build.log";
})

.Task("tests").Do(() =>
{
    if (!Directory.Exists(tests))
    {
        Directory.CreateDirectory(tests);
        System.Threading.Thread.Sleep(100); // HACK (adamralph): wait for the directory to be created
    }
})

.Exec("unit").DependsOn("build", "tests").Do(exec => exec
    .Run(xunitCommand)
    .With(unit, "/html", GetTestResultsPath(tests, unit, "html"), "/xml", GetTestResultsPath(tests, unit, "xml")))

.Task("output").Do(() =>
{
    if (!Directory.Exists(output))
    {
        Directory.CreateDirectory(output);
        System.Threading.Thread.Sleep(100); // HACK (adamralph): wait for the directory to be created
    }
})

.Task("pack").DependsOn("build", "clobber", "output").Do(() =>
{
    foreach (var pack in packs)
    {
        File.Copy(pack + ".nuspec", pack + ".nuspec.original", true);
    }

    try
    {
        foreach (var pack in packs)
        {
            File.WriteAllText(pack + ".nuspec", File.ReadAllText(pack + ".nuspec").Replace("0.0.0", version + versionSuffix));
            new Exec()
                .Run(nugetCommand)
                .With(
                    "pack", pack + ".csproj",
                    "-OutputDirectory", output,
                    "-Properties", "Configuration=Release",
                    "-IncludeReferencedProjects",
                    "-Verbosity " + nugetVerbosity)
                .Execute();
        }
    }
    finally
    {
        foreach (var pack in packs)
        {
            File.Copy(pack + ".nuspec.original", pack + ".nuspec", true);
            File.Delete(pack + ".nuspec.original");
        }
    }
})

.Run();

string GetTestResultsPath(string directory, string assembly, string extension)
{
    return Path.GetFullPath(
        Path.Combine(
            directory,
            string.Concat(
                Path.GetFileNameWithoutExtension(assembly),
                ".TestResults.",
                extension)));
}
