# nong CLI 当前能力表 v3.1.x

日期：2026-06-03
源：`nong commands --json` + AGENT.md + 实测

---

## 快速安装

```bash
dotnet tool install --global Angri450.Nong.Cli
nong commands --json       # 命令发现
nong word read file.docx   # 第一核心命令
```

---

## 已实现命令（24 个）

### word —— Word 文档引擎

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong word read <file>` | 提取纯文本 | .docx | `nong word read paper.docx` |
| `nong word preview <file>` | 7 步诊断 | .docx | `nong word preview paper.docx` |
| `nong word fill <tmpl> <data> -o <f>` | 模板填充 | .docx + .json | `nong word fill t.docx d.json -o out.docx` |
| `nong word rebuild <file> -o <f>` | 样式清理 | .docx | `nong word rebuild dirty.docx -o clean.docx` |

### inspect —— 论文诊断与写作

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong inspect diagnose <file>` | 完整论文诊断 | .txt | `nong inspect diagnose paper.txt --json` |
| `nong inspect refs <file>` | 参考文献检查 | .txt | `nong inspect refs paper.txt` |
| `nong inspect write-paper <spec> -o <f>` | 论文生成 | .json | `nong inspect write-paper spec.json -o paper.docx` |

### chart —— 统计与图表

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong chart analyze <data>` | ANOVA+Duncan+描述统计 | .json | `nong chart analyze groups.json` |
| `nong chart anova <data>` | 单因素方差分析 | .json | `nong chart anova groups.json --json` |
| `nong chart duncan <data> [--alpha]` | Duncan 多重比较 | .json | `nong chart duncan groups.json` |
| `nong chart bar <data> -o <png>` | 柱状图（误差棒+显著性字母） | .json | `nong chart bar groups.json -o fig.png --json` |

### excel —— Excel 数据入口

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong excel sheets <file>` | 列出 sheet | .xlsx | `nong excel sheets data.xlsx --json` |
| `nong excel read <file> [--sheet] [--range]` | 读取内容 | .xlsx | `nong excel read data.xlsx --json` |
| `nong excel to-groups <file> --group <col> --value <col> [--raw]` | Excel→分组数据 | .xlsx | `nong excel to-groups data.xlsx --group A --value B --raw` |

### diagram —— 科学图表

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong diagram flowchart <spec> -o <png>` | 流程图 | .json | `nong diagram flowchart spec.json -o flow.png` |
| `nong diagram network <spec> -o <png>` | 网络图 | .json | `nong diagram network spec.json -o net.png` |

### genre / icons —— 模板与素材

| 命令 | 功能 | 示例 |
|------|------|------|
| `nong genre list` | 列出格式模板 | `nong genre list --json` |
| `nong genre show <name>` | 查看模板内容 | `nong genre show degree-thesis` |
| `nong icons list` | 列出图标 | `nong icons list --json` |
| `nong icons search <q>` | 搜索图标 | `nong icons search "dna"` |

---

## 核心工作流

### 1. Excel → 统计 → 图表
```bash
nong excel to-groups data.xlsx --group A --value B --raw > groups.json
nong chart analyze groups.json --json
nong chart bar groups.json -o fig.png --json
```
→ artifacts.png 即带显著性字母的柱状图

### 2. Word 生成 → 再读取
```bash
nong inspect write-paper spec.json -o paper.docx --json
nong word preview paper.docx --json
nong word read paper.docx --json
```

### 3. 论文诊断
```bash
nong word read paper.docx > paper.txt
nong inspect diagnose paper.txt --json
nong inspect refs paper.txt --json
```
→ 读 diagnose.data.paperType / data.gapGrade / data.evidence[*]

---

## JSON 输出 schema

每个命令 `--json` 返回统一结构：

```json
{
  "status": "ok" | "error",
  "command": "word read",
  "summary": "...",
  "data": {},
  "issues": [],
  "artifacts": { "png": "fig.png" },
  "metrics": { "paragraphs": 29 },
  "errors": [],
  "meta": { "durationMs": 42, "version": "3.1.0" }
}
```

## 错误码

| 代码 | 名称 | 含义 |
|------|------|------|
| E001 | file_not_found | file not found |
| E002 | unsupported_format | wrong extension |
| E003 | missing_argument | arg required |
| E004 | internal_error | unexpected crash |
| E005 | dependency_missing | tool not installed |
| E006 | validation_failed | bad input |
| E007 | read_failed | can't read |
| E008 | write_failed | can't write |
| E009 | not_implemented | command not done yet |

### skill —— Skill 生命周期管理

| 命令 | 功能 | 输入 | 示例 |
|------|------|------|------|
| `nong skill validate <dir>` | 验证 SKILL.md 结构和引用 | 目录 | `nong skill validate ./word --json` |
| `nong skill scan <dir>` | 安全扫描 skill/插件目录 | 目录 | `nong skill scan ./GroundPA-Toolkit --json` |
| `nong skill inventory <dir>` | 列出 skill 目录内容 | 目录 | `nong skill inventory ./GroundPA-Toolkit --json` |
| `nong skill package <dir>` | validate+scan+打包为 .zip | 目录 | `nong skill package ./word --json` |

注意：请使用 `nong skill` 命令，不要使用旧版 `skill-manager` global tool。

---

## 输入格式速查

| 格式 | 喂给哪些命令 |
|------|------------|
| .docx | word read / preview / rebuild + word fill (template) |
| .txt | inspect diagnose / refs |
| .json (paper spec) | inspect write-paper |
| .json (groups: `{"A":[1,2],"B":[3,4]}`) | chart analyze / anova / duncan / bar |
| .json (diagram spec) | diagram flowchart / network |
| .xlsx | excel sheets / read / to-groups |

---

## 计划但未实现（stub，返回 E009）

word extract / dissect / stats / fonts / styles / validate / merge
inspect classify / structure / varplan / evidence / data-req / gap / semantics
chart line / scatter / pie
diagram tree
pptx read / slides
ocr local / cloud
excel create

---

## 关键约束

- pptx read — 暂桩，PptxCore 需适配 ShapeCrawler 合并
- Duncan — 使用简化 Q 值近似，正式论文需复核
- 生成命令 — 输出目录不存在会自动创建
- word rebuild — 输入输出不能同一路径
- stub — 所有未实现命令返回 E009 + EXIT:1
