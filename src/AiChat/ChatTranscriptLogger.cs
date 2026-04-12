using System.Globalization;
using System.Text;

/// <summary>
/// Appends chat activity to an optional transcript file configured via command-line arguments.
/// </summary>
internal sealed class ChatTranscriptLogger
{
    private readonly string? transcriptPath;
    private readonly Lock writeGate = new();

    private ChatTranscriptLogger(string? transcriptPath)
    {
        this.transcriptPath = transcriptPath;
        if (transcriptPath is not null)
        {
            var dir = Path.GetDirectoryName(transcriptPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
        }
    }

    /// <summary>
    /// Creates a transcript logger from process command-line arguments.
    /// </summary>
    /// <param name="args">Process command-line arguments.</param>
    /// <returns>Logger configured with transcript path when <c>-c</c> is provided; otherwise disabled.</returns>
    public static ChatTranscriptLogger FromCommandLine(string[] args)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "-c", StringComparison.Ordinal))
                return new ChatTranscriptLogger(args[i + 1]);
        }

        return new ChatTranscriptLogger(null);
    }

    /// <summary>
    /// Appends one post entry to the transcript.
    /// </summary>
    /// <param name="sender">Poster identity.</param>
    /// <param name="message">Posted message.</param>
    public void LogPost(string sender, string message)
    {
        if (transcriptPath is null)
            return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.f", CultureInfo.InvariantCulture);
        var entry = new StringBuilder()
            .Append("# ")
            .Append(sender)
            .Append("  *")
            .Append(timestamp)
            .AppendLine("*")
            .AppendLine()
            .AppendLine(message)
            .AppendLine()
            .AppendLine("---")
            .AppendLine()
            .ToString();

        WriteEntry(entry);
    }

    /// <summary>
    /// Appends one listen-call entry to the transcript.
    /// </summary>
    /// <param name="caller">Caller identity.</param>
    /// <param name="timeoutSeconds">Submitted listen timeout in seconds.</param>
    public void LogListen(string caller, int timeoutSeconds)
    {
        if (transcriptPath is null)
            return;

        var timestamp = DateTime.Now.ToString("HH:mm:ss.f", CultureInfo.InvariantCulture);
        var entry = $"- {caller} {timeoutSeconds}s *{timestamp}*{Environment.NewLine}";
        WriteEntry(entry);
    }

    /// <summary>
    /// Writes one entry atomically with process-local synchronization.
    /// </summary>
    /// <param name="entry">Entry text to append.</param>
    private void WriteEntry(string entry)
    {
        lock (writeGate)
        {
            File.AppendAllText(transcriptPath!, entry);
        }
    }
}
