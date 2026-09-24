using DesktopBoard.Core.Interfaces;
using DesktopBoard.Windows.DesktopIntegration;
using DesktopBoard.Windows.Startup;
using DesktopBoard.Windows.Wallpaper;
using Microsoft.Extensions.DependencyInjection;

namespace DesktopBoard.Windows;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDesktopBoardWindows(this IServiceCollection services)
    {
        services.AddSingleton<IDesktopHostService, DesktopHostService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ISystemWallpaperService, SystemWallpaperService>();
        return services;
    }
}
