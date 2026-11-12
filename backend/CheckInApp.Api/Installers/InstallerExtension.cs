using System.Reflection;
namespace CheckInApp.Api.Installers;

public static class InstallerExtension
{
    public static IServiceCollection InsertServiceInAssembly(this IServiceCollection services, IConfiguration configuration, Assembly assembly)
    {
        foreach (var type in assembly.GetTypes().Where(t => typeof(IInstaller).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
            .OrderBy(t => t.FullName))
            ((IInstaller)Activator.CreateInstance(type)!).InstallServices(services, configuration);
        return services;
    }
}
