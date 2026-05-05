using Vid2Sub.Domain.Enums;

namespace Vid2Sub.Domain.Interfaces;

/// <summary>
/// 模型提供者接口
/// 负责模型的下载、缓存和路径管理
/// </summary>
public interface IModelProvider
{
    /// <summary>
    /// 确保指定类型的模型可用（如不存在则下载）
    /// </summary>
    /// <param name="modelType">模型类型</param>
    /// <param name="progress">下载进度回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>模型文件的完整路径</returns>
    Task<string> EnsureModelAsync(
        ModelType modelType,
        IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// 检查指定模型是否已存在于本地
    /// </summary>
    /// <param name="modelType">模型类型</param>
    /// <returns>是否存在</returns>
    bool ModelExists(ModelType modelType);
    
    /// <summary>
    /// 获取模型文件路径
    /// </summary>
    /// <param name="modelType">模型类型</param>
    /// <returns>模型文件路径</returns>
    string GetModelPath(ModelType modelType);
}
