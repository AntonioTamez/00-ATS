using Eda.Inventory;
using Eda.Messaging;
using Eda.Notifications;
using Eda.Orders;

// Composition root: the only place that knows every participant exists.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMessaging();
builder.Services.AddOrdersModule();
builder.Services.AddInventoryModule();
builder.Services.AddNotificationsModule();
builder.Services.AddHostedService<PumpWorker>();

var app = builder.Build();

app.Services.ActivateSubscribers();     // every participant registers what it listens to

app.MapOrdersEndpoints();
app.MapInventoryEndpoints();
app.MapNotificationsEndpoints();

app.Run();

/// <summary>Stands in for "the broker delivers messages whenever it can": relays outboxes and delivers every 50 ms.</summary>
sealed class PumpWorker(IPump pump) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await pump.RunUntilIdleAsync(stoppingToken);
    }
}

public partial class Program;
