using NotificationWorker;

var builder = Host.CreateApplicationBuilder(args);

// ── RabbitMQ Settings ──────────────────────────────────────────────
var rabbitSettings = builder.Configuration
    .GetSection("RabbitMQ")
    .Get<RabbitMqSettings>() ?? new RabbitMqSettings
    {
        Host     = "localhost",
        Username = "eventsuser",
        Password = "eventspass"
    };

builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();