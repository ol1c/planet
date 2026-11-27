using Microsoft.Extensions.DependencyInjection;

namespace University.UI.Menu;

public abstract class MenuSekcji
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected MenuSekcji(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected Task WykonajAsync<TService>(Func<TService, Task> akcja) where TService : notnull =>
        Konsola.Bezpiecznie(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            await akcja(scope.ServiceProvider.GetRequiredService<TService>());
        });

    protected Task WykonajAsync(Func<IServiceProvider, Task> akcja) =>
        Konsola.Bezpiecznie(async () =>
        {
            using var scope = _scopeFactory.CreateScope();
            await akcja(scope.ServiceProvider);
        });

    public abstract Task UruchomAsync();
}
