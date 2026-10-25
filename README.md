# request-tracker-server

The .NET API of Request Tracker. Clone the main repository instead, which includes this one as its `backend/` submodule: https://github.com/TzivyaSternbuch/request-tracker

## Run locally

Prerequisites: .NET 10 SDK, and Docker Desktop running with about 2 GB of memory for SQL Server.

Run these commands from this folder (`backend/` in the main repository):

1. Start SQL Server 2022:

   ```bash
   docker compose up -d
   ```

   Wait until `docker compose ps` shows the `sqlserver` service as `healthy`. The container uses port 1433; if a
   local SQL Server already listens there, stop it first, or the container can't start.

2. Restore the local tools (`dotnet-ef`):

   ```bash
   dotnet tool restore
   ```

3. Run the API:

   ```bash
   dotnet run --project src/RequestTracker.Api
   ```

   Open Swagger UI at http://localhost:5100/swagger. The OpenAPI document is at http://localhost:5100/openapi/v1.json.

   On the first start the database is empty, so the API creates the test data: 100,000 requests, in about 15 seconds.
   Later starts keep the existing data.

4. Run the tests. Docker must be running: the integration tests start their own SQL Server container.

   ```bash
   dotnet test
   ```

To reset the test data at any time, stop the API and run the command below. It deletes and recreates the database with
the same 100,000 requests, then exits:

```bash
dotnet run --project src/RequestTracker.Api -- seed
```

The database schema is applied when the API starts in Development. The SQL Server password in
`compose.yaml` and `appsettings.Development.json` is for local development only.
