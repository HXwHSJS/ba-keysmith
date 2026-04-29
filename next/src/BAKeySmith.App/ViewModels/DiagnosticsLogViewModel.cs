using System.Collections.ObjectModel;
using BAKeySmith.App.Models;

namespace BAKeySmith.App.ViewModels;

public sealed class DiagnosticsLogViewModel : ViewModelBase
{
    public const int MaxEntries = 600;

    public ObservableCollection<DiagnosticLogEntry> Entries { get; } = new();

    public void Append(string name, string message)
    {
        Entries.Add(new DiagnosticLogEntry(DateTime.Now, name, message));
        while (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }
    }

    public void Clear()
    {
        Entries.Clear();
    }
}
