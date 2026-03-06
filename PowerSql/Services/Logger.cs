using System;
using System.Diagnostics;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace PowerSql.Services
{
    public static class Logger
    {
        private static IVsOutputWindowPane _pane;

        public static void Log(string message)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async delegate
            {
                try
                {
                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                    if (_pane == null)
                    {
                        if (Package.GetGlobalService(typeof(SVsOutputWindow)) is IVsOutputWindow outputWindow)
                        {
                            Guid paneGuid = new Guid("4A30B171-8970-4E38-AC3C-43BCF84AD0DE");
                            outputWindow.CreatePane(ref paneGuid, "POWERSQL Extension", 1, 1);
                            outputWindow.GetPane(ref paneGuid, out _pane);
                        }
                    }

                    if (_pane != null)
                    {
                        string logLine = $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n";
                        _pane.OutputStringThreadSafe(logLine);
                    }

                    // También escribimos en Debug Output
                    Debug.WriteLine($"[POWERSQL] {message}");
                }
                catch
                {
                    // Silencioso en caso de fallo crítico de logging
                }
            });
        }
    }
}
