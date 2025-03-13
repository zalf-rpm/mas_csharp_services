using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Capnp;
using Capnp.Rpc;
using Capnp.Rpc.Interception;
using Mas.Infrastructure.Common;
using Mas.Schema.Storage;
using R = Mas.Schema.Registry;
using P = Mas.Schema.Persistence;
using C = Mas.Schema.Common;
using Exception = System.Exception;

namespace Mas.Infrastructure.ServiceRegistry;

public class ServiceRegistry : R.IRegistry
{
    internal readonly ConcurrentDictionary<string, C.IdInformation> CatId2SupportedCategories;

    internal readonly ConcurrentDictionary<string, RegData> RegId2Entry;

    //private ConcurrentDictionary<string, (ulong[], string)> _extSRT2VatIdAndIntSRT = new(); // mapping of external sturdy ref token to internal one
    internal readonly IInterceptionPolicy SavePolicy;

    private Store.IContainer _categoriesStorage;
    //private ConcurrentDictionary<ulong[], Mas.Schema.Persistence.IRestorer> _vatId2Restorer = new();   

    public ServiceRegistry()
    {
        CatId2SupportedCategories = new ConcurrentDictionary<string, C.IdInformation>();
        RegId2Entry =
            new ConcurrentDictionary<string, RegData>(); //Tuple<string, Registry.Entry, Common.Unregister>>();
        SavePolicy = new InterceptPersistentPolicy(this);
    }

    public Restorer Restorer { get; set; }

    public string Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

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
        return Task.FromResult(new C.IdInformation
            { Id = Id, Name = Name, Description = Description });
    }

    #endregion

    public void SetCategoriesStorage(Store.IContainer storage)
    {
        _categoriesStorage = storage;
        //var objs = await storage.ListObjects();
        //Categories = objs.Select(o => (C.IdInformation)o.Value.AnyValue).ToArray();
    }


    private class InterceptPersistentPolicy(ServiceRegistry registry) : IInterceptionPolicy
    {
        //private ulong RestorerInterfaceId;
        //private ulong RestoreMethodId = 0;
        private readonly ulong _persistentInterfaceId =
            typeof(P.IPersistent).GetCustomAttribute<TypeIdAttribute>(false)?.Id ?? 0;

        private readonly ulong _saveMethodId = 0;

        //RestorerInterfaceId = typeof(P.IRestorer).GetCustomAttribute<Capnp.TypeIdAttribute>(false)?.Id ?? 0;

        public bool Equals([AllowNull] IInterceptionPolicy other)
        {
            return Equals(other);
        }

        public void OnCallFromAlice(CallContext callContext)
        {
            callContext.ForwardToBob();
        }

        public void OnReturnFromBob(CallContext callContext)
        {
            if (callContext.InterfaceId == _persistentInterfaceId && callContext.MethodId == _saveMethodId)
            {
                var result = CapnpSerializable.Create<P.Persistent.SaveResults>(callContext.OutArgs);
                var intSrt = result.SturdyRef.LocalRef.Text;
                var extSrt = Guid.NewGuid().ToString();
                registry.Restorer.InstallCrossDomainMapping(extSrt, result.SturdyRef.Vat.Id, intSrt);
                result.SturdyRef = registry.Restorer.SturdyRef(extSrt);
                var resultWriter = SerializerState.CreateForRpc<P.Persistent.SaveResults.WRITER>();
                result.serialize(resultWriter);
                callContext.OutArgs = resultWriter;
            }

            callContext.ReturnToAlice();
        }
    }

    #region implementation of IInterceptionPolicy

    public Task<C.IdInformation> CategoryInfo(string categoryId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(CatId2SupportedCategories.GetValueOrDefault(categoryId));
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