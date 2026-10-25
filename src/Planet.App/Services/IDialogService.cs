namespace Planet.App.Services;

public enum FileKind
{
    World,
    Png,
    Gif,
}

public interface IDialogService
{
    string? ShowOpenFileDialog();
    string? ShowSaveFileDialog(string defaultFileName, FileKind kind = FileKind.World);
    string? ShowFolderDialog(string title);
    void ShowError(string message);
}
