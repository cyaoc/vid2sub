namespace Vid2Sub.Infrastructure.Utilities;

/// <summary>
/// 控制台进度条
/// 实现 IProgress&lt;double&gt; 接口，用于显示下载等长时间操作的进度
/// </summary>
public sealed class ConsoleProgressBar : IProgress<double>, IDisposable
{
    private readonly string _label;
    private readonly long _totalBytes;
    private readonly int _barWidth;
    private readonly object _lock = new();
    
    private double _lastProgress;
    private bool _disposed;
    private bool _completed;
    
    /// <summary>
    /// 创建控制台进度条
    /// </summary>
    /// <param name="label">进度条标签</param>
    /// <param name="totalBytes">总字节数</param>
    /// <param name="barWidth">进度条宽度（字符数）</param>
    public ConsoleProgressBar(string label, long totalBytes, int barWidth = 30)
    {
        _label = label;
        _totalBytes = totalBytes;
        _barWidth = barWidth;
        _lastProgress = -1;
    }
    
    /// <summary>
    /// 报告进度
    /// </summary>
    /// <param name="progress">进度值 (0.0 - 1.0)</param>
    public void Report(double progress)
    {
        if (_disposed || _completed)
        {
            return;
        }
        
        lock (_lock)
        {
            // 避免过于频繁的更新（每 0.5% 更新一次）
            if (Math.Abs(progress - _lastProgress) < 0.005 && progress < 0.99)
            {
                return;
            }
            
            _lastProgress = progress;
            
            // 计算进度条
            var filledWidth = (int)(progress * _barWidth);
            var emptyWidth = _barWidth - filledWidth;
            
            var filledBar = new string('█', filledWidth);
            var emptyBar = new string('░', emptyWidth);
            
            // 计算已下载大小
            var downloadedBytes = (long)(progress * _totalBytes);
            var downloadedStr = FormatBytes(downloadedBytes);
            var totalStr = FormatBytes(_totalBytes);
            
            // 输出进度条（使用 ANSI 转义序列清除行并刷新）
            // \x1b[2K 清除整行, \r 回到行首
            System.Console.Write($"\x1b[2K\r{_label}: [{filledBar}{emptyBar}] {progress:P1}  {downloadedStr} / {totalStr}");
            System.Console.Out.Flush();
            
            // 如果完成，换行
            if (progress >= 1.0)
            {
                Complete();
            }
        }
    }
    
    /// <summary>
    /// 标记完成
    /// </summary>
    public void Complete()
    {
        if (_completed)
        {
            return;
        }
        
        _completed = true;
        System.Console.WriteLine();
    }
    
    /// <summary>
    /// 格式化字节大小为人类可读格式
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        var order = 0;
        var size = (double)bytes;
        
        while (size >= 1024 && order < sizes.Length - 1)
        {
            order++;
            size /= 1024;
        }
        
        return $"{size:0.#} {sizes[order]}";
    }
    
    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        
        _disposed = true;
        
        // 确保进度条结束时换行
        if (!_completed)
        {
            System.Console.WriteLine();
        }
    }
}
