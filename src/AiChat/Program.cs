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
builder.Services.AddSingleton<ChatState>();
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithMessageFilters(filters =>
    {
        ChatState? chatState = null;
        filters.AddIncomingFilter(next => async (context, cancellationToken) =>
        {
            var rawClientName = context.Server.ClientInfo?.Name;
            chatState ??= (context.Services
                ?? context.Server.Services
                ?? throw new InvalidOperationException("MCP message context does not provide a service provider."))
                .GetRequiredService<ChatState>();

            if (rawClientName is not null)
            {
                chatState.InitializeLastSentMessageToCurrentTailIfMissing(rawClientName);
            }

            await next(context, cancellationToken);
        });
    })
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
