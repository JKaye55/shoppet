# Shoppet (API + MAUI app)

This repository contains:
- `ShoppetAPI` - ASP.NET Core Web API (`net10.0`)
- `ShoppetApp` - .NET MAUI mobile app (`net10.0-*`)

## Prerequisites

- .NET SDK 10.0+
- MySQL 8+ (or compatible)
- MAUI workloads for local mobile builds:
  - `dotnet workload install maui-android`
  - `dotnet workload install maui-ios maui-maccatalyst` (macOS only)
  - Windows target requires Windows + MAUI workloads

## API configuration

The API reads the DB connection from:
1. `ConnectionStrings__DefaultConnection` environment variable, or
2. `SHOPPET_DB_CONNECTION` environment variable, or
3. `ConnectionStrings:DefaultConnection` in `appsettings*.json`

Example:

```bash
export SHOPPET_DB_CONNECTION="Server=127.0.0.1;Port=3306;Database=shoppetdb;User Id=YOUR_USER;******;"
```

### Run API

```bash
dotnet restore /home/runner/work/shoppet/shoppet/ShoppetAPI/ShoppetAPI.csproj
dotnet run --project /home/runner/work/shoppet/shoppet/ShoppetAPI/ShoppetAPI.csproj
```

Default local HTTP URL is `http://localhost:5020`.

### API health endpoints

- Liveness: `GET /health/live`
- Readiness (DB probe): `GET /health/ready`

## MAUI app configuration

`ShoppetApp/appsettings.json` provides platform-specific API base URL defaults:
- Android emulator: `http://10.0.2.2:5020`
- iOS simulator / Windows / Mac Catalyst: `http://localhost:5020`

Override at runtime with environment variables (double underscore for nesting), for example:

```bash
export SHOPPET_ShoppetApi__BaseUrl="http://192.168.1.100:5020"
# or simpler:
export SHOPPET_API_BASE_URL="http://192.168.1.100:5020"
```

Optional legacy local MySQL setting in app config:
- `ShoppetData:LegacyMySqlConnection` (empty by default; community flows use API paths)

## Build and test commands

### API

```bash
dotnet build /home/runner/work/shoppet/shoppet/ShoppetAPI/ShoppetAPI.csproj
dotnet test /home/runner/work/shoppet/shoppet/ShoppetAPI.Tests/ShoppetAPI.Tests.csproj
```

### MAUI (example Android)

```bash
dotnet workload restore /home/runner/work/shoppet/shoppet/ShoppetApp/ShoppetApp.csproj
dotnet build /home/runner/work/shoppet/shoppet/ShoppetApp/ShoppetApp.csproj -f net10.0-android
```

If the runner/machine does not have MAUI workloads installed, MAUI build will fail with `NETSDK1147` until workloads are installed.

## CI

GitHub Actions workflow at `.github/workflows/ci.yml`:
- Restores/builds/tests API + API smoke tests on Ubuntu
- Installs `maui-android` and builds MAUI Android on Windows
