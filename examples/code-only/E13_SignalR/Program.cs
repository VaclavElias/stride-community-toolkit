using E13_SignalR.Net;
using E13_SignalR.SignalR;
using E13_SignalR.Station;
using E13_SignalR_Shared;
using Stride.CommunityToolkit.Engine;
using Stride.CommunityToolkit.Scripts.Utilities;
using Stride.CommunityToolkit.Windows;
using Stride.Core.Diagnostics;
using Stride.Core.Mathematics;
using Stride.Engine;
using Stride.Games;
using Stride.Input;

// An orbital cargo deck with two consoles: this window, and a web page. A hatch above the deck
// releases containers in three sizes and six rusty paints; they drop under physics, skid, stack,
// and sometimes slide off the open edge and are lost. Either console can release, clear, shake and
// recolour; the game reports every release, landing and loss back, plus a census once a second.
//
// The hub is optional. The game connects in the background and keeps trying; with no hub the
// keyboard does everything and the overlay says LINK OFFLINE. Start the Blazor host to see the
// other console: E13_SignalR_Blazor, then open its page.
//
// Keys: 1 2 3 release small, medium, large - SPACE random - B batch of ten - C clear - X shake -
// T scheme list, then 1-5. Right-drag and WASD fly the camera.

const float HeartbeatSeconds = 1f;

var console = new StationConsole();
var link = new StationLink(ResolveHubUrl(), new StrideLoggerAdapter<SignalRHubClient>(GlobalLogger.GetLogger("SignalR")));

// Not `using`: the disposal order at the bottom is deliberate, and `using` would reverse it
var game = new Game();

var station = new StationScene(game);
var deck = new Deck(new ContainerFactory(game));
var commands = new StationCommands(deck, console);

var uptime = 0f;
var untilHeartbeat = HeartbeatSeconds;

WindowsDpiManager.EnablePerMonitorV2();

game.Run(start: Start, update: Update);

// Shutdown order is load-bearing, and so is the fact that neither line awaits.
//
// Game.Dispose tears down the graphics device, which has to happen on the thread that created it.
// Top-level statements run on the main thread only until their first await, and an awaited
// DisposeAsync resumes on a thread-pool thread - so `await using` here, or a `using var game`
// disposed after it, would tear the device down off the main thread and hang the process with the
// window still on screen. Blocking instead keeps both disposals on this thread, and closes the hub
// connection while the game's logger is still alive to report it.
link.DisposeAsync().AsTask().GetAwaiter().GetResult();

game.Dispose();

void Start(Scene scene)
{
    game.Window.Title = $"{Constants.StationName} - Stride + SignalR";
    game.Window.AllowUserResizing = true;
    station.Build(scene);

    // The deck tells the console log and the hub what happened; it does not know either exists
    deck.Released += container =>
    {
        station.Pulse();
        console.Note($"Released {Describe(container)} from {container.Origin}");
        link.ReportReleased(container);
    };

    deck.Landed += container =>
    {
        console.Note($"Landed {Describe(container)} after {container.AirTime:0.0} s");
        link.ReportLanded(container);
    };

    deck.Lost += container =>
    {
        console.Note($"Lost {Describe(container)} over the edge");
        link.ReportLost(container);
    };

    deck.Cleared += removed =>
    {
        console.Note($"Deck cleared, {removed} removed");
        link.ReportCleared(removed);
    };

    // Raised for a choice made here or one that came from the web; reporting it either way is what
    // keeps every open browser tab in the same scheme as the game
    console.SchemeChanged += scheme =>
    {
        console.Note($"Scheme {scheme.Name}");
        link.ReportScheme(scheme.Name);
    };

    DebugOverlay.GetOrCreate(game).AddSection("Station", OverlayLines);

    link.BeginConnect();
}

void Update(Scene scene, GameTime time)
{
    var deltaSeconds = (float)time.Elapsed.TotalSeconds;

    uptime += deltaSeconds;

    // Web requests first, on this thread, before anything reads the deck this frame
    link.Drain(commands);

    console.Update(game.Input, uptime);

    HandleKeys();

    deck.Update(deltaSeconds);

    station.Draw(console, deltaSeconds, uptime);

    untilHeartbeat -= deltaSeconds;

    if (untilHeartbeat <= 0f)
    {
        untilHeartbeat = HeartbeatSeconds;

        link.ReportDeck(deck.Snapshot(console.Scheme.Name, uptime));
    }
}

void HandleKeys()
{
    var input = game.Input;

    // While the scheme list is open the digits belong to it
    if (!console.IsMenuOpen)
    {
        if (input.IsKeyPressed(Keys.D1)) deck.Release(new ReleaseRequest(ContainerSize.Small), CommandOrigin.Game);
        if (input.IsKeyPressed(Keys.D2)) deck.Release(new ReleaseRequest(ContainerSize.Medium), CommandOrigin.Game);
        if (input.IsKeyPressed(Keys.D3)) deck.Release(new ReleaseRequest(ContainerSize.Large), CommandOrigin.Game);
    }

    if (input.IsKeyPressed(Keys.Space)) deck.Release(new ReleaseRequest(), CommandOrigin.Game);
    if (input.IsKeyPressed(Keys.B)) deck.ReleaseBatch(Constants.BatchSize, CommandOrigin.Game);
    if (input.IsKeyPressed(Keys.C)) deck.Clear();
    if (input.IsKeyPressed(Keys.X)) deck.Shake();
}

IReadOnlyList<TextElement> OverlayLines()
{
    var accent = console.Accent;
    var text = console.Text;
    var snapshot = deck.Snapshot(console.Scheme.Name, uptime);

    List<TextElement> lines =
    [
        new(Constants.StationName.ToUpperInvariant(), accent),
        new(link.IsConnected ? "LINK ONLINE  - web console connected" : "LINK OFFLINE - looking for the hub, keyboard still works", link.IsConnected ? Color.LightGreen : Color.Orange),
        new(string.Empty),
        new($"On deck {snapshot.OnDeck,3}   released {snapshot.Released,3}   lost {snapshot.Lost,3}   mass {snapshot.TotalMass,5:0.0} t", text),
        new($"Small {snapshot.BySize[0],3}   medium {snapshot.BySize[1],3}   large {snapshot.BySize[2],3}" + (deck.PendingCount > 0 ? $"   dropping {deck.PendingCount} more" : string.Empty), text),
        new(string.Empty),
        new("1 2 3 release small/medium/large   SPACE random   B batch of ten", Color.LightGray),
        new("C clear the deck   X shake the deck", Color.LightGray),
        new(string.Empty),
    ];

    lines.AddRange(console.MenuLines());
    lines.Add(new(string.Empty));

    if (console.Hail is { } hail)
    {
        lines.Add(new($"HAIL FROM WEB: {hail}", Color.Yellow));
        lines.Add(new(string.Empty));
    }

    foreach (var entry in console.Log)
    {
        lines.Add(new(entry, Hex.WithAlpha(text, 170)));
    }

    return lines;
}

static string Describe(ContainerEvent container) => $"#{container.Id} {container.Size.ToString().ToLowerInvariant()} {container.Paint.ToString().ToLowerInvariant()}";

/// <summary>The hub URL: the shared default, unless the environment says otherwise.</summary>
static Uri ResolveHubUrl()
{
    var overridden = Environment.GetEnvironmentVariable(Constants.HubUrlEnvironmentVariable);

    return Uri.TryCreate(overridden, UriKind.Absolute, out var url) ? url : new Uri(Constants.HubBaseUrl, Constants.HubPath);
}

/*
---example-metadata
slug: stride-signalr
title:
  en: Stride + SignalR - Orbital Cargo Deck
level: Advanced
category: Networking
complexity: 4
order: 160
description:
  en: |-
    A Stride game and a Blazor web page as two consoles of the same orbital cargo deck, talking both
    ways over a SignalR hub. Either console releases rusty cargo containers in three sizes through a
    hatch, clears the deck, shakes it, or switches the colour scheme, and the game reports every
    release, landing and loss back to the page along with a census once a second. The hub is
    optional: the game connects in the background, keeps retrying, and does everything from the
    keyboard when there is no hub at all. Two processes and a server make this the most involved
    example in the toolkit to run - start the Blazor app first so the hub exists, then the game.
concepts:
  - Connecting a Stride game to a SignalR hub as an optional feature that retries in the background
  - Typed contracts shared by game, hub and page, with nameof on every method name
  - "Marshalling hub callbacks onto the game thread: handlers enqueue, the update loop drains"
  - One ordered background queue for everything the game sends
  - A periodic snapshot so a page that opens late is complete within a second
  - "Two-way state: a colour scheme changed on either side changes the other"
  - Detecting landings and losses from body velocity and position, without contact handlers
  - Shutting a game down cleanly with a live network connection
tags:
  - 3D
  - Networking
  - SignalR
  - Blazor
  - Real Time
  - Multi Project
  - ShapeBatch
related:
  - E13_SignalR_Blazor
media: stride-game-engine-example17-signalr.webp
tocName: Stride + SignalR
screenshot: false
enabled: true
created: 2025-05-04
---
*/