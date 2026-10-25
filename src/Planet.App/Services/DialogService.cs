using System.Windows;
using Microsoft.Win32;
using Planet.Core.IO;

namespace Planet.App.Services;

public sealed class DialogService : IDialogService
{
    private const string WorldFilter = "Plansza Planet (*.txt)|*.txt|Wszystkie pliki (*.*)|*.*";

    public string? ShowOpenFileDialog()
    {
        var dialog = new OpenFileDialog { Filter = WorldFilter, DefaultExt = WorldFile.FileExtension };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string defaultFileName, FileKind kind = FileKind.World)
    {
        var (filter, extension) = kind switch
        {
            FileKind.Png => ("Obraz PNG (*.png)|*.png", ".png"),
            FileKind.Gif => ("Animacja GIF (*.gif)|*.gif", ".gif"),
            _ => (WorldFilter, WorldFile.FileExtension),
        };
        var dialog = new SaveFileDialog { Filter = filter, DefaultExt = extension, FileName = defaultFileName };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowFolderDialog(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void ShowError(string message) =>
        MessageBox.Show(message, "Planet", MessageBoxButton.OK, MessageBoxImage.Error);
}
