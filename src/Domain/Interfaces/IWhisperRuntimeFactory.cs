using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Interfaces;

public interface IWhisperRuntimeFactory
{
    Task<IWhisperRuntime> CreateAsync(
        string modelPath,
        ResolvedInferenceConfiguration inference,
        CancellationToken cancellationToken = default);
}
