using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Modbus.Desktop.Services;
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
            vm.ColumnsReady        += cols => Dispatcher.UIThread.Post(() => BuildColumns(cols));
            vm.SaveFileRequested   += OnSaveFileRequestedAsync;
            vm.AskResumeOrRestart   = OnAskResumeOrRestartAsync;
        }
    }

    private async Task<bool?> OnAskResumeOrRestartAsync()
    {
        bool? result = null;
        var loc = LocalizationService.Instance;

        var dialog = new Window
        {
            Title                  = loc["MmResumeTitle"],
            Width                  = 400,
            Height                 = 175,
            CanResize              = false,
            WindowStartupLocation  = WindowStartupLocation.CenterOwner,
            ShowInTaskbar          = false,
        };

        var resumeBtn = new Button
        {
            Content  = loc["MmResumeContinue"],
            Padding  = new Avalonia.Thickness(16, 8),
        };
        resumeBtn.Click += (_, _) => { result = true;  dialog.Close(); };

        var restartBtn = new Button
        {
            Content  = loc["MmResumeRestart"],
            Padding  = new Avalonia.Thickness(16, 8),
        };
        restartBtn.Click += (_, _) => { result = false; dialog.Close(); };

        var cancelBtn = new Button
        {
            Content = loc["Cancel"],
            Padding = new Avalonia.Thickness(16, 8),
        };
        cancelBtn.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin   = new Avalonia.Thickness(24),
            Spacing  = 20,
            Children =
            {
                new TextBlock
                {
                    Text        = loc["MmResumeMsg"],
                    TextWrapping = TextWrapping.Wrap,
                    FontSize    = 14,
                },
                new StackPanel
                {
                    Orientation         = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing             = 8,
                    Children            = { cancelBtn, restartBtn, resumeBtn },
                },
            },
        };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner is not null)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();

        return result;
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
