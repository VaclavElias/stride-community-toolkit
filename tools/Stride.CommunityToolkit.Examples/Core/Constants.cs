namespace Stride.CommunityToolkit.Examples.Core;

/// <summary>
/// Menu commands that are not examples.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Clears the console, tolerating a redirected output stream.
    /// </summary>
    /// <remarks>
    /// <see cref="Console.Clear"/> throws "The handle is invalid" when stdout is piped to a file or
    /// another process, which crashed the runner on startup before it printed anything at all.
    /// </remarks>
    public static void SafeClear()
    {
        try { Console.Clear(); } catch (IOException) { /* redirected output has nothing to clear */ }
    }

    /// <summary>Redraws the menu.</summary>
    public const string Clear = "Clear";

    /// <summary>Exits the runner.</summary>
    public const string Quit = "Quit";
}