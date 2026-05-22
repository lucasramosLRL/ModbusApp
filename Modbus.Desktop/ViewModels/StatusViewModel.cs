using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Modbus.Desktop.ViewModels;

public partial class StatusViewModel : ObservableObject
{
    private readonly string _modelName;

    public ObservableCollection<string> MeterErrors  { get; } = [];
    public ObservableCollection<string> ModuleErrors { get; } = [];

    public bool HasMeterErrors  => MeterErrors.Count  > 0;
    public bool HasModuleErrors => ModuleErrors.Count > 0;

    public StatusViewModel(string modelName)
    {
        _modelName = modelName;
        MeterErrors.CollectionChanged  += (_, _) => OnPropertyChanged(nameof(HasMeterErrors));
        ModuleErrors.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasModuleErrors));
    }

    public void UpdateMeterStatus(double rawValue)  => Refresh(MeterErrors,  "Erro",   (ushort)rawValue);
    public void UpdateModuleStatus(double rawValue) => Refresh(ModuleErrors, "ErroWF", (ushort)rawValue);

    private void Refresh(ObservableCollection<string> col, string regName, ushort raw)
    {
        var decoded = MeterErrorCodes.Decode(_modelName, regName, raw);
        col.Clear();
        foreach (var msg in decoded) col.Add(msg);
    }
}
