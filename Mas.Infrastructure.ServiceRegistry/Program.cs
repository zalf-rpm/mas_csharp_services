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

var idOption = new Option<string>(
    "--id",
    () => defaultId,
    "Unique identifier for the service registry");

var nameOption = new Option<string>(
    "--name",
    () => defaultId,
    "Display name for the service registry");

var descOption = new Option<string>(
    "--description",
    () => "",
    "Description of the service registry");

var categoriesContainerSrOption = new Option<string>(
    "--categories-container-sr",
    () => "",
    "Categories container sturdy reference");

var restorerContainerSrOption = new Option<string>(
    "--restorer-container-sr",
    () => "",
    "Restorer container sturdy reference");

var portOption = new Option<int>(
    "--port",
    () => 42000,
    "TCP port to listen on");

var checkIpOption = new Option<string>(
    "--check-IP",
    () => "8.8.8.8",
    "IP address to check connectivity against");

var checkPortOption = new Option<int>(
    "--check-port",
    () => 53,
    "Port to check connectivity against");


// Add options to command
rootCommand.AddOption(idOption);
rootCommand.AddOption(nameOption);
rootCommand.AddOption(descOption);
rootCommand.AddOption(categoriesContainerSrOption);
rootCommand.AddOption(restorerContainerSrOption);
rootCommand.AddOption(portOption);
rootCommand.AddOption(checkIpOption);
rootCommand.AddOption(checkPortOption);


// Set the handler
rootCommand.SetHandler(RunServiceRegistry, idOption, nameOption, descOption, categoriesContainerSrOption,
    restorerContainerSrOption,
    portOption, checkIpOption, checkPortOption);

// Parse the command line arguments
return await rootCommand.InvokeAsync(args);


// Define this method to start your service registry with the parsed parameters
async Task RunServiceRegistry(string id, string name, string desc, string categoriesContainerSr,
    string restorerContainerSr, int tcpPort, string checkIp, int checkPort)
{
    var restorer = new Restorer { TcpHost = ConnectionManager.GetLocalIPAddress(checkIp, checkPort) };
    using var conMan = new ConnectionManager(restorer);

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
    var registrySturdyRef = restorer.SaveStr(BareProxy.FromImpl(registry)).Item1;
    Console.WriteLine($"registry_sr: {registrySturdyRef}");


    var registrar = new Registrar(registry, restorer);
    var regSturdyRef = restorer.SaveStr(BareProxy.FromImpl(registrar)).Item1;
    Console.WriteLine($"registrar_sr: {regSturdyRef}");


    var registryAdmin = new Admin(registry);
    var registryAdminSturdyRef = restorer.SaveStr(BareProxy.FromImpl(registryAdmin)).Item1;
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
}