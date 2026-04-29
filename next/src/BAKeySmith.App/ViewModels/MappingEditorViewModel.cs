using System.Collections.ObjectModel;
using BAKeySmith.App.Commands;
using BAKeySmith.App.Models;
using BAKeySmith.Core.Configuration;

namespace BAKeySmith.App.ViewModels;

public sealed class MappingEditorViewModel : ViewModelBase
{
    private bool _isDirty;
    private MappingEditorRow? _selectedMapping;
    private string _editingTrigger = "q";
    private string _editingType = "simple";
    private string _editingTarget = "1";
    private string _editingMode = "hold";
    private string _editingScript = "loop 2\ntap esc\nend";
    private string _lastError = string.Empty;

    public MappingEditorViewModel()
    {
        AddCommand = new RelayCommand(AddFromEditor);
        UpdateCommand = new RelayCommand(UpdateFromEditor);
        RemoveCommand = new RelayCommand(RemoveSelected);
        ClearCommand = new RelayCommand(ClearEditorForm);
    }

    public ObservableCollection<MappingEditorRow> Mappings { get; } = new();
    public IReadOnlyList<string> MappingTypes { get; } = ["simple", "macro"];
    public IReadOnlyList<string> MappingModes { get; } = ["hold", "tap"];

    public RelayCommand AddCommand { get; }
    public RelayCommand UpdateCommand { get; }
    public RelayCommand RemoveCommand { get; }
    public RelayCommand ClearCommand { get; }

    public bool IsDirty
    {
        get => _isDirty;
        private set => SetProperty(ref _isDirty, value);
    }

    public MappingEditorRow? SelectedMapping
    {
        get => _selectedMapping;
        set
        {
            if (SetProperty(ref _selectedMapping, value) && value is not null)
            {
                LoadEditorForm(value);
            }
        }
    }

    public string EditingTrigger
    {
        get => _editingTrigger;
        set => SetProperty(ref _editingTrigger, value);
    }

    public string EditingType
    {
        get => _editingType;
        set
        {
            if (SetProperty(ref _editingType, value))
            {
                OnPropertyChanged(nameof(IsEditingMacro));
            }
        }
    }

    public string EditingTarget
    {
        get => _editingTarget;
        set => SetProperty(ref _editingTarget, value);
    }

    public string EditingMode
    {
        get => _editingMode;
        set => SetProperty(ref _editingMode, value);
    }

    public string EditingScript
    {
        get => _editingScript;
        set => SetProperty(ref _editingScript, value);
    }

    public bool IsEditingMacro => string.Equals(EditingType, "macro", StringComparison.OrdinalIgnoreCase);

    public string LastError
    {
        get => _lastError;
        private set => SetProperty(ref _lastError, value);
    }

    public void LoadFromConfig(AppConfigV1 config)
    {
        Mappings.Clear();
        foreach (var mapping in config.Mappings)
        {
            Mappings.Add(MappingEditorRow.FromConfig(mapping));
        }

        SelectedMapping = null;
        ClearEditorForm();
        MarkClean();
    }

    public void Add(MappingEditorRow row)
    {
        if (Mappings.Any(existing =>
                string.Equals(existing.Trigger, row.Trigger, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"触发键重复: {row.Trigger}");
        }

        Mappings.Add(row);
        SelectedMapping = row;
        MarkDirty();
    }

    public void Update(MappingEditorRow selected, MappingEditorRow replacement)
    {
        if (Mappings.Any(existing =>
                !ReferenceEquals(existing, selected) &&
                string.Equals(existing.Trigger, replacement.Trigger, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"触发键重复: {replacement.Trigger}");
        }

        var index = Mappings.IndexOf(selected);
        if (index < 0)
        {
            throw new InvalidOperationException("请先选择要更新的映射。");
        }

        var updated = replacement.PreserveIdentityFrom(selected);
        Mappings[index] = updated;
        SelectedMapping = updated;
        MarkDirty();
    }

    public void AddFromEditor()
    {
        ExecuteEditorAction(() => Add(ReadEditorForm()));
    }

    public void UpdateFromEditor()
    {
        ExecuteEditorAction(() =>
        {
            if (SelectedMapping is null)
            {
                throw new InvalidOperationException("请先选择要更新的映射。");
            }

            Update(SelectedMapping, ReadEditorForm());
        });
    }

    public void RemoveSelected()
    {
        ExecuteEditorAction(() =>
        {
            if (SelectedMapping is null)
            {
                throw new InvalidOperationException("请先选择要删除的映射。");
            }

            Remove(SelectedMapping);
            ClearEditorForm();
        });
    }

    public void ClearEditorForm()
    {
        SelectedMapping = null;
        EditingTrigger = "q";
        EditingType = "simple";
        EditingMode = "hold";
        EditingTarget = "1";
        EditingScript = "loop 2\ntap esc\nend";
        LastError = string.Empty;
    }

    public void Remove(MappingEditorRow selected)
    {
        if (!Mappings.Remove(selected))
        {
            throw new InvalidOperationException("请先选择要删除的映射。");
        }

        SelectedMapping = null;
        MarkDirty();
    }

    public IReadOnlyList<MappingConfigV1> ToConfigMappings()
    {
        return Mappings.Select(row => row.ToConfig()).ToArray();
    }

    public void MarkClean()
    {
        IsDirty = false;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    private MappingEditorRow ReadEditorForm()
    {
        var trigger = EditingTrigger.Trim();
        if (string.IsNullOrWhiteSpace(trigger))
        {
            throw new InvalidOperationException("触发键不能为空。");
        }

        if (IsEditingMacro)
        {
            return new MappingEditorRow
            {
                Trigger = trigger,
                Type = "macro",
                Mode = string.Empty,
                Target = string.Empty,
                Script = EditingScript
            };
        }

        var target = EditingTarget.Trim();
        if (string.IsNullOrWhiteSpace(target))
        {
            throw new InvalidOperationException("simple 映射需要目标键。");
        }

        return new MappingEditorRow
        {
            Trigger = trigger,
            Type = "simple",
            Mode = string.IsNullOrWhiteSpace(EditingMode) ? "hold" : EditingMode,
            Target = target,
            Script = string.Empty
        };
    }

    private void LoadEditorForm(MappingEditorRow row)
    {
        EditingTrigger = row.Trigger;
        EditingTarget = row.Target;
        EditingScript = string.IsNullOrWhiteSpace(row.Script)
            ? "loop 2\ntap esc\nend"
            : row.Script;
        EditingType = string.IsNullOrWhiteSpace(row.Type) ? "simple" : row.Type;
        EditingMode = string.IsNullOrWhiteSpace(row.Mode) ? "hold" : row.Mode;
        LastError = string.Empty;
    }

    private void ExecuteEditorAction(Action action)
    {
        try
        {
            LastError = string.Empty;
            action();
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
        }
    }
}
