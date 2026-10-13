using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using SshSenderApp.ViewModels;

namespace SshSenderApp.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
    }

    // Opens the native file picker (Android's document picker on the phone, a normal dialog on Windows).
    private async void OnPickFiles(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var picked = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose files to send",
            AllowMultiple = true
        });

        var list = new List<PickedFile>();
        foreach (var item in picked)
        {
            long size = 0;
            try
            {
                var props = await item.GetBasicPropertiesAsync();
                size = (long)(props.Size ?? 0);
            }
            catch
            {
                // size unknown; upload still works, progress just can't be calculated
            }

            var file = item; // capture for the lambda
            list.Add(new PickedFile(file.Name, size, () => file.OpenReadAsync()));
        }

        vm.SetFiles(list);
    }
}
