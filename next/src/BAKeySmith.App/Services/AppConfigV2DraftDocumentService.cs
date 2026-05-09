using System.IO;
using BAKeySmith.Core.Configuration.V2;

namespace BAKeySmith.App.Services;

public sealed class AppConfigV2DraftDocumentService
{
    public const string DraftFileSuffix = ".v2.draft.json";

    private readonly AppConfigV2JsonSerializer _serializer = new();

    public AppConfigV2DraftDocumentResult Load(string path)
    {
        var normalizedPath = ValidateDraftPath(path);
        if (!File.Exists(normalizedPath))
        {
            return new AppConfigV2DraftDocumentResult(
                normalizedPath,
                null,
                [
                    new AppConfigV2Diagnostic(
                        AppConfigV2DiagnosticSeverity.Info,
                        AppConfigV2DiagnosticCode.DraftFileMissing,
                        "AppConfigV2 draft file does not exist. No file was created.",
                        normalizedPath)
                ],
                Loaded: false,
                Saved: false,
                IsDraftOnly: true,
                "AppConfigV2 draft file does not exist. No file was created.");
        }

        AppConfigV2SerializationResult result;
        try
        {
            result = _serializer.Parse(File.ReadAllText(normalizedPath));
        }
        catch (Exception ex) when (IsIoException(ex))
        {
            return FromIoError(
                normalizedPath,
                ex,
                loaded: false,
                saved: false,
                "AppConfigV2 draft file could not be read.");
        }

        return AppConfigV2DraftDocumentResult.FromSerializationResult(
            normalizedPath,
            result,
            loaded: result.Config is not null,
            saved: false,
            message: result.Config is null
                ? "AppConfigV2 draft file could not be loaded."
                : "AppConfigV2 draft file loaded.");
    }

    public async Task<AppConfigV2DraftDocumentResult> SaveAsync(
        string path,
        AppConfigV2 config,
        CancellationToken cancellationToken)
    {
        var normalizedPath = ValidateDraftPath(path);
        var json = _serializer.Serialize(config);
        var result = _serializer.Parse(json);
        if (result.BlocksSave)
        {
            return AppConfigV2DraftDocumentResult.FromSerializationResult(
                normalizedPath,
                result,
                loaded: false,
                saved: false,
                message: "AppConfigV2 draft contains blocking diagnostics. File was not written.");
        }

        try
        {
            var directory = Path.GetDirectoryName(normalizedPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Draft-only direct write. Production AppConfigV2 document service must
            // add atomic replace and unknown-field preservation before user config
            // migration or GUI/runtime integration.
            await File.WriteAllTextAsync(normalizedPath, json, cancellationToken);
        }
        catch (Exception ex) when (IsIoException(ex))
        {
            return FromIoError(
                normalizedPath,
                ex,
                loaded: false,
                saved: false,
                "AppConfigV2 draft file could not be written.");
        }

        return AppConfigV2DraftDocumentResult.FromSerializationResult(
            normalizedPath,
            result,
            loaded: false,
            saved: true,
            message: "AppConfigV2 draft file saved with known V2 fields only.");
    }

    private static string ValidateDraftPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("AppConfigV2 draft sandbox requires an explicit file path.");
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path.Trim());
        }
        catch (Exception ex) when (IsIoException(ex))
        {
            throw new InvalidOperationException("AppConfigV2 draft sandbox path is not valid.", ex);
        }

        // Draft sandbox path policy is intentionally a filename/suffix guard only.
        // Callers must choose an explicit developer/test path. A production service
        // needs a stronger root policy before any user-config integration.
        var fileName = Path.GetFileName(fullPath);
        if (!fileName.EndsWith(DraftFileSuffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"AppConfigV2 draft sandbox path must end with '{DraftFileSuffix}'.");
        }

        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException("AppConfigV2 draft sandbox path must be a file, not a directory.");
        }

        return fullPath;
    }

    private static AppConfigV2DraftDocumentResult FromIoError(
        string path,
        Exception exception,
        bool loaded,
        bool saved,
        string message)
    {
        return new AppConfigV2DraftDocumentResult(
            path,
            null,
            [
                new AppConfigV2Diagnostic(
                    AppConfigV2DiagnosticSeverity.Error,
                    AppConfigV2DiagnosticCode.DraftDocumentIoError,
                    $"{message} {exception.GetType().Name}: {exception.Message}",
                    path,
                    BlocksSave: true,
                    BlocksLive: true)
            ],
            loaded,
            saved,
            IsDraftOnly: true,
            message);
    }

    private static bool IsIoException(Exception exception)
    {
        return exception is IOException or
            UnauthorizedAccessException or
            ArgumentException or
            NotSupportedException;
    }
}

public sealed record AppConfigV2DraftDocumentResult(
    string Path,
    AppConfigV2? Config,
    IReadOnlyList<AppConfigV2Diagnostic> Diagnostics,
    bool Loaded,
    bool Saved,
    bool IsDraftOnly,
    string Message)
{
    public IReadOnlyList<AppConfigV2Diagnostic> Diagnostics { get; init; } =
        Diagnostics.ToArray();

    public bool HasErrors =>
        Diagnostics.Any(diagnostic => diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error);

    public bool BlocksSave =>
        Diagnostics.Any(diagnostic =>
            diagnostic.BlocksSave ||
            diagnostic.Severity == AppConfigV2DiagnosticSeverity.Error);

    public bool BlocksLive =>
        Diagnostics.Any(diagnostic =>
            diagnostic.BlocksLive ||
            diagnostic.Severity is AppConfigV2DiagnosticSeverity.Error or AppConfigV2DiagnosticSeverity.LiveBlocker);

    public bool CanSaveDraft =>
        Config is not null &&
        !BlocksSave;

    public static AppConfigV2DraftDocumentResult FromSerializationResult(
        string path,
        AppConfigV2SerializationResult result,
        bool loaded,
        bool saved,
        string message)
    {
        return new AppConfigV2DraftDocumentResult(
            path,
            result.Config,
            result.Diagnostics,
            loaded,
            saved,
            IsDraftOnly: true,
            message);
    }
}
