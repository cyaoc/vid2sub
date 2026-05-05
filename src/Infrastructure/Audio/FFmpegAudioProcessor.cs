using System.Diagnostics;
using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.Infrastructure.Audio;

/// <summary>
/// FFmpeg 音频预处理器
/// 将各种格式的媒体文件转换为 Whisper 所需的 16kHz 单声道 WAV 格式
/// </summary>
public sealed class FFmpegAudioProcessor(ResolvedEnvironmentConfiguration config) : IAudioProcessor
{
    private readonly List<string> _tempFiles = [];
    private readonly object _lock = new();
    private bool _disposed;
    
    /// <summary>
    /// 将媒体文件转换为 16kHz 单声道 WAV 格式
    /// </summary>
    public async Task<string> ConvertToWavAsync(string inputPath, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException("Input file not found", inputPath);
        }
        
        // 确保临时目录存在
        var tempDir = Path.GetFullPath(config.TempDir);
        Directory.CreateDirectory(tempDir);
        
        // 生成临时文件路径
        var outputPath = Path.Combine(tempDir, $"{Guid.NewGuid():N}.wav");
        
        using var process = new Process
        {
            StartInfo = CreateStartInfo(inputPath, outputPath)
        };
        
        try
        {
            process.Start();
            
            // 读取 stderr (FFmpeg 的进度信息输出到 stderr)
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            
            await process.WaitForExitAsync(cancellationToken);
            
            if (process.ExitCode != 0)
            {
                var error = await errorTask;
                throw new InvalidOperationException($"FFmpeg conversion failed (exit code: {process.ExitCode}): {error}");
            }
            
            // 记录临时文件以便清理
            lock (_lock)
            {
                _tempFiles.Add(outputPath);
            }
            
            return outputPath;
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            CleanupTempFile(outputPath);
            throw;
        }
        catch (Exception ex)
        {
            // 转换失败时清理可能已创建的输出文件
            CleanupTempFile(outputPath);
            
            if (ex.Message.Contains("not found") || ex.Message.Contains("No such file"))
            {
                throw new InvalidOperationException(
                    $"FFmpeg not found. Please ensure FFmpeg is installed and in PATH, or specify the full path in config. Current path: {config.FfmpegPath}", ex);
            }
            
            throw;
        }
    }

    public ProcessStartInfo CreateStartInfo(string inputPath, string outputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = config.FfmpegPath,
            UseShellExecute = false,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add("-ar");
        startInfo.ArgumentList.Add("16000");
        startInfo.ArgumentList.Add("-ac");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-c:a");
        startInfo.ArgumentList.Add("pcm_s16le");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("wav");
        startInfo.ArgumentList.Add(outputPath);

        return startInfo;
    }
    
    /// <summary>
    /// 清理指定的临时文件
    /// </summary>
    public void CleanupTempFile(string wavPath)
    {
        if (string.IsNullOrEmpty(wavPath) || !File.Exists(wavPath))
        {
            return;
        }
        
        try
        {
            File.Delete(wavPath);
            lock (_lock)
            {
                _tempFiles.Remove(wavPath);
            }
        }
        catch
        {
            // 忽略清理错误
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cancellation cleanup.
        }
    }
    
    /// <summary>
    /// 释放资源并清理所有临时文件
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        
        // 清理所有临时文件
        List<string> filesToDelete;
        lock (_lock)
        {
            filesToDelete = [.. _tempFiles];
            _tempFiles.Clear();
        }
        
        await Task.Run(() =>
        {
            foreach (var file in filesToDelete)
            {
                try
                {
                    if (File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch
                {
                    // 忽略清理错误
                }
            }
        });
    }
}
