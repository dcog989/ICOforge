namespace ICOforge
{
    public interface IDialogService
    {
        IEnumerable<string>? ShowOpenFileDialog();
        string? ShowFolderPickerDialog();
        string? ShowSaveDialog();
        string? ShowColorPickerDialog(string initialColor);
        void ShowAnalysisReport(string report, string title);
        void ShowMessageBox(string message, string title);
        void OpenInExplorer(string path);
    }
}