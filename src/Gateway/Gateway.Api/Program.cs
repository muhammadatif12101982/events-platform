using Microsoft.AspNetCore.Authentication.JwtBearer;

var builder = WebApplication.CreateBuilder(args);

// ── JWT Bearer Authentication ──────────────────────────────────────
// Validates tokens issued by our IdentityServer
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    // Where to find the signing keys (IdentityServer's discovery endpoint)
    options.Authority = builder.Configuration["IdentityServer:Authority"];

    // The audience this gateway expects in incoming tokens
    options.Audience = "orders-api";

    // Allow HTTP in development (no TLS yet)
    options.RequireHttpsMetadata = false;
});

builder.Services.AddAuthorization();

// ── YARP Reverse Proxy ─────────────────────────────────────────────
// Loads route and cluster config from appsettings.json
builder.Services.AddReverseProxy()
.LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// All proxied routes require a valid JWT
app.MapReverseProxy();

//app.MapGet("/", () => "Hello World!");

app.Run();
