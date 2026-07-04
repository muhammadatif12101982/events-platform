using Microsoft.EntityFrameworkCore;
using Orders.Api;
using Orders.Api.Features.Orders;
using Orders.Api.Features.Products;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// ── Database ───────────────────────────────────────────────────────
builder.Services.AddDbContext<OrdersDbContext>(options => 
options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── Authentication ─────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    // Validate tokens issued by our IdentityServer
    options.Authority = builder.Configuration["IdentityServer:Authority"];

    // This service only accepts tokens intended for orders-api
    options.Audience = "orders-api";

    // Allow HTTP in development
    options.RequireHttpsMetadata = false;

    // Accept tokens where issuer is either the internal Docker name
    // OR localhost (when tokens fetched directly from host machine)
    options.TokenValidationParameters = new ()
    {
        ValidIssuers = [
            "http://identity-server:5001",
            "http://localhost:5001"
        ]
    };
});

// ── Authorization ──────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    // Require read scope for GET endpoints
    options.AddPolicy("orders.read", policy => policy.RequireClaim("scope", "orders.read"));

    // Require write scope for POST/PUT/DELETE endpoints
    options.AddPolicy("orders.write", policy => policy.RequireClaim("scope", "orders.write"));
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

//app.MapGet("/", () => "Hello World!");
//app.MapGet("/", () => "Orders API is running");

// ── Routes ─────────────────────────────────────────────────────────
// Products endpoints
app.MapPost("/products", CreateProduct.Handle)
.RequireAuthorization("orders.write");

app.MapGet("/products", ListProducts.Handle)
.RequireAuthorization("orders.read");

// Orders endpoints
app.MapPost("/orders", CreateOrder.Handle)
.RequireAuthorization("orders.write");

app.MapGet("/orders/{id:int}", GetOrder.Handle)
.RequireAuthorization("orders.read");

app.Run();
