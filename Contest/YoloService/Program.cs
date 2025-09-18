using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using YoloService.Services;
using YoloService.Handlers;

var builder = WebApplication.CreateBuilder(args);

// Configure services
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
});

builder.Services.AddSingleton<YoloDetectionService>();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseCors();
app.UseWebSockets();

// Initialize YOLO service
var yoloService = app.Services.GetRequiredService<YoloDetectionService>();
await yoloService.InitializeAsync();

// Register for graceful shutdown
app.Lifetime.ApplicationStopping.Register(() =>
{
    Console.WriteLine("Shutting down YOLO service...");
    yoloService.Dispose();
});

// WebSocket endpoint
app.Map("/ws", async context =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await WebSocketHandler.HandleWebSocket(webSocket, yoloService);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// Simple HTTP endpoint for testing
app.MapGet("/", () => "YOLO WebSocket Server is running! Connect to ws://localhost:5000/ws");

// Configure to listen only on localhost
app.Urls.Add("http://localhost:5000");

Console.WriteLine("YOLO WebSocket server starting on http://localhost:5000");
Console.WriteLine("WebSocket endpoint: ws://localhost:5000/ws");
Console.WriteLine("Press Ctrl+C to stop the server");

app.Run();
