using Capnp.Rpc;
using Capnp.Rpc.Interception;
using Mas.Infrastructure.Common;
using R = Mas.Schema.Registry;
using P = Mas.Schema.Persistence;
using C = Mas.Schema.Common;

namespace Mas.Infrastructure.ServiceRegistry;

public class Registrar(ServiceRegistry reg, Restorer restorer) : R.IRegistrar
{
    public void Dispose()
    {
        Console.WriteLine("RegistratorImpl.Dispose");
    }

    #region implementation of Mas.C.IIdentifiable

    public Task<C.IdInformation> Info(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new C.IdInformation
        {
            Id = "Registrar_" + reg.Id, Name = "Registrar of " + reg.Name,
            Description = "Registrar description of " + reg.Description
        });
    }

    #endregion

    #region implementation of Mas.Schema.Registry.IRegistrar

    // register @0 (cap :Common.Identifiable, regName :Text, categoryId :Text) -> (unreg :Common.Action, reregSR :Text);
    public Task<(R.Registrar.IUnregisterCapability, P.SturdyRef)> Register(R.Registrar.RegParams ps,
        CancellationToken cancellationToken = default)
    {
        if (ps.CategoryId == null || ps.RegName == null)
            return Task.FromResult<(R.Registrar.IUnregisterCapability, P.SturdyRef)>((null, null));

        // Return if category doesnt exist exist
        if (!reg.CatId2SupportedCategories.ContainsKey(ps.CategoryId))
            return Task.FromResult<(R.Registrar.IUnregisterCapability, P.SturdyRef)>((null, null));

        try
        {
            // uuid to register cap under
            var regId = Guid.NewGuid().ToString();

            // attach a membrane around the capability to intercept save messages
            var interceptedCap = reg.SavePolicy.Attach(ps.Cap);

            // if this capabiity is supposed to be restorable accross domains, register it at the restorer
            if (ps.XDomain != null && ps.XDomain.Restorer != null)
                restorer.AddOrUpdateCrossDomainRestore(ps.XDomain.VatId, ps.XDomain.Restorer);

            // create an unregister action
            // var unreg = new Common.Action(() =>
            // {
            //     _registry._regId2Entry.TryRemove(regId, out var removedRegData);
            //     removedRegData.ReregUnsave?.Do();
            // }, restorer: _restorer, callActionOnDispose: true);


            // Create an unregister capability
            var unregCap = new UnregisterCapability(reg, regId);

            var regData = new RegData
            {
                Entry = new R.Registry.Entry
                {
                    CategoryId = ps.CategoryId,
                    Ref = interceptedCap,
                    Name = ps.RegName
                },
                UnregisterCapability = unregCap,
                Cap = Proxy.Share(ps.Cap)
            };
            // Create an unregister capability and sturdy ref to it
            // var reregCap = new ReregisterCapability(reg, );


            // create an reregister action and sturdy ref to it
            // var rereg = new Common.Action1((object anyp) =>
            // {
            //     if (anyp is C.IIdentifiable cap)
            //     {
            //         var interceptedCap = reg._savePolicy.Attach(cap);
            //
            //         reg._regId2Entry[regId] = new RegData
            //         {
            //             Entry = new R.Registry.Entry
            //             {
            //                 CategoryId = ps.CategoryId,
            //                 Ref = interceptedCap,
            //                 Name = ps.RegName
            //             },
            //             Unreg = unreg,
            //             Cap = Proxy.Share(cap)
            //         };
            //     }
            // });
            //
            // get the sturdy ref to the reregister action
            // var res = restorer.Save(BareProxy.FromImpl(reregCap));

            // and save the unsave action to remove the sturdy ref on unregistration of the capability
            // regData.ReregUnsave = res.UnsaveAction;

            reg.RegId2Entry[regId] = regData;

            // !!! note it is fine to accept manually aquired sturdy refs to the unreg action
            // !!! to not be automatically removed on unregistration of the capability as the user might
            // !!! still want to keep the aquired sturdy ref to get a reference to the capability
            // !!! in this case the registry's vat acts just as proxy and not anymore as registry
            return Task.FromResult<(R.Registrar.IUnregisterCapability, P.SturdyRef)>((unregCap,
                new P.SturdyRef()));
        }
        catch (RpcException e)
        {
            Console.Error.WriteLine(e.Message);
        }

        return Task.FromResult<(R.Registrar.IUnregisterCapability, P.SturdyRef)>((null, null));
    }

    #endregion

    #region implementation of UnregisterCapability

    private class UnregisterCapability(ServiceRegistry registry, string regId) : R.Registrar.IUnregisterCapability
    {
        public void Dispose()
        {
            Console.WriteLine("UnregisterCapabilityImpl.Dispose");
        }


        public Task<bool> Unregister(CancellationToken cancellationToken = default)
        {
            var removed = registry.RegId2Entry.TryRemove(regId, out var removedRegData);
            return Task.FromResult(removed);
        }
    }

    #endregion

    #region implementation of ReregisterCapability

    private class ReregisterCapability(ServiceRegistry registry, Restorer restorer, string regId)
        : R.Registrar.IReregisterCapability
    {
        public void Dispose()
        {
            Console.WriteLine("ReregisterCapabilityImpl.Dispose");
        }

        public Task<P.Persistent.SaveResults> Save(P.Persistent.SaveParams arg,
            CancellationToken cancellationToken = default)
        {
            if (restorer == null) return Task.FromResult<P.Persistent.SaveResults>(null);

            var res = restorer.Save(BareProxy.FromImpl(this));
            return null;
        }


        public Task<bool> Reregister(CancellationToken cancellationToken = default)
        {
            var removed = registry.RegId2Entry.TryRemove(regId, out var removedRegData);
            return Task.FromResult(removed);
        }
    }

    #endregion
}