using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ICOforge.Services
{
    /// <summary>
    /// Modern dialog service replacing deprecated WindowsAPICodePack with enhanced UX
    /// </summary>
    public class ModernDialogService : IDialogService
    {
        private readonly Window _ownerWindow;
        private readonly string _recentFilesPath;
        private readonly string _recentFoldersPath;
        private readonly List<string> _recentFiles = new();
        private readonly List<string> _recentFolders = new();

        private static readonly string[] ValidImageExtensions = { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".svg", ".webp", ".tif", ".tiff" };

        public ModernDialogService(Window ownerWindow)
        {
            _ownerWindow = ownerWindow;
            var appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ICOforge");
            Directory.CreateDirectory(appDataPath);
            _recentFilesPath = Path.Combine(appDataPath, "recent_files.json");
            _recentFoldersPath = Path.Combine(appDataPath, "recent_folders.json");
            LoadRecentData();
        }

        public IEnumerable<string>? ShowOpenFileDialog()
        {
            var dialog = new OpenFileDialog
            {
                Title = "Select Image Files",
                Multiselect = true,
                Filter = BuildImageFilter(),
                FilterIndex = 1,
                InitialDirectory = GetLastUsedFolder()
            };

            // Add file preview and validation
            dialog.FileOk += ValidateImageFiles;

            if (dialog.ShowDialog(_ownerWindow) == true)
            {
                var selectedFiles = dialog.FileNames.ToList();
                UpdateRecentFiles(selectedFiles);
                var firstFile = dialog.FileNames.FirstOrDefault();
                var folder = firstFile != null ? Path.GetDirectoryName(firstFile) : null;
                if (folder != null)
                {
                    UpdateRecentFolders(folder);
                }
                return selectedFiles;
            }

            return null;
        }

        public string? ShowFolderPickerDialog()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a folder containing images",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = GetLastUsedFolder()
            };

            // Ensure Vista-style dialog for better UX
            if (Environment.OSVersion.Version.Major >= 6)
            {
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedFolder = dialog.SelectedPath;
                UpdateRecentFolders(selectedFolder);
                return selectedFolder;
            }

            return null;
        }

        public string? ShowSaveDialog()
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a custom output folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true,
                SelectedPath = GetDefaultOutputFolder()
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedFolder = dialog.SelectedPath;
                UpdateRecentFolders(selectedFolder);
                return selectedFolder;
            }

            return null;
        }

        public string? ShowColorPickerDialog(string initialColor)
        {
            using var colorDialog = new System.Windows.Forms.ColorDialog
            {
                FullOpen = true,
                ShowHelp = true,
                AnyColor = true,
                SolidColorOnly = false
            };

            try
            {
                var wpfColor = (Color)ColorConverter.ConvertFromString(initialColor);
                colorDialog.Color = System.Drawing.Color.FromArgb(wpfColor.A, wpfColor.R, wpfColor.G, wpfColor.B);
            }
            catch
            {
                /* Ignore invalid hex - use default */
            }

            if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                var selectedColor = colorDialog.Color;
                return $"#{selectedColor.R:X2}{selectedColor.G:X2}{selectedColor.B:X2}";
            }
            return null;
        }

        public void ShowAnalysisReport(string report, string title)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var scrollViewer = new ScrollViewer
                {
                    Content = new System.Windows.Controls.TextBox
                    {
                        Text = report,
                        IsReadOnly = true,
                        TextWrapping = TextWrapping.NoWrap,
                        AcceptsReturn = true,
                        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas"),
                        FontSize = 12,
                        Background = System.Windows.Media.Brushes.White,
                        Foreground = System.Windows.Media.Brushes.Black
                    },
                    MaxHeight = 400,
                    MaxWidth = 600,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                };

                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = title,
                    Content = scrollViewer,
                    CloseButtonText = "OK",
                    MaxWidth = 650,
                    MaxHeight = 500
                };
                _ = messageBox.ShowDialogAsync();
            });
        }

        public void ShowMessageBox(string message, string title)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var messageBox = new Wpf.Ui.Controls.MessageBox
                {
                    Title = title,
                    Content = message,
                    CloseButtonText = "OK"
                };
                _ = messageBox.ShowDialogAsync();
            });
        }

        public void OpenInExplorer(string path)
        {
            try
            {
                Process.Start("explorer.exe", path);
            }
            catch (Exception ex)
            {
                ShowMessageBox($"Could not open path: {path}\nError: {ex.Message}", "Explorer Error");
            }
        }

        private string BuildImageFilter()
        {
            var filters = new List<string>
            {
                "Image Files|" + string.Join(";", ValidImageExtensions.Select(ext => $"*{ext}")),
                "PNG Files|*.png",
                "JPEG Files|*.jpg;*.jpeg",
                "Bitmap Files|*.bmp",
                "GIF Files|*.gif",
                "SVG Files|*.svg",
                "WebP Files|*.webp",
                "TIFF Files|*.tif;*.tiff",
                "All files|*.*"
            };
            return string.Join("|", filters);
        }

        private void ValidateImageFiles(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (sender is OpenFileDialog dialog && dialog.FileNames.Length > 0)
            {
                var invalidFiles = dialog.FileNames
                    .Where(file => !IsValidImageFile(file))
                    .ToList();

                if (invalidFiles.Any())
                {
                    var message = invalidFiles.Count == 1
                        ? $"The file '{Path.GetFileName(invalidFiles.First())}' is not a valid image file."
                        : $"{invalidFiles.Count} files are not valid image files:\n{string.Join("\n", invalidFiles.Select(Path.GetFileName))}";

                    ShowMessageBox(message, "Invalid File Selection");
                    e.Cancel = true;
                }
            }
        }

        private bool IsValidImageFile(string filePath)
        {
            try
            {
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                return ValidImageExtensions.Contains(extension) && File.Exists(filePath);
            }
            catch
            {
                return false;
            }
        }

        private string GetLastUsedFolder()
        {
            return _recentFolders.FirstOrDefault() ?? GetDefaultFolder();
        }

        private string GetDefaultFolder()
        {
            // Try common image folders in order of preference
            var folders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            };

            return folders.FirstOrDefault(Directory.Exists) ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        private string GetDefaultOutputFolder()
        {
            // Try Downloads first for output, then fallback to Documents
            var folders = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads",
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            };

            return folders.FirstOrDefault(Directory.Exists) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        private void UpdateRecentFiles(IEnumerable<string> files)
        {
            foreach (var file in files.Take(10)) // Keep last 10 files
            {
                if (File.Exists(file) && !_recentFiles.Contains(file))
                {
                    _recentFiles.Insert(0, file);
                }
            }

            // Keep only most recent 20 files
            if (_recentFiles.Count > 20)
            {
                _recentFiles.RemoveRange(20, _recentFiles.Count - 20);
            }

            SaveRecentFiles();
        }

        private void UpdateRecentFolders(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            _recentFolders.Remove(folder);
            _recentFolders.Insert(0, folder);

            // Keep only most recent 10 folders
            if (_recentFolders.Count > 10)
            {
                _recentFolders.RemoveRange(10, _recentFolders.Count - 10);
            }

            SaveRecentFolders();
        }

        private void LoadRecentData()
        {
            try
            {
                if (File.Exists(_recentFilesPath))
                {
                    var filesJson = File.ReadAllText(_recentFilesPath);
                    var files = JsonSerializer.Deserialize<List<string>>(filesJson);
                    if (files != null)
                    {
                        _recentFiles.AddRange(files.Where(File.Exists));
                    }
                }

                if (File.Exists(_recentFoldersPath))
                {
                    var foldersJson = File.ReadAllText(_recentFoldersPath);
                    var folders = JsonSerializer.Deserialize<List<string>>(foldersJson);
                    if (folders != null)
                    {
                        _recentFolders.AddRange(folders.Where(Directory.Exists));
                    }
                }
            }
            catch
            {
                // Ignore errors loading recent data
            }
        }

        private void SaveRecentFiles()
        {
            try
            {
                var json = JsonSerializer.Serialize(_recentFiles);
                File.WriteAllText(_recentFilesPath, json);
            }
            catch
            {
                // Ignore errors saving recent files
            }
        }

        private void SaveRecentFolders()
        {
            try
            {
                var json = JsonSerializer.Serialize(_recentFolders);
                File.WriteAllText(_recentFoldersPath, json);
            }
            catch
            {
                // Ignore errors saving recent folders
            }
        }
    }
}
