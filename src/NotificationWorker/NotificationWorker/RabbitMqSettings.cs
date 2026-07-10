namespace NotificationWorker;

public class RabbitMqSettings
{
    public required string Host     { get; set; }
    public required string Username { get; set; }
    public required string Password { get; set; }
    public string ExchangeName { get; set; } = "events-platform";
    public string QueueName    { get; set; } = "notification-worker";
    public string RoutingKey   { get; set; } = "ordercreated";
}