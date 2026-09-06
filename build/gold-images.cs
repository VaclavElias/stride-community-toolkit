#:package SixLabors.ImageSharp@3.1.12

// Gold-image regression for the code-only examples.
//
//   dotnet run --file build/gold-images.cs                                   compare every golden in tests/gold
//   dotnet run --file build/gold-images.cs -- --only shape-batch             compare one
//   dotnet run --file build/gold-images.cs -- --only shape-batch --update    capture and make it the golden
//   dotnet run --file build/gold-images.cs -- --only shape-batch --noise     capture twice, report run-to-run drift
//   dotnet run --file build/gold-images.cs -- --only junkyard-box2d --frame 300 --update
//
// The --file switch is required: the repository root contains Stride.CommunityToolkit.ndproj, and
// without it the SDK runs that project and passes this script to it as an argument.
//
// WHAT IT IS FOR. A shader or renderer change that is meant to be invisible (a refactor) or meant
// to be visible in a known way (an anti-aliasing profile, a colour curve) needs more than a pair
// of screenshots and a squint. This captures the example the same way the documentation
// screenshots are taken - in-engine, at a fixed frame, on a fixed timestep (see ScreenshotCapture
// in src/Stride.CommunityToolkit/Engine) - and compares the pixels against a golden PNG committed
// under tests/gold, the way Stride's own graphics tests do: the per-pixel maximum channel
// difference goes into a histogram and a rule says how many pixels may land in each bucket.
// The default rule is Stride's - any pixel differing by 3 or more fails - and
// tests/gold/thresholds.jsonc relaxes it per image where a scene cannot be made deterministic.
//
// WHAT IT WRITES. Every run leaves the new capture, a diff mask and a side-by-side contact sheet
// under screenshots-review/gold (gitignored). --update copies the capture over the golden;
// nothing is committed by this script.
//
// DETERMINISM. Frame N on a fixed timestep with one update per draw is the same simulated instant
// every run; the remaining sources of drift are physics engines stepping on worker threads, GPU
// driver differences between machines, and the window size, which is checked and reported.
// STRIDE_GRAPHICS_SOFTWARE_RENDERING=1 (--warp) selects the WARP software adapter, the way
// Stride's own graphics tests run, for goldens that must match across machines; without it the
// real GPU is used, which is deterministic on one machine and much faster.

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

var only = new List<string>();
var update = false;
var noise = false;
var warp = false;
int? frameOverride = null;
var timeout = TimeSpan.FromMinutes(5);

for (var i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--only" when i + 1 < args.Length:
            only.Add(args[++i]);
            break;
        case "--update":
            update = true;
            break;
        case "--noise":
            noise = true;
            break;
        case "--warp":
            warp = true;
            break;
        case "--frame" when i + 1 < args.Length && int.TryParse(args[i + 1], out var f):
            frameOverride = f;
            i++;
            break;
        case "--timeout" when i + 1 < args.Length && int.TryParse(args[i + 1], out var minutes):
            timeout = TimeSpan.FromMinutes(minutes);
            i++;
            break;
        default:
            Console.Error.WriteLine($"Unknown argument: {args[i]}");
            return 1;
    }
}

if (update && only.Count == 0)
{
    Console.Error.WriteLine("--update needs --only <slug>: rewriting every golden in one unattended run is not something to do by accident.");
    return 1;
}

var root = RepositoryRoot();
var manifestPath = Path.Combine(root, "tools", "Stride.CommunityToolkit.Examples.Launcher", "examples-manifest.json");
var goldDirectory = Path.Combine(root, "tests", "gold");
var reviewDirectory = Path.Combine(root, "screenshots-review", "gold");
var stagingDirectory = Path.Combine(root, "bin", "screenshots");

if (!File.Exists(manifestPath))
{
    Console.Error.WriteLine($"No manifest at {manifestPath}. Build the launcher first, or run the generator's 'generate' command.");
    return 1;
}

Directory.CreateDirectory(goldDirectory);
Directory.CreateDirectory(reviewDirectory);
Directory.CreateDirectory(stagingDirectory);

using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
var examples = document.RootElement.GetProperty("examples").EnumerateArray()
    .Where(e => Text(e, "slug") is not null && Text(e, "projectPath") is not null)
    .ToDictionary(e => Text(e, "slug")!, StringComparer.OrdinalIgnoreCase);

// Without --only the suite is whatever has a golden: the goldens define what is under test.
var slugs = only.Count > 0
    ? only
    : Directory.EnumerateFiles(goldDirectory, "*.png").Select(Path.GetFileNameWithoutExtension).Select(s => s!).OrderBy(s => s).ToList();

if (slugs.Count == 0)
{
    Console.WriteLine($"No goldens in {goldDirectory}. Make one with --only <slug> --update.");
    return 0;
}

var rules = LoadRules(Path.Combine(goldDirectory, "thresholds.jsonc"));
var results = new List<Result>();

foreach (var slug in slugs)
{
    if (!examples.TryGetValue(slug, out var example))
    {
        Console.Error.WriteLine($"  ✖ {slug}: not in the manifest");
        results.Add(new Result(slug, Outcome.Error, "not in the manifest"));
        continue;
    }

    var frame = frameOverride ?? Int(example, "screenshotFrame");
    var capturePath = Path.Combine(reviewDirectory, $"{slug}.png");
    var goldPath = Path.Combine(goldDirectory, $"{slug}.png");

    Console.WriteLine($"  · {slug} ({Text(example, "projectName")}, frame {frame?.ToString() ?? "default"})");

    if (!Capture(root, Text(example, "projectPath")!, capturePath, frame, warp, timeout, out var failure))
    {
        Console.Error.WriteLine($"    ✖ {failure}");
        results.Add(new Result(slug, Outcome.Error, failure));
        continue;
    }

    if (noise)
    {
        // A second capture of the same thing: what the harness would see with no change at all.
        var secondPath = Path.Combine(reviewDirectory, $"{slug}-second.png");

        if (!Capture(root, Text(example, "projectPath")!, secondPath, frame, warp, timeout, out failure))
        {
            Console.Error.WriteLine($"    ✖ {failure}");
            results.Add(new Result(slug, Outcome.Error, failure));
            continue;
        }

        var drift = Compare(capturePath, secondPath, Resolve(rules, slug), Path.Combine(reviewDirectory, $"{slug}-noise.png"));

        Console.WriteLine($"    run-to-run: {drift.Describe()}");
        results.Add(new Result(slug, drift.Passed ? Outcome.Pass : Outcome.Fail, drift.Describe(), drift));
        continue;
    }

    if (update)
    {
        File.Copy(capturePath, goldPath, overwrite: true);
        Console.WriteLine($"    ✅ golden written: tests/gold/{slug}.png");
        results.Add(new Result(slug, Outcome.Updated, "golden written"));
        continue;
    }

    if (!File.Exists(goldPath))
    {
        Console.Error.WriteLine($"    ✖ no golden at tests/gold/{slug}.png - run with --update to make one");
        results.Add(new Result(slug, Outcome.Error, "no golden"));
        continue;
    }

    var stats = Compare(capturePath, goldPath, Resolve(rules, slug), Path.Combine(reviewDirectory, $"{slug}-diff.png"));

    Console.WriteLine($"    {(stats.Passed ? "✅" : "✖")} {stats.Describe()}");
    results.Add(new Result(slug, stats.Passed ? Outcome.Pass : Outcome.Fail, stats.Describe(), stats));
}

Console.WriteLine();
Console.WriteLine($"Passed {results.Count(r => r.Outcome == Outcome.Pass)}, failed {results.Count(r => r.Outcome == Outcome.Fail)}, " +
                  $"updated {results.Count(r => r.Outcome == Outcome.Updated)}, errors {results.Count(r => r.Outcome == Outcome.Error)}.");

if (!update)
{
    var indexPath = Path.Combine(reviewDirectory, "index.html");

    File.WriteAllText(indexPath, BuildIndex(results, goldDirectory, noise), new UTF8Encoding(false));
    Console.WriteLine($"Contact sheet: {indexPath}");
}

return results.Any(r => r.Outcome is Outcome.Fail or Outcome.Error) ? 1 : 0;

// Runs one example with capture enabled and waits for it to exit on its own.
static bool Capture(string root, string projectPath, string pngPath, int? frame, bool warp, TimeSpan timeout, out string failure)
{
    failure = string.Empty;

    File.Delete(pngPath);

    var exampleDirectory = Path.GetDirectoryName(Path.Combine(root, "examples", "code-only", projectPath.Replace('/', Path.DirectorySeparatorChar)))!;
    var project = Directory.EnumerateFiles(exampleDirectory, "*.*proj").FirstOrDefault();

    // A file-based app has no project file and is run by naming its source directly.
    var arguments = project is not null
        ? $"run --project \"{project}\""
        : $"run \"{Path.Combine(root, "examples", "code-only", projectPath.Replace('/', Path.DirectorySeparatorChar))}\"";

    var startInfo = new ProcessStartInfo("dotnet", arguments)
    {
        WorkingDirectory = exampleDirectory,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    };

    startInfo.Environment["STRIDE_TOOLKIT_CAPTURE"] = pngPath;

    if (frame is { } value)
    {
        startInfo.Environment["STRIDE_TOOLKIT_CAPTURE_FRAME"] = value.ToString();
    }

    if (warp)
    {
        startInfo.Environment["STRIDE_GRAPHICS_SOFTWARE_RENDERING"] = "1";
    }

    using var process = Process.Start(startInfo);

    if (process is null)
    {
        failure = "could not start dotnet";
        return false;
    }

    // Drained so the pipes cannot fill and deadlock the child.
    _ = process.StandardOutput.ReadToEndAsync();
    _ = process.StandardError.ReadToEndAsync();

    if (!process.WaitForExit((int)timeout.TotalMilliseconds))
    {
        try { process.Kill(entireProcessTree: true); } catch { /* already gone */ }

        failure = $"did not exit within {timeout.TotalMinutes:0} minutes";
        return false;
    }

    if (!File.Exists(pngPath))
    {
        failure = "the example exited without writing a screenshot";
        return false;
    }

    return true;
}

// Stride's comparison, ported: the maximum channel difference of every pixel, counted into a
// histogram, and a rule saying how many pixels may fall into each bucket. Alpha is ignored: the
// saved render target carries whatever alpha the renderer left behind, which means nothing.
//
// The diff mask marks pixels that differ by 3 or more in red and by 1 or 2 in yellow, over a
// dimmed copy of the new capture, so a failure can be located without opening both images.
static Stats Compare(string actualPath, string expectedPath, AllowBucket[] buckets, string diffPath)
{
    using var actual = Image.Load<Rgba32>(actualPath);
    using var expected = Image.Load<Rgba32>(expectedPath);

    if (actual.Width != expected.Width || actual.Height != expected.Height)
    {
        return new Stats
        {
            Passed = false,
            SizeMismatch = $"{actual.Width}x{actual.Height} vs golden {expected.Width}x{expected.Height}"
        };
    }

    var histogram = new int[256];
    long squaredError = 0;
    var maxDiff = 0;

    // Where the pixels at 3 or more are, so a failure can be placed without opening the mask
    int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;

    using var mask = new Image<Rgba32>(actual.Width, actual.Height);

    for (var y = 0; y < actual.Height; y++)
    {
        for (var x = 0; x < actual.Width; x++)
        {
            var a = actual[x, y];
            var e = expected[x, y];
            var r = Math.Abs(a.R - e.R);
            var g = Math.Abs(a.G - e.G);
            var b = Math.Abs(a.B - e.B);
            var d = Math.Max(r, Math.Max(g, b));

            histogram[d]++;
            squaredError += (long)(r * r + g * g + b * b);
            maxDiff = Math.Max(maxDiff, d);

            if (d >= 3)
            {
                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
            }

            mask[x, y] = d >= 3
                ? new Rgba32(255, 40, 40, 255)
                : d > 0
                    ? new Rgba32(255, 220, 0, 255)
                    : new Rgba32((byte)(a.R / 4), (byte)(a.G / 4), (byte)(a.B / 4), 255);
        }
    }

    mask.SaveAsPng(diffPath);

    var pixels = actual.Width * actual.Height;
    var mse = pixels > 0 ? (double)squaredError / (pixels * 3) : 0;

    return new Stats
    {
        Pixels = pixels,
        Exact = histogram[0],
        Bucket1To2 = Sum(histogram, 1, 2),
        Bucket3To5 = Sum(histogram, 3, 5),
        Bucket6To15 = Sum(histogram, 6, 15),
        Bucket16Plus = Sum(histogram, 16, 255),
        MaxDiff = maxDiff,
        Bounds = maxX >= 0 ? $"x {minX}-{maxX}, y {minY}-{maxY}" : null,
        Psnr = mse > 0 ? 10.0 * Math.Log10(255.0 * 255.0 / mse) : double.PositiveInfinity,
        Passed = buckets.All(bucket => Sum(histogram, bucket.Min, Math.Min(bucket.Max, 255)) <= bucket.Limit)
    };

    static int Sum(int[] histogram, int from, int to)
    {
        var total = 0;

        for (var d = from; d <= to; d++) total += histogram[d];

        return total;
    }
}

// thresholds.jsonc: [{ "image": "junkyard-box2d", "allow": { "3-5": 2000, "6-15": 300, "16+": 50 } }]
// A bucket not listed for a matching rule is unlimited; an image with no rule gets the default,
// which allows nothing at 3 or above. Comments with // are stripped before parsing.
static Rule[] LoadRules(string path)
{
    if (!File.Exists(path)) return [];

    var json = Regex.Replace(File.ReadAllText(path), @"//.*?$", "", RegexOptions.Multiline);

    // Read by hand: file-based apps run with reflection serialisation disabled.
    using var document = JsonDocument.Parse(json);
    var rules = new List<Rule>();

    foreach (var element in document.RootElement.EnumerateArray())
    {
        var allow = new Dictionary<string, int>();

        if (element.TryGetProperty("allow", out var allowElement) && allowElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var pair in allowElement.EnumerateObject())
            {
                allow[pair.Name] = pair.Value.GetInt32();
            }
        }

        rules.Add(new Rule(Text(element, "image"), allow));
    }

    return rules.ToArray();
}

static AllowBucket[] Resolve(Rule[] rules, string slug)
{
    var rule = rules.FirstOrDefault(r => string.Equals(r.Image, slug, StringComparison.OrdinalIgnoreCase));

    if (rule?.Allow is null || rule.Allow.Count == 0)
    {
        return [new AllowBucket(3, int.MaxValue, 0)];
    }

    return rule.Allow.Select(pair => AllowBucket.Parse(pair.Key, pair.Value)).ToArray();
}

// One row per example: golden, new capture and the diff mask side by side, with the numbers.
static string BuildIndex(List<Result> results, string goldDirectory, bool noise)
{
    var html = new StringBuilder();

    html.Append("<!doctype html><meta charset=\"utf-8\"><title>Gold images</title>");
    html.Append("<style>body{font:14px system-ui;margin:16px;background:#111;color:#ddd}h2{margin:24px 0 4px}");
    html.Append(".row{display:grid;grid-template-columns:repeat(3,1fr);gap:8px}.row img{width:100%;background:#000}");
    html.Append(".pass{color:#7c6}.fail{color:#f66}.err{color:#fa4}.cap{color:#999;font-size:12px}</style>");
    html.Append($"<h1>Gold images - {DateTime.Now:yyyy-MM-dd HH:mm}</h1>");

    foreach (var result in results)
    {
        var cls = result.Outcome switch { Outcome.Pass => "pass", Outcome.Fail => "fail", _ => "err" };

        html.Append($"<h2>{result.Slug} <span class=\"{cls}\">{result.Outcome}</span></h2><div class=\"cap\">{Escape(result.Message)}</div>");

        if (result.Outcome is Outcome.Pass or Outcome.Fail)
        {
            var left = noise ? $"{result.Slug}.png" : Path.Combine(goldDirectory, $"{result.Slug}.png");
            var middle = noise ? $"{result.Slug}-second.png" : $"{result.Slug}.png";
            var right = noise ? $"{result.Slug}-noise.png" : $"{result.Slug}-diff.png";

            html.Append("<div class=\"row\">");
            html.Append($"<div><div class=\"cap\">{(noise ? "first run" : "golden")}</div><img src=\"{Uri(left)}\"></div>");
            html.Append($"<div><div class=\"cap\">{(noise ? "second run" : "new")}</div><img src=\"{Uri(middle)}\"></div>");
            html.Append($"<div><div class=\"cap\">diff (red 3+, yellow 1-2)</div><img src=\"{Uri(right)}\"></div>");
            html.Append("</div>");
        }
    }

    return html.ToString();

    static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;");
    static string Uri(string path) => Path.IsPathRooted(path) ? new Uri(path).AbsoluteUri : path;
}

static string? Text(JsonElement element, string name)
    => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

static int? Int(JsonElement element, string name)
    => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;

static string RepositoryRoot([CallerFilePath] string scriptPath = "")
    => Directory.GetParent(Path.GetDirectoryName(scriptPath)!)!.FullName;

enum Outcome { Pass, Fail, Updated, Error }

record Result(string Slug, Outcome Outcome, string Message, Stats? Stats = null);

record Rule(string? Image, Dictionary<string, int>? Allow);

readonly record struct AllowBucket(int Min, int Max, int Limit)
{
    public static AllowBucket Parse(string key, int limit)
    {
        if (key.EndsWith('+')) return new AllowBucket(int.Parse(key[..^1], CultureInfo.InvariantCulture), int.MaxValue, limit);

        var parts = key.Split('-');

        return parts.Length == 2
            ? new AllowBucket(int.Parse(parts[0], CultureInfo.InvariantCulture), int.Parse(parts[1], CultureInfo.InvariantCulture), limit)
            : new AllowBucket(int.Parse(key, CultureInfo.InvariantCulture), int.Parse(key, CultureInfo.InvariantCulture), limit);
    }
}

sealed class Stats
{
    public int Pixels;
    public int Exact;
    public int Bucket1To2;
    public int Bucket3To5;
    public int Bucket6To15;
    public int Bucket16Plus;
    public int MaxDiff;
    public string? Bounds;
    public double Psnr;
    public bool Passed;
    public string? SizeMismatch;

    public string Describe()
        => SizeMismatch is not null
            ? $"size mismatch: {SizeMismatch}"
            : $"exact {Percent(Exact)}, 1-2: {Bucket1To2}, 3-5: {Bucket3To5}, 6-15: {Bucket6To15}, 16+: {Bucket16Plus}, max {MaxDiff}, " +
              $"PSNR {(double.IsPositiveInfinity(Psnr) ? "inf" : Psnr.ToString("0.0", CultureInfo.InvariantCulture))} dB" +
              (Bounds is not null ? $", 3+ within {Bounds}" : "");

    private string Percent(int count) => Pixels == 0 ? "-" : (100.0 * count / Pixels).ToString("0.00", CultureInfo.InvariantCulture) + "%";
}