using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Modbus.Mobile.Avalonia.ViewModels;

namespace Modbus.Mobile.Avalonia;

/// <summary>
/// Given a view model, returns the corresponding view by naming convention
/// (FooViewModel -> FooView).
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        var name = param.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);

        if (type != null)
            return (Control)Activator.CreateInstance(type)!;

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data) => data is ViewModelBase;
}
