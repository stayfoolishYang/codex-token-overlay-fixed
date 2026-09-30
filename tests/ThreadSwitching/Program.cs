using CodexTokenOverlay;
using System.Text.Json;

var root = Path.Combine(Path.GetTempPath(), "OverlayThreadSwitching-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var a = "aaaaaaaa-1111-2222-3333-444444444444";
var b = "bbbbbbbb-1111-2222-3333-444444444444";
var missing = "cccccccc-1111-2222-3333-444444444444";
var count = 0;
void Check(bool pass, string message)
{
    if (!pass) throw new Exception(message);
    count++;
}
string WriteLog(string id, long total, bool hasTokens = true)
{
    var path = Path.Combine(root, $"rollout-2026-09-30T00-00-00-{id}.jsonl");
    File.WriteAllText(path, "{\"type\":\"session_meta\",\"payload\":{\"originator\":\"Codex Desktop\",\"source\":\"vscode\"}}\n");
    if (hasTokens)
        File.AppendAllText(path, JsonSerializer.Serialize(new
        {
            type = "event_msg",
            payload = new
            {
                type = "token_count",
                info = new
                {
                    total_token_usage = new { total_tokens = total },
                    last_token_usage = new { input_tokens = total / 2 },
                    model_context_window = 128000
                }
            }
        }) + "\n");
    return path;
}
try
{
    var pathA = WriteLog(a, 11111);
    var pathB = WriteLog(b, 22222);
    File.SetLastWriteTimeUtc(pathB, DateTime.UtcNow.AddHours(-1));
    using var monitor = new TokenLogMonitor(root) { RequirePreferredThread = true, PreferredThreadId = a };
    var snapshotA = monitor.Poll();
    Check(snapshotA?.ThreadId == a && snapshotA.TotalTokens == 11111, "Select A");
    monitor.PreferredThreadId = b;
    var snapshotB = monitor.Poll();
    Check(snapshotB?.ThreadId == b && snapshotB.TotalTokens == 22222, "Idle B wins over newer A file");
    monitor.PreferredThreadId = missing;
    Check(monitor.Poll() is null && monitor.ActiveThreadId == missing, "Missing B cannot show A");
    WriteLog(missing, 0, false);
    Check(monitor.Poll() is null, "No-token thread cannot retain preceding values");
    monitor.PreferredThreadId = a;
    Check(monitor.Poll()?.ThreadId == a, "Return to A");
    monitor.PreferredThreadId = null;
    Check(monitor.Poll() is null && monitor.ActiveThreadId is null, "No visible route must clear old values");
    monitor.PreferredThreadId = a;
    monitor.Poll();
    monitor.PinActiveSession = true;
    monitor.PreferredThreadId = b;
    Check(monitor.Poll(forceFullScan: true)?.ThreadId == a, "Pinning preserves A");
    monitor.PinActiveSession = false;
    Check(monitor.Poll()?.ThreadId == b, "Unpin follows B");
    monitor.PinnedThreadId = a;
    monitor.PinActiveSession = true;
    Check(monitor.Poll()?.ThreadId == a, "Pin uses displayed thread even if the last reader had selected B");

    var routeA = new ActiveThreadRouteStatus(a, 1, true, 1, null);
    var routeB = new ActiveThreadRouteStatus(b, 1, true, 2, null);
    var returnedA = routeA with { Version = 3 };
    var readA = new ThreadPollTicket(1, routeA.Version, false);
    Check(readA.CanApply(1, routeA, false, snapshotA), "Current result accepted");
    Check(!readA.CanApply(2, routeB, false, snapshotA), "Old A result rejected after B selection");
    var slowRead = new TaskCompletionSource<TokenSnapshot?>();
    var readB = new ThreadPollTicket(2, routeB.Version, false);
    // Finish a B read only after the selection has already returned to A.
    var delayedCommit = Task.Run(async () => readB.CanApply(3, returnedA, false, await slowRead.Task));
    slowRead.SetResult(snapshotB);
    Check(!await delayedCommit, "Delayed B cannot overwrite A after A -> B -> A");
    Check(!readA.CanApply(1, returnedA, false, snapshotA), "Route generation invalidates same-ID earlier read");
    Check(!new ThreadPollTicket(3, 3, false).CanApply(3, returnedA, false, snapshotB), "Mismatched snapshot rejected");
    Check(!readA.CanApply(1, routeA, true, snapshotA), "Pin toggle invalidates in-flight result");
    Check(new ThreadPollTicket(4, 1, true).CanApply(4, routeB, true, snapshotA), "Pinned read ignores background route changes");
    Console.WriteLine($"PASS: {count} selection, idle-log, missing-log, locking and delayed-result checks.");
}
finally
{
    Directory.Delete(root, recursive: true);
}
