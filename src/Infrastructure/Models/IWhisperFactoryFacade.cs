namespace Vid2Sub.Infrastructure.Models;

/// <summary>
/// Thin facade over Whisper.net static/native APIs so adapter behavior can be unit tested.
/// </summary>
public interface IWhisperFactoryFacade
{
    IWhisperFactoryHandle FromPath(string modelPath, bool useGpu);
}
