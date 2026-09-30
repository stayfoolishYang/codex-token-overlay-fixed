using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace CodexTokenOverlay;

// Reads only the foreground window's accessible document title. IPC stream
// subscriptions can include background conversations and are not selection data.
internal sealed class CodexVisibleThreadMonitor : IDisposable
{
    private static readonly Condition DocumentCondition = new AndCondition(
        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document),
        new PropertyCondition(AutomationElement.AutomationIdProperty, "RootWebArea"));
    private readonly object _sync = new();
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Task _runner;
    private readonly string _indexPath;
    private ActiveThreadRouteStatus _status = new(null, 0, false, 0, null);
    private VisibleThreadTitleIndex _titleIndex = VisibleThreadTitleIndex.Empty;
    private DateTime _indexWriteUtc = DateTime.MinValue;
    private long _indexLength = -1;
    private IntPtr _observedWindow;
    private string? _observedTitle;
    private bool _wasForeground;
    private DateTime _lastObservationUtc = DateTime.MinValue;
    private int _disposed;

    public CodexVisibleThreadMonitor(string sessionRoot)
    {
        var parent = Directory.GetParent(Path.GetFullPath(sessionRoot));
        _indexPath = Path.Combine(parent?.FullName ?? sessionRoot, "session_index.jsonl");
        _runner = Task.Run(() => RunAsync(_cancellation.Token));
    }

    public ActiveThreadRouteStatus GetStatus()
    {
        lock (_sync)
        {
            if (_status.ThreadId is not null
                && DateTime.UtcNow - _lastObservationUtc > TimeSpan.FromSeconds(2))
            {
                // A stalled accessibility provider must not leave an old task's
                // numbers on screen indefinitely. The runner may recover later.
                _status = new ActiveThreadRouteStatus(null, 0, false,
                    _status.Version + 1, "当前窗口观测超时，等待重新识别。");
            }
            return _status;
        }
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            ObserveForegroundWindow();
            try
            {
                await Task.Delay(150, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
        }
    }

    private void ObserveForegroundWindow()
    {
        if (!CodexWindowLocator.TryGetForegroundCodexTarget(out var target))
        {
            // The overlay is hidden outside Codex. Keep the last selection, then
            // force a fresh validation on return instead of selecting a busy log.
            _wasForeground = false;
            return;
        }

        var window = target.HostWindow.Handle;
        var returningToForeground = !_wasForeground;
        _wasForeground = true;
        string? title = null;
        string? threadId = null;
        string? error = null;
        var observedDocument = false;
        var titleChangedDuringRead = false;
        try
        {
            var root = AutomationElement.FromHandle(window);
            var document = root?.FindFirst(TreeScope.Descendants, DocumentCondition);
            if (document is not null)
            {
                title = document.Current.Name;
                observedDocument = true;
                if (TryRefreshTitleIndex(out var indexError))
                {
                    threadId = _titleIndex.FindUniqueThread(title);
                    if (threadId is null)
                    {
                        error = "当前对话标题未能唯一匹配本地会话，等待识别。";
                    }
                }
                else
                {
                    error = indexError;
                }

                var currentTitle = document.Current.Name;
                if (!string.Equals(title, currentTitle, StringComparison.Ordinal))
                {
                    title = currentTitle;
                    threadId = null;
                    error = "当前 Codex 对话已切换，等待识别。";
                    titleChangedDuringRead = true;
                }
                if (threadId is not null && !IsTitleIndexRevisionCurrent())
                {
                    threadId = null;
                    error = "本地会话标题索引正在更新，等待重新识别。";
                }
            }
            else
            {
                error = "当前 Codex 窗口的对话标题尚未可用。";
            }
        }
        catch (Exception exception) when (exception is COMException
            or InvalidOperationException or UnauthorizedAccessException
            or ElementNotAvailableException)
        {
            threadId = null;
            observedDocument = false;
            error = "读取当前 Codex 对话标题失败，等待重试。";
        }

        // UIA can complete after the user focuses another window. Never commit
        // that observation as the new foreground conversation.
        if (!CodexWindowLocator.TryGetForegroundCodexTarget(out var current))
        {
            _wasForeground = false;
            return;
        }
        if (current.HostWindow.Handle != window)
        {
            _wasForeground = false;
            PublishObservation(current.HostWindow.Handle, null, null, false,
                "当前 Codex 窗口已切换，等待识别。", true);
            return;
        }

        PublishObservation(window, title, threadId, observedDocument, error,
            returningToForeground || titleChangedDuringRead);
    }

    private void PublishObservation(IntPtr window, string? title, string? threadId,
        bool observedDocument, string? error, bool forceVersionChange)
    {
        lock (_sync)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            var changed = forceVersionChange
                || _observedWindow != window
                || !string.Equals(_observedTitle, title, StringComparison.Ordinal)
                || !string.Equals(_status.ThreadId, threadId, StringComparison.OrdinalIgnoreCase)
                || _status.IsConnected != observedDocument
                || !string.Equals(_status.LastError, error, StringComparison.Ordinal);
            _lastObservationUtc = DateTime.UtcNow;
            _observedWindow = window;
            _observedTitle = title;
            if (changed)
            {
                _status = new ActiveThreadRouteStatus(
                    threadId, 1, observedDocument, _status.Version + 1, error);
            }
        }
    }

    private bool TryRefreshTitleIndex(out string? error)
    {
        error = null;
        try
        {
            var file = new FileInfo(_indexPath);
            if (!file.Exists)
            {
                _titleIndex = VisibleThreadTitleIndex.Empty;
                _indexWriteUtc = DateTime.MinValue;
                _indexLength = -1;
                error = "本地会话标题索引尚未可用，等待识别。";
                return false;
            }

            var writeUtc = file.LastWriteTimeUtc;
            var length = file.Length;
            if (writeUtc == _indexWriteUtc && length == _indexLength)
            {
                if (!_titleIndex.IsComplete)
                {
                    error = "本地会话标题索引记录尚未完整，等待重新识别。";
                    return false;
                }
                return true;
            }

            using var stream = new FileStream(_indexPath, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream, Encoding.UTF8, true, 16 * 1024);
            var parsed = VisibleThreadTitleIndex.Parse(ReadLines(reader));
            file.Refresh();
            if (!file.Exists || file.LastWriteTimeUtc != writeUtc || file.Length != length
                || stream.Length != length)
            {
                // A rewrite/append during parsing is not an atomic revision and
                // can hide a duplicate or rename. Wait for a stable read.
                _titleIndex = VisibleThreadTitleIndex.Empty;
                _indexWriteUtc = DateTime.MinValue;
                _indexLength = -1;
                error = "本地会话标题索引正在更新，等待重新识别。";
                return false;
            }

            _titleIndex = parsed;
            _indexWriteUtc = writeUtc;
            _indexLength = length;
            if (!parsed.IsComplete)
            {
                error = "本地会话标题索引记录尚未完整，等待重新识别。";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Do not let a stale cached name masquerade as a current match.
            _titleIndex = VisibleThreadTitleIndex.Empty;
            _indexWriteUtc = DateTime.MinValue;
            _indexLength = -1;
            error = "本地会话标题索引暂不可读，等待重试。";
            return false;
        }
    }

    private bool IsTitleIndexRevisionCurrent()
    {
        try
        {
            var file = new FileInfo(_indexPath);
            return file.Exists && file.LastWriteTimeUtc == _indexWriteUtc
                && file.Length == _indexLength;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static IEnumerable<string> ReadLines(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _cancellation.Cancel();
        // A slow UIA provider must not block the UI thread while exiting. There
        // is only one runner, and the token is disposed after that runner ends.
        _ = _runner.ContinueWith(_ => _cancellation.Dispose(),
            CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}
