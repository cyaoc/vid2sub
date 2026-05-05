using Vid2Sub.Domain.Enums;

namespace Vid2Sub.Domain.Models;

public sealed record TranscriptionError(
    TranscriptionStage Stage,
    string Code,
    string Message,
    string? Path = null);

public static class TranscriptionErrorCodes
{
    public const string InputMissing = "input_missing";
    public const string InputDuplicate = "input_duplicate";
    public const string OutputCollision = "output_collision";
    public const string OutputExists = "output_exists";
    public const string ModelProvisioningFailed = "model_provisioning_failed";
    public const string RuntimeInitializationFailed = "runtime_initialization_failed";
    public const string AudioConversionFailed = "audio_conversion_failed";
    public const string TranscriptionFailed = "transcription_failed";
    public const string OutputWriteFailed = "output_write_failed";
}
