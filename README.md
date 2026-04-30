# ExcelDash (ASP.NET Core + MySQL)

Simple Razor Pages app that:

- Accepts an uploaded `.xlsx`
- Stores the parsed sheet in MySQL
- Renders a minimal dashboard (basic chart + preview)

## Local run (MySQL via Docker)

1) Start MySQL:

```bash
cd excel-dashboard
docker compose up -d
```

2) Run the app:

```bash
cd excel-dashboard/ExcelDash
dotnet run
```

The default dev connection string lives in `excel-dashboard/ExcelDash/appsettings.Development.json`.

## Configuration

Set the connection string using either:

- `ConnectionStrings__Default` (recommended for containers)
- `ConnectionStrings:Default` in `appsettings*.json`

Example:

```bash
export ConnectionStrings__Default="Server=localhost;Port=3306;Database=exceldash;User=exceldash;Password=exceldash;TreatTinyAsBoolean=false"
```

## Paketo build (example)

From the repo root:

```bash
pack build exceldash \
  --builder paketobuildpacks/builder-jammy-base \
  --path excel-dashboard/ExcelDash
```

Runtime config (MySQL) should be supplied as env var at deploy time:

```bash
ConnectionStrings__Default=...
```

### .NET version note

This project targets `net8.0` (LTS) to maximize compatibility with Paketo builders.
