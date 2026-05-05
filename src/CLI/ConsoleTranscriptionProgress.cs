using Vid2Sub.Domain.Interfaces;
using Vid2Sub.Domain.Enums;
using Vid2Sub.Domain.Models;

namespace Vid2Sub.CLI;

public sealed class ConsoleTranscriptionProgress(Vid2SubLogLevel logLevel) : ITranscriptionProgress
{
    public void Report(TranscriptionProgressEvent progressEvent)
    {
        if (logLevel == Vid2SubLogLevel.Quiet)
        {
            return;
        }

        Console.WriteLine(progressEvent.Message);
    }
}
