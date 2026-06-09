# CLAUDE.md — Nong.Cli.Net

Pure .NET scientific document generation toolkit. Zero JavaScript. One merged foundation DLL.

## 仓库

- GitHub: `https://github.com/angri450/Nong.Cli.Net`
- Gitee: `https://gitee.com/angri450/Nong.Cli.Net`
- GitCode: `git@gitcode.com:angri450/Nong.Cli.Net.git`
- 主分支: `main`
- 协议: Apache-2.0

## 当前进度（2026-06-08）

- 发布版本: `4.0.0`
- 当前主线: GitHub / Gitee / GitCode 的 `master`
- 4.0.0 NuGet 主线包已全部发布（14 个包，详见 `.claude/references/project-structure.md`）
- OCR runtime 已拆到独立 `Nong.OcrRuntime` 仓库维护，主仓库只消费已发布 runtime 版本
- 4.0.0 发布验证见 `log/changelog/2026-06-08-nong-4.0.0-release.md`
- 重要架构变化:
  - `chart` / `diagram` PNG 渲染已隔离到隐藏 worker: `nong __render-worker ...`
  - 主 CLI 进程只做参数校验和子进程调度，不直接执行 SkiaSharp/ScottPlot native 渲染
  - 如果 native 渲染崩溃，worker 子进程退出，主 CLI 返回结构化错误，不拖垮主进程
  - 默认测试套件不跑 Chart/Diagram/OCR/PDF 的 native 图像渲染路径

---

## Agent 开发工作流（强制）

每次 Agent 开发任务必须按以下四步循环执行：

### 1. Plan — 先交互确认方案

- 在动手写代码之前，先跟用户确认要做什么、怎么改、影响范围
- 将确认后的方案写入 `log/plans/YYYY-MM-DD-topic.md`
- 同步更新 `log/plans/index.md`

### 2. Build — 按方案施工

- 开始前先读 `log/plans/index.md` 确认当前方案
- 需要详细参考信息时，按下方导航表读 `.claude/references/` 中的对应文件
- 需要了解历史上下文时，先读 `log/guidance/index.md` 和 `log/changelog/index.md`，再按需读具体文件

### 3. Changelog — 完工记录

- 代码完工后，写变更记录到 `log/changelog/YYYY-MM-DD-topic.md`
- 同步更新 `log/changelog/index.md`
- 如果是发布相关变更，写清楚影响哪个包、Skill Agent 需要怎么跟进

### 4. Debug — 收集反馈

- 用户测试反馈存入 `log/debug/YYYY-MM-DD-topic.md`
- 同步更新 `log/debug/index.md`
- Debug 反馈是下一轮 Plan 的输入

---

## 导航表

需要以下信息时，读对应的参考文件（按需加载，不要全读）：

| 你需要 | 去这里 |
|--------|--------|
| 当前施工方案 | `log/plans/index.md` |
| 历史开发指导 / Stage 蓝图 | `log/guidance/index.md` |
| 历史变更记录 | `log/changelog/index.md` |
| 用户测试反馈 | `log/debug/index.md` |
| 仓库级维护文档 | `docs/` |

详细的构建规范、NuGet 发布流程、第三方源码注意事项等开发参考信息存放在 `.claude/references/`（本地文件，不推送到远程仓库）。Agent 开发时可直接读取。

---

## 禁止事项

- 不要引入 JavaScript 依赖
- 不要用 Python 实现核心功能；本地 OCR 必须走纯 .NET PP-OCRv5
- 不要为第三方库创建独立的 `.csproj` — 统一走 ThirdParty
- 不要提交第三方上游 `.git/` 目录；第三方源码只能作为 fork-pinned source snapshot 提交，清单维护在 `docs/DEPENDENCY_CONTROL.md`
- 不要用 PowerShell 的 `-replace` 批量编辑 `.csproj` — 会损坏 XML，用 Edit 工具逐文件改
- 不要把 Chart/Diagram/OCR/PDF render 的真实 PNG 生成测试加回默认 `dotnet test`

---

## 第三方源码快照规则

- 第三方源码目录要保留，因为 `ThirdParty/ThirdParty.csproj` 直接编译这些源码。
- 每个上游先 fork 到 `https://github.com/angri450/`，在 `docs/DEPENDENCY_CONTROL.md` 记录 fork URL 和 locked commit。
- 拉取/刷新上游源码时，先在 `_archive/` clone/fetch，确认 commit，再复制需要的源码、资源、LICENSE、NOTICE 到本仓库；`_archive/` 是本地临时区，不能提交。
- 复制进本仓库前必须删除上游 `.git/`、CI 缓存、包输出、demo、example、test、`bin/`、`obj/` 等非必要内容。
- 如果需要保留裁剪说明，在对应第三方目录放 `NONG_SOURCE.md`；已有本地适配和冲突处理也要写入 CLAUDE 或 changelog。
- 不要把第三方源码改成 Git submodule；当前策略是“源码快照 + 固定 fork commit + 本仓库直接编译”。

---

## 对项目维护者的说明

你不用自己写代码。告诉 Claude 要做什么，Claude 负责：
- 写代码、修 bug、加功能
- 改 csproj、build、pack、推送 NuGet
- 创建 GitHub Release
- 整理项目结构

你只需要：
- 决定方向（"加个饼图"、"发版"、"修那个 bug"）
- 提供 API Key（NuGet 推送时，环境变量 `$env:NUGET_API_KEY` 已就绪）
- 在 GitHub 网页上手动把 master 合并到 main（你想合的时候）
