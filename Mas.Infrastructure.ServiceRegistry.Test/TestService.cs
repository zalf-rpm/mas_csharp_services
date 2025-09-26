using Mas.Rpc.Test;
using Mas.Schema.Common;

namespace Mas.Infrastructure.ServiceRegistry.Test;

public interface ITestService:IA, IIdentifiable { }
public class TestService(IdInformation idInformation) : ITestService    
{
    public void Dispose()
    {
        Console.WriteLine("Dispose");
    }

    public Task<string> Method(string param, CancellationToken cancellationToken_ = default)
    {
        return Task.FromResult("Hello " + param);
    }

    public Task<IdInformation> Info(CancellationToken cancellationToken_ = default)
    {
        return Task.FromResult(idInformation);
    }
}