using System.Diagnostics;

namespace Vid2Sub.Infrastructure.Workflow;

public sealed class Vid2SubProcessRunner
{
    public async Task<ToolResult<RunVid2SubResult>> RunAsync(
        RunVid2SubRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.ExecutablePath))
        {
            return ToolResult<RunVid2SubResult>.Failure(
                WorkflowErrorCodes.FileNotFound,
                "vid2sub executable was not found.",
                request.ExecutablePath);
        }

        var outputValidation = PathScopeGuard.ValidateWriteTarget(
            request.OutputPath,
            request.OutputRoot,
            request.OverwriteConfirmed);

        if (outputValidation.Status == ToolStatus.Failed)
        {
            return ToolResult<RunVid2SubResult>.Failure(
                outputValidation.Code!,
                outputValidation.Message!,
                outputValidation.Details);
        }

        var stagingDir = Path.Combine(request.OutputRoot, $".vid2sub-workflow-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDir);

        using var process = new Process { StartInfo = CreateStartInfo(request, stagingDir) };
        try
        {
            process.Start();
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            var stderr = await stderrTask;
            await stdoutTask;

            if (process.ExitCode != 0)
            {
                return ToolResult<RunVid2SubResult>.Failure(
                    WorkflowErrorCodes.ProcessFailed,
                    $"vid2sub failed with exit code {process.ExitCode}.",
                    stderr);
            }

            var subtitlePath = Directory.EnumerateFiles(stagingDir, $"*.{request.Format}")
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();

            if (subtitlePath is null)
            {
                return ToolResult<RunVid2SubResult>.Failure(
                    WorkflowErrorCodes.ProcessFailed,
                    "vid2sub completed but did not produce a subtitle file.",
                    stagingDir);
            }

            var directory = Path.GetDirectoryName(outputValidation.Data!);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Move(subtitlePath, outputValidation.Data!, overwrite: request.OverwriteConfirmed);
            return ToolResult<RunVid2SubResult>.Success(new RunVid2SubResult(outputValidation.Data!, stderr));
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch (Exception ex)
        {
            return ToolResult<RunVid2SubResult>.Failure(
                WorkflowErrorCodes.ProcessFailed,
                "vid2sub process could not be started.",
                ex.Message);
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    public static ProcessStartInfo CreateStartInfo(RunVid2SubRequest request, string? outputDirOverride = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = request.ExecutablePath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(request.InputPath);
        startInfo.ArgumentList.Add("--output-dir");
        startInfo.ArgumentList.Add(outputDirOverride ?? request.OutputRoot);
        startInfo.ArgumentList.Add("--format");
        startInfo.ArgumentList.Add(request.Format);

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            startInfo.ArgumentList.Add("--language");
            startInfo.ArgumentList.Add(request.Language);
        }

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(request.Model);
        }

        if (request.OverwriteConfirmed)
        {
            startInfo.ArgumentList.Add("--overwrite");
        }

        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best-effort cancellation cleanup.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Best-effort cleanup for staging files.
        }
    }
}

public sealed record RunVid2SubRequest(
    string ExecutablePath,
    string InputPath,
    string OutputRoot,
    string OutputPath,
    string Format,
    string? Language,
    string? Model,
    bool OverwriteConfirmed);

public sealed record RunVid2SubResult(
    string? SubtitlePath,
    string? Stderr);
