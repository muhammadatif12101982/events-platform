using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Instrumentation.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Orders.Api;
using Orders.Api.Features.Orders;
using Orders.Api.Features.Products;
using Orders.Api.Infrastructure;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication ─────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"];
        options.Audience  = "orders-api";
        options.RequireHttpsMetadata = false;
        options.TokenValidationParameters = new()
        {
            ValidIssuers =
            [
                "http://identity-server:5001",
                "http://localhost:5001"
            ]
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("orders.read",  p => p.RequireClaim("scope", "orders.read"));
    options.AddPolicy("orders.write", p => p.RequireClaim("scope", "orders.write"));
});

// ── RabbitMQ + Outbox Publisher ────────────────────────────────────
var rabbitSettings = builder.Configuration
    .GetSection("RabbitMQ")
    .Get<RabbitMqSettings>() ?? new RabbitMqSettings
    {
        Host     = "localhost",
        Username = "eventsuser",
        Password = "eventspass"
    };

builder.Services.AddSingleton(rabbitSettings);
builder.Services.AddHostedService<OutboxPublisher>();

// ── OpenTelemetry ──────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName:    "orders-api",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddSource("Orders.Api.OutboxPublisher")  // ← must be here
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddEntityFrameworkCoreInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(
                builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

// Expose /metrics for Prometheus
app.MapPrometheusScrapingEndpoint();

// ── Routes ─────────────────────────────────────────────────────────
app.MapGet("/products", ListProducts.Handle)
    .RequireAuthorization("orders.read");

app.MapPost("/products", CreateProduct.Handle)
    .RequireAuthorization("orders.write");

app.MapPost("/orders", CreateOrder.Handle)
    .RequireAuthorization("orders.write");

app.MapGet("/orders/{id:int}", GetOrder.Handle)
    .RequireAuthorization("orders.read");

app.Run();