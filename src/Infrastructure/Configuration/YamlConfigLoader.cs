using Vid2Sub.Domain.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vid2Sub.Infrastructure.Configuration;

/// <summary>
/// YAML 配置加载器
/// 负责从 YAML 文件加载应用程序配置
/// </summary>
public sealed class YamlConfigLoader
{
    private readonly IDeserializer _deserializer;
    
    public YamlConfigLoader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }
    
    /// <summary>
    /// 从文件加载配置
    /// </summary>
    /// <param name="filePath">配置文件路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>应用程序配置</returns>
    public async Task<AppConfiguration> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new AppConfiguration();
        }
        
        var yaml = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Load(yaml);
    }
    
    /// <summary>
    /// 从 YAML 字符串加载配置
    /// </summary>
    /// <param name="yaml">YAML 内容</param>
    /// <returns>应用程序配置</returns>
    public AppConfiguration Load(string yaml)
    {
        if (string.IsNullOrWhiteSpace(yaml))
        {
            return new AppConfiguration();
        }
        
        return _deserializer.Deserialize<AppConfiguration>(yaml) ?? new AppConfiguration();
    }
    
    /// <summary>
    /// 获取默认配置文件路径
    /// </summary>
    public static string GetDefaultConfigPath()
    {
        // 优先查找当前目录
        var currentDir = Path.Combine(Directory.GetCurrentDirectory(), "config.yaml");
        if (File.Exists(currentDir))
        {
            return currentDir;
        }
        
        // 然后查找程序所在目录
        var appDir = Path.Combine(AppContext.BaseDirectory, "config.yaml");
        if (File.Exists(appDir))
        {
            return appDir;
        }
        
        return currentDir;
    }
}
