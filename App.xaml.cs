using System.Windows;
using System.Windows.Media;
using ICOforge.Views;
using Wpf.Ui.Appearance;

namespace ICOforge
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var initialTheme = ApplicationThemeManager.GetSystemTheme() == SystemTheme.Dark
                ? ApplicationTheme.Dark
                : ApplicationTheme.Light;

            ApplicationThemeManager.Apply(initialTheme);

            ApplicationAccentColorManager.Apply(
                (Color)ColorConverter.ConvertFromString("#1097e5"),
                initialTheme
            );

            ApplicationThemeManager.Changed += OnThemeChanged;

            var mainWindow = new MainWindow();
            mainWindow.Show();
        }

        private void OnThemeChanged(ApplicationTheme currentApplicationTheme, Color systemAccent)
        {
            ApplicationAccentColorManager.Apply(
                (Color)ColorConverter.ConvertFromString("#1097e5"),
                currentApplicationTheme
            );
        }
    }
}