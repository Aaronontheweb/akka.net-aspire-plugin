// placeholder - will be implemented in Phase 3
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.MapGet("/", () => "Hello from Akka.NET Aspire Sample!");
app.Run();
