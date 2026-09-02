using Stride.Engine;
using Stride.Engine.Design;
using Stride.Games;
using Stride.Graphics;
using Stride.Rendering;
using Stride.Streaming;

namespace Stride.CommunityToolkit.Engine;

/// <summary>
/// What a Game Studio project gets from its <c>GameSettings</c> asset, made available to a game
/// that has none.
/// </summary>
/// <remarks>
/// <para>
/// The engine reads a <see cref="GameSettings"/> asset in <c>Game.PrepareContext</c> and, only if it
/// finds one, registers itself as the <see cref="IGameSettingsService"/>, applies the rendering
/// settings to the graphics device manager, sets the shader compilation mode and the streaming
/// settings. Several subsystems then read the service on their own: <c>AudioSystem</c> for HRTF,
/// <c>Bullet2PhysicsSystem</c> for <c>PhysicsSettings</c>, <c>BepuConfiguration</c> for its
/// simulations, <c>DynamicNavigationMeshSystem</c> for <c>NavigationSettings</c>. A code-only game
/// has no asset, so none of that happens and every one of those subsystems runs on its fallback.
/// </para>
/// <para>
/// <see cref="UseGameSettings"/> closes that gap: it builds a <see cref="GameSettings"/> in code,
/// registers it as the service, and applies it the way the engine would have. Call it before
/// <c>Run</c>.
/// </para>
/// </remarks>
public static class GameSettingsExtensions
{
    /// <summary>
    /// Registers a code-built <see cref="GameSettings"/> for a game that has no <c>GameSettings</c>
    /// asset, and applies it the way the engine applies the asset. Must be called before <c>Run</c>.
    /// </summary>
    /// <param name="game">The game to configure.</param>
    /// <param name="configure">
    /// Adjusts the settings before they are registered. The settings start empty - no
    /// configurations, and <see cref="GameSettings.CompilationMode"/> at its default - so nothing
    /// changes unless it is set here; add a configuration with
    /// <see cref="GameSettings.GetOrCreateConfiguration{T}"/>. Optional.
    /// </param>
    /// <returns>The registered settings, for reading back or adjusting further before <c>Run</c>.</returns>
    /// <remarks>
    /// <para>
    /// What gets applied, and when:
    /// <list type="bullet">
    ///   <item><description>
    ///   The settings are registered as the <see cref="IGameSettingsService"/> immediately, so the
    ///   audio, physics and navigation systems find them when they initialise inside <c>Run</c>.
    ///   </description></item>
    ///   <item><description>
    ///   A <see cref="RenderingSettings"/> configuration, if one was added, is applied to the
    ///   <see cref="GraphicsDeviceManager"/> immediately - graphics profile, back buffer size and
    ///   colour space - mirroring <c>Game.PrepareContext</c>. As in the engine, the back buffer and
    ///   colour space are only applied while <see cref="Game.AutoLoadDefaultSettings"/> is
    ///   <see langword="true"/>.
    ///   </description></item>
    ///   <item><description>
    ///   <see cref="GameSettings.CompilationMode"/> and a <see cref="StreamingSettings"/>
    ///   configuration, if one was added, are applied when the engine raises
    ///   <see cref="Game.GameStarted"/> - the point at which the <see cref="EffectSystem"/> exists
    ///   and nothing has compiled yet.
    ///   </description></item>
    /// </list>
    /// </para>
    /// <para>
    /// On the compilation mode, measured rather than assumed: on Direct3D 11 the compiler applies the
    /// optimisation level only when the mode turns debug information off, so
    /// <see cref="CompilationMode.Debug"/> (the engine's default for a game without settings) and
    /// <see cref="CompilationMode.Release"/> produce identical bytecode. Only
    /// <see cref="CompilationMode.AppStore"/> changes the output - optimisation level 2 with the
    /// debug symbols stripped, which also removes shader source from tools such as RenderDoc. Vulkan
    /// and Direct3D 12 consume the SPIR-V directly and ignore the mode. Bytecode is cached per mode,
    /// so changing it compiles every shader once more on the next run.
    /// </para>
    /// <para>
    /// Asset URLs on the settings - default scene, graphics compositor, splash screen - are not
    /// applied: a code-only game builds those in code, and the toolkit's scene helpers are the way
    /// to do it.
    /// </para>
    /// <para>
    /// This is for games without a <c>GameSettings</c> asset. A project that has one already
    /// registers the engine's own service, and <c>Run</c> will then fail with "Service is already
    /// registered" - use the asset instead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="game"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    /// The game is already running, or an <see cref="IGameSettingsService"/> is already registered.
    /// </exception>
    /// <example>
    /// <code>
    /// using var game = new Game();
    ///
    /// game.UseGameSettings(settings =>
    /// {
    ///     settings.GetOrCreateConfiguration&lt;AudioEngineSettings&gt;().HrtfSupport = true;      // Windows only; ignored on OpenAL
    ///     settings.GetOrCreateConfiguration&lt;RenderingSettings&gt;().DefaultBackBufferWidth = 1600;
    ///     settings.CompilationMode = CompilationMode.AppStore;                              // shipping build: optimised, no shader symbols
    /// });
    ///
    /// game.Run(start: Start);
    /// </code>
    /// </example>
    public static GameSettings UseGameSettings(this Game game, Action<GameSettings>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(game);

        if (game.IsRunning)
            throw new InvalidOperationException($"{nameof(UseGameSettings)} must be called before the game runs: the settings are read while the engine initialises.");

        if (game.Services.GetService<IGameSettingsService>() is not null)
            throw new InvalidOperationException($"An {nameof(IGameSettingsService)} is already registered; {nameof(UseGameSettings)} can only be called once, and only for a game without a GameSettings asset.");

        var settings = new GameSettings();

        configure?.Invoke(settings);

        game.Services.AddService<IGameSettingsService>(new CodeOnlyGameSettingsService(settings));

        ApplyRenderingSettings(game, settings);

        OnGameStarted(game, startedGame =>
        {
            startedGame.EffectSystem.SetCompilationMode(settings.CompilationMode);

            // The engine only pushes streaming settings when an asset exists; with none, the manager's own
            // defaults stand. Same rule here: only a configuration the caller added is pushed.
            if (settings.Configurations.OfType<StreamingSettings>().FirstOrDefault() is { } streaming)
                startedGame.Streaming.SetStreamingSettings(streaming);
        });

        return settings;
    }

    /// <summary>
    /// The half of <c>Game.PrepareContext</c> that can be reproduced from outside: rendering settings
    /// onto the device manager, before the device is created.
    /// </summary>
    private static void ApplyRenderingSettings(Game game, GameSettings settings)
    {
        if (settings.Configurations.OfType<RenderingSettings>().FirstOrDefault() is not { } rendering)
            return;

        if (game.GraphicsDeviceManager is not GraphicsDeviceManager deviceManager)
            return;

        if (rendering.DefaultGraphicsProfile > 0)
        {
            deviceManager.ShaderProfile ??= rendering.DefaultGraphicsProfile;

            if (game.AutoLoadDefaultSettings)
                deviceManager.PreferredGraphicsProfile = [rendering.DefaultGraphicsProfile];
        }

        if (!game.AutoLoadDefaultSettings)
            return;

        if (rendering.DefaultBackBufferWidth > 0) deviceManager.PreferredBackBufferWidth = rendering.DefaultBackBufferWidth;
        if (rendering.DefaultBackBufferHeight > 0) deviceManager.PreferredBackBufferHeight = rendering.DefaultBackBufferHeight;

        deviceManager.PreferredColorSpace = rendering.ColorSpace;
    }

    /// <summary>
    /// Runs <paramref name="action"/> once, when <paramref name="game"/> raises the static
    /// <see cref="Game.GameStarted"/> event.
    /// </summary>
    /// <remarks>
    /// The event is static and fires for every game in the process, so the handler filters on the
    /// sender and unsubscribes itself - a game created after this one must not see a stale handler.
    /// </remarks>
    private static void OnGameStarted(Game game, Action<Game> action)
    {
        Game.GameStarted += Handler;

        void Handler(object? sender, EventArgs e)
        {
            if (!ReferenceEquals(sender, game))
                return;

            Game.GameStarted -= Handler;

            action(game);
        }
    }

    /// <summary>
    /// What the engine registers when it finds a <c>GameSettings</c> asset, for settings built in code.
    /// </summary>
    private sealed class CodeOnlyGameSettingsService : IGameSettingsService
    {
        // Explicit rather than a primary constructor: a primary constructor is always public, and a
        // public member on a private type is exactly what the analyzer flags.
        internal CodeOnlyGameSettingsService(GameSettings settings)
        {
            Settings = settings;
        }

        public GameSettings Settings { get; }
    }
}