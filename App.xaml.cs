using Microsoft.Extensions.DependencyInjection;
using System.Configuration;
using System.Data;
using System.Windows;
using SZExtractorGUI.Services;
using SZExtractorGUI.ViewModels;
using SZExtractorGUI.Views;
using Configuration = SZExtractorGUI.Services.Configuration;
using System;
using System.Diagnostics;
using System.IO;
using SZExtractorGUI.Models;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace SZExtractorGUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private ServiceProvider _serviceProvider;
    private IInitializationService _initService;
    private readonly CancellationTokenSource _shutdownCts = new();

    public App()
    {
        // Add assembly resolution handler
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);
        var settings = new Settings(); // Use default Tools path
        var dllPath = Path.Combine(settings.ToolsDirectory, $"{assemblyName.Name}.dll");

        return File.Exists(dllPath) ? Assembly.LoadFrom(dllPath) : null;
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
        
        // Store reference to initialization service
        _initService = _serviceProvider.GetRequiredService<IInitializationService>();
        
        // Clear any existing error state
        var errorHandler = _serviceProvider.GetRequiredService<IErrorHandlingService>();
        errorHandler.ClearError();
        
        // Get main window and show it immediately
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // Start initialization in background
        _ = Task.Run(async () =>
        {
            try
            {
                await _initService.InitializeAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[App] Background initialization failed: {ex.Message}");
            }
        });
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Register Configuration and Settings
        services.AddSingleton<Configuration>();
        services.AddSingleton(provider => provider.GetRequiredService<Configuration>().Settings);

        // Register Core Services
        services.AddSingleton<IApplicationEvents, ApplicationEvents>();
        services.AddSingleton<IErrorHandlingService, ErrorHandlingService>();
        services.AddSingleton<IHttpClientFactory, HttpClientFactory>();
        
        // Register Service Layer - No Circular Dependencies
        services.AddSingleton<ISzExtractorService, SzExtractorService>();
        services.AddSingleton<IServerConfigurationService, ServerConfigurationService>();
        services.AddSingleton<IServerLifecycleService, ServerLifecycleService>();
        services.AddSingleton<IInitializationService, InitializationService>();
        services.AddSingleton<IRetryService, RetryService>();
        services.AddSingleton<IBackgroundOperationsService, BackgroundOperationsService>();

        
        // Register Feature Services
        services.AddSingleton<IContentTypeService, ContentTypeService>();
        services.AddSingleton<IFetchOperationService, FetchOperationService>();
        services.AddSingleton<IItemFilterService, ItemFilterService>();

        // Register ViewModels and Views
        services.AddTransient<FetchPageViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<FetchPage>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        _shutdownCts.Cancel();
        
        if (_initService != null)
        {
            await _initService.ShutdownAsync();
        }

        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
        else
        {
            _serviceProvider?.Dispose();
        }

        _shutdownCts.Dispose();
        base.OnExit(e);
    }
}

