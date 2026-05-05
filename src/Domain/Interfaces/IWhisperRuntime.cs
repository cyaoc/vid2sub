namespace Vid2Sub.Domain.Interfaces;

public interface IWhisperRuntime : IAsyncDisposable
{
    string RuntimeDescription { get; }

    IWhisperProcessor CreateProcessor();
}
