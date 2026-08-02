using System.Globalization;
using Stride.CommunityToolkit.AssetGenerator.Core;

namespace Stride.CommunityToolkit.AssetGenerator;

/// <summary>
/// Command line entry point. Kept dependency-free so it can be invoked from an MSBuild
/// <c>Exec</c> task without dragging anything into the consuming build.
/// </summary>
public static class Program
{
    private const string ToolName = "StrideToolkitAssetGenerator";

    /// <summary>Runs the generator.</summary>
    public static int Main(string[] args)
    {
        try
        {
            if (args.Contains("--help") || args.Contains("-h"))
            {
                PrintUsage();

                return 0;
            }

            var arguments = CommandLine.Parse(args);
            var options = arguments.ToOptions();
            var verbose = arguments.Flag("verbose");

            var result = new Core.AssetGenerator().Generate(options);

            Report(result, options, verbose);

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"{ToolName} : error STCT0000: {exception.Message}");

            return 1;
        }
    }

    private static void Report(AssetGenerationResult result, AssetGeneratorOptions options, bool verbose)
    {
        foreach (var message in result.Messages)
        {
            var origin = message.File ?? ToolName;

            switch (message.Severity)
            {
                case MessageSeverity.Error:
                    Console.Error.WriteLine($"{origin} : error {message.Code}: {message.Text}");
                    break;

                case MessageSeverity.Warning:
                    Console.WriteLine($"{origin} : warning {message.Code}: {message.Text}");
                    break;

                default:
                    if (verbose) Console.WriteLine($"  {message.Code}: {message.Text}");
                    break;
            }
        }

        foreach (var asset in result.CreatedAssets)
        {
            Console.WriteLine($"  Created {asset}");
        }

        foreach (var entry in result.PackageEntriesAdded)
        {
            Console.WriteLine($"  Registered {entry}");
        }

        if (verbose)
        {
            foreach (var skipped in result.SkippedResources)
            {
                Console.WriteLine($"  Skipped {skipped}");
            }
        }

        if (!result.AnyChanges)
        {
            if (verbose) Console.WriteLine($"  Nothing to do ({options.ProjectName}).");

            return;
        }

        if (options.DryRun) Console.WriteLine("  Dry run: no files were written.");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            Generates Stride asset files (.sdsnd) for raw resources in a code-only project and
            registers them in the project's .sdpkg so the asset compiler picks them up.

            Usage:
              Stride.CommunityToolkit.AssetGenerator [options]

            Options:
              --project-dir <path>        Project directory. Default: current directory.
              --project-name <name>       Project name, used to locate <name>.sdpkg.
                                          Default: the project directory's name.
              --package <path>            Explicit .sdpkg path. Overrides --project-name.
              --assets-folder <name>      Folder for generated assets. Default: Assets.
              --resources-folder <name>   Folder holding raw files. Default: Resources.
              --no-resource-folder        Do not add the resources folder to ResourceFolders.
              --sample-rate <hz>          Sample rate for new sound assets. Default: 44100.
              --compression-ratio <n>     Compression ratio for new sound assets. Default: 10.
              --stream-from-disk          Set StreamFromDisk on new sound assets.
              --spatialized               Set Spatialized on new sound assets.
              --dry-run                   Report what would change without writing.
              --verbose                   Also report skipped files.
              -h, --help                  Show this help.

            Existing asset files are never overwritten and nothing is ever deleted.
            """);
    }

    /// <summary>
    /// Minimal <c>--name value</c> / <c>--flag</c> parser.
    /// </summary>
    private sealed class CommandLine
    {
        private readonly Dictionary<string, string?> _values = new(StringComparer.OrdinalIgnoreCase);

        public static CommandLine Parse(string[] args)
        {
            var commandLine = new CommandLine();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (!arg.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"Unexpected argument '{arg}'. Run with --help for usage.");
                }

                var name = arg[2..];

                if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
                {
                    commandLine._values[name] = args[++i];
                }
                else
                {
                    commandLine._values[name] = null;
                }
            }

            return commandLine;
        }

        public bool Flag(string name) => _values.ContainsKey(name);

        public string? Value(string name) => _values.GetValueOrDefault(name);

        public int Int(string name, int fallback)
            => Value(name) is { Length: > 0 } text
                ? int.Parse(text, CultureInfo.InvariantCulture)
                : fallback;

        public AssetGeneratorOptions ToOptions()
        {
            var projectDirectory = Path.GetFullPath(Value("project-dir") ?? Directory.GetCurrentDirectory());

            var projectName = Value("project-name")
                ?? new DirectoryInfo(projectDirectory).Name;

            return new AssetGeneratorOptions
            {
                ProjectDirectory = projectDirectory,
                ProjectName = projectName,
                PackageFilePath = Value("package"),
                AssetsFolder = Value("assets-folder") ?? "Assets",
                ResourcesFolder = Value("resources-folder") ?? "Resources",
                EnsureResourceFolder = !Flag("no-resource-folder"),
                DryRun = Flag("dry-run"),
                Sound = new SoundAssetOptions
                {
                    SampleRate = Int("sample-rate", 44100),
                    CompressionRatio = Int("compression-ratio", 10),
                    StreamFromDisk = Flag("stream-from-disk"),
                    Spatialized = Flag("spatialized")
                }
            };
        }
    }
}
