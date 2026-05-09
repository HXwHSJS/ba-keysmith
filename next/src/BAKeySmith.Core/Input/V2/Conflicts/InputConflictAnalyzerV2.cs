namespace BAKeySmith.Core.Input.V2.Conflicts;

public sealed class InputConflictAnalyzerV2
{
    public IReadOnlyList<InputConflictReport> Analyze(IEnumerable<InputBindingV2> bindings)
    {
        var bindingList = bindings.ToArray();
        var reports = new List<InputConflictReport>();

        for (var leftIndex = 0; leftIndex < bindingList.Length; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < bindingList.Length; rightIndex++)
            {
                var left = bindingList[leftIndex];
                var right = bindingList[rightIndex];
                var report = AnalyzePair(left, right);
                if (report is not null)
                {
                    reports.Add(report);
                }
            }
        }

        return reports;
    }

    private static InputConflictReport? AnalyzePair(InputBindingV2 left, InputBindingV2 right)
    {
        if (left.Role == InputBindingRole.MappingTrigger &&
            right.Role == InputBindingRole.MappingTrigger &&
            BindingsConflict(left, right))
        {
            return Error(
                InputConflictCode.DuplicateMappingTrigger,
                "Mapping triggers overlap.",
                left,
                right);
        }

        if (IsRolePair(left, right, InputBindingRole.ControlHotkey, InputBindingRole.MappingTrigger) &&
            BindingsConflict(left, right))
        {
            return Error(
                InputConflictCode.ControlHotkeyConflictsWithMappingTrigger,
                "Control hotkey overlaps a mapping trigger.",
                left,
                right);
        }

        if (IsRolePair(left, right, InputBindingRole.CoordinateRecordHotkey, InputBindingRole.MappingTrigger) &&
            BindingsConflict(left, right))
        {
            return Error(
                InputConflictCode.CoordinateRecordHotkeyConflictsWithMappingTrigger,
                "Coordinate record hotkey overlaps a mapping trigger.",
                left,
                right);
        }

        if ((left.Role == InputBindingRole.EmergencyStopHotkey ||
             right.Role == InputBindingRole.EmergencyStopHotkey) &&
            BindingsConflict(left, right))
        {
            return Error(
                InputConflictCode.EmergencyStopHotkeyConflictsWithBinding,
                "Emergency stop hotkey overlaps another BAKS binding.",
                left,
                right);
        }

        return null;
    }

    private static bool BindingsConflict(InputBindingV2 left, InputBindingV2 right)
    {
        if (left.Hotkey is not null && right.Hotkey is not null)
        {
            return HotkeysConflict(left.Hotkey, right.Hotkey);
        }

        if (left.SingleInput is not null && right.SingleInput is not null)
        {
            return InputOverlap.Overlaps(left.SingleInput, right.SingleInput);
        }

        if (left.SingleInput is not null && right.Hotkey is not null)
        {
            return HotkeyContainsInput(right.Hotkey, left.SingleInput);
        }

        if (left.Hotkey is not null && right.SingleInput is not null)
        {
            return HotkeyContainsInput(left.Hotkey, right.SingleInput);
        }

        return false;
    }

    private static bool HotkeysConflict(HotkeySpecV2 left, HotkeySpecV2 right)
    {
        if (!InputOverlap.Overlaps(left.MainKey, right.MainKey))
        {
            return false;
        }

        // V2 skeleton choice: hotkey-vs-hotkey conflicts require an exact
        // modifier set size with per-position overlap. For example,
        // ctrl+shift+f8 does not conflict with ctrl+f8 here. Future runtime
        // hotkey matching must preserve or explicitly replace this rule.
        if (left.Modifiers.Count != right.Modifiers.Count)
        {
            return false;
        }

        var unmatched = right.Modifiers.ToList();
        foreach (var leftModifier in left.Modifiers)
        {
            var matchIndex = unmatched.FindIndex(rightModifier =>
                InputOverlap.Overlaps(leftModifier, rightModifier));
            if (matchIndex < 0)
            {
                return false;
            }

            unmatched.RemoveAt(matchIndex);
        }

        return unmatched.Count == 0;
    }

    private static bool HotkeyContainsInput(HotkeySpecV2 hotkey, InputSpec input)
    {
        return hotkey.Components.Any(component => InputOverlap.Overlaps(component, input));
    }

    private static bool IsRolePair(
        InputBindingV2 left,
        InputBindingV2 right,
        InputBindingRole first,
        InputBindingRole second)
    {
        return (left.Role == first && right.Role == second) ||
            (left.Role == second && right.Role == first);
    }

    private static InputConflictReport Error(
        InputConflictCode code,
        string message,
        InputBindingV2 left,
        InputBindingV2 right)
    {
        return new InputConflictReport(
            InputConflictSeverity.Error,
            code,
            $"{message} ({left.Name}: {FormatBinding(left)} / {right.Name}: {FormatBinding(right)})",
            [left.Id, right.Id],
            [left.Name, right.Name],
            BlocksSave: true,
            BlocksLive: true);
    }

    private static string FormatBinding(InputBindingV2 binding)
    {
        return binding.Hotkey?.CanonicalText ??
            binding.SingleInput?.CanonicalName ??
            "<empty>";
    }
}
