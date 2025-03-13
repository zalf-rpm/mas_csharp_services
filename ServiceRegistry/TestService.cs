using Mas.Schema.Common;

namespace Mas.Infrastructure.ServiceRegistry;

public class TestService(IdInformation idInformation) : IIdentifiable
{
    public void Dispose()
    {
        Console.WriteLine("Dispose");
    }

    public Task<IdInformation> Info(CancellationToken cancellationToken = new())
    {
        return Task.FromResult(idInformation);
    }

    public void Hello()
    {
        Console.WriteLine("Hello");
    }
}
