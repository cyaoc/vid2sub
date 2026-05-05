using Vid2Sub.Domain.Models;

namespace Vid2Sub.Domain.Interfaces;

public interface ITranscriptionProgress
{
    void Report(TranscriptionProgressEvent progressEvent);
}
