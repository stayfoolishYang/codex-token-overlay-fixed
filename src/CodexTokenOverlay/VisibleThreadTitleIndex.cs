using System.Text.Json;

namespace CodexTokenOverlay;

// The index is append-only in normal use. Rebuilding on every file revision also
// handles truncation, replacement and deletion without retaining obsolete names.
internal sealed class VisibleThreadTitleIndex
{
    private readonly Dictionary<string, string?> _threadByTitle;

    private VisibleThreadTitleIndex(Dictionary<string, string?> threadByTitle, bool isComplete = true)
    {
        _threadByTitle = threadByTitle;
        IsComplete = isComplete;
    }

    public bool IsComplete { get; }

    public static VisibleThreadTitleIndex Empty { get; } = new(new(StringComparer.Ordinal));

    public static VisibleThreadTitleIndex Parse(IEnumerable<string> lines)
    {
        var latestNameById = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var isComplete = true;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object
                    || !root.TryGetProperty("id", out var idElement)
                    || idElement.ValueKind != JsonValueKind.String
                    || !Guid.TryParse(idElement.GetString(), out var parsedId)
                    || !root.TryGetProperty("thread_name", out var nameElement)
                    || nameElement.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
                {
                    continue;
                }

                // The last complete record for each ID supersedes earlier names.
                // Null/blank names remove a previous title instead of reviving it.
                var name = nameElement.GetString();
                latestNameById[parsedId.ToString()] = string.IsNullOrWhiteSpace(name) ? null : name;
            }
            catch (JsonException)
            {
                // Codex may still be appending the final JSONL record. A future
                // length/write-time revision retries it. The incomplete record
                // may contain a rename or a duplicate of an otherwise unique title,
                // so this revision cannot safely identify any conversation.
                isComplete = false;
            }
        }

        var threadByTitle = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (id, title) in latestNameById)
        {
            if (title is null)
            {
                continue;
            }

            if (!threadByTitle.TryAdd(title, id))
            {
                // Ambiguous titles cannot identify the visible conversation safely.
                threadByTitle[title] = null;
            }
        }

        return new VisibleThreadTitleIndex(threadByTitle, isComplete);
    }

    public string? FindUniqueThread(string? documentTitle)
    {
        return IsComplete && !string.IsNullOrWhiteSpace(documentTitle)
            && _threadByTitle.TryGetValue(documentTitle, out var id)
                ? id
                : null;
    }
}
