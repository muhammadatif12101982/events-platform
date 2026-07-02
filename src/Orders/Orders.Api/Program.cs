using Microsoft.EntityFrameworkCore;
using Orders.Api;
using Orders.Api.Features.Orders;
using Orders.Api.Features.Products;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

//app.MapGet("/", () => "Hello World!");
//app.MapGet("/", () => "Orders API is running");

// Products endpoints
app.MapPost("/products", CreateProduct.Handle);
app.MapGet("/products", ListProducts.Handle);

// Orders endpoints
app.MapPost("/orders", CreateOrder.Handle);
app.MapGet("/orders/{id:int}", GetOrder.Handle);

app.Run();
