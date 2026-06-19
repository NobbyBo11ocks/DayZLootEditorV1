using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using DZServerToolkit.Services;
using DZServerToolkit.ViewModels;
using DZServerToolkit.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DZServerToolkit;

public partial class App : Application
{
    private ServiceProvider? _services;
    private static bool _exceptionHandlersRegistered;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        RegisterGlobalExceptionHandlers();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        _services = serviceCollection.BuildServiceProvider(validateScopes: true);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = _services.GetRequiredService<MainWindow>();

            if (_services.GetRequiredService<IFileDialogService>() is AvaloniaFileDialogService fileDialogService)
            {
                fileDialogService.SetHost(mainWindow);
            }

            desktop.MainWindow = mainWindow;
            desktop.Exit += (_, _) => _services?.Dispose();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IFileDialogService, AvaloniaFileDialogService>();
        services.AddSingleton<ITypesXmlService, TypesXmlService>();
        services.AddSingleton<IValidationService, ValidationService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<ICustomCeService, CustomCeService>();
        services.AddSingleton<ILootProfileService, LootProfileService>();
        services.AddSingleton<IRecentFilesService, RecentFilesService>();
        services.AddSingleton<ITextDiffService, TextDiffService>();

        services.AddSingleton<TypesEditorViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        if (_exceptionHandlersRegistered)
        {
            return;
        }

        _exceptionHandlersRegistered = true;

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception exception)
            {
                CrashLogService.LogException("AppDomain.CurrentDomain.UnhandledException", exception);
                return;
            }

            CrashLogService.LogMessage(
                "AppDomain.CurrentDomain.UnhandledException",
                $"Unhandled non-exception payload: {args.ExceptionObject}");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            CrashLogService.LogException("TaskScheduler.UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            var logPath = CrashLogService.LogException("Dispatcher.UIThread.UnhandledException", args.Exception);
            CrashLogService.LogMessage(
                "Dispatcher.UIThread.UnhandledException",
                $"Application shutdown triggered after fatal UI exception. Crash log: {logPath}");

            args.Handled = true;

            if (Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Shutdown(-1);
                return;
            }

            Environment.FailFast(
                $"A fatal UI thread exception occurred. See crash log: {logPath}",
                args.Exception);
        };
    }
}
