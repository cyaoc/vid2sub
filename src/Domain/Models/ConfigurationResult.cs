namespace Vid2Sub.Domain.Models;

public sealed record ConfigurationError(
    string Source,
    string Key,
    string? Value,
    string Message);

public sealed class ConfigurationResult
{
    private ConfigurationResult(ResolvedAppConfiguration? configuration, IReadOnlyList<ConfigurationError> errors)
    {
        Configuration = configuration;
        Errors = errors;
    }

    public ResolvedAppConfiguration? Configuration { get; }

    public IReadOnlyList<ConfigurationError> Errors { get; }

    public bool IsSuccess => Configuration is not null && Errors.Count == 0;

    public static ConfigurationResult Success(ResolvedAppConfiguration configuration) => new(configuration, []);

    public static ConfigurationResult Failure(IReadOnlyList<ConfigurationError> errors) => new(null, errors);
}
