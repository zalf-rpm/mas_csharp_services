using System.Collections.Concurrent;
using Capnp.Rpc;
using Mas.Infrastructure.Common;
using Mas.Schema.Storage;
using R = Mas.Schema.Registry;
using C = Mas.Schema.Common;
using Exception = System.Exception;

namespace Mas.Infrastructure.ServiceRegistry;

public class ServiceRegistry(C.IdInformation idInformation, Restorer restorer) : R.IRegistry
{
    internal C.IdInformation IdInformation { get; set; } = idInformation;
    internal Restorer Restorer { get; } = restorer;
    internal ConcurrentDictionary<string, C.IdInformation> CatId2SupportedCategories { get; } = new();
    internal ConcurrentDictionary<string, RegData> RegId2Entry { get; } = new();

    internal Store.IContainer CategoriesStorage { get; set; }


    public C.IdInformation[] Categories
    {
        get => CatId2SupportedCategories.Values.ToArray();
        set
        {
            foreach (var cat in value)
                try
                {
                    CatId2SupportedCategories[cat.Id] = cat;
                }
                catch (Exception)
                {
                }
        }
    }

    public void Dispose()
    {
        // dispose registered caps
        foreach (var oid2E in RegId2Entry)
        {
            oid2E.Value.Entry.Ref?.Dispose(); // registered services
            oid2E.Value.UnregisterCapability.Dispose();
        }

        Console.WriteLine("Dispose");
    }

    #region implementation of Mas.C.IIdentifiable

    public Task<C.IdInformation> Info(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IdInformation);
    }

    #endregion


    #region implementation of IInterceptionPolicy

    public Task<C.IdInformation> CategoryInfo(string categoryId, CancellationToken cancellationToken = default)
    {
        return CatId2SupportedCategories.TryGetValue(categoryId, out var category)
            ? Task.FromResult(category)
            : Task.FromResult<C.IdInformation>(null);
    }

    public Task<IReadOnlyList<R.Registry.Entry>> Entries(string categoryId,
        CancellationToken cancellationToken = default)
    {
        var entries = new List<R.Registry.Entry>();
        if (categoryId != null)
            entries.AddRange((from p in RegId2Entry
                where p.Value.Entry.CategoryId == categoryId
                select p.Value.Entry).Select(e => new R.Registry.Entry
            {
                CategoryId = e.CategoryId,
                Ref = Proxy.Share(e.Ref),
                Name = e.Name
            }));
        else
            entries.AddRange((from p in RegId2Entry select p.Value.Entry).Select(e => new R.Registry.Entry
            {
                CategoryId = e.CategoryId,
                Ref = Proxy.Share(e.Ref),
                Name = e.Name
            }));
        return Task.FromResult<IReadOnlyList<R.Registry.Entry>>(entries);
    }

    public Task<IReadOnlyList<C.IdInformation>> SupportedCategories(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<C.IdInformation>>(CatId2SupportedCategories.Values.ToList());
    }

    #endregion
}