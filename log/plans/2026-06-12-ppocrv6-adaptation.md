# 施工方案：PP-OCRv6 适配全线计划

## 背景

PaddleOCR 正式发布了 PP-OCRv6。与 v5 相比，核心变化：

| 维度 | PP-OCRv5 | PP-OCRv6 |
|------|----------|----------|
| 分级体系 | mobile / server | tiny / small / medium |
| 检测 Backbone | MobileNetV3 / PPLCNetV3 | **PPLCNetV4** (三个尺寸统一) |
| 检测 Neck | LK-PAN | RepLKFPN (tiny/small) / RepLKPAN+intracl (medium) |
| 识别 Backbone | MobileNetV3 / PPLCNetV3 | **PPLCNetV4** |
| 识别 Head | CTCHead 单头 | **MultiHead** (CTC + NRTR) |
| 识别 Neck | 无 | reshape (tiny) / lightsvtr (small/medium) |
| 模型格式 | .pdmodel + .pdiparams | **PIR** (inference.json + .pdiparams) |
| 字典 | ppocrv5_dict.txt (18383行) | ppocrv6_dict.txt (18708行) / ppocrv6_tiny_dict.txt (6904行) |
| 分类器 | ch_PP-OCRv3 | 复用 ch_PP-OCRv3 (无新 cls 模型) |

## 现场测试结果 (2026-06-12)

PP-OCRv6 medium 模型 (det 59MB + rec 73MB) 下载并实测：

```
Test 1  PaddleConfig.FromModelDir (low-level)      PASS
Test 2  ModelVersion.V5 wrappers                   PASS
Test 3  FullOcrModel                               PASS
Test 4  PaddleOcrDetector from dir                  PASS
Test 5  End-to-end synthetic image OCR              PASS (置信度 1.00 / 0.98)
```

**核心结论**：PaddleInference 3.3.1.70 runtime 原生支持 PIR 格式。Sdcb.PaddleOCR 3.3.1 没有 ModelVersion.V6，但用 V5 枚举 + FileDetectionModel/FileRecognizationModel 从目录加载可正常工作。不需要更新原生 runtime DLL。

观察到的弃用警告：`PD_ConfigSetOnednnCacheCapacity` 替代旧 API — 这是 PaddleInference C API 层面的变化，当前不影响功能，但标注为后续版本需要跟踪的信号。

## 适配原则

1. **Runtime 包不轻易更新**（5 个平台 nuget 每个 50-80MB，推送/镜像同步成本高），但 **必须更新时就要更新**。v6 模型格式变成 PIR、Paddle 3.0 废弃了部分 API 字段，当前 runtime 尚可兼容，但如果后续 v6.x 引入不兼容算子，runtime 必须跟着升级。
2. **CLI 做索引，不做重逻辑**。CLI 只负责模型 ID 路由、下载 URL 模板、字典文件引用、版本号常量。不嵌模型加载逻辑。
3. **使用指导在 Nong.Toolkit.Net 的 OCR skill**。命令路由表、模型选择、安装说明全部在 SKILL.md + references 中维护。

## 三层改动清单

### 第一层：Nong.OcrRuntime (native 运行时仓库)

**决策：本次不更新。**

理由：
- 测试证明 3.3.1.70 runtime 完全兼容 v6 模型
- v6 模型使用的 PPLCNetV4、RepLKPAN、MultiHead、lightsvtr 等新算子全部在 native 层执行，C# 层不感知
- 上游包版本暂时不变 (Sdcb.PaddleInference 3.3.1.70, OpenCvSharp4 不变)

**何时必须更新**：
- PaddleInference 上游发新版，修复了 v6 模型的已知 bug
- v6.x 模型引入了 PaddleInference 3.3.1.70 不支持的新算子
- API 弃用 (PD_ConfigSetOnednnCacheCapacity) 从警告变成硬错误
- 上游 OpenCvSharp4 或 PaddleInference 有安全漏洞修复

更新时操作：`VERSION` -> 5.0.0，csproj 同步，pack-runtimes.ps1 重新打包，推送 5 个 nupkg。

### 第二层：Nong.Cli.Net (CLI + MultiModal)

**改动项 (小改动，共 6 处)**：

| # | 文件 | 改动 | 原因 |
|---|------|------|------|
| A | `Cli/Common/OcrRuntimeVersion.cs` | `Current` 不改。添加 v6 模型版本常量 | runtime 没变，版本号不动 |
| B | `MultiModal/OcrModels.cs` | 新建 `PpOcrV6Client` 类，或改造 `PpOcrV5Client` 支持 `ModelId` 参数 (pp-ocrv6-tiny/small/medium) | 支持多模型 ID |
| C | `MultiModal/PpOcrV5/PpOcrV5ModelResolver.cs` | 添加 v6 模型缓存路径 `pp-ocrv6-{size}` + v6 字典路径解析 | v6 模型分三层，字典不同 |
| D | `Cli/Commands/OcrCommands.cs` | `install-model` 支持新 model-id: `pp-ocrv6-tiny` / `pp-ocrv6-small` / `pp-ocrv6-medium` | CLI 索引路由 |
| E | `Cli/Commands/OcrCommands.cs` | `models` 命令列出 v6 三层模型 | 模型清单 |
| F | `Cli/Commands/OcrCommands.cs` | `check-env` 输出增加 v6 状态字段 | 环境检查 |

**不改动**：
- `PpOcrV5Client` 保留，v5 模型仍然可用
- `PaddleOcrVlClient` (云端 OCR) 不动
- nuget 包引用不变

### 第三层：Nong.Toolkit.Net OCR skill

**改动项**：

| # | 文件 | 改动 |
|---|------|------|
| A | `ocr/SKILL.md` | 路由表增加 v6 模型条目；模型 ID 对照表；版本选择建议 |
| B | `ocr/references/ocr-local.md` | v6 模型的安装命令、缓存路径、字典文件位置 |
| C | `ocr/references/runtime-chain.md` | 更新运行时链路图，标注 v5/v6 共用同一 native runtime |
| D | `ocr/examples/` | 新增 v6 安装和本地识别示例 |

## 模型下载 URL 模板

v6 推理模型托管地址：

```
https://paddle-model-ecology.bj.bcebos.com/paddlex/official_inference_model/paddle3.0.0/
  PP-OCRv6_tiny_det_infer.tar
  PP-OCRv6_tiny_rec_infer.tar
  PP-OCRv6_small_det_infer.tar
  PP-OCRv6_small_rec_infer.tar
  PP-OCRv6_medium_det_infer.tar
  PP-OCRv6_medium_rec_infer.tar
```

字典文件从 PaddleOCR 仓库 embedded：
```
ppocr/utils/dict/ppocrv6_dict.txt       → v6 medium / small
ppocr/utils/dict/ppocrv6_tiny_dict.txt  → v6 tiny
ppocr/utils/dict/ppocrv5_dict.txt       → v5 (保留)
```

## 使用体验目标

用户最终看到的命令 (保持现有模式，只加 model-id)：

```powershell
# 当前 v5
nong ocr install-model pp-ocrv5-mobile --source <mirror> --json

# v6 新增
nong ocr install-model pp-ocrv6-medium --source <mirror> --json
nong ocr install-model pp-ocrv6-small --source <mirror> --json
nong ocr install-model pp-ocrv6-tiny --source <mirror> --json

# 模型清单
nong ocr models --json
# 输出增加 v6 三层

# 环境检查
nong ocr check-env --json
# 输出增加 localDotNetPpOcrV6 状态

# 本地识别 (engines 不变，按 install 的模型自动选)
nong ocr local scan.png --json
```

## 执行顺序

1. **Nong.Cli.Net** — PpOcrV5ModelResolver 扩展 + PpOcrV6Client + OcrCommands 路由
2. **Nong.OcrRuntime** — 暂不动；观察 PaddleInference 上游更新节奏
3. **Nong.Toolkit.Net** — OCR skill 文档同步更新
4. **打包测试** — 用 v6 模型目录跑实际图片识别
5. **发布 CLI** — 小版本 bump (4.0.1 或 4.1.0)，不涉及 runtime 包重新打包

## 风险

| 风险 | 等级 | 应对 |
|------|------|------|
| Sdcb.PaddleOCR 上游发新版加入 ModelVersion.V6，我们用法可能不兼容 | 低 | 当前用 V5 枚举 + 目录加载的方式是合法公开 API |
| v6 模型后续更新引入不兼容算子 | 低 | Runtime 仓库已准备好 bump 流程 |
| PIR 格式细节变化导致 CreatePredictor 失败 | 低 | 已在当前模型文件上验证通过 |
| 字典行数变化影响识别结果 | 无 | ppocrv6_dict.txt 直接从 PaddleOCR 仓库提取，已验证 |
