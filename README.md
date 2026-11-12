# Yohuke Danmu Core

Shared Bilibili HTTP APIs, login credentials, live protocols, account sessions, room search and history reconciliation for Danmu Panel and Danmu Neo.

The `net10.0` library has no UI, MVVM, localization, persistence or application-directory dependencies. Structured errors retain their original exception types; each application renders `CoreErrorInfo` in its current language. Generated protobuf types and parsing internals are kept private to the library and its test project.

## Build and test

```sh
dotnet build Sources/Yohuke.DanmuCore.sln
dotnet test Sources/Yohuke.DanmuCore.Tests/Yohuke.DanmuCore.Tests.csproj
```

## Consume from the applications

Both applications reference this repository through their `Core` Git submodule. Initialize it with `git submodule update --init --recursive`, then build the application solution. Runtime policies such as gift currency filtering, display colors, UI state and on-disk records belong to each application.

## License

See [LICENSE](LICENSE).
