# TIA Portal Openness Assembly Resolver

## Assembly Resolver
The assembly resolver must be registered before any access to Siemens.Engineering classes is done, e.g. on startup of the app.
``` csharp
static void Main(string[] args)
{
    Api.Global.Openness().Initialize();
    RunTiaPortal();
}

private static void RunTiaPortal()
{
    var tiaPortal = new TiaPortal(TiaPortalMode.WithUserInterface);
}

```
The resolver will

- check the registry for TIA Portal installations
- running processes
- environment variable 'TiaPortalLocation'

It will collect all Siemens.Engineering assembly found and automatically resolve the correct assembly. E.g. the app was build using TIA Openness V16, at runtime TIA Portal V17 is found. The resolver will automatically resolve the V16 assembly from the V17 installation. This behaviour can be changed by providing an assembly selector:
``` csharp
Api.Global.Openness().Initialize(engineeringAssemblySelector: assemblies => assemblies.First());
```
> In this example `assemblies` is the list of all found Siemens.Engineering assemblies ordered by version. In this case the latest version is selected.
or a fixed TIA Portal Version
``` csharp
Api.Global.Openness().Initialize(tiaMajorVersion: 16);
```

## TIA Portal processes
When executing `TiaPortal.GetProcesses()` only the processes of the same version are found. This means, if the app is supporting multiple versions, only the TIA Portal processes of the resolved assembly will be returned. When calling `Process.GetProcessesByName("Siemens.Automation.Portal.exe")` besides TIA Portal processes also PLCSIM and other processes are returned.
To get all TIA Portal processes use `GetTiaPortalProcesses` instead.
``` csharp
var tiaPortalProcesses = Api.Global.Openness().GetTiaPortalProcesses();
```
This will find all processes and load the Siemenes.Engineering assembly in a temporary app domain. Within this app domain the `TiaPortal.GetProcesses()` method will be called and the found processes are returned. After that, the app domain is unloaded.
Afterwards the assembly resolver can be hooked to the selected TIA Portal process and attach to the instance
``` csharp
Api.Global.Openness().Initialize(process);
```

## Openness user group
In order to be able to connect to the TIA Portal Openness API the user needs to be a member of the windows group  `Siemens TIA Openness`.  

Check if user is in the group
 ```csharp
if (Api.Global.Openness().IsUserInGroup())
    // user is in group
else
    // not in group
 ```

Add the user to the group
 ```csharp
if (await Api.Global.Openness().AddUserToGroupAsync())
    // user was added
else
    // user not added
 ```
> When executing `Api.Global.Openness().AddUserToGroupAsync()` a new process will be opened and admin privileges will be requested. If admin privileges are granted, the user will be added, otherwise false. For this change to take effect the user needs to re-login.