# Building SemPtr

Building SemPtr on your own is very straightforward and simple.\
You'll just need git to clone the repository, a .NET 10 SDK or later and `dotnet` CLI, and optionally, if you want to build the documentation, DocFx installed on your system.

## Getting the source

### Requirements

- Git 1.7.5 or later

### Cloning the repository and initializing submodules

This repository uses submodules. At the moment the only submodules used by this repository are the ones required for building the documentation. So if you don't want to build the documentation, you can skip initializing and updating the submodules.

#### Clone with submodules

To clone the repository with submodules, use the following command:

```shell
git clone --recurse-submodules [<repository-url>](https://github.com/fruediger/SemPtr.git)
```

#### Initialize submodules later

If you cloned the repository without initializing the submodules, you can do also do that later (e.g., if you decided to build the documentation later) using the following commands:

```shell
cd SemPtr
git submodule update --init --recursive
```

## Building the project from source

### Requirements

- .NET 10 SDK or later
- `dotnet` CLI (comes with the .NET SDK most of the time)

### Building the solution

> [!NOTE]
>
> You should first make sure you are in the correct directory, which is the root of the cloned repository. If you just cloned the repository, you can navigate into it using:
>
> ```shell
> cd SemPtr
> ```
>
> All the following commands should be run from the root of the cloned repository, except where stated otherwise.

Building the solution is very straightforward. You can just run a simple command:

```shell
dotnet build ./src
```

Optionally you can pass an additional `-c <configuration>` argument where `<configuration>` is either `Debug` or `Release` to specify the build configuration.

### Testing the solution

Optionally, if you want to run the tests for the solution, you can easily do that by using the following command:

```shell
dotnet test ./src
```

You can also specify the build configuration to test by using an additional `-c <configuration>` argument, just like when [building the solution](#building-the-solution).

### Packing a NuGet package

If you want, you can create a NuGet package from the project using the following command:

```shell
dotnet pack ./src/SemPtr -c Release
```

> [!NOTE]
> The difference in command structure between packing and building/testing the solution.
>
> When packing, you should specify the main project directory (i.e., `./src/SemPtr`) directly instead of the solution directory. Otherwise, you'll end up with a bunch of separate NuGet packages for each satellite project in the solution.
>
> Also, while packing a NuGet package still supports specifying different build configurations, it is absolutely recommended to pack in the `Release` configuration, hence the `-c Release` argument in the command above.

You can also generate a symbols package for the NuGet package by adding `-p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg` to the command above.
This will create a `.snupkg` symbol package file alongside the `.nupkg` package file.

## Building the documentation from source

### Requirements

- DocFx 2.78.6-preview.49 or later (early versions might work, but 2.78.6-preview.49 is confirmed to be working; versions 2.78.5 and earlier are known to be not able to build the documentation)
- Git submodules initialized for your local clone of the repository (see [Cloning the repository and initializing submodules](#cloning-the-repository-and-initializing-submodules))

### Setting up DocFx

Building the documentation is a bit more involved than building the project itself.\
At the time of writing this, release versions of DocFx (2.78.5 and earlier) are not able to build the documentation for SemPtr due to them shipping with an integrated version of the Roslyn compiler that is too old to build some of the satellite projects in the solution.

That's why, at moment, if you want to build the documentation, you are required to use a preview version of the DocFx tool (2.78.6-preview.49 or later).

#### Adding dotnet/docfx as a NuGet package source

Preview versions of DocFx are not hosted on [nuget.org](https://www.nuget.org/packages/docfx#versions-body-tab), rather they are hosted as GitHub Packages in the [dotnet/docfx](https://github.com/dotnet/docfx) repository.
Since GitHub Packages is not a NuGet feed that is automatically added to your system, you will need to add it manually.

Please refer to [GitHub: Working with the NuGet registry](https://docs.github.com/en/packages/working-with-a-github-packages-registry/working-with-the-nuget-registry) on how to do that.\
The URL to use for the NuGet feed is `https://nuget.pkg.github.com/dotnet/index.json` and you can name it something like `github-dotnet`.

In short, you can add the feed using the following command:

```shell
dotnet nuget add source --name github-dotnet --username <username> --password <token> --store-password-in-clear-text https://nuget.pkg.github.com/dotnet/index.json
```

where `<username>` is your GitHub username and `<token>` is a personal access token (PAT) with at least the `read:packages` scope.

#### Installing DocFx

Once you have added the GitHub Packages NuGet feed, you can install the required preview version of DocFx using the following command:

```shell
dotnet tool install --global docfx --version 2.78.6-preview.49 --add-source https://nuget.pkg.github.com/dotnet/index.json
```

> [!NOTE]
> Note that the command above installs DocFx as a global tool and the commands below assume that you have installed DocFx as such.
> If you want to install DocFx as a local tool instead, you'll need to adjust the commands below accordingly.

#### Building the documentation

If everything is set up correctly, make sure you are in the `docs` directory from the root of the cloned repository. If not, you can navigate into it using:

```shell
cd docs
```

Then you can build the documentation using two simple commands.

First, to build the API documentation, run:
```shell
docfx metadata
```

Then, to build the rest of the documentation, run:
```shell
docfx build
```

And you should now have the documentation built in the `_site` subdirectory.

#### Serving the documentation

Alternatively, if you like to, you can also serve the documentation with a local web server using the following command:
```shell
docfx ./docfx.json --serve
```

You can then open your web browser and navigate to <http://localhost:8080> to view the documentation.

## Issue building the project or documentation

If you encounter any issues while building the project or the documentation, please feel free to open up an issue in the repository under [Issues](https://github.com/fruediger/SemPtr/issues), after checking first if there are already any existing issues that are similar to yours.
