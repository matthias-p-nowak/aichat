using System.ComponentModel;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol;
using ModelContextProtocol.Server;

/// <summary>
/// Provides MCP tools for chat client interactions.
/// </summary>
[McpServerToolType]
internal sealed class ChatTools(IHttpContextAccessor httpContextAccessor, McpServer server)
{
    private static readonly ChatState state = new();
    private static readonly ChatTranscriptLogger transcriptLogger = ChatTranscriptLogger.FromCommandLine(Environment.GetCommandLineArgs());
    /// <summary>
    /// Posts a message and returns all posts as [poster, message] pairs.
    /// </summary>
    /// <param name="message">Message text to post.</param>
    /// <returns>Chat transcript as list of [poster, message] pairs.</returns>
    [Description("Post a chat message and return all posts as [poster, message] pairs.")]
    [McpServerTool(Name = "post", Title = "Post", ReadOnly = false, Idempotent = false, OpenWorld = false, Destructive = false)]
    public IReadOnlyList<string[]> Post([Description("Message text to post.")] string message)
    {
        var poster = server.ClientInfo?.Name ?? httpContextAccessor.HttpContext?.Request.Headers["Mcp-Session-Id"].FirstOrDefault() ?? "unknown";
        transcriptLogger.LogPost(poster, message);
        return state.AddPost(poster, message);
    }

    /// <summary>
    /// Waits for new chat posts, blocking up to the given timeout.
    /// </summary>
    /// <param name="timeoutMilliseconds">Maximum time to wait for new messages, in milliseconds.</param>
    /// <returns>New posts as [poster, message] pairs; may be empty on timeout.</returns>
    [Description("Wait for new chat posts, blocking up to the given timeout. Returns all new [poster, message] pairs; may be empty on timeout.")]
    [McpServerTool(Name = "listen", Title = "Listen", ReadOnly = true, Idempotent = false, OpenWorld = false, Destructive = false)]
    public async Task<IReadOnlyList<string[]>> Listen(
        [Description("Maximum time to wait for new messages, in milliseconds.")] int timeoutMilliseconds,
        CancellationToken cancellationToken)
    {
        var poster = server.ClientInfo?.Name ?? httpContextAccessor.HttpContext?.Request.Headers["Mcp-Session-Id"].FirstOrDefault() ?? "unknown";
        transcriptLogger.LogListen(poster);
        return await state.ListenAsync(poster, timeoutMilliseconds, cancellationToken);
    }
}
