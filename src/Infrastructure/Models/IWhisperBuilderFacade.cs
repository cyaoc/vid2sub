namespace Vid2Sub.Infrastructure.Models;

public interface IWhisperBuilderFacade
{
    IWhisperBuilderFacade WithLanguage(string language);

    IWhisperBuilderFacade WithThreads(int threads);

    IWhisperBuilderFacade WithBeamSize(int beamSize);

    IWhisperNativeProcessor Build();
}
