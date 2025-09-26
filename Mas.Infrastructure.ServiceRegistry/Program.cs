using System.CommandLine;
using System.Net;
using Capnp.Rpc;
using Mas.Infrastructure.Common;
using Mas.Infrastructure.ServiceRegistry;
using Mas.Schema.Common;
using Mas.Schema.Storage;
using Admin = Mas.Infrastructure.ServiceRegistry.Admin;
using Exception = System.Exception;
using Registrar = Mas.Infrastructure.ServiceRegistry.Registrar;
using Restorer = Mas.Infrastructure.Common.Restorer;

var rootCommand = new RootCommand("Service Registry Application");

var defaultId = Guid.NewGuid().ToString();

var idOption = new Option<string>("--id")
    { Description = "Unique identifier for the service registry", DefaultValueFactory = parseResult => defaultId };

var nameOption = new Option<string>("--name")
    { Description = "Display name for the service registry", DefaultValueFactory = parseResult => defaultId };

var descOption = new Option<string>("--description")
    { Description = "Description of the service registry", DefaultValueFactory = parseResult => "" };

var categoriesContainerSrOption = new Option<string>("--categories-container-sr")
    { Description = "Categories container sturdy reference", DefaultValueFactory = parseResult => "" };

var restorerContainerSrOption = new Option<string>("--restorer-container-sr")
    { Description = "Restorer container sturdy reference", DefaultValueFactory = parseResult => "" };

var portOption = new Option<int>("--port")
    { Description = "TCP port to listen on", DefaultValueFactory = parseResult => 42000 };

var checkIpOption = new Option<string>("--check-IP")
    { Description = "IP address to check connectivity against", DefaultValueFactory = parseResult => "8.8.8.8" };

var checkPortOption = new Option<int>("--check-port")
    { Description = "Port to check connectivity against", DefaultValueFactory = parseResult => 53 };

// Add options to command
rootCommand.Options.Add(idOption);
rootCommand.Options.Add(nameOption);
rootCommand.Options.Add(descOption);
rootCommand.Options.Add(categoriesContainerSrOption);
rootCommand.Options.Add(restorerContainerSrOption);
rootCommand.Options.Add(portOption);
rootCommand.Options.Add(checkIpOption);
rootCommand.Options.Add(checkPortOption);

// Parse the command line arguments
var parseResult = rootCommand.Parse(args);

var checkIp = parseResult.GetValue(checkIpOption);
var checkPort = parseResult.GetValue(checkPortOption);
var id = parseResult.GetValue(idOption);
var name = parseResult.GetValue(nameOption);
var desc = parseResult.GetValue(descOption);
var tcpPort = parseResult.GetValue(portOption);
var categoriesContainerSr = parseResult.GetValue(categoriesContainerSrOption);
var restorerContainerSr = parseResult.GetValue(restorerContainerSrOption);

// Define this method to start your service registry with the parsed parameters
var restorer = new Restorer { TcpHost = ConnectionManager.GetLocalIPAddress() };
using var conMan = new ConnectionManager();

var registry = new ServiceRegistry(new IdInformation
{
    Id = id,
    Name = name,
    Description = desc
}, restorer);

conMan.Bind(IPAddress.Any, tcpPort, restorer);
restorer.TcpPort = conMan.Port;

try
{
    if (categoriesContainerSr != "")
        registry.CategoriesStorage = await conMan.Connect<Store.IContainer>(categoriesContainerSr);
    if (restorerContainerSr != "")
        restorer.StorageContainer =
            await conMan.Connect<Store.IContainer>(restorerContainerSr);
}

catch (Exception e)
{
    Console.WriteLine($"Exception trying to register registry SR. Exception {e}");
}

Console.WriteLine("Started ServiceRegistry with these Categories:");
foreach (var cat in registry.Categories) Console.WriteLine(cat.Id);
var registrySturdyRef = restorer.SaveStr(BareProxy.FromImpl(registry), "registry").Item1;
Console.WriteLine($"registry_sr: {registrySturdyRef}");

var registrar = new Registrar(registry, restorer);
var regSturdyRef = restorer.SaveStr(BareProxy.FromImpl(registrar), "registrar").Item1;
Console.WriteLine($"registrar_sr: {regSturdyRef}");

var registryAdmin = new Admin(registry);
var registryAdminSturdyRef = restorer.SaveStr(BareProxy.FromImpl(registryAdmin), "registry_admin").Item1;
Console.WriteLine($"registry_admin_sr: {registryAdminSturdyRef}");

var serviceAdmin = new Mas.Infrastructure.Common.Service.Admin(registry, info =>
    registry.IdInformation = new IdInformation
    {
        Id = info.Id,
        Name = info.Name,
        Description = info.Description
    });

var serviceAdminSturdyRef = restorer.SaveStr(BareProxy.FromImpl(serviceAdmin)).Item1;
Console.WriteLine($"service_admin_sr: {serviceAdminSturdyRef}");

while (true) Thread.Sleep(1000);