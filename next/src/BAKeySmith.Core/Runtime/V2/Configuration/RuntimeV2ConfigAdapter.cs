using BAKeySmith.Core.Configuration.V2;

namespace BAKeySmith.Core.Runtime.V2.Configuration;

public sealed class RuntimeV2ConfigAdapter
{
    private readonly AppConfigV2Validator _validator = new();
    private readonly MappingActionSourcePlanBuilderV2 _planBuilder = new();

    public RuntimeV2ConfigAdapterResult Build(AppConfigV2 config)
    {
        var diagnostics = _validator.Validate(config)
            .Select(diagnostic => RuntimeV2ConfigAdapterDiagnostic.FromAppConfig(
                diagnostic,
                MappingIdForPath(config, diagnostic.Path)))
            .ToList();

        if (diagnostics.Any(diagnostic => diagnostic.BlocksRuntime))
        {
            return Result([], diagnostics);
        }

        var entries = new List<RuntimeV2MappingEntry>();
        for (var index = 0; index < config.Mappings.Count; index++)
        {
            var mapping = config.Mappings[index];
            if (!mapping.Enabled)
            {
                continue;
            }

            var mappingPath = $"mappings[{index}]";
            if (mapping.Trigger is not { Kind: TriggerKindV2.SingleInput, Input: { } triggerInput } ||
                mapping.ActionSource is null)
            {
                continue;
            }

            var build = _planBuilder.Build(mapping.ActionSource, $"{mappingPath}.action");
            diagnostics.AddRange(build.Diagnostics.Select(diagnostic =>
                RuntimeV2ConfigAdapterDiagnostic.FromAppConfig(diagnostic, mapping.Id)));

            if (!build.Success || build.Plan is null)
            {
                continue;
            }

            entries.Add(new RuntimeV2MappingEntry(
                mapping.Id,
                string.IsNullOrWhiteSpace(mapping.Name) ? mapping.Id : mapping.Name,
                triggerInput,
                build.Plan,
                mappingPath));
        }

        return Result(entries, diagnostics);
    }

    private static RuntimeV2ConfigAdapterResult Result(
        IEnumerable<RuntimeV2MappingEntry> entries,
        IReadOnlyList<RuntimeV2ConfigAdapterDiagnostic> diagnostics)
    {
        return new RuntimeV2ConfigAdapterResult(
            new RuntimeV2MappingRegistry(entries),
            diagnostics.ToArray());
    }

    private static string? MappingIdForPath(AppConfigV2 config, string path)
    {
        const string prefix = "mappings[";
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var close = path.IndexOf(']', prefix.Length);
        if (close < 0 ||
            !int.TryParse(path[prefix.Length..close], out var index) ||
            index < 0 ||
            index >= config.Mappings.Count)
        {
            return null;
        }

        var id = config.Mappings[index].Id;
        return string.IsNullOrWhiteSpace(id) ? null : id;
    }
}
