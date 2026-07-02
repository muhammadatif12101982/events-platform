using Microsoft.EntityFrameworkCore;
using Orders.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<OrdersDbContext>(options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var app = builder.Build();

//app.MapGet("/", () => "Hello World!");
app.MapGet("/", () => "Orders API is running");

app.Run();
