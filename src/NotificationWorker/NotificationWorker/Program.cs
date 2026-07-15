using NotificationWorker;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

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

// ── OpenTelemetry ──────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName:    "notification-worker",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(
                builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
        }));

var host = builder.Build();
host.Run();