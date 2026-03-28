/// <summary>
/// Holds shared state for the chat session.
/// </summary>
internal sealed class ChatState
{
    private readonly Lock gate = new();

    private PostNode tail= new("", "", null);
    // it contains the last post send to this poster
    private readonly Dictionary<string, PostNode> lastSentMessageByPoster = new(StringComparer.Ordinal);


    /// <summary>
    /// Adds a message to the chat log and returns a snapshot of all posts.
    /// </summary>
    /// <param name="poster">Poster name.</param>
    /// <param name="message">Posted message.</param>
    /// <returns>Snapshot of all post pairs as [poster, message].</returns>
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
    /// Waits for new posts since the caller's last interaction, blocking up to the given timeout.
    /// </summary>
    /// <param name="poster">Poster name used to track the read position.</param>
    /// <param name="timeoutMilliseconds">Maximum time to wait for new messages.</param>
    /// <returns>New posts as [poster, message] pairs; empty list on timeout.</returns>
    public IReadOnlyList<string[]> Listen(string poster, int timeoutMilliseconds)
    {
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

        startNode.WaitNext(timeoutMilliseconds);

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
            snapshot.Add([node.Poster, node.Message]);
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
        public PostNode? Next
        {
            get => nextNode;
            set
            {
                nextNode = value;
                if (value is not null && !nextAvailable.IsSet)
                {
                    nextAvailable.Signal();
                }
            }
        }
        private readonly CountdownEvent nextAvailable = new(initialCount: 1);

        public bool WaitNext(int timeoutMilliseconds)
        {
            return nextAvailable.Wait(timeoutMilliseconds);
        }
    }
}
