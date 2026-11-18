using Vid2Sub.Domain.Enums;

namespace Vid2Sub.Domain.Interfaces;

/// <summary>Supplies Whisper model files; implementations decide where they come from.</summary>
public interface IModelProvider
{
    /// <summary>Returns the local path of the model file, downloading it first when it is missing.</summary>
    Task<string> EnsureModelAsync(ModelType modelType, CancellationToken cancellationToken = default);
}
