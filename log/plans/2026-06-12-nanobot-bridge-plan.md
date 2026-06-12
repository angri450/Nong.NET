# 2026-06-12 NanoBot bridge — 三项目完整对接规划

日期: 2026-06-12
状态: pending（暂不进施工）

## 背景

Nong 三项目架构：Toolkit（Skill 层）→ CLI（执行层）→ NanoBot（Agent Runtime）。

Toolkit 和 CLI 已完成 OOXML 全覆盖施工（51 属性全闭环，P0-P2 全清）。NanoBot 之前有过一轮基建（SkillLoader 二段渐进式、run_nong bridge、system status 端点），但真正的工具注入还没完成。

## 当前 NanoBot 状态

### 已完成

| 组件 | 文件 | 状态 |
|------|------|------|
| SkillLoader 二段式 | `Nanobot.Core/Skills/SkillLoader.cs` | done — catalog→skill→reference |
| run_nong bridge | `Nanobot.Core/Tools/Builtin/NongTool.cs` | done — workspace 边界、allowlist、timeout、结构化错误 |
| nong capabilities 发现 | `NongTool.DiscoverCapabilitiesAsync()` | done — 调 `nong commands --json` |
| skill tools | `Nanobot.Core/Tools/Builtin/SkillTools.cs` | done — get_catalog, load_skill, load_reference |
| system status | `Nanobot.Web/Program.cs` /status 端点 | done — CLI/Toolkit/NanoBot 三态探测 |
| AgentLoop 集成 | `AgentLoop.cs` | partial — 已接 catalog 但未接 tools |

### 未完成

| 组件 | 缺口 |
|------|------|
| Tool registry | 14 条 word CLI 命令没有注册为 AgentLoop 工具 |
| Skill trigger 匹配 | catalog 加载了但未被 AgentLoop 用于 skill 激活决策 |
| 对话级工具注入 | `run_nong` 只在显式调用时有效，没有自动匹配 |
| Chart/PDF/OCR/Diagram | 非 word 模块（图表、扫描、OCR）完全未接入 |

## 规划施工

### Phase 1: Tool auto-registration（P0）

NanoBot 启动时，通过 `NongTool.DiscoverCapabilitiesAsync()` 获得完整命令面（`nong commands --json`），动态注册 14 个 word 模块命令为 agent 工具。Tool 的 description 从 Manifest.cs 的命令描述中提取。

```csharp
// Pseudocode for auto-registration
var capabilities = await NongTool.DiscoverCapabilitiesAsync();
foreach (var cmd in capabilities.Commands.Where(c => c.Module == "word"))
{
    var tool = new RunNongTool(cmd.Name, cmd.Description);
    toolRegistry.Register(tool);
}
```

工作量：~200 行 C#（AgentLoop + ToolRegistry 扩展），0 个新类。

### Phase 2: Skill trigger routing（P1）

AgentLoop 每次接收到 user message 后，调用 `SkillLoader.MatchSkill(userMessage)`——根据 message 内容匹配对应的 Skill（word/pdf/chart 等）。匹配到的 skill 自动 `LoadSkill` 注入 context。

```
User: "把这个表格紧缩一下"
→ SkillLoader.MatchSkill("把这个表格紧缩一下")
→ 匹配 word skill（触发词：表格、紧缩）
→ LoadSkill("word") → 注入 word 指令到 agent context
→ Agent 查看 context → 调用 nong word compact-tables
```

工作量：~150 行 C#（SkillMatcher 类 + AgentLoop 集成），1 个新类。

### Phase 3: Post-tool skill injection（P1）

Agent 调完工具后，根据返回的 JSON 结果自动注入对应的 skill reference——比如 `nong word estimate` 返回了 8 页、1 问题页，自动加载 `page-layout.md` 作为后续决策的参考。

工作量：~100 行 C#，0 个新类。

### Phase 4: Cross-module chat context（P2）

Chart/PDF/OCR/Diagram 模块接入。启动时注册所有 109 命令作为可用工具，根据对话内容在行进行动态 tool discovery。Web UI 面板展示当前已激活的 skills 和 tools。

工作量：~300 行 C#，1-2 个新类。

## 工作估算

| Phase | 新增 C# | 新类 | 优先级 |
|-------|---------|------|--------|
| Phase 1: Tool auto-reg | ~200 | 0 | P0 |
| Phase 2: Skill routing | ~150 | 1 | P1 |
| Phase 3: Post-tool injection | ~100 | 0 | P1 |
| Phase 4: Cross-module | ~300 | 2 | P2 |
| **Total** | **~750** | **3** | |

## 依赖关系

Phase 1 → Phase 2 → Phase 3（依赖链路）
Phase 4 独立（可随时插入）

## 施工时机

Toolkit 和 CLI 当前处于稳定态（15 条 word 命令、51 OOXML 属性覆盖）。NanoBot 桥接是最自然的下一步——它让 Toolkit 的 skill 描述和 CLI 的命令能力直接注入 agent 决策环路。

建议在完成以下前置条件后启动：
- [x] CLI 4.0.2+ 所有 word 命令发布到 NuGet
- [x] Toolkit SKILL.md 渐进式披露重构完毕
- [x] OOXML 深挖全闭环
- [ ] NanoBot 回归测试全部 102 条通过（上次未验证）
