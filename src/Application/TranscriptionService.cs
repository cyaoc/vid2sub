using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Utilities;
using Vid2Sub.Infrastructure.Models;
using Vid2Sub.Infrastructure.Subtitles;
using Whisper.net;
using Whisper.net.Ggml;

namespace Vid2Sub.Application;

/// <summary>
/// 转录服务
/// 协调音频预处理、Whisper 推理和字幕输出的完整流程
/// </summary>
public sealed class TranscriptionService(
    IModelProvider modelProvider,
    IAudioProcessor audioProcessor,
    AppConfiguration config) : IAsyncDisposable
{
    private WhisperFactory? _whisperFactory;
    private bool _disposed;
    private bool _forceCpu;
    
    /// <summary>
    /// 处理文件（统一入口，支持单文件和多文件）
    /// </summary>
    /// <param name="inputPaths">输入文件路径集合</param>
    /// <param name="outputDir">输出目录（可选，null = 输出到各文件同目录）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>转录结果集合</returns>
    public async IAsyncEnumerable<TranscriptionResult> ProcessAsync(
        IEnumerable<string> inputPaths,
        string? outputDir = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var files = inputPaths.ToList();
        var total = files.Count;
        var current = 0;
        
        // 如果指定了输出目录，确保目录存在
        if (!string.IsNullOrEmpty(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        
        foreach (var inputPath in files)
        {
            current++;
            Console.WriteLine($"\n[{current}/{total}] Processing: {Path.GetFileName(inputPath)}");
            
            // 计算输出路径
            var outputPath = DetermineOutputPath(inputPath, outputDir);
            
            TranscriptionResult? result = null;
            
            try
            {
                result = await ProcessSingleFileAsync(inputPath, outputPath, cancellationToken);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed: {inputPath}");
                Console.Error.WriteLine($"  Error: {ex.Message}");
                
                // 继续处理下一个文件
                result = new TranscriptionResult(inputPath, []);
            }
            
            yield return result;
        }
    }
    
    /// <summary>
    /// 处理单个文件（内部方法）
    /// </summary>
    private async Task<TranscriptionResult> ProcessSingleFileAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input file not found", inputPath);
        }
        
        var verbose = config.Output.Verbose;
        
        if (verbose)
        {
            Console.WriteLine($"Processing file: {inputPath}");
        }
        
        // 1. 确保模型可用（带进度条）
        var modelType = config.Model.GetModelType();
        string modelPath;
        
        if (!modelProvider.ModelExists(modelType))
        {
            // 模型不存在，需要下载，显示进度条
            var estimatedSize = WhisperModelProvider.GetEstimatedModelSize(modelType);
            using var progress = new ConsoleProgressBar($"Downloading {modelType}", estimatedSize);
            modelPath = await modelProvider.EnsureModelAsync(modelType, progress, cancellationToken);
        }
        else
        {
            modelPath = await modelProvider.EnsureModelAsync(modelType, cancellationToken: cancellationToken);
        }
        
        // 2. 初始化 Whisper (如果尚未初始化)
        await EnsureWhisperInitializedAsync(modelPath, verbose);
        
        // 3. 转换音频格式
        if (verbose)
        {
            Console.WriteLine("Converting audio format...");
        }
        
        var wavPath = await audioProcessor.ConvertToWavAsync(inputPath, cancellationToken);
        
        try
        {
            // 4. 执行转录
            if (verbose)
            {
                Console.WriteLine("Transcribing...");
            }
            
            var segments = await TranscribeAsync(wavPath, cancellationToken);
            
            // 5. 输出字幕文件
            var writer = SubtitleWriterFactory.Create(config.Output.GetOutputFormat());
            await writer.WriteAsync(segments, outputPath, cancellationToken);
            
            if (verbose)
            {
                Console.WriteLine($"Subtitle saved: {outputPath}");
            }
            
            return new TranscriptionResult(
                inputPath,
                segments,
                config.Inference.Language == "auto" ? null : config.Inference.Language);
        }
        finally
        {
            // 清理临时文件
            audioProcessor.CleanupTempFile(wavPath);
        }
    }
    
    /// <summary>
    /// 执行转录
    /// </summary>
    private async Task<List<TranscriptionSegment>> TranscribeAsync(
        string wavPath,
        CancellationToken cancellationToken)
    {
        var segments = new List<TranscriptionSegment>();
        
        try
        {
            using var processor = _whisperFactory!.CreateBuilder()
                .WithLanguage(config.Inference.Language)
                .WithThreads(config.Inference.GetEffectiveThreads())
                .Build();
            
            await using var fileStream = File.OpenRead(wavPath);
            
            await foreach (var result in processor.ProcessAsync(fileStream, cancellationToken))
            {
                var segment = new TranscriptionSegment(result.Start, result.End, result.Text);
                segments.Add(segment);
                
                if (config.Output.Verbose)
                {
                    Console.WriteLine($"  [{FormatTime(result.Start)} -> {FormatTime(result.End)}] {result.Text}");
                }
            }
        }
        catch (Exception ex) when (IsGpuMemoryError(ex) && !_forceCpu)
        {
            Console.Error.WriteLine("Warning: GPU out of memory or driver not supported, falling back to CPU mode...");
            
            // 标记强制使用 CPU
            _forceCpu = true;
            
            // 重新初始化
            var modelPath = modelProvider.GetModelPath(config.Model.GetModelType());
            await ReinitializeAsync(modelPath);
            
            // 重试转录
            return await TranscribeAsync(wavPath, cancellationToken);
        }
        
        return segments;
    }
    
    /// <summary>
    /// 确保 Whisper 已初始化
    /// </summary>
    private async Task EnsureWhisperInitializedAsync(string modelPath, bool verbose)
    {
        if (_whisperFactory is not null)
        {
            return;
        }
        
        await Task.Run(() =>
        {
            // 依赖 Whisper.net Autoprobe 自动选择最佳运行时
            // 优先级: CUDA > Vulkan > CoreML > CPU
            _whisperFactory = WhisperFactory.FromPath(modelPath);
        });
        
        if (verbose)
        {
            Console.WriteLine("Whisper engine initialized (Autoprobe mode)");
        }
    }
    
    /// <summary>
    /// 重新初始化 Whisper
    /// </summary>
    private async Task ReinitializeAsync(string modelPath)
    {
        _whisperFactory?.Dispose();
        _whisperFactory = null;
        
        await Task.Run(() =>
        {
            // 重新初始化，Whisper.net 会自动探测可用的运行时
            _whisperFactory = WhisperFactory.FromPath(modelPath);
        });
        
        Console.WriteLine("Whisper engine reinitialized");
    }
    
    /// <summary>
    /// 判断是否为 GPU 显存错误
    /// </summary>
    private static bool IsGpuMemoryError(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("out of memory") ||
               message.Contains("cuda") ||
               message.Contains("vulkan") ||
               message.Contains("gpu") ||
               message.Contains("device") ||
               message.Contains("driver");
    }
    
    /// <summary>
    /// 确定输出文件路径
    /// </summary>
    /// <param name="inputPath">输入文件路径</param>
    /// <param name="outputDir">输出目录（null = 使用输入文件同目录）</param>
    /// <returns>完整的输出文件路径</returns>
    private string DetermineOutputPath(string inputPath, string? outputDir)
    {
        var fileName = Path.GetFileNameWithoutExtension(inputPath);
        var writer = SubtitleWriterFactory.Create(config.Output.GetOutputFormat());
        
        // 如果指定了输出目录，使用该目录；否则使用输入文件同目录
        var directory = !string.IsNullOrEmpty(outputDir) 
            ? outputDir 
            : Path.GetDirectoryName(inputPath) ?? ".";
        
        return Path.Combine(directory, fileName + writer.FileExtension);
    }
    
    /// <summary>
    /// 格式化时间显示
    /// </summary>
    private static string FormatTime(TimeSpan time)
    {
        return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}";
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        
        _whisperFactory?.Dispose();
        await audioProcessor.DisposeAsync();
    }
}
