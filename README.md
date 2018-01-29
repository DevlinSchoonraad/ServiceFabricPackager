# ServiceFabricPackager

## TLDR
A tool that manages complex Service Fabric application packaging process.

## Description
Service Fabric Packager is a tool that helps you with more advanced SF Packaging scenarions. Current list of features includes:
* Auto-discovers SF Application projects in a folder
* Tracks changes in a package (code/config/data) for each of the Service Fabric Services found.
* Does partial packaging and updates all the manifests with correct versions
* Keeps an external history of what packages changed (Azure Blob)
* Supports external configuration of manifests (Azure Blob).

## How to build
Clone repo
```
cd src\SFPackager
dotnet restore
dotnet build -c Debug
dotnet publish -c Debug
```
You'll then end up with the compiled code in ```bin\CONFIGURATION\netcoreapp1.0\PLATFORM\publish```

## How to use
To create a local package, first build your solution.
Then run ```sfpackager.exe -l -p "c:\path\to\configfolder" -f "configfile.name" -s "c:\path\to\SF\solution.sln" -b "Debug\Release" -i "custom-version-tag"```

The config folder is a folder on your local drive that the packager will store the version map in and also read the config file from.
The ```configfile.name``` is the config file for the packager. It should be in the configfolder. An example will be provided below.
The ```custom-version-tag``` is a string that will be added behind the rolling version number. We use the current git commit hash.

## Command line switches
| switch  | fullswitch | description | required |
| ------- | ---------- | ----------- | -------- |
| -a      | --azureStorage | Use Azure storage as config folder | Mutually exclusive with -l |
| -n      | --storageAccountName | Storage account name | Required with -a |
| -k      | --storageAccountKey | Storage account secret | Required with -a |
| -c      | --storageAccountContainer | Azure Storage container to use | Required with -a |
| -l      | --localStorage | Use a local folder as config foler | Mutually exclusive with -a |
| -p      | --localStoragePath | The path to the config folder | Required with -l |
| -f      | --configFileName | The filename of the config file | Required |
| -s      | --solutionFile | The VS Solution fullpath that contains the SF App(s) | Required |
| -b      | --buildConfiguration | What build config to build. Defaults to Release | |
| -i      | --versionIdentifier | The version number/tag/string for this relelase | Required |
| -e      | --secureCluster | If connecting to a secure cluster | |
| -x      | --forcePackage | Forces the packager to ignore whats currently deployed, and just packages everything | |
| -d      | --cleanOutput | Clean the output folder before packaging | |
| -o      | --packageOutput | The output folder to put the packages in. Defaults to SOURCEPATH/sfpackaging | |
| -v      | --verbose | More verbose output during packaging | |
| -t      | --extraArgs | Extra args to pass through to dotnet publish | |

## Config file example
```
{
  "Cluster": {
    "Endpoint": "localhost",
    "Port": 19080,
    "PfxFile": "file.pfx",
    "PfxKey": "mysupersecretpfxkey"
  },
  "HashIncludeExtensions": [
    ".dll",
    ".exe",
    ".config"
  ],
  "HashSpecificExcludes": [
    "nondeterministic.dll"
  ],
  "GuestExecutables": [
    {
      "PackageName": "MyGuestPackage",
      "ApplicationTypeName": "MyGuestType",
      "GuestRunAs": {
        "UserName": "MyUser",
        "AccountType": "LocalSystem"
      }
    }
  ],
  "ExternalIncludes": [
    {
      "ApplicationTypeName": "AppTypeName",
      "ServiceManifestName": "ServiceName",
      "PackageName": "Config",
      "SourceFileName": "myconfig.json",
      "TargetFileName": "config.json",
      "Source": "notused"
    }
  ],
  "Enchipherment": [
    {
      "ApplicationTypeName": "MyApp",
      "Name": "MyService",
      "CertName": "MyEnchipherCert",
      "CertThumbprint": "CA11FE..."
    }
  ],
  "Https": [
    {
      "ApplicationTypeName": "ApiType",
      "ServiceManifestName": "ServiceName",
      "EndpointName": "ServiceEndpointHttps",
      "CertThumbprint": "10FEA0..."
    }
  ],
  "Endpoints": [
    {
      "ApplicationTypeName": "ApiType",
      "ServiceManifestName": "ServiceName",
      "EndpointName": "ServiceEndpointHttp",
      "Port": 80,
      "Protocol": "http",
      "Type": "Input"
    },
    {
      "ApplicationTypeName": "ApiType",
      "ServiceManifestName": "ServiceName",
      "EndpointName": "ServiceEndpointHttps",
      "Port": 443,
      "Protocol": "https",
      "Type": "Input"
    }
  ]
}
```

## Description of the elements in config.json
### Cluster
Basic information about the cluster you are pulling deployed apps data from. `PfxFile` and `PfxKey` is only needed if connecting to a secured cluster.

### HashIncludeExtensions
What extensions to include when computing the hash of your code packages

### HashSpecificExcludes
Specific file(s) you want excluded during hashing because i.e. they are not deterministic. An example would be resource files.

### GuestExecutables
For defining guest executables. Should be pretty self-explanatory based on the example above.

### ExternalIncludes
Files that are kept out of repo but should be copied in during packaging. These will be hased and included in the version docuemnt along side all other files

### Enchipherment
Used for defining enchipherment certificate. This will then be added to the correct ApplicationManifest during packaing. This requires the certificates to already be deployed on the nodes (for example through VMSS ARM).

### Https
Defines the HTTPS certificates. This requires the certificates to already be deployed on the nodes (for example through VMSS ARM).

### Endpoints
Defines endpoints to be configured in ServiceManifests

## Actual useful information
### Tracking changes
For the change tracking to work properly, all your Service Fabric projects must be built with the deterministic flag enabled in Roslyn (see https://blogs.msdn.microsoft.com/dotnet/2016/04/02/whats-new-for-c-and-vb-in-visual-studio/)
When the packager is run, it will compute a hash of all files in each package folder (Data, Config & Code). It will use these hashes to determine if a package has changed.

### Packaging and version updates
Service Fabric packager will connect to your cluster and get the versions of the currently deployed applications (this is a bit simplistic today).
Based on what it finds, it will then select the next version number.
When it starts packaging, it will load the version map of the currently running versions. Based on that, it'll compare the local package hashes against the
version map for the current version and determine what to package.
The version map also contains the current version of all the things, so it knows what version to assign to the different Applications, Services and Packages.
