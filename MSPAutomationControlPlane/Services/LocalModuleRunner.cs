using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using MSPAutomationControlPlane.Domain;

namespace MSPAutomationControlPlane.Services;

public sealed class LocalModuleRunner(LocalModuleRunnerOptions options)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        WriteIndented = true
    };

    public async Task<LocalModuleRunResult> TryRunAsync(
        JobRecord job,
        string actor,
        CancellationToken cancellationToken)
    {
        if (!options.Enabled)
        {
            return LocalModuleRunResult.Skipped("Local module execution is disabled.");
        }

        var modulesRoot = ResolveModulesRoot();
        if (modulesRoot is null)
        {
            return LocalModuleRunResult.Skipped("Local modules root could not be resolved.");
        }

        var projectPath = Path.Combine(
            modulesRoot,
            job.ModuleId,
            "src",
            ToPascalCase(job.ModuleId),
            $"{ToPascalCase(job.ModuleId)}.csproj");

        if (!File.Exists(projectPath))
        {
            return LocalModuleRunResult.Skipped($"Local module project was not found: {projectPath}");
        }

        var workRoot = ResolveWorkRoot(modulesRoot);
        var jobWorkRoot = Path.Combine(workRoot, job.Id);
        var inputPath = Path.Combine(jobWorkRoot, "input", "job.json");
        var outputPath = Path.Combine(jobWorkRoot, "output", "result.json");

        Directory.CreateDirectory(Path.GetDirectoryName(inputPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var moduleInput = CreateModuleInput(job, actor);
        await File.WriteAllTextAsync(
            inputPath,
            JsonSerializer.Serialize(moduleInput, JsonOptions),
            cancellationToken);

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"run --project \"{projectPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };

        process.StartInfo.Environment["CONTROL_PLANE_INPUT_PATH"] = inputPath;
        process.StartInfo.Environment["CONTROL_PLANE_OUTPUT_PATH"] = outputPath;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stdout.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                stderr.AppendLine(args.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            return new LocalModuleRunResult(
                false,
                process.ExitCode,
                null,
                $"Local module exited with code {process.ExitCode}. {stderr}".Trim());
        }

        if (!File.Exists(outputPath))
        {
            return new LocalModuleRunResult(false, process.ExitCode, null, "Local module did not write an output file.");
        }

        await using var outputStream = File.OpenRead(outputPath);
        var output = await JsonSerializer.DeserializeAsync<JsonElement>(
            outputStream,
            JsonOptions,
            cancellationToken);

        return new LocalModuleRunResult(
            true,
            process.ExitCode,
            output,
            string.IsNullOrWhiteSpace(stdout.ToString()) ? "Local module completed successfully." : stdout.ToString().Trim());
    }

    private string? ResolveModulesRoot()
    {
        if (!string.IsNullOrWhiteSpace(options.ModulesRoot))
        {
            return Path.GetFullPath(options.ModulesRoot);
        }

        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "modules");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private string ResolveWorkRoot(string modulesRoot)
    {
        return string.IsNullOrWhiteSpace(options.WorkRoot)
            ? Path.Combine(Directory.GetParent(modulesRoot)!.FullName, ".work", "local-modules")
            : Path.GetFullPath(options.WorkRoot);
    }

    private static object CreateModuleInput(JobRecord job, string actor)
    {
        return new
        {
            schemaVersion = "1.0",
            jobId = job.Id,
            moduleId = job.ModuleId,
            moduleVersion = job.ModuleVersion,
            requestedBy = new
            {
                userId = actor,
                displayName = actor,
                upn = actor
            },
            clientConnectionId = job.TenantContext.ClientId,
            targetScope = job.TargetScope,
            parameters = job.Parameters
        };
    }

    private static string ToPascalCase(string value)
    {
        var builder = new StringBuilder();
        foreach (var part in value.Split(['-', '.', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            builder.Append(char.ToUpperInvariant(part[0]));
            if (part.Length > 1)
            {
                builder.Append(part[1..]);
            }
        }

        return builder.ToString();
    }
}
