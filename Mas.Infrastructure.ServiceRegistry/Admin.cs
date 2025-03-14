using Capnp.Rpc;
using R = Mas.Schema.Registry;
using C = Mas.Schema.Common;

namespace Mas.Infrastructure.ServiceRegistry;

internal class Admin(ServiceRegistry registry) : R.IAdmin
{
    public void Dispose()
    {
    }

    #region implementation of IIdentifiable

    public Task<C.IdInformation> Info(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new C.IdInformation
        {
            Id = "Admin_" + registry.IdInformation.Id, Name = "Admin of " + registry.IdInformation.Name,
            Description = "Admin description of " + registry.IdInformation.Description
        });
    }

    #endregion

    #region implementation of Mas.Schema.Registry.IAdmin

    public Task<bool> AddCategory(C.IdInformation category, bool upsert,
        CancellationToken cancellationToken = default)
    {
        if (registry.CatId2SupportedCategories.ContainsKey(category.Id) && !upsert) return Task.FromResult(false);
        registry.CatId2SupportedCategories[category.Id] = category;
        return Task.FromResult(true);

        // // save new category to storage
        // if (_registry._categoriesStorage != null) {
        //     await _registry._categoriesStorage.AddObject(new S.Storage.Store.Container.Object {
        //         Key = category.Id, Value = new S.Storage.Store.Container.Object.value {
        //             AnyValue=category
        //         }
        //     });
        // }
    }

    public Task<IReadOnlyList<string>> MoveObjects(IReadOnlyList<string> objectIds, string? toCatId,
        CancellationToken cancellationToken = default)
    {
        // check that the move to category actually exists, else treat it as none existing
        if (!registry.CatId2SupportedCategories.ContainsKey(toCatId)) toCatId = null;

        // if there is no category to move to, do rather nothing
        // another option would be to remove the objects, but this is what removeObjects is for
        if (toCatId == null) return Task.FromResult<IReadOnlyList<string>>(new List<string>());

        var moved = new List<string>();
        foreach (var oid in objectIds)
            try
            {
                registry.RegId2Entry[oid].Entry.CategoryId = toCatId;
                moved.Add(oid);
            }
            catch (KeyNotFoundException)
            {
            }

        return Task.FromResult<IReadOnlyList<string>>(moved);
    }

    public Task<R.IRegistry> Registry(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<R.IRegistry>(registry);
    }

    public Task<IReadOnlyList<C.IIdentifiable>> RemoveCategory(string? categoryId,
        string? moveObjectsToCategoryId,
        CancellationToken cancellationToken = default)
    {
        var removed = new List<C.IIdentifiable>();
        // no category means nothing to remove
        if (categoryId == null) return Task.FromResult<IReadOnlyList<C.IIdentifiable>>(removed);

        // check that the move to category actually exists, else treat it as none existing
        if (moveObjectsToCategoryId != null &&
            !registry.CatId2SupportedCategories.ContainsKey(moveObjectsToCategoryId))
            moveObjectsToCategoryId = null;

        // move objects from old to new category
        // but remember objects to be removed, to remove them outside of the iterator
        var removedIds = new List<string>();
        foreach (var oid2Entry in from p in registry.RegId2Entry
                 where p.Value.Entry.CategoryId == categoryId
                 select p)
            if (moveObjectsToCategoryId == null)
            {
                removed.Add(Proxy.Share(oid2Entry.Value.Entry.Ref));
                removedIds.Add(oid2Entry.Key);
            }
            else
            {
                oid2Entry.Value.Entry.CategoryId = moveObjectsToCategoryId;
            }

        // remove remembered objects from registry
        foreach (var oid in removedIds) registry.RegId2Entry.Remove(oid, out var removedValue);

        // finally remove the category
        registry.CatId2SupportedCategories.TryRemove(categoryId, out _);

        // remove category from storage
        //await _registry._categoriesStorage.RemoveObject(categoryId);

        return Task.FromResult<IReadOnlyList<C.IIdentifiable>>(removed);
    }

    public async Task<IReadOnlyList<C.IIdentifiable>> RemoveObjects(IReadOnlyList<string> objectIds,
        CancellationToken cancellationToken = default)
    {
        var removed = new List<C.IIdentifiable>();
        foreach (var oid in objectIds)
            try
            {
                var entry = registry.RegId2Entry[oid];
                var obj = entry.Entry.Ref;
                await registry.RegId2Entry[oid].UnregisterCapability.Unregister(cancellationToken);
                if (!registry.RegId2Entry.ContainsKey(oid)) removed.Add(Proxy.Share(entry.Entry.Ref));
            }
            catch (KeyNotFoundException)
            {
            }

        return removed;
    }

    #endregion
}