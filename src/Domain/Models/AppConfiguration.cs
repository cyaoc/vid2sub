namespace Vid2Sub.Domain.Models;

/// <summary>
/// 应用程序配置，映射 config.yaml 结构
/// </summary>
public sealed class AppConfiguration
{
    /// <summary>
    /// 模型配置
    /// </summary>
    public ModelConfig Model { get; set; } = new();
    
    /// <summary>
    /// 推理参数配置
    /// </summary>
    public InferenceConfig Inference { get; set; } = new();
    
    /// <summary>
    /// 外部环境配置
    /// </summary>
    public EnvironmentConfig Environment { get; set; } = new();
    
    /// <summary>
    /// 输出设置
    /// </summary>
    public OutputConfig Output { get; set; } = new();
}

/// <summary>
/// 模型配置
/// </summary>
public sealed class ModelConfig
{
    /// <summary>
    /// 模型类型: Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo
    /// </summary>
    public string Type { get; set; } = "LargeV3Turbo";
    
    /// <summary>
    /// 模型存储目录路径
    /// </summary>
    public string StorageDir { get; set; } = "./models";
    
}

/// <summary>
/// 推理参数配置
/// </summary>
public sealed class InferenceConfig
{
    /// <summary>
    /// 识别语言: "zh", "en", "ja", "auto"
    /// </summary>
    public string Language { get; set; } = "auto";
    
    /// <summary>
    /// 并行处理线程数 (0 = 自动使用 CPU 核心数)
    /// </summary>
    public int Threads { get; set; } = 0;
    
    /// <summary>
    /// 束搜索大小 (Beam Size)
    /// </summary>
    public int BeamSize { get; set; } = 5;
}

/// <summary>
/// 外部环境配置
/// </summary>
public sealed class EnvironmentConfig
{
    /// <summary>
    /// FFmpeg 执行程序路径
    /// </summary>
    public string FfmpegPath { get; set; } = "ffmpeg";
    
    /// <summary>
    /// 临时文件存放目录
    /// </summary>
    public string TempDir { get; set; } = "./temp";
}

/// <summary>
/// 输出设置配置
/// </summary>
public sealed class OutputConfig
{
    /// <summary>
    /// 输出目录 (空字符串 = 与输入文件同目录)
    /// </summary>
    public string OutputDir { get; set; } = "";
    
    /// <summary>
    /// 输出格式: text, srt, vtt
    /// </summary>
    public string Format { get; set; } = "vtt";
    
    public string LogLevel { get; set; } = "information";
}
