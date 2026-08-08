// Local dev NuGet package builder for the Stride Community Toolkit.
//
//   dotnet run --file build/pack-local.cs
//   dotnet run --file build/pack-local.cs -- --version 1.0.0-dev2 --configuration Debug
//   dotnet run --file build/pack-local.cs -- --clean
//
// The --file switch is required rather than optional here: the repository root contains
// Stride.CommunityToolkit.ndproj, and without --file the SDK runs that project and passes this
// script to it as an argument.
//
// Packs the publishable toolkit projects into the local feed at bin/packages, which the repository
// NuGet.config exposes as the "toolkit-local" source. That lets an example consume the toolkit the
// way a real user does - via #:package / PackageReference - rather than through a ProjectReference.
//
// The version stays fixed at 1.0.0-dev by design, so a reference like
//   #:package Stride.CommunityToolkit.Bepu@1.0.0-dev
// keeps working across rebuilds and never has to be edited. NuGet keys its global cache by
// id + version and will not re-extract a rebuilt package of the same version, so this script purges
// the matching cache folders first - the same trick Stride uses in build/install-gamestudio.targets.

using System.Diagnostics;
using System.Runtime.CompilerServices;

var version = "1.0.0-dev";
var configuration = "Release";
var cleanOnly = false;

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--version" when i + 1 < args.Length: version = args[++i]; break;
        case "--configuration" when i + 1 < args.Length: configuration = args[++i]; break;
        case "--clean": cleanOnly = true; break;
        case "--help" or "-h":
            Console.WriteLine("Usage: dotnet run build/pack-local.cs -- [--version <v>] [--configuration <c>] [--clean]");
            return 0;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

var repositoryRoot = FindRepositoryRoot();

if (repositoryRoot is null)
{
    Console.Error.WriteLine("Could not locate the repository root (no Stride.CommunityToolkit.slnx found in any parent directory).");
    return 1;
}

var feedDirectory = Path.Combine(repositoryRoot, "bin", "packages");

// Mirrors PACK_PROJECTS in .github/workflows/dotnet-nuget.yml. Stride.CommunityToolkit.Windows
// needs an explicit runtime during restore, matching the "|--runtime win-x64" suffix used there.
var projects = new (string RelativePath, string? RestoreArguments)[]
{
    ("src/Stride.CommunityToolkit/Stride.CommunityToolkit.csproj", null),
    ("src/Stride.CommunityToolkit.Windows/Stride.CommunityToolkit.Windows.csproj", "--runtime win-x64"),
    ("src/Stride.CommunityToolkit.Linux/Stride.CommunityToolkit.Linux.csproj", null),
    ("src/Stride.CommunityToolkit.Skyboxes/Stride.CommunityToolkit.Skyboxes.csproj", null),
    ("src/Stride.CommunityToolkit.Bepu/Stride.CommunityToolkit.Bepu.csproj", null),
    ("src/Stride.CommunityToolkit.Bullet/Stride.CommunityToolkit.Bullet.csproj", null),
    ("src/Stride.CommunityToolkit.DebugShapes/Stride.CommunityToolkit.DebugShapes.csproj", null),
    ("src/Stride.CommunityToolkit.ImGui/Stride.CommunityToolkit.ImGui.csproj", null),
};

PurgeCachedExtractions(version);
RemoveStalePackages(feedDirectory, version);

if (cleanOnly)
{
    Console.WriteLine($"Cleaned local feed and cache entries for {version}.");
    return 0;
}

Directory.CreateDirectory(feedDirectory);

Console.WriteLine($"Packing {projects.Length} project(s) as {version} ({configuration}) into {feedDirectory}");
Console.WriteLine();

foreach (var (relativePath, restoreArguments) in projects)
{
    var projectPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    var projectName = Path.GetFileNameWithoutExtension(projectPath);

    Console.WriteLine($"  {projectName}");

    // Restore/build/pack are run as separate steps, matching the CI workflow: the Windows package
    // only restores correctly with an explicit runtime, which `dotnet pack` alone cannot express.
    var versionArgument = $"-p:Version={version}";

    if (!RunDotnet($"restore \"{projectPath}\" {versionArgument} {restoreArguments}") ||
        !RunDotnet($"build \"{projectPath}\" --no-restore -c {configuration} {versionArgument}") ||
        !RunDotnet($"pack \"{projectPath}\" --no-build -c {configuration} {versionArgument} -o \"{feedDirectory}\""))
    {
        Console.Error.WriteLine($"Failed while packing {projectName}.");
        return 1;
    }
}

var consumerConfigPath = WriteConsumerNuGetConfig(feedDirectory);

Console.WriteLine();
Console.WriteLine($"Done. {Directory.GetFiles(feedDirectory, $"*.{version}.nupkg").Length} package(s) in {feedDirectory}");
Console.WriteLine();
Console.WriteLine("A ready-to-use consumer config was written to:");
Console.WriteLine($"  {consumerConfigPath}");
Console.WriteLine();
Console.WriteLine("Copy it next to the project that should consume these packages FIRST, then add the");
Console.WriteLine("references, for example:");
Console.WriteLine($"  #:package Stride.CommunityToolkit.Bepu@{version}");
Console.WriteLine();
Console.WriteLine($"That order matters. \"{version}\" means \"at least {version}\", and prerelease labels compare");
Console.WriteLine("alphabetically, so \"dev\" sorts before \"preview\". Without this config NuGet does not fail -");
Console.WriteLine("it quietly resolves an older 1.0.0-preview.* from nuget.org instead. If the references were");
Console.WriteLine("added first, run \"dotnet restore\" (or delete obj/) to recover; adding the config alone");
Console.WriteLine("will not, because the wrong resolution is already cached in obj/project.assets.json.");
Console.WriteLine();
Console.WriteLine("Nothing machine-wide needs changing: NuGet merges that config with the existing one,");
Console.WriteLine("so nuget.org and Stride Dev keep resolving exactly as before.");

return 0;

// Writes a consumer-ready NuGet.config beside the packages, so testing them elsewhere is a copy
// rather than hand-written XML. It lives inside the feed folder deliberately: NuGet discovers config
// files by walking up from a project directory, and nothing is ever built here, so this file only
// takes effect once it has been copied somewhere that needs it.
static string WriteConsumerNuGetConfig(string feedDirectory)
{
    // The path is absolute so the file keeps working wherever it is copied to.
    var contents = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <!--
          Consumes locally built Stride Community Toolkit packages from the repository's dev feed.
          Generated by build/pack-local.cs - copy this file next to the project that should use them.

          Copy this file BEFORE adding the package references. A reference to "1.0.0-dev" means "at
          least 1.0.0-dev", and prerelease labels compare alphabetically, so "dev" sorts before
          "preview" and an older published 1.0.0-preview.* satisfies it. Without this file NuGet does
          not fail - it silently resolves the older package from nuget.org.

          If the references were added first, adding this file alone does not fix it: the wrong
          resolution is already recorded in obj/project.assets.json and a plain build keeps using it.
          Run "dotnet restore" explicitly (or delete obj/) to recover.

          The packageSourceMapping entry is required, not optional. Once any NuGet config on the
          machine defines packageSourceMapping - the Stride dev setup does - a source that is not
          mapped is silently never consulted, and these packages quietly resolve to an older
          -preview version from nuget.org instead of failing outright.

          Both patterns are needed. "Stride.CommunityToolkit.*" requires a dot after the prefix, so it
          matches Stride.CommunityToolkit.Bepu and friends but NOT the base Stride.CommunityToolkit
          package. Without the exact-name pattern the base package falls through to the nuget.org
          catch-all and resolves to an old published prerelease, because NuGet compares prerelease
          labels alphabetically and "dev" sorts before "preview".

          Both are more specific than the "Stride.*" mapped to the Stride Dev feed, and NuGet resolves
          by longest matching prefix, so they win without disturbing how Stride packages resolve.
        -->
        <configuration>
          <packageSources>
            <add key="toolkit-local" value="{feedDirectory.TrimEnd(Path.DirectorySeparatorChar)}" />
          </packageSources>
          <packageSourceMapping>
            <packageSource key="toolkit-local">
              <package pattern="Stride.CommunityToolkit" />
              <package pattern="Stride.CommunityToolkit.*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>

        """;

    var path = Path.Combine(feedDirectory, "NuGet.config");

    File.WriteAllText(path, contents);

    return path;
}

// Walks up from this script's directory looking for the solution that marks the repository root.
//
// CallerFilePath, not AppContext.BaseDirectory: a file-based app is compiled into the SDK's temp
// cache, so BaseDirectory points somewhere under %TEMP%\dotnet\runfile rather than at this file.
// CallerFilePath is baked in at compile time and gives the script's real location. The working
// directory is used as a fallback for the case where the script has been moved after compilation.
static string? FindRepositoryRoot([CallerFilePath] string scriptPath = "")
{
    var directory = File.Exists(scriptPath)
        ? Path.GetDirectoryName(scriptPath)
        : Environment.CurrentDirectory;

    while (!string.IsNullOrEmpty(directory))
    {
        if (File.Exists(Path.Combine(directory, "Stride.CommunityToolkit.slnx")))
        {
            return directory;
        }

        directory = Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
    }

    return null;
}

// NuGet will not re-extract a rebuilt package that carries a version it has already cached, so the
// previous extraction has to go or consumers keep resolving yesterday's DLLs.
static void PurgeCachedExtractions(string version)
{
    var packagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");

    if (string.IsNullOrEmpty(packagesRoot))
    {
        packagesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    if (!Directory.Exists(packagesRoot))
    {
        return;
    }

    var purged = 0;

    foreach (var packageDirectory in Directory.GetDirectories(packagesRoot, "stride.communitytoolkit*"))
    {
        var versionDirectory = Path.Combine(packageDirectory, version);

        if (!Directory.Exists(versionDirectory))
        {
            continue;
        }

        Directory.Delete(versionDirectory, recursive: true);
        purged++;
    }

    if (purged > 0)
    {
        Console.WriteLine($"Purged {purged} stale cache extraction(s) for {version}.");
    }
}

static void RemoveStalePackages(string feedDirectory, string version)
{
    if (!Directory.Exists(feedDirectory))
    {
        return;
    }

    foreach (var package in Directory.GetFiles(feedDirectory, $"*.{version}.nupkg"))
    {
        File.Delete(package);
    }
}

static bool RunDotnet(string arguments)
{
    var startInfo = new ProcessStartInfo("dotnet", arguments)
    {
        RedirectStandardOutput = true,
        RedirectStandardError = true,
    };

    using var process = Process.Start(startInfo);

    if (process is null)
    {
        return false;
    }

    var standardOutput = process.StandardOutput.ReadToEnd();
    var standardError = process.StandardError.ReadToEnd();

    process.WaitForExit();

    if (process.ExitCode != 0)
    {
        Console.Error.WriteLine(standardOutput);
        Console.Error.WriteLine(standardError);
    }

    return process.ExitCode == 0;
}
