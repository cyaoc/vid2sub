namespace Vid2Sub.Infrastructure.Models;

public interface IWhisperFactoryHandle : IAsyncDisposable
{
    string RuntimeDescription { get; }

    IWhisperBuilderFacade CreateBuilder();
}
