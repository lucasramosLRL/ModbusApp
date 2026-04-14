using System.Collections.ObjectModel;

namespace Modbus.Desktop.ViewModels;

public class ReadingGroupViewModel
{
    public string GroupKey { get; }
    public string GroupName => Services.LocalizationService.Instance[GroupKey];
    public ObservableCollection<ElectricalReadingViewModel> Readings { get; } = new();

    public ReadingGroupViewModel(string groupKey)
    {
        GroupKey = groupKey;
    }
}
