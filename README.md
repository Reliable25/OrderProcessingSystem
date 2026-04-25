# OrderProcessing

A resilient order processing backend built with ASP.NET Core (.NET 8) and EF Core.  
Implements transactional order placement with pessimistic locking to avoid overselling, idempotency protection, in-process event handling (payment, inventory confirmation, notification), structured logging (Serilog), background cleanup for idempotency records, unit and integration tests.

---

## Contents

- `src/OrderProcessing.API` — ASP.NET Core Web API
- `src/OrderProcessing.Application` — Application layer (commands, handlers, DTOs)
- `src/OrderProcessing.Domain` — Domain entities, events, interfaces
- `src/OrderProcessing.Infrastructure` — EF Core, repositories, services, hosted jobs
- `tests/OrderProcessing.Tests.Unit` — Unit tests
- `tests/OrderProcessing.Tests.Integration` — Integration tests (uses in-process host + configured DB)
- `docker-compose.yml` — Optional local Docker stack (SQL Server + API)
- `scripts/init_db.sql` — SQL Server bootstrap script (optional)

---

## Quick overview

Key features:
- Place order endpoint (`POST /api/orders`) supports multi-line items and idempotency keys.
- Prevents overselling by using:
  - Serializable transactions (UnitOfWork.ExecuteInTransactionAsync)
  - Provider-specific row locks (SQL Server `WITH (UPDLOCK, ROWLOCK)`, PostgreSQL `FOR UPDATE`)
- Idempotency:
  - `IdempotencyRecord` persisted with unique `Key`.
  - Stores `RequestHash`, `ResourceId` (OrderId), `ResponsePayload` and `Status`.
  - Policy: same key + identical payload returns the original resource; same key + different payload returns 409 Conflict.
- Event-driven in-process pipeline:
  - `OrderPlacedEventHandler` simulates payment -> inventory confirmation -> notification.
- Structured logging with Serilog to console and rolling files.
- Background cleanup service removes old idempotency records (TTL configurable).
- Deterministic seeding in dev: two products are seeded on first run with known GUIDs for testing:
  - `11111111-1111-1111-1111-111111111111` (Widget A)
  - `22222222-2222-2222-2222-222222222222` (Widget B)

---

## Prerequisites

- .NET 8 SDK
- SQL Server (LocalDB, SQL Server Express, or container)
- Docker (optional; only required if you want to run the docker-compose stack)
- Optional: `dotnet-ef` for creating migrations

---

## Setup (local development)

1. Clone repository
   - git clone <repo-url>

2. Restore & build
   - dotnet restore
   - dotnet build

3. Configure connection string
   - Default location: `src/OrderProcessing.API/appsettings.Development.json`
   - Key: `ConnectionStrings:DefaultConnection`
   - Example LocalDB:
     - `Server=(localdb)\mssqllocaldb;Database=OrderProcessingDb;Trusted_Connection=True;MultipleActiveResultSets=true`
   - Example SQL Server container:
     - `Server=localhost,1433;Database=OrderProcessingDb;User Id=sa;Password=Your_password123;TrustServerCertificate=True;`

4. Run the API
   - dotnet run --project src/OrderProcessing.API
   - On first run the app calls `EnsureCreated()` and seeds two deterministic products if none exist. Logs include seeded product IDs.

5. Swagger UI
   - When running in Development, visit `https://localhost:{port}/swagger` to inspect endpoints.

---

## Running in Docker (optional)

1. Start stack:
   - docker-compose -f src/OrderProcessing.API/docker-compose.yml up --build
2. Wait for SQL Server to be healthy and API to seed (check container logs).
3. API will use connection string from `docker-compose.yml` environment.

---

## Database / Migrations

- Startup behavior: the application applies EF Core migrations at startup using `Database.Migrate()` so the schema is kept in sync with committed migrations.
- Development workflow:
  1. Install EF tooling (if needed):
     - `dotnet tool install --global dotnet-ef`
  2. Create a migration after model changes:
     - `dotnet ef migrations add <MigrationName> -p src/OrderProcessing.Infrastructure -s src/OrderProcessing.API`
  3. Apply migrations to the database (local manual apply if desired):
     - `dotnet ef database update -p src/OrderProcessing.Infrastructure -s src/OrderProcessing.API`
- Important:
  - Commit generated migration C# files to source control. `Database.Migrate()` at startup applies committed migrations automatically (recommended for production).
  - Avoid relying on `EnsureCreated()` for schema evolution—use migrations to maintain history and compatibility.

---

## Tests

Unit tests:
- Location: `tests/OrderProcessing.Tests.Unit`
- Run: dotnet test tests/OrderProcessing.Tests.Unit

Integration tests:
- Location: `tests/OrderProcessing.Tests.Integration`
- These tests start an in-process test host (`WebApplicationFactory<Program>`) and use the connection string from your API appsettings (Development config) by default.
- Ensure `ConnectionStrings:DefaultConnection` points to a reachable test database before running.
- Run: dotnet test tests/OrderProcessing.Tests.Integration

Notes:
- Integration tests rely on the deterministic seed product ID `11111111-1111-1111-1111-111111111111`. Use that ID when composing test requests, or query `/api/products` to see seeded values.

---

## Controller actions (summary)

- `POST /api/orders` — Place new order
  - Request: `PlaceOrderRequest` (customer info, items, optional `IdempotencyKey`)
  - Behavior:
    - Validates payload via FluentValidation
    - If `IdempotencyKey` present: checks / creates `IdempotencyRecord` to enforce idempotency policy
    - Locks product rows, reserves stock inside a serializable transaction
    - Creates order aggregate and confirms it (raises OrderPlacedEvent)
    - Publishes event to `OrderPlacedEventHandler` which simulates payment, confirms inventory, sends notification
  - Responses:
    - 201 Created with `OrderResponse` on success
    - 404 Not Found if product not found
    - 409 Conflict on insufficient stock or idempotency key misuse
    - 400 Bad Request for validation failures

- `GET /api/orders/{id}` — Get order by id

- `GET /api/products` — List active products (includes stock quantity)
- `GET /api/products/{id}` — Get single product

---

## Architecture decisions & rationale

- Clean architecture: domain entities own invariants (e.g., `Product.TryReserveStock`, `Order.Confirm()`); repositories and UnitOfWork handle persistence and transactions.
- EF Core chosen for rapid development, LINQ, and transactional support.
- Concurrency strategy:
  - Transactional approach with serializable isolation level + pessimistic locking (provider-specific hints) ensures no oversells under concurrent requests.
  - Tradeoff: conservative approach reduces concurrency and may affect throughput. If high throughput required, consider optimistic concurrency with retries or a reservation service/queue.
- Idempotency:
  - Implemented as a persisted record with uniqueness constraint on `Key`.
  - Uses request fingerprint (SHA-256) to detect payload mismatch and prevent silent inconsistencies.
  - Reason: explicit behavior is safer and easy to reason about for clients.
- Event flow:
  - Kept in-process for simplicity (good for demo and tests). For production, replace with a durable broker (RabbitMQ / Azure Service Bus / Kafka) for durability and decoupling.
- Logging:
  - Serilog for structured logs, console + rolling files.
  - Logs include seeded IDs and important lifecycle events (order placement, payment simulation, notifications).

---

## Trade-offs and limitations

- Serializable isolation + row locks provides correctness but reduces concurrency and may increase contention in high-load situations.
- In-process event handling reduces complexity but lacks durability; a crash between commit and event publishing could lose downstream processing. To harden, publish domain events to a durable outbox / message broker within the same transaction (Outbox pattern).
- `EnsureCreated()` is convenient for dev but not suitable for production schema evolution. Use EF migrations for production.
- Idempotency table grows over time; background cleanup service prunes older records (configurable TTL) but consider archiving for audit needs.
- Integration tests run against configured DB; CI should provision an isolated test database or use Testcontainers for deterministic environments.

---

## Operational considerations

- Monitor:
  - Order success/failure rates
  - Payment retry/failure metrics
  - Latency of order placement and inventory confirmation
- Retries:
  - External calls (payments) should use resilient retry with exponential backoff (Polly)
- Observability:
  - Add correlation IDs for requests and enrich logs for traceability
  - Consider OpenTelemetry for distributed tracing in multi-service setups

---

## How to extend

- Replace in-process `OrderPlacedEventHandler` with a message broker consumer/producer and implement Outbox pattern for safe event publishing.
- Implement optimistic concurrency with rowversion for products, including retry logic for competing reservations.
- Add API versioning and authentication/authorization (JWT).
- Add metrics and health checks.

---

## Troubleshooting

- "Product not found" when posting an order:
  - Ensure the API used the expected connection string (check Program.cs startup logs).
  - Query `GET /api/products` to list seeded products and their IDs.
  - Confirm you are using a seeded product ID or have created products in the configured DB.

- Integration tests failing:
  - Ensure `ConnectionStrings:DefaultConnection` in `src/OrderProcessing.API/appsettings.Development.json` points to a running SQL Server instance accessible by the test environment.
  - Increase the seeding wait timeout if your DB startup is slow.
