<p align="center">
  <img src="Sources/Yohuke.DanmuCore/Assets/favicon.png" width="112" alt="Yohuke Danmu Core icon" />
</p>

<h1 align="center">Yohuke Danmu Core</h1>

<p align="center">The shared Bilibili foundation for Danmu Neo and Danmu Panel</p>

<p align="center">
  <a href="#build-and-test">Build &amp; Test</a> ·
  <a href="#consume-from-the-applications">Integration</a> ·
  <a href="#license">License</a>
</p>

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
