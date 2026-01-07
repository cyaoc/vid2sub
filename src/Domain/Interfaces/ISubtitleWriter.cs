using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Interfaces;

/// <summary>
/// 字幕写入器接口
/// 负责将转录结果输出为特定格式的字幕文件
/// </summary>
public interface ISubtitleWriter
{
    /// <summary>
    /// 获取此写入器支持的文件扩展名
    /// </summary>
    string FileExtension { get; }
    
    /// <summary>
    /// 将转录片段写入到指定文件
    /// </summary>
    /// <param name="segments">转录片段集合</param>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task WriteAsync(
        IEnumerable<TranscriptionSegment> segments,
        string outputPath,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 将转录片段格式化为字符串
    /// </summary>
    /// <param name="segments">转录片段集合</param>
    /// <returns>格式化后的字幕内容</returns>
    string Format(IEnumerable<TranscriptionSegment> segments);
}
