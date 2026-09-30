# Codex 上下文监视器（Windows 改造版）

基于 MIT 项目 soleillevant0125/codex-token-overlay，保留原作者许可证。
这是跟随 Codex 窗口的本地伴随工具，不是原生界面插件。

本仓库包含 Windows 对话切换修复，暂未发布包含此修复的 Release，
请先按下文从源码构建。上游发行包不包含本仓库 Unreleased 的改动。

## 使用

双击 `启动上下文监视器.cmd`。显示当前选中对话的上下文剩余百分比。
托盘右键可以调整位置、选择显示字段、锁定会话、关闭提醒或退出。
首次启动默认显示累计 tokens 和上下文剩余。

“锁定当前会话”锁定点击时已显示数值的对话，取得 token 快照后才可用。
切换对话仍会显示该会话的数据，取消勾选后恢复跟随当前窗口。

提醒默认在剩余 20%、10%、5% 时触发。每个对话独立去重；从严重不足
直接跨过多个阈值只提示最严重一级。压缩后恢复超过阈值 3 个百分点，
该阈值重新启用提醒。通知由 Windows 通知设置控制。

启动脚本会创建 `local-settings.json` 作为本改造版独立配置。
退出工具后可修改 `ContextAlertThresholds`，例如 `[25, 15, 5]`，再启动。
不会覆盖原工具的用户设置。运行期间去重，重启后允许重新提醒。

## 数据与限制

剩余 = 100 × (1 - 最近一次输入 tokens / 日志报告的模型窗口)。
包含缓存输入，不能使用累计 tokens 计算上下文。数据来自最新日志事件，
不是实时预测；下一次消息、工具结果和输出仍可能触发提前压缩。

Windows 通过 UI Automation 只读前台 Codex 主窗口中 `RootWebArea` 文档
的标题，再与 `sessions` 目录旁的 `session_index.jsonl` 精确匹配；每个
对话 ID 使用最新完整记录。同名的多个对话、未知或无可识别标题、
索引缺失或未写完时等待，不按后台会话的最近写入时间猜测当前对话。
对话已识别但缺少日志或 token 快照时也显示等待。

识别到切换后先清除上个对话的数值，再解析目标日志。发布路由前复核
窗口、标题和索引版本；路由变化或锁定状态变化后返回的过期后台结果
会被丢弃，快速 A → B → A 也不会让旧的 B 结果覆盖 A。
目标轮询间隔为 150 ms，可访问性提供方、磁盘或界面调度可能增加延迟，
不保证点击后 150 ms 内完成更新。失焦时悬浮窗隐藏并保留最近路由，
Codex 重新进入前台后会再次验证。读取过程不控制窗口、不改变焦点，
也不修改 Codex 数据。

无法识别当前对话时暂停自动提醒，手动锁定已显示会话时可以提醒。
上下文窗口未知时显示破折号。文档标题、索引、IPC 和日志格式属内部
接口，Codex 更新后可能需要适配。本版未修改 macOS：仍通过本地 IPC
跟随任务，IPC 不可用时回退到最近更新的兼容根会话。

## 构建与验证

构建需要 .NET 10 SDK。

```powershell
dotnet run --project tests/VisibleThreadRouting -c Release
dotnet run --project tests/ThreadSwitching -c Release
dotnet run --project tests/ContextAlerts -c Release
.\scripts\Test-LogParser.ps1
.\scripts\Test-OverlayLogic.ps1 -Area All
dotnet publish src/CodexTokenOverlay/CodexTokenOverlay.csproj -c Release -r win-x64 --self-contained true -o dist/win-x64
.\scripts\Test-PublishedExecutable.ps1 -ExecutablePath .\dist\win-x64\CodexTokenOverlay.exe
.\scripts\Test-PeArchitecture.ps1 -ExecutablePath .\dist\win-x64\CodexTokenOverlay.exe -Architecture x64
```

三组合成测试分别包含 25 项标题路由、16 项会话选择/缺失日志/锁定/
过期结果、18 项提醒检查。原有日志解析、悬浮窗逻辑、发布 EXE 和 PE
架构回归继续保留。合成测试不代表所有 Codex Desktop 版本的实机兼容性，
也不能证明固定更新延迟；需另外验证前台对话及窗口切换。

便携包位于 `dist/win-x64`，运行无需另装 .NET。
源项目：[soleillevant0125/codex-token-overlay](https://github.com/soleillevant0125/codex-token-overlay)
