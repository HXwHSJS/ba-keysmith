using System.IO;
using BAKeySmith.Core.Configuration;
using BAKeySmith.Core.Contracts;

namespace BAKeySmith.App.Services;

public sealed class ConfigDocumentService
{
    private const string AppDataFolderName = "BAKeySmith";
    private const string DefaultConfigFileName = "config.json";
    private readonly string _userConfigDirectory;

    public ConfigDocumentService(string? userConfigDirectory = null)
    {
        _userConfigDirectory = string.IsNullOrWhiteSpace(userConfigDirectory)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                AppDataFolderName)
            : userConfigDirectory;
    }

    public string DefaultConfigPath => Path.Combine(_userConfigDirectory, DefaultConfigFileName);

    public ConfigDocument Load(string path)
    {
        if (!File.Exists(path))
        {
            var config = new AppConfigV1();
            return new ConfigDocument(
                path,
                config,
                AppConfigSerializer.ToRuntimeConfig(config, [], []),
                [],
                [$"配置文件不存在，使用默认配置: {path}"]);
        }

        try
        {
            var result = AppConfigSerializer.Parse(File.ReadAllText(path));
            return new ConfigDocument(
                path,
                result.Config,
                result.RuntimeConfig,
                result.Errors,
                result.Warnings);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            return new ConfigDocument(
                path,
                new AppConfigV1(),
                RuntimeConfig.Empty,
                [ex.Message],
                []);
        }
    }

    public async Task SaveAsync(
        string path,
        AppConfigV1 config,
        bool createBackup,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("请先选择配置文件路径。");
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        if (createBackup && File.Exists(path))
        {
            File.Copy(path, $"{path}.bak", overwrite: true);
        }

        await File.WriteAllTextAsync(
            path,
            AppConfigSerializer.Save(config),
            cancellationToken);
    }

    public ConfigValidationResult Validate(AppConfigV1 config)
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        try
        {
            var runtimeConfig = AppConfigSerializer.ToRuntimeConfig(config, errors, warnings);
            return new ConfigValidationResult(runtimeConfig, errors, warnings);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            errors.Add(ex.Message);
            return new ConfigValidationResult(RuntimeConfig.Empty, errors, warnings);
        }
    }

    public static string FindDefaultConfigPath()
    {
        return new ConfigDocumentService().DefaultConfigPath;
    }
}

public sealed record ConfigDocument(
    string Path,
    AppConfigV1 Config,
    RuntimeConfig RuntimeConfig,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool Success => Errors.Count == 0;
}

public sealed record ConfigValidationResult(
    RuntimeConfig RuntimeConfig,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool Success => Errors.Count == 0;
}
