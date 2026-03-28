var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.WebHost.UseUrls("http://0.0.0.0:4713");

builder.Services.AddHttpContextAccessor();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithTools<ChatTools>();

var app = builder.Build();

AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
{
    var ex = args.ExceptionObject as Exception;
    app.Logger.LogCritical(ex, "Unhandled exception");
};

app.MapMcp("/mcp");

app.Logger.LogInformation("AiChat MCP server listening on http://0.0.0.0:4713/mcp");
await app.RunAsync();
