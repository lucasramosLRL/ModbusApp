using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Threading;
using Modbus.Desktop.ViewModels;
using System.Collections.Generic;

namespace Modbus.Desktop.Views;

public partial class MassMemoryView : UserControl
{
    public MassMemoryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (DataContext is MassMemoryViewModel vm)
            vm.ColumnsReady += cols => Dispatcher.UIThread.Post(() => BuildColumns(cols));
    }

    private void BuildColumns(IReadOnlyList<GrandezaColumn> cols)
    {
        RecordsGrid.Columns.Clear();
        AddTextColumn("Bloco", "Block");
        AddTextColumn("Data",  "Date");
        AddTextColumn("Hora",  "Time");
        for (int i = 0; i < cols.Count; i++)
            AddTextColumn(cols[i].Code, $"Values[{i}]");
        AddTextColumn("CS", "ChecksumOkText");
    }

    private void AddTextColumn(string header, string bindingPath) =>
        RecordsGrid.Columns.Add(new DataGridTextColumn
        {
            Header   = header,
            Binding  = new Binding(bindingPath),
            IsReadOnly = true,
        });
}
