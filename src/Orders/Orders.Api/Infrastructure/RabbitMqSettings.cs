namespace Orders.Api.Infrastructure;

public class RabbitMqSettings
{
    public required string Host { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public int    Port         { get; set; } = 5672;
    public string ExchangeName { get; set; } = "events-platform";
}