# Whisper.net 集成开发规范 (Cursor Agent 指令)

## 1. 项目概览与环境约束
- **目标**: 构建一个跨平台、环境自适应的 Whisper 语音转文字工具。
- **运行时环境**: .NET 10
- **Language**: C# 14 (充分利用 Primary Constructors 增强版、更强的类型推导等)
- **硬件加速策略 (核心)**: 
    - **优先级管理**: 遵循 `CUDA > Vulkan > CoreML > CPU` 的加载策略。
    - **Windows 普适化**: 必须引用 `Whisper.net.Runtime.Vulkan` 以支持广大非 Nvidia 显卡（AMD/Intel 核显）用户。
    - **依赖注入**: 引用所有必要的 Runtime NuGet 包，确保 Autoprobe 机制生效。

## 2. 架构设计原则 (SOLID & DDD)
在编写代码时必须遵循以下工程标准：
- **Single Responsibility (SRP)**: 每个类仅负责一个功能。例如：`AudioService` 仅处理转码，不涉及模型下载。
- **Dependency Inversion (DIP)**: 依赖于接口而非具体实现。例如：定义 `IModelProvider` 接口，方便未来扩展不同的下载源。
- **DDD 思想**: 
    - **Domain Layer**: 包含识别任务的逻辑、语言枚举、结果模型。
    - **Infrastructure Layer**: 负责 FFmpeg 进程调用、磁盘 IO、NuGet 运行时加载。
- **Clean Code**: 优先使用 C# 14 的新特性（如 Primary Constructors, Collection Expressions），保持代码简洁、高可读。

## 3. 技术方案约束

### A. 配置与参数管理
- **覆盖逻辑**: 命令行参数 > YAML 配置文件 > 默认值。
- **库支持**: `YamlDotNet`, `System.CommandLine`。
- **无感加速**: 禁止在业务层写任何平台探测 (OS/GPU) 逻辑。
- **异常处理**: 必须处理“显存溢出”或“驱动不支持”的异常，并能优雅降级到 CPU。
- 优先使用 C# 14 的新特性简化代码（如更简洁的集合表达式、增强型 Lambda 参数等）。
- 使用 `Scoped` 资源管理，确保 Native 资源在异步流结束时立即释放。

### B. 音频适配流水线 (Infrastructure)
- **FFmpeg 规范**: 
    - 必须通过 `System.Diagnostics.Process` 直接调用。
    - 参数: `-i {in} -ar 16000 -ac 1 -c:a pcm_s16le -f wav {out}`。
    - **生命周期**: 必须实现 `IDisposable` 以确保临时文件在任务结束或崩溃时能被清理。

### C. 环境探测与初始化 (Autoprobe)
- **禁止**: 禁止使用手动 OS 判断来切换 GPU/CPU。
- **逻辑**: 直接初始化 `WhisperFactory`，通过运行时包自动发现硬件加速器。
- **日志**: 启动时必须打印 `factory.RuntimeDescription`。

### D. 异步与资源管理
- 全程使用 `async/await` 处理文件 IO 和长耗时推理任务。
- 确保 `WhisperFactory` 和 `WhisperProcessor` 被正确释放，避免显存/内存泄漏。

## 4. 推荐目录结构
- `/src/Domain`: 实体、接口、配置模型。
- `/src/Infrastructure`: FFmpeg 实现、模型下载器、文件系统访问。
- `/src/Application`: 协调业务流的主服务。
- `/src/CLI`: `Program.cs` 及其参数解析逻辑。

## 5. 交互指令建议
- "请按照 @agents.md 要求的 SOLID 原则，将目前的逻辑重构为 Domain 和 Infrastructure 两层。"
- "基于 .NET 10 语法实现 @agents.md 中的音频预处理逻辑。"
- "检查代码是否符合 @agents.md 的资源释放（Dispose）规范，防止显存泄漏。"