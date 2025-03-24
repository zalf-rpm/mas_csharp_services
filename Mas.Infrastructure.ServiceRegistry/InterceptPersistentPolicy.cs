using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Capnp;
using Capnp.Rpc.Interception;
using P = Mas.Schema.Persistence;


namespace Mas.Infrastructure.ServiceRegistry;

public class InterceptPersistentPolicy(ServiceRegistry registry) : IInterceptionPolicy
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

    #region implementation of IInterceptionPolicy

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

    #endregion
}
