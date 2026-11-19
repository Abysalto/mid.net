# AbySalto.Mid: Architecture & Design Decisions

A middleware Web API (.NET 9, Clean Architecture). Products come from the external **DummyJSON** API.
Users (JWT auth), baskets and favorites are stored in **PostgreSQL via EF Core (Npgsql)**. (Originally planned on SQL Server; switched to Postgres, which runs natively everywhere and was already available.)

> Status: design agreed, implementation not started. Open decisions are marked **OPEN**.

---

## 1. Layers & dependency direction

```
        AbySalto.Mid (WebApi)
           │            │
           ▼            ▼
   Mid.Application ◄── Mid.Infrastructure
           │
           ▼
      Mid.Domain        (references nothing)
```

| Project | References | Holds |
|---|---|---|
| `Mid.Domain` | nothing | Entities + business rules, domain exceptions (plain C#, no EF) |
| `Mid.Application` | Domain | Use cases (services), DTOs, validators, **interfaces** for outside needs |
| `Mid.Infrastructure` | Application | `AppDbContext`, EF configurations, migrations, DummyJSON client, JWT, password hashing, caching |
| `Mid` (WebApi) | Application + Infrastructure | Controllers, middleware, Swagger, `Program.cs` |

- **Dependency inversion.** Application defines `IProductClient` and `IAppDbContext`, and Infrastructure implements them. At runtime, calls go *outward* (Controller → Service → Infrastructure). At compile time, references point *inward*.
- **Composition root.** WebApi references Infrastructure **only** so that `Program.cs` can call `AddInfrastructure()` and wire interfaces to implementations. It's also the EF migrations startup project.
- Infrastructure implementations are **`internal`**. Only `DependencyInjection.AddInfrastructure()` is public, so controllers can't use them directly.
- Rule: `using AbySalto.Mid.Infrastructure` inside Application or Domain means the layering is broken.

### Where new business logic goes
| The rule needs… | Put it in | Example |
|---|---|---|
| Only the entity's own data | Domain entity method | max 20 products per basket, merge duplicate lines |
| A pure calculation over several objects | Domain service (no I/O) | 10% off over 100€ |
| Outside data (DB, DummyJSON) | Application service | can't exceed stock, first-order discount |
| A new outside capability | Interface in Application + implementation in Infrastructure | `IEmailSender` → `SmtpEmailSender` |
| New stored data | Domain property + Infra mapping + migration + DTO | `BasketItem.Note` |

> **Domain decides, Application orchestrates, Infrastructure executes, WebApi translates HTTP.**

---

## 2. Models, DTOs & naming

| Kind | Example | Layer |
|---|---|---|
| Entities | `User`, `Basket`, `BasketItem`, `Favorite`, `Order`, `OrderItem` | Domain |
| Table mapping | `BasketItemConfiguration : IEntityTypeConfiguration<BasketItem>` | Infrastructure |
| DTOs (service and API contract) | `BasketDto`, `AddBasketItemRequest`, `ProductDto`, `PagedResult<T>` | Application (per feature `Dtos/`) |
| External API DTOs | `DummyJsonProduct`, `DummyJsonProductsResponse` | Infrastructure (internal) |
| Options | `JwtOptions`, `DummyJsonOptions` | Infrastructure |

- **Entities never cross the service interface and never leave the API.** Exposing them would leak `PasswordHash`, cause serialization cycles (`Basket → Items → Basket`) and tie the API contract to the DB schema.
- Services load the entity, apply domain rules, save, **map to a DTO** and return it. Mapping is hand-written (`BasketMappings.cs`), with no AutoMapper.
- **Naming:** outputs `*Dto`, inputs `*Request`. The exception is `AuthResponse`, because it's a token envelope. The class name appears as the schema name in Swagger and in generated clients, so it is part of the public contract.
- Requests never carry **price** or **user id**. The server decides both (the user id comes from JWT claims).

### Basket models (6 classes)
```csharp
// Domain
public class Basket     { Id, UserId, CreatedAt, UpdatedAt, IReadOnlyCollection<BasketItem> Items }
public class BasketItem { Id, BasketId, ProductId, ProductTitle, Thumbnail, Quantity, UnitPrice, AddedAt }

// Application
public record BasketDto(int Id, IReadOnlyList<BasketItemDto> Items, int TotalItems, decimal TotalPrice);
public record BasketItemDto(int ProductId, string Title, string? Thumbnail, int Quantity, decimal UnitPrice, decimal LineTotal);
public record AddBasketItemRequest(int ProductId, int Quantity);          // POST adds quantity
public record UpdateBasketItemQuantityRequest(int Quantity);              // PUT sets quantity
```

---

## 3. Database

```
                         ┌──────────────────────┐
                         │        Users         │
                         ├──────────────────────┤
                         │ PK Id                │
                         │    Email (unique)    │
                         │    Username          │
                         │    PasswordHash      │
                         │    CreatedAt         │
                         └──┬────────┬───────┬──┘
                       1    │        │ 1     │ 1
          ┌─────────────────┘        │       └──────────────────┐
          │ 1                        │ N                        │ N
┌─────────┴────────────┐  ┌──────────┴───────────┐  ┌ ─ ─ ─ ─ ─ ┴ ─ ─ ─ ─ ─ ┐
│       Baskets        │  │      Favorites       │        Orders (receipt)
├──────────────────────┤  ├──────────────────────┤  ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│ PK Id                │  │ PK,FK UserId         │    PK Id
│ FK UserId (unique)   │  │ PK    ProductId ·····│··    FK UserId
│    CreatedAt         │  │       ProductTitle   │  :    OrderNumber (unique)
│    UpdatedAt         │  │       Thumbnail      │  :    Status
└─────────┬────────────┘  │       CreatedAt      │  :    TotalAmount
          │ 1             └──────────────────────┘  :    CreatedAt
          │ N                                       : └ ─ ─ ─ ─ ─ ┬ ─ ─ ─ ─ ─ ┘
┌─────────┴────────────┐                            :             │ 1 : N
│     BasketItems      │                            : ┌ ─ ─ ─ ─ ─ ┴ ─ ─ ─ ─ ─ ┐
├──────────────────────┤                            :        OrderItems
│ PK Id                │                            : ├ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┤
│ FK BasketId          │                            :    PK Id
│    ProductId ········│···········┐                :    FK OrderId
│    ProductTitle      │           :                :       ProductId ·········┐
│    Thumbnail         │           :                :       ProductTitle (copy) :
│    Quantity (> 0)    │           :                :       UnitPrice   (copy)  :
│    UnitPrice         │           :                :       Quantity            :
│    AddedAt           │           :                :       LineTotal           :
│ UQ (BasketId,        │           :                : └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘
│     ProductId)       │           :                :                          :
└──────────────────────┘           ▼                ▼                          ▼
                    ╔════════════════════════════════════════════════════════════╗
                    ║        DummyJSON  /products   (external, not in our DB)    ║
                    ╚════════════════════════════╤═══════════════════════════════╝
                                                 : stock copied once (seed)
                                                 ▼
                                      ┌ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┐
                                        Inventory
                                        PK ProductId
                                           Quantity (>= 0)
                                           RowVersion
                                      └ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ┘

 ───  foreign key        ···  logical link by ProductId (no FK, the product lives in DummyJSON)
 ┌ ─ ┐  optional checkout tables (stretch)
```

### Relationships
- **User 1 ── 1 Basket**: one active basket per user (unique `UserId`).
- **Basket 1 ── N BasketItems**: each line belongs to exactly one basket.
- **Basket N ── N Product** is the real many-to-many, resolved **through BasketItems** (the join table). It's an explicit entity because the join carries data: `Quantity`, `UnitPrice`, `AddedAt`.
- **User N ── N Product** through **Favorites**, the same pattern (composite PK `(UserId, ProductId)` prevents duplicates).
- **User 1 ── N Orders, Order 1 ── N OrderItems** (stretch).

### Key choices
- **There is no Products table.** DummyJSON is the catalogue, and we store only `ProductId`.
- **Money** is stored as `decimal(18,2)`, never `double`.
- **Receipts are copies, not links.** OrderItems snapshot the title and price, so editing the basket or a DummyJSON price change never alters an old receipt.
- All mapping (keys, indexes, precision, cascades) goes in `Infrastructure/Persistence/Configurations`.

### Migrations & seed data (implemented 2026-11-18)
- `InitialCreate` holds the schema only.
- Initial state (demo users, and later the `Inventory` stock copied once from DummyJSON) goes in a **separate seed migration** (`SeedInitialData`) using `migrationBuilder.InsertData`, with fixed ids. Its `Down` deletes exactly those rows.
- Why a migration and not seeding at startup: it runs once, it's versioned with the schema, and `dotnet ef database update` reproduces the same state on every machine. A startup seeder runs on every boot and needs its own "already seeded?" checks.
- Seeded password hashes must be produced by the same hasher the app uses (`Pbkdf2PasswordHasher`, format `v1.{iterations}.{salt}.{key}`; no ASP.NET Identity, decided 2026-11-18). Generate them once and paste the hash. The only plain passwords committed are the demo accounts' passwords, which are listed in README.md.
- On startup, `DatabaseInitializer` runs `MigrateAsync()` when `Database:MigrateOnStartup` is true (Development). That creates the DB if it's missing and applies both migrations. In production, migrations run as a deploy step instead.
- Why not `HasData` in the configurations: it re-diffs the seed on every new migration and fights private setters and constructors. A dedicated migration keeps the seed explicit.

---

## 4. When we call DummyJSON

We use **proxy + cache for browsing, and snapshots for user data**:

| Operation | DummyJSON call? |
|---|---|
| Browse / search products | Cached per query, so only on a cache miss |
| Add to basket | One lookup (existence, price, stock), almost always a cache hit |
| Get basket / favorites | **None.** Title, thumbnail and price are snapshotted on the row |
| Get orders / receipts | **None.** Full snapshot |

- DummyJSON has **no "get many by ids" endpoint**, so never enrich line by line in a loop.
- Snapshot prices can go stale, so re-check them at checkout.
- Rejected alternative: syncing the whole catalogue (about 194 products) into a local table. It would make every read local, but it stops being a middleware and doesn't scale to large catalogues.
- **Don't use DummyJSON's `/carts`.** It only simulates writes and persists nothing.

---

## 5. Add to basket: checklist

1. **Ownership:** take the user id from JWT claims (`ICurrentUser`), never from the request. Get or create the basket, and handle the race on creation with the unique `Basket.UserId` index.
2. **Product exists:** look it up through `IProductClient`. DummyJSON 404 → our 404. DummyJSON down or timing out → 502/503, **not** 404.
3. **Merge:** if the product is already in the basket, increase the quantity instead of adding a second row (unique `(BasketId, ProductId)`).
4. **Lost updates:** use a `rowversion` concurrency token or an atomic `ExecuteUpdateAsync(q => q.Quantity + n)`.
5. **Semantics:** `POST /items` **adds** a quantity, `PUT /items/{id}` **sets** it.
6. **Validation:** `quantity >= 1` with an upper limit (99 or the stock level), and `productId > 0`. No price from the client.
7. **Money:** convert DummyJSON's numbers to `decimal`. `UnitPrice` is a snapshot taken when the item is added. Totals are computed on the server.
8. **One `SaveChangesAsync`** per request.

| Case | Status |
|---|---|
| Added / updated | 200 (+ basket) |
| Invalid quantity | 400 |
| No or invalid token | 401 |
| Unknown product | 404 |
| More than in stock | 409 / 422 |
| DummyJSON down | 502 / 503 |

---

## 6. Stock & concurrency ("1 in stock, many buyers")

- **The basket is intent, not commitment.** Adding to it does **not** reserve stock, and several users may add the last item. Most baskets are abandoned, so reserving at add time would lock up stock.
- On add, we only **soft-check** that this user's quantity is no more than `stock`. Optionally, show "only N left".
- DummyJSON stock is **read-only**, so true reservation needs our own `Inventory` table (stretch).
- **At checkout**, if implemented, use an atomic conditional decrement:
  ```sql
  UPDATE Inventory SET Quantity = Quantity - @qty
  WHERE ProductId = @id AND Quantity >= @qty   -- 0 rows affected = sold out
  ```
  Never read the stock, check it in C# and then write: two requests can both read "1" and oversell.
- For scarce items such as tickets or drops, use **time-boxed reservations**: a `Reservations(ProductId, UserId, Qty, ExpiresAt)` table plus a background job that releases expired ones.

---

## 7. Scalability

**Runtime (load):**
- Stateless API (JWT), so it scales horizontally.
- Product cache is `HybridCache` (.NET 9): L1 per-instance memory, L2 a Postgres table shared by all instances. At higher load, swap L2 for Redis (one DI line, the `IDistributedCache` stays the seam).
- DummyJSON is the bottleneck and single point of failure: use `AddStandardResilienceHandler()` (timeout, retry, circuit breaker) plus caching.
- DB: the indexes and unique constraints above, `AsNoTracking` on reads, async everywhere, a `pageSize` cap.

**Codebase (growth):** that's what CQRS or vertical slices help with. MediatR has **no effect** on runtime scalability.

---

## 8. Services vs CQRS/MediatR (**OPEN**)

| | Service per feature | CQRS + MediatR |
|---|---|---|
| Files | ~1 interface + 1 class per feature | request + handler (+ validator) per use case, ~3x the files |
| Readability | Go-to-definition leads straight to the logic | Indirection through `ISender.Send()` |
| Cross-cutting concerns | Filters, middleware, explicit calls | Pipeline behaviours, written once |
| Growth | Services can grow big | Each use case stays isolated |
| Licensing | none | MediatR v13+ is commercial (pin 12.x or write a small dispatcher) |
| Fit | ~12 endpoints, so a good fit | Overkill at this size |

**Recommendation:** services, shaped so the move to CQRS is cheap: one method per use case, each with its own request and response DTO, and no HTTP concerns in services. Note in the README: *"Services chosen for this scope; each method maps 1:1 to a command/query handler if use cases grow."*
Choose MediatR only if the brief or the job ad expects CQRS.

---

## 9. Folder layout (depends on §8)

**Services → by feature, then by type:**
```
Application/Baskets/   (plural: a .Basket namespace would shadow the Basket entity, CS0118)
├─ Dtos/          BasketDto, BasketItemDto, AddBasketItemRequest, UpdateBasketItemQuantityRequest
├─ Validators/
├─ BasketMappings.cs
├─ IBasketService.cs
└─ BasketService.cs
```

**CQRS / handlers → by feature, then by intent (vertical slices):**
```
Application/Baskets/   (plural: a .Basket namespace would shadow the Basket entity, CS0118)
├─ AddItem/          AddBasketItemRequest, Validator, Handler
├─ UpdateQuantity/
├─ RemoveItem/
├─ Clear/
├─ GetBasket/
└─ Shared/           BasketDto, BasketItemDto, BasketMappings
```

---

## 10. Skeleton issues to fix first (fixed 2026-11-18)

- No `<ProjectReference>`s between the projects.
- `Infrastructure/DependencyInjection.cs` names the method `AddInfrastructur` (typo), while `Program.cs` calls `AddInfrastructure`.
- `Program.cs` registers `AddControllers()` / `AddOpenApi()` twice, and the Swagger title says "Desk Link".
- Controllers derive from `Controller` instead of `ControllerBase` + `[ApiController]`.
- `AbySalto.Mid.http` still points at `/weatherforecast`.
- The skeleton's `Trusted_Connection` SQL Server string doesn't work on macOS. The project now uses PostgreSQL with a user-secret connection string.
