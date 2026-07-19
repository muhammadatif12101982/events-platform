using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

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

builder.Services.AddAuthorization();

// ── YARP ───────────────────────────────────────────────────────────
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// ── OpenTelemetry ──────────────────────────────────────────────────
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName:    "gateway",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation(o =>
        {
            o.RecordException = true;
        })
        .AddHttpClientInstrumentation()
        .AddOtlpExporter(o =>
        {
            o.Endpoint = new Uri(
                builder.Configuration["Otlp:Endpoint"] ?? "http://localhost:4317");
        }))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddPrometheusExporter());

// ── Rate Limiting ──────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Fixed window: 100 requests per minute per client IP
    options.AddFixedWindowLimiter("fixed", o =>
    {
        o.Window            = TimeSpan.FromMinutes(1);
        o.PermitLimit       = 100;
        o.QueueLimit        = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Sliding window: 20 requests per 10 seconds — burst protection
    options.AddSlidingWindowLimiter("sliding", o =>
    {
        o.Window              = TimeSpan.FromSeconds(10);
        o.PermitLimit         = 20;
        o.SegmentsPerWindow   = 2;
        o.QueueLimit          = 0;
    });

    // Return 429 Too Many Requests when limit exceeded
    options.RejectionStatusCode = 429;

    options.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync(
            "Rate limit exceeded. Please slow down.", ct);
    };
});

// ── CORS ───────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development: allow localhost on common dev ports
            policy.WithOrigins(
                    "http://localhost:3000",
                    "http://localhost:5173",
                    "http://localhost:4200")
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
        else
        {
            // Production: explicitly named origins only — no wildcards
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? [];

            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .WithMethods("GET", "POST", "PUT", "DELETE")
                .AllowCredentials();
        }
    });
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseCors("AllowedOrigins");  // ← add this
app.UseRateLimiter();  // ← add this

// Apply sliding window limiter to all proxied routes
app.MapReverseProxy().RequireRateLimiting("sliding");

// Expose /metrics endpoint for Prometheus scraping
app.MapPrometheusScrapingEndpoint();

app.MapReverseProxy();

app.Run();