namespace Vid2Sub.Domain.Interfaces;

/// <summary>
/// 音频预处理器接口
/// 负责将各种格式的媒体文件转换为 Whisper 所需的 WAV 格式
/// </summary>
public interface IAudioProcessor : IAsyncDisposable
{
    /// <summary>
    /// 将媒体文件转换为 16kHz 单声道 WAV 格式
    /// </summary>
    /// <param name="inputPath">输入媒体文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>转换后的 WAV 文件路径</returns>
    Task<string> ConvertToWavAsync(string inputPath, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 清理指定的临时文件
    /// </summary>
    /// <param name="wavPath">WAV 文件路径</param>
    void CleanupTempFile(string wavPath);
}
