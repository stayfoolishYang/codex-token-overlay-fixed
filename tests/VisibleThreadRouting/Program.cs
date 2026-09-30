using System.Text.Json;
using CodexTokenOverlay;

const string a = "aaaaaaaa-1111-2222-3333-444444444444";
const string b = "bbbbbbbb-1111-2222-3333-444444444444";
const string c = "cccccccc-1111-2222-3333-444444444444";
var checks = 0;
string Record(string id, string? name) => JsonSerializer.Serialize(new
{
    id,
    thread_name = name,
    updated_at = "2026-09-30T00:00:00Z"
});
void Check(VisibleThreadTitleIndex index, string? title, string? expected, string explanation)
{
    var actual = index.FindUniqueThread(title);
    if (actual != expected)
    {
        throw new Exception($"{explanation}: expected {expected ?? "null"}, actual {actual ?? "null"}");
    }
    checks++;
}

var index = VisibleThreadTitleIndex.Parse([Record(a, "会话 A"), Record(b, "会话 B")]);
Check(index, "会话 A", a, "Initial title A");
Check(index, "会话 B", b, "Idle title B");
Check(index, "会话 A", a, "Return A without log writes");
Check(index, "Codex", null, "Home without matching title");
Check(index, null, null, "No document name");
Check(index, "", null, "Empty document name");
Check(index, "   ", null, "Whitespace document name");
Check(index, "会话 a", null, "Exact case-sensitive match");
Check(index, "会话 A ", null, "No fuzzy title matching");

index = VisibleThreadTitleIndex.Parse([Record(a, "重复"), Record(b, "重复"), Record(c, "重复")]);
Check(index, "重复", null, "Two or more duplicate titles are ambiguous");
index = VisibleThreadTitleIndex.Parse([Record(a, "重复"), Record(a, "重复")]);
Check(index, "重复", a, "Repeated records of one ID are not duplicates");
index = VisibleThreadTitleIndex.Parse([Record(a, "旧名"), Record(a, "新名"), Record(b, "会话 B")]);
Check(index, "旧名", null, "Rename invalidates old title");
Check(index, "新名", a, "Latest complete record wins");

index = VisibleThreadTitleIndex.Parse([Record(a, "旧名"), Record(a, null)]);
Check(index, "旧名", null, "Null title removes old title");
index = VisibleThreadTitleIndex.Parse([Record(a, "旧名"), Record(a, "   ")]);
Check(index, "旧名", null, "Blank title removes old title");
index = VisibleThreadTitleIndex.Parse([Record(b, "会话 B")]);
Check(index, "旧名", null, "Rebuilt index does not retain deleted IDs");
Check(index, "会话 B", b, "Other entries survive rebuild");

index = VisibleThreadTitleIndex.Parse([Record(a, "会话 A"), "{\"id\":\"" + b + "\",\"thread_name\":\"会话 B"]);
Check(index, "会话 A", null, "Partial record may hide a duplicate or rename, so wait");
Check(index, "会话 B", null, "Partial trailing JSON cannot create a match");
index = VisibleThreadTitleIndex.Parse([Record(a, "会话 A"), Record(b, "会话 B")]);
Check(index, "会话 B", b, "Completed next file revision becomes matchable");
Check(index, "会话 A", a, "Complete entries become usable again after stable revision");

index = VisibleThreadTitleIndex.Parse([
    "null", "[]", Record("bad-id*", "危险名称"),
    "{\"id\":\"" + a + "\",\"thread_name\":23}", Record(a.ToUpperInvariant(), "大小写 ID")]);
Check(index, "危险名称", null, "Invalid thread IDs are excluded");
Check(index, "大小写 ID", a, "Valid IDs are canonicalized");
index = VisibleThreadTitleIndex.Parse([Record(a, "会话 A"), "{broken}"]);
Check(index, "会话 A", null, "Malformed record cannot hide duplicate title metadata");
Check(VisibleThreadTitleIndex.Empty, "任意标题", null, "Missing index has no selection");

Console.WriteLine($"PASS: {checks} exact-title, ambiguity, rename, deletion and partial-record checks.");
