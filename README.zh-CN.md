# CodexTokenSaver

中文 | [English](README.md)

CodexTokenSaver 是一个 Windows 优先的本地工具包，用 `docs/codex` 上下文包、交接摘要和托盘红黄绿灯，帮助 Codex 用户降低长会话继续工作时的 token 压力。

> 非官方项目，不属于 OpenAI 产品。它依赖 Codex 本地日志文件；如果 Codex 未来修改本地日志格式，本项目可能需要适配。

## 为什么需要它

Codex 会话越长，后续每一轮可能携带的历史上下文越大，响应越慢，token 消耗也越高。贴图很多的会话尤其容易变重。

CodexTokenSaver 不是把当前已经很重的会话原地变小，而是提供一套可执行的迁移流程：

1. 把长期项目状态写入文件。
2. 会话变重时生成精简交接提示词。
3. 新开一个干净 Codex 会话，只带交接摘要和必要文件。
4. 用 Token Lights 判断什么时候该准备交接。

## 包含什么

- **Token Lights**：Windows 托盘监控，读取 Codex 最近的 `token_count` 事件并显示绿/黄/红状态。
- **Handoff Summary skill**：可安装的 Codex skill，用于生成给新会话使用的精简交接提示词。
- **Context pack 模板**：用于建立 `docs/codex/` 项目上下文包。
- **AGENTS 示例**：让 agent 默认把重要项目状态写入文件，而不是依赖聊天历史。
- **安装脚本**：源码优先安装，不在仓库提交预编译 exe。

## 快速安装

要求：

- Windows 10/11
- Python 3，可通过 `py -3` 或 `python` 调用
- Windows .NET Framework 编译器，通常位于 `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`

安装：

```powershell
git clone https://github.com/newHashub/CodexTokenSaver.git
cd CodexTokenSaver
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1
```

启动 Token Lights：

```cmd
apps\token-lights\start.cmd
```

停止 Token Lights：

```cmd
apps\token-lights\stop.cmd
```

创建 Windows 开机启动快捷方式：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -CreateStartupShortcut
```

指定 Python 路径：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\install.ps1 -PythonPath C:\Path\To\python.exe
```

## Token Lights 是什么

Token Lights 读取活跃 Codex 会话日志：

```text
%USERPROFILE%\.codex\sessions\
%USERPROFILE%\.codex\session_index.jsonl
```

它同时使用两个信号：

- 最近一次 `last_token_usage.input_tokens`：表示单轮输入有多重
- `input_tokens / model_context_window`：表示离模型上下文窗口上限有多近

判灯规则：

- 绿色：input 低于 `80k`，且窗口占用低于 `50%`
- 黄色：input `80k` 到低于 `150k`，或窗口占用 `50%` 到低于 `75%`
- 红色：input `150k+`，或窗口占用 `75%+`

托盘图标显示最近活跃会话里的最严重状态。悬停显示：

```text
5小时 剩余额度 重置时间
1周   剩余额度 重置日期
灯    会话名称 最新 input tokens 窗口占用%
```

右键打开控制卡片，包含：

- 5 小时和 1 周剩余额度
- 最近会话
- 重会话
- 刷新控制
- 退出

已归档会话会被排除。

## 交接工作流

安装后，`handoff-summary` skill 会复制到：

```text
%USERPROFILE%\.codex\skills\handoff-summary\
```

在 Codex 里说：

```text
交接
```

或者：

```text
生成给新会话使用的 handoff summary。
```

它会生成一个精简提示词，让新 Codex 会话无需携带旧会话全部历史也能继续工作。

对于重要项目，它会优先让新会话读取这些持久文件：

```text
docs/codex/context.md
docs/codex/current-state.md
docs/codex/decisions.md
docs/codex/runbook.md
docs/codex/handoff.md
```

## 推荐使用方式

1. 正常使用 Codex。
2. 重要项目把状态沉淀到 `docs/codex/`。
3. Token Lights 变黄时，如果任务还会继续，开始准备交接。
4. Token Lights 变红时，生成 handoff 并切到新会话。
5. 新会话确认接上后，再归档或关闭旧会话。

## 隐私和安全边界

CodexTokenSaver 是本地优先工具。

它会：

- 读取本地 Codex JSONL 日志
- 在 `apps/token-lights/` 内写入运行态文件
- 把 handoff skill 复制到本地 Codex skills 目录

它不会：

- 上传数据
- 修改 Codex 数据库
- 重命名、归档或发送会话消息
- 修改 Codex 账号设置
- 把会话内容发送到服务器

生成的运行态文件，例如 `tray-state.json`，可能包含真实会话名和 token 使用数据。它们已经被 `.gitignore` 排除，不应该发布。

更多说明见 [docs/privacy.md](docs/privacy.md)。

## 仓库结构

```text
apps/token-lights/        Windows 托盘监控源码
skills/handoff-summary/   可安装的 Codex skill
templates/context-pack/   docs/codex 模板
templates/agents/         AGENTS.md 示例
docs/                     安装、工作流、隐私、故障排查文档
```

## 开发检查

```powershell
python -m py_compile apps\token-lights\codex_token_lights.py
powershell -NoProfile -ExecutionPolicy Bypass -File apps\token-lights\build-popup.ps1
```

## 当前限制

- Windows 优先。
- 依赖 Codex 本地 JSONL 日志结构。
- Token Lights 是预警信号，不是精确计费面板。
- 窗口占用百分比依赖 Codex token 日志里存在 `model_context_window`。
- 不会自动创建新会话、自动归档或自动操作账号。

## 许可证

MIT
