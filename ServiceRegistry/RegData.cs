using R = Mas.Schema.Registry;
using C = Mas.Schema.Common;

namespace Mas.Infrastructure.ServiceRegistry;

public struct RegData
{
    public string Id { get; set; }

    public R.Registry.Entry Entry { get; set; }

    public R.Registrar.IUnregisterCapability UnregisterCapability { get; set; }


    public R.Registrar.IReregisterCapability ReregisterCapability { get; set; }

    public C.IIdentifiable Cap { get; set; }
}