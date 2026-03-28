var port = 4713;
for (var i = 0; i < args.Length - 1; i++)
{
    if (args[i] == "-p" && int.TryParse(args[i + 1], out var p))
        port = p;
}

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

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

app.Logger.LogInformation("AiChat MCP server listening on http://0.0.0.0:{Port}/mcp", port);
await app.RunAsync();
