using System.Configuration;
using System.Data;
using System.Windows;
using FamilyTreeApp.Core;
using FamilyTreeApp.UI.Windows;

namespace FamilyTreeApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        // Check if this is the first run
        var settings = SettingsManager.Current;
        
        if (!settings.FirstRunComplete)
        {
            // Show first run configuration window
            var firstRunWindow = new FirstRunWindow();
            var result = firstRunWindow.ShowDialog();
            
            // If user closed without completing, exit the app
            if (result != true)
            {
                Shutdown();
                return;
            }
        }
        
        // Show main window
        var mainWindow = new MainWindow();
        mainWindow.Show();
    }
}

