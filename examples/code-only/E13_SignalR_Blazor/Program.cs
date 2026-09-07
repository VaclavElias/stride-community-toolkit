using E13_SignalR_Blazor.Components;
using E13_SignalR_Blazor.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// The same route the game resolves against Constants.HubBaseUrl
app.MapHub<StationHub>(Constants.HubPath);

app.Run();
/*
---example-metadata
slug: stride-signalr-server
title:
  en: Stride + SignalR - Blazor Server
level: Advanced
category: Networking
complexity: 3
order: 170
description:
  en: |-
    The other half of the SignalR example: a minimal Blazor server hosting the hub, and the web
    console of the orbital cargo deck - release, clear, shake and scheme buttons, a live census and
    an event feed of what the game reports. The hub itself is a relay with no state. It is an
    ASP.NET host rather than a Stride app, so there is no scene and nothing to render - it is listed
    here only because it has to be started first, and it is documented as part of stride-signalr
    rather than on a page of its own.
concepts:
  - Hosting a SignalR hub in a minimal Blazor server app
  - A strongly typed hub that implements the shared contract and relays with Clients.Others
  - A Blazor page as a hub client, with automatic reconnect
  - Deriving "game online" from the age of the last heartbeat
  - A colour scheme shared with the game as CSS variables
  - "Start this before the Stride app: the game connects to the hub"
tags:
  - Networking
  - SignalR
  - Blazor
  - ASP.NET
  - Server
  - Multi Project
related:
  - E13_SignalR
docs: false
screenshot: false
enabled: true
created: 2025-05-04
---
*/