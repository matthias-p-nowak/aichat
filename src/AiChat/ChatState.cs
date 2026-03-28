using System.Globalization;

/// <summary>
/// Holds shared state for the chat session.
/// </summary>
internal sealed class ChatState
{
    private readonly Lock gate = new();

    private PostNode tail= new("", "", null);
    // read-position marker: the tail node at the time of the poster's last post or listen call
    private readonly Dictionary<string, PostNode> lastSentMessageByPoster = new(StringComparer.Ordinal);


    /// <summary>
    /// Adds a message to the chat log and returns new posts since the caller's last interaction.
    /// </summary>
    /// <param name="poster">Poster name.</param>
    /// <param name="message">Posted message.</param>
    /// <returns>Delta snapshot as [poster, message, timestamp] triples since the caller's previous marker.</returns>
    public IReadOnlyList<string[]> AddPost(string poster, string message)
    {
        lock (gate)
        {

            if (!lastSentMessageByPoster.TryGetValue(poster, out var startNode))
            {
                startNode = tail;
            }

            AppendPost(poster, message);
            lastSentMessageByPoster[poster] = tail;
            return BuildSnapshot(startNode);
        }
    }

    /// <summary>
    /// Waits asynchronously for new posts since the caller's last interaction, up to the given timeout.
    /// </summary>
    /// <param name="poster">Poster name used to track the read position.</param>
    /// <param name="timeoutMilliseconds">Maximum time to wait for new messages; must be ≥ 0.</param>
    /// <param name="cancellationToken">Token to cancel the wait early.</param>
    /// <returns>New posts as [poster, message, timestamp] triples; empty list on timeout.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeoutMilliseconds"/> is negative.</exception>
    public async Task<IReadOnlyList<string[]>> ListenAsync(string poster, int timeoutMilliseconds, CancellationToken cancellationToken)
    {
        if (timeoutMilliseconds < 0)
            throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), "Timeout must be non-negative.");
        PostNode startNode;
        lock (gate)
        {
            if (!lastSentMessageByPoster.TryGetValue(poster, out startNode!))
                startNode = tail;

            if (startNode.Next is not null)
            {
                lastSentMessageByPoster[poster] = tail;
                return BuildSnapshot(startNode);
            }
        }

        await startNode.WaitNextAsync(timeoutMilliseconds, cancellationToken);

        lock (gate)
        {
            lastSentMessageByPoster[poster] = tail;
            return BuildSnapshot(startNode);
        }
    }

    private void AppendPost(string poster, string message)
    {
        var newNode = new PostNode(poster, message, null);
        tail.Next = newNode;
        tail = newNode;
    }

    private static List<string[]> BuildSnapshot(PostNode startNode)
    {
        var snapshot = new List<string[]>();
        for (var node = startNode.Next; node is not null; node = node.Next)
        {
            snapshot.Add([node.Poster, node.Message, node.Timestamp]);
        }

        return snapshot;
    }


    /// <summary>
    /// Single node in the in-memory post list.
    /// </summary>
    /// <param name="Poster">Poster name.</param>
    /// <param name="Message">Posted message text.</param>
    /// <param name="Next">Next post node.</param>
    private sealed class PostNode(string poster, string message, PostNode? next)
    {
        private PostNode? nextNode = next;

        public string Poster { get; } = poster;
        public string Message { get; } = message;
        public string Timestamp { get; } = DateTime.Now.ToString("HH:mm:ss.f", CultureInfo.InvariantCulture);
        public PostNode? Next
        {
            get => nextNode;
            set
            {
                nextNode = value;
                if (value is not null)
                    nextAvailable.TrySetResult(true);
            }
        }
        private readonly TaskCompletionSource<bool> nextAvailable = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Waits asynchronously until the next node is set or the timeout/cancellation fires.
        /// </summary>
        public async Task<bool> WaitNextAsync(int timeoutMilliseconds, CancellationToken cancellationToken)
        {
            if (nextAvailable.Task.IsCompleted)
                return true;
            try
            {
                return await nextAvailable.Task.WaitAsync(TimeSpan.FromMilliseconds(timeoutMilliseconds), cancellationToken);
            }
            catch (TimeoutException)
            {
                return false;
            }
        }
    }
}
