# MarketAgent Dashboard

`MarketAgent.Web` is a minimal React + TypeScript dashboard for the existing MarketAgent API.

## Backend

Run the API from the repository root:

```powershell
dotnet run --project src/MarketAgent.Api
```

The default HTTP launch URL is:

```text
http://localhost:5215
```

The `https` launch profile also exposes:

```text
https://localhost:7085
```

The API enables CORS for the local Vite frontend in Development:

```text
http://localhost:5173
https://localhost:5173
```

## Frontend

Install Node.js 20 or newer, then run:

```powershell
cd MarketAgent.Web
npm install
npm run dev
```

Open:

```text
http://localhost:5173
```

## Configuration

Set the API base URL with:

```powershell
$env:VITE_MARKETAGENT_API_BASE_URL="https://localhost:7085"
```

If the variable is not set, the dashboard falls back to:

```text
http://localhost:5215
```

## Dashboard Actions

- `Run Ingestion` calls `POST /api/ingestion/run`.
- `Run Signals` calls `POST /api/signals/run`.
- `Generate Briefing` calls `POST /api/briefing/run`.
- `Refresh Dashboard` tries `POST /api/briefing/run`, then `POST /api/signals/run`, then a mock preview if the API is unavailable.

The mock preview is only used when the API cannot be reached.
