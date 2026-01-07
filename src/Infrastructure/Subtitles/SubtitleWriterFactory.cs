using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Interfaces;

namespace Vid2Sub.Infrastructure.Subtitles;

/// <summary>
/// 字幕写入器工厂
/// 根据输出格式创建对应的写入器
/// </summary>
public static class SubtitleWriterFactory
{
    /// <summary>
    /// 根据输出格式获取对应的字幕写入器
    /// </summary>
    /// <param name="format">输出格式</param>
    /// <returns>字幕写入器实例</returns>
    public static ISubtitleWriter Create(OutputFormat format) => format switch
    {
        OutputFormat.Srt => new SrtWriter(),
        OutputFormat.Vtt => new VttWriter(),
        OutputFormat.Text => new PlainTextWriter(),
        _ => new VttWriter()
    };
    
    /// <summary>
    /// 根据文件扩展名获取对应的字幕写入器
    /// </summary>
    /// <param name="extension">文件扩展名（含或不含点）</param>
    /// <returns>字幕写入器实例</returns>
    public static ISubtitleWriter CreateFromExtension(string extension)
    {
        var ext = extension.TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "srt" => new SrtWriter(),
            "vtt" => new VttWriter(),
            "txt" => new PlainTextWriter(),
            _ => new VttWriter()
        };
    }
}
