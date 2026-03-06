using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Task = System.Threading.Tasks.Task;

namespace PowerSql
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PowerSqlPackage.PackageGuidString)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad("8D497B0D-26E0-466D-A08D-7CC1A8C822F2", PackageAutoLoadFlags.BackgroundLoad)] // Guid específico del editor de texto
    public sealed class PowerSqlPackage : AsyncPackage
    {
        public const string PackageGuidString = "4e49ea2f-1a3b-4688-9d62-10f54070a927"; // Mismo ID del manifest

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // Switch al hilo de interfaz de usuario
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            // Forzar inicialización del Logger
            Services.Logger.Log("PowerSqlPackage inicializado exitosamente en SSMS.");
        }
    }
}
