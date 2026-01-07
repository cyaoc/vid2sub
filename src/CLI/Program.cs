using System.CommandLine;
using System.CommandLine.Parsing;
using Vid2Sub.Application;
using Vid2Sub.Domain.Models;
using Vid2Sub.Infrastructure.Audio;
using Vid2Sub.Infrastructure.Configuration;
using Vid2Sub.Infrastructure.Models;

namespace Vid2Sub.CLI;

/// <summary>
/// Vid2Sub 命令行入口
/// 视频/音频转字幕工具
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        var rootCommand = new RootCommand("Vid2Sub - Video/audio to subtitle tool powered by Whisper.net");
        
        // 定义参数
        var inputArgument = new Argument<string[]>("input")
        {
            Description = "Input file(s) or directory path (supports multiple)",
            Arity = ArgumentArity.OneOrMore
        };
        
        // 定义选项
        var outputOption = new Option<string?>("--output-dir", "-o")
        {
            Description = "Output directory (default: same as input file)"
        };
        
        var formatOption = new Option<string?>("--format", "-f")
        {
            Description = "Output format: srt, vtt, text"
        };
        
        var languageOption = new Option<string?>("--language", "-l")
        {
            Description = "Recognition language: auto, zh, en, ja, etc."
        };
        
        var modelOption = new Option<string?>("--model", "-m")
        {
            Description = "Model type: Tiny, Base, Small, Medium, LargeV3, LargeV3Turbo"
        };
        
        var configOption = new Option<string?>("--config", "-c")
        {
            Description = "Specify config file path"
        };
        
        var verboseOption = new Option<bool>("--verbose", "-v")
        {
            Description = "Verbose output mode"
        };
        
        var threadsOption = new Option<int?>("--threads", "-t")
        {
            Description = "Number of processing threads"
        };
        
        // 添加到根命令
        rootCommand.Arguments.Add(inputArgument);
        rootCommand.Options.Add(outputOption);
        rootCommand.Options.Add(formatOption);
        rootCommand.Options.Add(languageOption);
        rootCommand.Options.Add(modelOption);
        rootCommand.Options.Add(configOption);
        rootCommand.Options.Add(verboseOption);
        rootCommand.Options.Add(threadsOption);
        
        // 设置处理程序
        rootCommand.SetAction(parseResult =>
        {
            var inputs = parseResult.GetValue(inputArgument) ?? [];
            var output = parseResult.GetValue(outputOption);
            var format = parseResult.GetValue(formatOption);
            var language = parseResult.GetValue(languageOption);
            var model = parseResult.GetValue(modelOption);
            var configPath = parseResult.GetValue(configOption);
            var verbose = parseResult.GetValue(verboseOption);
            var threads = parseResult.GetValue(threadsOption);
            
            try
            {
                return RunAsync(
                    inputs,
                    output,
                    format,
                    language,
                    model,
                    configPath,
                    verbose,
                    threads,
                    CancellationToken.None).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nOperation cancelled");
                return 130;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 1;
            }
        });
        
        return rootCommand.Parse(args).Invoke();
    }
    
    /// <summary>
    /// 执行转录任务
    /// </summary>
    private static async Task<int> RunAsync(
        string[] inputs,
        string? output,
        string? format,
        string? language,
        string? model,
        string? configPath,
        bool verbose,
        int? threads,
        CancellationToken cancellationToken)
    {
        // 1. 加载配置（优先级: 命令行参数 > YAML 配置 > 默认值）
        var configLoader = new YamlConfigLoader();
        var yamlPath = configPath ?? YamlConfigLoader.GetDefaultConfigPath();
        var config = await configLoader.LoadAsync(yamlPath, cancellationToken);
        
        // 1.5 解析相对路径（使其相对于可执行文件所在目录）
        config.ResolveRelativePaths();
        
        // 2. 应用命令行覆盖
        ApplyCommandLineOverrides(config, format, language, model, verbose, threads);
        
        if (config.Output.Verbose)
        {
            Console.WriteLine("Vid2Sub - Video/Audio to Subtitle Tool");
            Console.WriteLine($"Config file: {(File.Exists(yamlPath) ? yamlPath : "using defaults")}");
            Console.WriteLine($"Model: {config.Model.Type}");
            Console.WriteLine($"Language: {config.Inference.Language}");
            Console.WriteLine($"Output format: {config.Output.Format}");
            Console.WriteLine();
        }
        
        // 3. 初始化服务
        var modelProvider = new WhisperModelProvider(config.Model);
        var audioProcessor = new FFmpegAudioProcessor(config.Environment);
        
        await using var transcriptionService = new TranscriptionService(modelProvider, audioProcessor, config);
        
        // 4. 收集所有输入文件
        var filesToProcess = CollectInputFiles(inputs);
        
        if (filesToProcess.Count == 0)
        {
            Console.Error.WriteLine("Error: No valid input files found");
            return 1;
        }
        
        Console.WriteLine($"Found {filesToProcess.Count} file(s) to process\n");
        
        // 5. 处理文件（统一使用 ProcessAsync）
        var successCount = 0;
        var failCount = 0;
        
        await foreach (var result in transcriptionService.ProcessAsync(
            filesToProcess,
            output,  // 输出目录（null = 输入文件同目录）
            cancellationToken))
        {
            if (result.Segments.Count > 0)
            {
                successCount++;
            }
            else
            {
                failCount++;
            }
        }
        
        // 6. 输出统计
        Console.WriteLine();
        Console.WriteLine($"Processing complete: {successCount} succeeded, {failCount} failed");
        
        return failCount > 0 ? 1 : 0;
    }
    
    /// <summary>
    /// 应用命令行参数覆盖
    /// </summary>
    private static void ApplyCommandLineOverrides(
        AppConfiguration config,
        string? format,
        string? language,
        string? model,
        bool verbose,
        int? threads)
    {
        if (!string.IsNullOrEmpty(format))
        {
            config.Output.Format = format;
        }
        
        if (!string.IsNullOrEmpty(language))
        {
            config.Inference.Language = language;
        }
        
        if (!string.IsNullOrEmpty(model))
        {
            config.Model.Type = model;
        }
        
        if (verbose)
        {
            config.Output.Verbose = true;
        }
        
        if (threads.HasValue)
        {
            config.Inference.Threads = threads.Value;
        }
    }
    
    /// <summary>
    /// 收集所有输入文件
    /// </summary>
    private static List<string> CollectInputFiles(string[] inputs)
    {
        var supportedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm",  // 视频格式
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma"   // 音频格式
        };
        
        var files = new List<string>();
        
        foreach (var input in inputs)
        {
            var fullPath = Path.GetFullPath(input);
            
            if (Directory.Exists(fullPath))
            {
                // 目录：收集所有支持的文件
                var dirFiles = Directory.EnumerateFiles(fullPath, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f);
                
                files.AddRange(dirFiles);
            }
            else if (File.Exists(fullPath))
            {
                // 单个文件
                files.Add(fullPath);
            }
            else
            {
                // 尝试作为通配符模式处理
                var directory = Path.GetDirectoryName(fullPath);
                var pattern = Path.GetFileName(fullPath);
                
                if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
                {
                    var matchedFiles = Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly)
                        .Where(f => supportedExtensions.Contains(Path.GetExtension(f)))
                        .OrderBy(f => f);
                    
                    files.AddRange(matchedFiles);
                }
                else
                {
                    Console.Error.WriteLine($"Warning: Input not found '{input}'");
                }
            }
        }
        
        return files.Distinct().ToList();
    }
}
