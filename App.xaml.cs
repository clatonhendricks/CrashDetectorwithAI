using System;
using System.Windows;
using Microsoft.Win32;

namespace CrashDetectorwithAI
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private const string AppName = "CrashDetectorwithAI";
        private const string RunKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            RegisterStartupIfNeeded();
        }

        private void RegisterStartupIfNeeded()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
                if (key == null) return;

                var existingValue = key.GetValue(AppName);
                if (existingValue != null) return;

                string exePath = Environment.ProcessPath ?? AppDomain.CurrentDomain.BaseDirectory + AppName + ".exe";
                key.SetValue(AppName, $"\"{exePath}\"");
            }
            catch
            {
                // Silently fail — startup registration is non-critical
            }
        }
    }
}

