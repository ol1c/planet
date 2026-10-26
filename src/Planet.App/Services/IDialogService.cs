namespace Planet.App.Services;

public enum FileKind
{
    World,
    Png,
}

public interface IDialogService
{
    string? ShowOpenFileDialog();
    string? ShowSaveFileDialog(string defaultFileName, FileKind kind = FileKind.World);
    void ShowError(string message);
}
