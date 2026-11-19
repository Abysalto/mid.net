# AbySalto.Mid.Infrastructure

Implements Application's interfaces: EF Core persistence (PostgreSQL via Npgsql), the DummyJSON client, password hashing and JWT signing. Wired up in `DependencyInjection.cs` (`AddInfrastructure`).

## Persistence

- `AppDbContext` is `internal`, exposed as `IAppDbContext`. Mapping lives in `Persistence/Configurations/*` (no attributes on entities).
- **Migrations** (`Persistence/Migrations`): `InitialCreate` (schema), `SeedInitialData` (hand-written demo data, fixed ids). Generate with `dotnet ef migrations add <Name> -p AbySalto.Mid.Infrastructure -s AbySalto.Mid -o Persistence/Migrations`, then **read the generated file** before keeping it.
- Check `dotnet ef migrations has-pending-model-changes` after mapping changes.
- `ef migrations remove` needs a live DB; without one, delete the files and restore the snapshot by hand.
- `ProductCategory` is stored as text (`HasConversion<string>()`); a new non-null column needs a valid default (`"Unknown"`), not `""`.
- `DatabaseInitializer` migrates on startup when `Database:MigrateOnStartup` is true (Development); it retries transient `NpgsqlException`s while Postgres boots.
- The basket's concurrency token is Postgres's system column `xmin` (`Property<uint>("Version").IsRowVersion()`); unique indexes guard races (email, basket per user, line per product).
- Raw SQL in configs/migrations uses Postgres quoting (`"Quantity"`), not `[Quantity]`. Store UTC only: `timestamp with time zone` rejects non-UTC `DateTime`.
- Seeding explicit ids into identity columns: Npgsql emits the `setval` that moves the sequence on; check it in `dotnet ef migrations script`.
- After `migrations add`, rebuild before the next `migrations add --no-build`, or the second one diffs against a stale model and duplicates the schema.

## DummyJSON client (`ExternalApis/DummyJson`)

- Typed HttpClient + `AddStandardResilienceHandler`; responses cached by request URL with `HybridCache`: L1 memory (`LocalCacheSeconds`) + L2 Postgres table `CacheEntries` (`CacheMinutes`) via our own `Caching/PostgresDistributedCache` (raw Npgsql, not EF), cleaned by `CacheCleanupService`. Not-found is cached too, as `ProductLookup(null)`. Keys are `dummyjson:v1:<url>`: bump `v1` when a cached type's shape changes. Queries are normalized in `ProductService` (trim, canonical `sortBy`, lower-case `order`) and the search is lower-cased in the URL, so equivalent queries share one key.
- HybridCache silently ignores `MemoryDistributedCache` as L2: tests that need a shared L2 must use another `IDistributedCache` (see `DummyJsonCachingTests`).
- Cache values are serialized with System.Text.Json: cached types (`UpstreamPage`, `ProductLookup`, `ProductDto`) must round-trip (public constructor/properties).
- 404 → `null`; transport/timeout/5xx → `ProductCatalogUnavailableException` (503), logged as a warning.
- `select=` limits fields to what `ProductDto` needs.
- Search + category: DummyJSON's search for all hits (`limit=0`), then `InMemoryProductQuery.FilterByCategory` filters by the enum, sorts and pages.
- An unmapped category slug becomes `ProductCategory.Unknown` and logs a warning: regenerate the enum.

## Auth

- `Pbkdf2PasswordHasher`: PBKDF2-HMAC-SHA512, format `v1.{iterations}.{salt}.{key}`, constant-time compare. Changing the format means regenerating the seed hashes.
- `JwtTokenGenerator` + `JwtValidation` build from the same `JwtOptions`, so signing and validation can't drift. `sub` is the user's Guid.
- Keep all `Microsoft.IdentityModel.*` packages on one version.
