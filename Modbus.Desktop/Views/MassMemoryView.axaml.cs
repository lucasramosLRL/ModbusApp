using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Modbus.Desktop.ViewModels;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

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
        {
            vm.ColumnsReady      += cols => Dispatcher.UIThread.Post(() => BuildColumns(cols));
            vm.SaveFileRequested += OnSaveFileRequestedAsync;
        }
    }

    private async Task<Stream?> OnSaveFileRequestedAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title           = "Exportar Memória de Massa",
            SuggestedFileName = "memoria_massa.txt",
            FileTypeChoices =
            [
                new FilePickerFileType("Texto (TSV)") { Patterns = ["*.txt"] },
                new FilePickerFileType("Todos os arquivos") { Patterns = ["*"] },
            ],
        });

        return file is null ? null : await file.OpenWriteAsync();
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
            Header     = header,
            Binding    = new Binding(bindingPath),
            IsReadOnly = true,
        });
}
