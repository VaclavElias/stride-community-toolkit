using E13_SignalR;
using E13_SignalR.Core;
using E13_SignalR.Services;
using E13_SignalR.SignalR;
using Stride.CommunityToolkit.Engine;
using Stride.Core.Diagnostics;
using Stride.Engine;

var game = new Game();

// Create a Stride logger to surface .NET logs into Stride's console
var strideLogger = GlobalLogger.GetLogger("SignalR");
var loggerAdapter = new StrideLoggerAdapter<SignalRHubClient>(strideLogger);

var hubUrl = new Uri(GameSettings.HubBaseUrl, GameSettings.HubPath);

// Owned here rather than only registered: the service holds a live hub connection, and disposing it
// closes that connection on the way out. Declared after the game so it is released before it.
var screenService = new ScreenService(hubUrl, loggerAdapter);

game.Services.AddService(screenService);

game.Run(start: (Scene rootScene) =>
{
    SceneBuilder.Build(game, rootScene);
});

// Shutdown order is load-bearing, and so is the fact that neither line awaits.
//
// Game.Dispose tears down the graphics device, which has to happen on the thread that created it.
// Top-level statements run on the main thread only until their first await, and an awaited
// DisposeAsync resumes on a thread-pool thread - so `await using` here, or a `using var game`
// disposed after it, would tear the device down off the main thread and hang the process with the
// window still on screen. Blocking instead keeps both disposals on this thread, and closes the hub
// connection while the game's logger is still alive to report it.
screenService.DisposeAsync().AsTask().GetAwaiter().GetResult();

game.Dispose();
/*
---example-metadata
slug: stride-signalr
title:
  en: Stride + SignalR
level: Advanced
category: Networking
complexity: 4
order: 160
description:
  en: |-
    A Stride game and a Blazor web page talking to each other in real time over a SignalR hub, so a
    button in a browser can spawn an entity in the running game and the game can report back. Two
    processes and a server make this the most involved example in the toolkit to run: start the Blazor
    app first so the hub exists, then start the game, which connects to it.
concepts:
  - Connecting a Stride app to a SignalR hub
  - Registering handlers for incoming messages
  - Sending events from the game to a web client
  - Driving scene changes from a remote command
  - "Marshalling hub callbacks onto the game loop before touching the scene"
  - Sharing message contracts through a common project
tags:
  - 3D
  - Networking
  - SignalR
  - Blazor
  - Real Time
  - Multi Project
related:
  - E13_SignalR_Blazor
media: stride-game-engine-example17-signalr.webp
tocName: Stride + SignalR
screenshot: false
enabled: true
created: 2025-05-04
---
*/