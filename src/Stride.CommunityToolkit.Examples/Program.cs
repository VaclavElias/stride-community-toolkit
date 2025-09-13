using Pastel;
using Stride.CommunityToolkit.Examples.Core;
using System.Drawing;
using System.Linq;

var examples = new ExampleProvider().GetExamples();

DisplayMenu();

while (true)
{
    HandleUserInput();
}

void DisplayMenu()
{
    Console.Clear();

    Console.WriteLine("Stride Community Toolkit Examples".Pastel(Color.LightBlue));
    Console.WriteLine();

    var maxIdWidth = examples.Max(e => e.Id.Length);

    // Build a consistent color for each category using the same hue with different lightness
    var categories = examples
        .Select(e => e.Category)
        .Where(c => !string.IsNullOrWhiteSpace(c))
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
        .ToList();

    var categoryColors = BuildCategoryPalette(categories);

    foreach (var example in examples)
    {
        var idPadded = example.Id.PadLeft(maxIdWidth);
        var left = Navigation($"[{idPadded}]");

        var categoryLabel = example.Category is { Length: > 0 } cat
            ? $" [{cat}]".Pastel(categoryColors[cat])
            : string.Empty;

        // Slightly lighter color for project name so it's visually related to category
        var right = example.ProjectName is { Length: > 0 } pn
            ? (example.Category is { Length: > 0 } cat2
                ? $" ({pn})".Pastel(Lighten(categoryColors[cat2], 0.18f))
                : $" ({pn})".Pastel(Color.LightGray))
            : string.Empty;

        Console.WriteLine($"{left}{categoryLabel} {example.Title}{right}");
    }

    Console.WriteLine();
}

void HandleUserInput()
{
    Console.WriteLine($"Enter example id and press {"ENTER".Pastel(Color.FromArgb(165, 229, 250))} to run it.");
    Console.WriteLine("(Debug output may appear; you can ignore it and type another id at any time.)".Pastel(Color.GreenYellow));
    Console.Write("Choice: ");

    var choice = Console.ReadLine() ?? "";

    var example = examples.Find(x => string.Equals(x.Id, choice, StringComparison.OrdinalIgnoreCase));

    if (example is null)
    {
        Console.WriteLine("Invalid choice. Try again.".Pastel(Color.Red));
    }
    else
    {
        example.Action();

        if (example.Title == Constants.Clear)
        {
            DisplayMenu();
        }

        if (example.Title != Constants.Quit && example.Title != Constants.Clear)
        {
            Console.WriteLine("It might take a few moments to start the example...");
        }
    }

    Console.WriteLine();

}

static string Navigation(string text) => text.Pastel(Color.LightGreen);

static Dictionary<string, Color> BuildCategoryPalette(List<string> categories)
{
    var result = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
    if (categories.Count == 0) return result;

    // Choose a single hue (blue-ish) and vary lightness per category
    const double hue = 205; // degrees
    const double saturation = 0.75; // 0..1

    var minL = 0.35; // darker
    var maxL = 0.70; // lighter

    for (int i = 0; i < categories.Count; i++)
    {
        var t = categories.Count == 1 ? 0.5 : (double)i / (categories.Count - 1);
        var lightness = minL + (maxL - minL) * t;
        result[categories[i]] = HslToRgb(hue, saturation, lightness);
    }

    return result;
}

static Color HslToRgb(double h, double s, double l)
{
    h = (h % 360 + 360) % 360; // wrap
    s = Math.Clamp(s, 0, 1);
    l = Math.Clamp(l, 0, 1);

    double c = (1 - Math.Abs(2 * l - 1)) * s;
    double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
    double m = l - c / 2;

    (double r1, double g1, double b1) = h switch
    {
        < 60 => (c, x, 0.0),
        < 120 => (x, c, 0.0),
        < 180 => (0.0, c, x),
        < 240 => (0.0, x, c),
        < 300 => (x, 0.0, c),
        _ => (c, 0.0, x)
    };

    int r = (int)Math.Round((r1 + m) * 255);
    int g = (int)Math.Round((g1 + m) * 255);
    int b = (int)Math.Round((b1 + m) * 255);

    return Color.FromArgb(r, g, b);
}

static Color Lighten(Color color, float by)
{
    by = Math.Clamp(by, -1f, 1f);
    int r = (int)Math.Clamp(color.R + 255 * by, 0, 255);
    int g = (int)Math.Clamp(color.G + 255 * by, 0, 255);
    int b = (int)Math.Clamp(color.B + 255 * by, 0, 255);
    return Color.FromArgb(r, g, b);
}