# CK-ControlChannel TCP implementation

TCP-based implementations of [CK.ControlChannel server and client](https://github.com/invenietis/CK-ControlChannel-Abstractions).

## Build requirements

- Windows
- Powershell
- [.NET Core SDK 2.0](https://www.microsoft.com/net/download/core) (with .NET Core 1.1)
- [Visual Studio 2017](https://www.visualstudio.com/) (any edition) with .NET framework build tools

## Build instructions

1. Clone the repository
2. In Powershell, run `CodeCakeBuilder/Bootstrap.ps1`
3. Run `CodeCakeBuilder/bin/Release/CodeCakeBuilder.exe`

## NuGet packages

| Feed             | Server | Client | Common |
| ---------------- | ------ | ------ | ------ |
| **NuGet stable** | (TBD)  | (TBD)  | (TBD)  |
| NuGet prerelease | (TBD)  | (TBD)  | (TBD)  |
| MyGet preview    | [![MyGet Badge](https://buildstats.info/myget/zoopin-preview/CK.ControlChannel.Tcp.Server)](https://www.myget.org/feed/zoopin-preview/package/nuget/CK.ControlChannel.Tcp.Server) | [![MyGet Badge](https://buildstats.info/myget/zoopin-preview/CK.ControlChannel.Tcp.Client)](https://www.myget.org/feed/zoopin-preview/package/nuget/CK.ControlChannel.Tcp.Client) | [![MyGet Badge](https://buildstats.info/myget/zoopin-preview/CK.ControlChannel.Tcp.Common)](https://www.myget.org/feed/zoopin-preview/package/nuget/CK.ControlChannel.Tcp.Common) |
| MyGet CI         | [![MyGet Badge](https://buildstats.info/myget/zoopin-ci/CK.ControlChannel.Tcp.Server)](https://www.myget.org/feed/zoopin-preview/package/nuget/CK.ControlChannel.Tcp.Server) | [![MyGet Badge](https://buildstats.info/myget/zoopin-ci/CK.ControlChannel.Tcp.Client)](https://www.myget.org/feed/zoopin-preview/package/nuget/CK.ControlChannel.Tcp.Client) | [![MyGet Badge](https://buildstats.info/myget/zoopin-ci/CK.ControlChannel.Tcp.Server)](https://www.myget.org/feed/zoopin-preview/package/nuget/CK.ControlChannel.Tcp.Common) |

## Build status

| Branch   | Visual Studio 2017 |
| -------- | ------- |
| latest | [![Latest build](https://ci.appveyor.com/api/projects/status/uqboxbedxgmehc9k?svg=true)](https://ci.appveyor.com/project/ZooPin/ck-controlchannel-tcp) |
| `develop`  | [![Latest develop build](https://ci.appveyor.com/api/projects/status/uqboxbedxgmehc9k/branch/develop?svg=true)](https://ci.appveyor.com/project/ZooPin/ck-controlchannel-tcp/branch/develop) |

## Contributing

Anyone and everyone is welcome to contribute. Please take a moment to
review the [guidelines for contributing](CONTRIBUTING.md).

## License

Assets in this repository are licensed with the MIT License. For more information, please see [LICENSE.md](LICENSE.md).

## Open-source licenses

This repository and its components use the following open-source projects:

- [invenietis/CK-ControlChannel-Abstractions](https://github.com/invenietis/CK-ControlChannel-Abstractions), licensed under the [MIT License](https://github.com/invenietis/CK-ControlChannel-Abstractions/blob/master/LICENSE.md)
- [invenietis/CK-Core](https://github.com/invenietis/CK-Core), licensed under the [GNU Lesser General Public License v3.0](https://github.com/invenietis/CK-Core/blob/master/LICENSE)
- [invenietis/CK-Text](https://github.com/invenietis/CK-Text), licensed under the [MIT License](https://github.com/invenietis/CK-Text/blob/master/LICENSE)
- [invenietis/CK-ActivityMonitor](https://github.com/invenietis/CK-ActivityMonitor), licensed under the [GNU Lesser General Public License v3.0](https://github.com/invenietis/CK-ActivityMonitor/blob/master/LICENSE)
