# Codex Token 状态条

[English](README.md)

Codex Token 状态条是一个只读桌面小工具，用于显示 Codex Desktop 当前选中任务的 Token 使用情况。Windows 根据前台窗口的可访问文档标题和本地会话索引识别对话，macOS 通过 Codex 本机 IPC 跟随任务。两者均从本地 JSONL 会话日志读取统计数据，切换到静止的旧任务也不要求日志产生新写入。

> [!IMPORTANT]
> 这是非官方社区项目，并非由 OpenAI 开发、认可或提供支持。它依赖 Codex Desktop 的可访问文档标题、会话索引、本地 JSONL 格式和内部 IPC 消息；这些内部实现可能在未来版本中改变。

本仓库包含 Windows 对话切换修复，暂未发布包含此修复的 Release；请按[源码构建](#从源码构建)使用。上游发行包不包含本仓库 Unreleased 中记录的改动。

## Windows 上下文余量与提醒

新增上下文剩余百分比，首次运行默认显示在收起状态右侧。默认在剩余
20%、10%、5% 时触发 Windows 通知；按对话独立去重，压缩后恢复超过
阈值 3 个百分点可再次提醒。托盘“上下文不足提醒”可关闭通知。

保留本地只读架构，Windows 对话识别改为前台窗口标题与会话索引匹配。
计算使用最近一次输入 tokens；模型窗口未知时显示破折号，无法识别
当前对话时暂停自动提醒。显式锁定当前已显示的会话时仍可提醒。
百分比是日志快照，不保证预测下一次请求或压缩时间。

详细配置、编译和限制见 [改造说明](README.context-monitor.zh-CN.md)。

## 主要功能

- 跟随 Codex Desktop 当前选中的任务，包括当前没有运行的任务。
- 识别到任务切换后解析该任务已有日志，不要求日志产生新写入。
- 两个平台都显示总量、输入、输出、缓存命中 Token、推导出的缓存未命中、推理输出和上下文占用；Windows 另外显示缓存命中率。
- 可以自由选择实际显示哪些字段，并保证至少保留一个字段。
- Windows 使用不会抢占输入焦点、跟随 Codex 主窗口的胶囊和系统托盘菜单。
- macOS 使用原生菜单栏，并可从菜单设置登录时启动。
- Windows 识别到切换后先清除旧数值，无法唯一识别当前标题时显示等待。
- macOS 内部 IPC 不可用时自动退回最近更新的 Codex Desktop 根会话。
- 只读取本地状态，不含遥测、分析、网络 API 或上传功能。

## 下载

目前没有包含本仓库 Windows 切换修复的下载包。下表说明原项目的[上游发行文件](https://github.com/soleillevant0125/codex-token-overlay/releases)；使用本次修复请从源码构建。

| 平台 | 文件 | 说明 |
| --- | --- | --- |
| Windows x64 轻量版 | `CodexTokenOverlay-win-x64-lite.zip` | 约 100 KB，需要 .NET 10 Desktop Runtime。 |
| Windows x64 独立版 | `CodexTokenOverlay-win-x64.zip` | 约 46 MB，无需安装 .NET，保持原下载名。 |
| Windows Arm64 轻量版 | `CodexTokenOverlay-win-arm64-lite.zip` | 约 100 KB，需要 Arm64 .NET 10 Desktop Runtime。 |
| Windows Arm64 独立版 | `CodexTokenOverlay-win-arm64.zip` | 无需安装 .NET，保持原下载名。 |
| macOS Apple Silicon | `CodexTokenOverlay-macos-arm64.zip` | M1、M2、M3、M4 及后续 M 系列芯片首选。 |
| macOS Intel | `CodexTokenOverlay-macos-x64.zip` | 运行 macOS 14 或更高版本的 Intel Mac。 |

每个 ZIP 旁都提供 `.sha256` 校验文件。Windows 轻量版与 macOS 包处于相近的体积档位；它使用与独立版完全相同的应用代码，只是调用系统中共享的 .NET 运行时。请从[微软官方 .NET 10 下载页](https://dotnet.microsoft.com/download/dotnet/10.0)安装与系统架构一致的 **Desktop Runtime**。不想安装运行时或不确定时，直接选择文件名中不带 `-lite` 的独立版。

Windows Arm64 安装包目前由 x64 环境交叉构建，并在 CI 中完成 PE 架构检查；在以后记录 Arm64 真机测试前，不应将其描述为已经过 Arm64 真机验证。

所有 Windows 版本均不依赖 PowerShell。macOS 用户无需安装 Xcode、Swift 或 Homebrew。

## Windows 使用方法

1. 按下文从本仓库源码构建并发布。已安装对应 .NET 10 Desktop Runtime 时可生成 Lite；Standalone 会包含运行时。
2. 将生成的本地发行包解压到任意位置，或直接打开发布目录。
3. 双击 `CodexTokenOverlay.exe`。

Windows 默认使用手动主窗口吸附。请在托盘选择**调整位置和大小…**，再把胶囊拖到 Codex 主窗口上。程序会保存主窗口八个参考点（四个角点和四条边的中点）中离胶囊最近的一点及相对偏移，因此 Codex 窗口移动或调整大小时，胶囊会继续跟随同一参考点和相对位置。放到内置宠物、桌面、其他应用或任何其他非主窗口的 Codex 表面都属于无效操作：目标高亮会清除、无法保存，并立即恢复到上一个有效位置。拖动胶囊右下角的缩放手柄，可以在 60%–130% 之间连续等比调整整个胶囊与展开面板，包括文字、间距、圆角和内边距。

按 **Enter** 或在托盘选择**完成调整**即可保存；按 **Esc** 或选择**取消调整**会完整恢复编辑前的位置和比例。选择**重置到 Codex 右上**会恢复主窗口右上吸附和 100% 比例。

可在托盘菜单的**收起时显示 > 左侧指标/右侧指标**中分别选择收起状态的两项内容。兼容子菜单**传统定位**保留**标题栏右上、自动吸附、窗口内右上、窗口内右下**，供旧工作流继续使用；选择任一传统位置会停用手动吸附，直到再次调整或重置。传统的窗口内右下模式会向上展开。标题栏模式会在请求比例之下选择能够完整容纳在标题栏内的最大比例，不会移入 Codex 客户区；空间恢复时会自动恢复请求比例。其他狭窄位置仍会按“双指标 → 单指标 → 隐藏”降级，空间恢复后自动显示。

普通点击胶囊会展开完整指标面板；再次点击或点击其他位置会收起，点击面板内部则保持展开。整个过程不会夺走 Codex 输入框焦点。这里的视觉吸附由一个跟随 Codex 窗口几何位置的独立伴随窗口实现，并非注入 Codex 进程，也不是真正嵌入其 UI 树。胶囊、展开面板、编辑装饰和吸附目标环会实时跟随 Windows 应用的浅色/深色模式，无需重启，也不提供手动主题选项。只有可识别的 Codex Desktop 窗口位于前台时胶囊才显示，Codex 失去前台后会隐藏。托盘菜单还可选择展开字段、锁定当前任务、临时隐藏或退出。

**锁定当前会话**锁定的是点击时已经显示数值的对话；之后切换任务仍显示该对话的数据，取消勾选后恢复跟随。取得 token 快照后才能使用此项，不会锁定尚未完成的后台读取目标。

GitHub 上的未签名程序可能触发 Windows SmartScreen。请先确认文件来自本仓库并核对 SHA-256，再选择“更多信息 > 仍要运行”。

## macOS 使用方法

1. M 系列 Mac 下载 `CodexTokenOverlay-macos-arm64.zip`；Intel Mac 下载 x64 文件。
2. 解压后，将 `CodexTokenOverlay.app` 移入 `/Applications`。
3. 打开应用。Token 会显示在 macOS 菜单栏，不会出现 Dock 图标。
4. 点击菜单栏文字即可选择字段、锁定任务、开启登录时启动或退出。

目前公开的 macOS 包使用 ad-hoc 完整性签名，尚未使用 Developer ID 公证。首次打开时 Gatekeeper 可能要求按住 Control 点击应用并选择“打开”，或者在“系统设置 > 隐私与安全性”中选择“仍要打开”。请仅在确认下载来源和校验值后这样操作；不需要执行终端命令或全局关闭系统安全机制。

若要彻底消除首次信任提示，需要 Apple Developer Program 的 Developer ID Application 证书和 Apple 公证。仓库的打包结构已为此留好基础，但项目中不会存放 Apple 私钥或证书。

## 系统要求和路径

- Windows 10/11，或 macOS 14 及以上版本。
- Windows 轻量版需要与系统架构一致的 .NET 10 Desktop Runtime；独立版不需要。
- Codex Desktop 与本工具在同一个交互用户下运行。
- 当前用户能够读取 Codex Desktop 的本地会话数据。

会话目录按以下顺序解析：

1. 开发或测试时显式传入的 `--sessions <路径>`。
2. 设置 `CODEX_HOME` 时使用 `$CODEX_HOME/sessions`。
3. 默认使用 `~/.codex/sessions`。

Windows 还会读取上述 `sessions` 目录旁的 `session_index.jsonl`，将前台文档标题匹配到对话 ID。索引缺失或不可读时显示等待识别。

应用本身没有固定位置要求，不过 macOS 推荐放入 `/Applications`，这样“登录时启动”和 Gatekeeper 的行为更稳定。

用户设置位置：

- Windows：`%LOCALAPPDATA%\CodexTokenOverlay\settings.json`
- macOS：标准偏好域 `io.github.soleillevant0125.CodexTokenOverlay`

开发者和测试程序可以使用 `--settings <绝对-JSON-路径>` 隔离 Windows 设置。该参数仅用于开发和测试，不是正常用户设置，正常安装和启动不需要传入。

## 指标说明

| 字段 | 含义 |
| --- | --- |
| 总量 | 当前任务累计的 `total_token_usage.total_tokens`。 |
| 输入 | 累计输入 Token。 |
| 输出 | 累计输出 Token。 |
| 缓存命中 | 累计缓存输入 Token，它是输入 Token 的子集。 |
| 缓存命中率（Windows） | 按 `缓存输入 / 输入 * 100%` 计算并限制在 0–100%；输入为零或不可用时显示 0%。可选为 Windows 展开字段或收起状态的左/右指标，并在新建 Windows 设置中默认显示。 |
| 缓存未命中 | 由 `max(0, 输入 - 缓存输入)` 推导。 |
| 上下文 | 最近一次模型调用的 Token 数与 `model_context_window` 的对比。 |
| 推理 | 日志中存在该字段时显示累计推理输出 Token。 |
| 任务 ID | Codex conversation/thread 标识。 |

这些数值来自本地会话日志事件，不等同于账单、API 费用计算或权威的 ChatGPT 套餐用量。

## 当前任务跟随原理

程序不会修改 Codex 数据。Windows 使用以下流程：

1. 通过 Windows UI Automation 只读前台 Codex 主窗口中 `RootWebArea` 文档的名称，不控制窗口或改变焦点。
2. 从 `session_index.jsonl` 中取得每个对话 ID 最新的完整标题记录，精确匹配当前文档标题。多个 ID 同名、未知标题、索引不可用或记录不完整时等待识别。
3. 识别到路由变化后先清除上个对话的数值，再读取对应根会话 JSONL 中最后一个完整的 `token_count` 事件。日志或 token 快照缺失时显示等待。
4. 发布识别结果前复核前台窗口、文档标题和索引版本。后台日志结果必须仍属于当前路由及切换轮次，快速 A → B → A 或锁定状态变化后到达的旧结果会被丢弃。

Windows 的对话识别与界面刷新目标轮询间隔为 150 ms，不保证点击后 150 ms 内完成刷新；可访问性提供方、磁盘读取或界面调度仍可能造成延迟。切换不依赖日志产生新写入，无法识别时也不会根据最近写入的后台日志猜测当前对话。Codex 失去前台时隐藏悬浮窗并保留最近路由，重新进入前台后再次验证当前对话。

macOS 保持原有行为：以只读客户端连接 `$CODEX_HOME/ipc/ipc.sock`（兼容旧版临时 Socket），通过本地 IPC 跟随任务 ID 并解析对应根会话日志；IPC 不可用时退回最近更新的兼容根会话。macOS 版会验证 IPC 路径确实是当前用户拥有的 Unix Socket，并验证其目录不可被其他用户写入。程序只连接，不会创建、删除或替换 Codex 的 Socket。

## 隐私说明

- 会话文件只在本机读取，不会被修改。
- Token 数值和任务 ID 不会离开电脑。
- 本程序不会传输任何会话内容。
- 源码仓库和发行包均不包含真实 Codex 会话日志。

Codex JSONL 可能包含对话内容。报告问题时请勿上传这些文件；通常只需提供现象、应用版本、操作系统和 Codex Desktop 版本。

## 常见问题

### 切换任务后没有更新

Windows 先检查是否勾选了**锁定当前会话**。前台标题需要与本地索引中的一个对话精确匹配；同名对话可改为不同名称。打开已完成过模型回复的对话，确认配置的 `sessions` 目录旁存在 `session_index.jsonl`。未知标题、缺失日志、不可用的 UI Automation 数据或未写完的索引会显示等待，不会借用另一个对话的数值。修改 `CODEX_HOME` 后重启本工具。150 ms 是轮询目标，不是完成刷新时间的保证。

macOS 仍从内部 IPC 获取任务信号。请同时重启 Codex Desktop 和本工具；如果 Codex 刚更新，请检查项目是否已有新版本。其回退模式能显示近期 Token，但不一定能识别界面中选中的未运行任务。

### macOS 菜单栏显示 `Token —`

- 打开至少完成过一次模型回复的 Codex 任务。
- 确认 `~/.codex/sessions` 存在，或自定义 `CODEX_HOME` 对图形应用可见。
- 修改 `CODEX_HOME` 后重启本工具。
- 如果新版 Codex 改变了 IPC，菜单会先退回最近的兼容根会话。

### 卸载

- Windows：从托盘退出并删除解压目录；如需清除设置，可删除 `%LOCALAPPDATA%\CodexTokenOverlay`。
- macOS：从菜单栏退出，关闭“登录时启动”，再从 `/Applications` 删除 `CodexTokenOverlay.app`。

## 从源码构建

### Windows

开发需要 .NET 10 SDK：

```powershell
dotnet restore .\src\CodexTokenOverlay\CodexTokenOverlay.csproj
dotnet build .\src\CodexTokenOverlay\CodexTokenOverlay.csproj -c Release
dotnet run --project .\tests\VisibleThreadRouting -c Release
dotnet run --project .\tests\ThreadSwitching -c Release
dotnet run --project .\tests\ContextAlerts -c Release
.\scripts\Test-LogParser.ps1
.\scripts\Test-OverlayLogic.ps1 -Area All
```

合成数据测试分别包含：25 项标题路由检查、16 项会话选择/日志/锁定/过期结果检查、18 项上下文提醒检查。原有日志解析和悬浮窗逻辑脚本继续验证既有行为。这些测试不能证明所有 Codex Desktop 版本的兼容性或真实刷新延迟，仍需在目标桌面验证实际前台对话切换。

同时生成本地轻量版和独立版发行包：

```powershell
.\scripts\Publish-Local.ps1 -RuntimeIdentifier win-x64 -Variant Both
```

也可以把 `-Variant` 设置为 `Lite` 或 `Standalone`，只生成其中一种。

发布脚本会检查两种 Windows 目标的 PE 架构，并对 x64 产物执行原有的 EXE 探针；Arm64 仍为交叉构建。单独发布到 `dist/win-x64` 后也可手动验证：

```powershell
.\scripts\Test-PublishedExecutable.ps1 -ExecutablePath .\dist\win-x64\CodexTokenOverlay.exe
.\scripts\Test-PeArchitecture.ps1 -ExecutablePath .\dist\win-x64\CodexTokenOverlay.exe -Architecture x64
```

### macOS

开发需要 macOS 14 或更高版本以及 Xcode Command Line Tools：

```bash
swift test --package-path macos
./script/build_and_run.sh --verify
```

仓库的 Codex 环境也会把同一个脚本显示为 **Run** 操作。生成发行形态的 `.app`：

```bash
./macos/script/package_app.sh \
  --arch "$(uname -m)" \
  --configuration release \
  --version 0.3.0 \
  --output artifacts/macos-local
```

## 仓库文件说明

- `src/CodexTokenOverlay`：保持独立的 .NET/WinForms Windows 应用。
- `packaging/windows`：Windows 轻量版的共享运行时说明。
- `macos/Package.swift`：macOS 原生 SwiftPM 工程。
- `macos/Sources/CodexTokenCore`：会话发现、Token 解析和 Unix IPC 任务路由。
- `macos/Sources/CodexTokenOverlayMac`：原生 AppKit 菜单栏应用。
- `macos/Tests`：仅使用合成数据的解析与任务切换测试。
- `macos/script/package_app.sh`：组装 `.app`、验证架构并执行 ad-hoc 签名。
- `script/build_and_run.sh`：macOS 开发构建、启动和调试入口。
- `.github/workflows`：Windows 与 macOS 的 CI 和 Release 自动化。

## 许可证

本项目使用 MIT License，详见 [LICENSE](LICENSE)。
