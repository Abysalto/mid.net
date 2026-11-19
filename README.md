# AbySalto.Mid

Middleware Web API (.NET 9). Products come from [DummyJSON](https://dummyjson.com/docs/products); users, baskets and favorites live in PostgreSQL via EF Core (Npgsql). Design notes: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Local setup

Prerequisites: .NET 9 SDK, a PostgreSQL server (13+), `dotnet-ef` (`dotnet tool install -g dotnet-ef`).

### 1. A Postgres server

**Already running one?** Skip to step 2. The app creates its database on first run; the user in the connection string needs the `CREATEDB` privilege, or create the database yourself first (`CREATE DATABASE abysalto;`).

**No server?** Start one with Docker:

```bash
cp .env.example .env          # then edit POSTGRES_PASSWORD in .env
docker compose up -d
```

### 2. Set the secrets (once per machine)

Secrets never go in `appsettings*.json`. In Development they come from
[user-secrets](https://learn.microsoft.com/aspnet/core/security/app-secrets), stored outside the repo
(`~/.microsoft/usersecrets/<UserSecretsId>/secrets.json`):

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=abysalto;Username=<your user>;Password=<your password>" \
  --project AbySalto.Mid

# JWT signing key: any random string of 32+ characters
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)" --project AbySalto.Mid
```

The app refuses to start without `Jwt:Key`, so a missing secret shows up immediately instead of on the first login.

### 3. Run

```bash
dotnet run --project AbySalto.Mid
```

On startup in Development (`Database:MigrateOnStartup = true`), the app **creates the database if it doesn't exist** and applies the pending migrations:

1. `InitialCreate` — schema (tables, indexes, a `Quantity >= 1` check; the basket's concurrency token is Postgres's built-in `xmin`)
2. `SeedInitialData` — demo data
3. `AddCacheEntries` — the `CacheEntries` table behind the shared product cache

On an up-to-date database this is a no-op. Swagger UI is at the root URL.

To migrate manually instead: `dotnet ef database update -p AbySalto.Mid.Infrastructure -s AbySalto.Mid`.


## Where configuration lives

| Setting | Where | Secret? |
|---|---|---|
| `DummyJson:BaseUrl`, `DummyJson:CacheMinutes` (default 10; 0 turns caching off), `DummyJson:LocalCacheSeconds` (default 60) | `appsettings.json` (optional, defaults in code) | no (public API) |
| `Database:MigrateOnStartup` | `appsettings.json` (false) / `appsettings.Development.json` (true) | no |
| `ConnectionStrings:DefaultConnection` | user-secrets (dev), env var `ConnectionStrings__DefaultConnection` (prod) | **yes** |
| Postgres password for the optional Docker server | `.env` (git-ignored), read by `docker-compose.yml` | **yes** |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | no |
| `Jwt:Key` | user-secrets (dev), env var `Jwt__Key` (prod) | **yes** |
| `Basket:MaxDistinctProducts` (default 50, 1–500), `Basket:MaxQuantity` (default 99, 1–10000) | optional; `appsettings.json` or env var (`Basket__MaxQuantity`) | no |
| `RateLimiting:Auth:PermitLimit` (default 10), `RateLimiting:Auth:WindowSeconds` (default 60): login/register attempts per client IP | optional; `appsettings.json` or env var | no |

Any setting can be overridden by an environment variable, using `__` for nesting.

## Demo data

| Login | Password | State |
|---|---|---|
| `demo@abysalto.local` | `Demo123!` | basket: 2× product 1, 1× product 6; favorite: product 16 |
| `jane@abysalto.local` | `Demo123!` | empty |

These are local demo accounts only.

## Endpoints

| Method | Route | Notes |
|---|---|---|
| POST | `/api/auth/register` | `{ email, username, password }` → 201 + token. Password: 8–128 chars, a letter and a digit. 409 if the email exists |
| POST | `/api/auth/login` | `{ email, password }` → 200 + token. 401 with the same message for an unknown email and a wrong password |
| GET | `/api/auth/me` | requires `Authorization: Bearer <accessToken>` |
| GET | `/api/products?search=&category=&page=&pageSize=&sortBy=&order=` | public; paged (see below); `search` and `category` can be combined (search within a category) |
| GET | `/api/products/{id}` | 404 if DummyJSON has no such product |
| GET | `/api/products/categories` | every value `category` accepts: `{ category: "HomeDecoration", slug: "home-decoration", name: "Home Decoration" }` |
| GET | `/api/basket` | 🔒 my basket; empty (`id: 0`) if I have none yet |
| POST | `/api/basket/items` | 🔒 `{ productId, quantity }`; adds to the line if the product is already there. Price is taken from DummyJSON at that moment |
| PUT | `/api/basket/items/{productId}` | 🔒 `{ quantity }`; sets the quantity |
| DELETE | `/api/basket/items/{productId}` | 🔒 removes the line; returns the updated basket |
| DELETE | `/api/basket` | 🔒 empties the basket → 204 |
| GET | `/api/favorites` | 🔒 newest first |
| PUT | `/api/favorites/{productId}` | 🔒 idempotent; repeating it returns the existing favorite |
| DELETE | `/api/favorites/{productId}` | 🔒 → 204; 404 if it isn't a favorite |

🔒 = requires `Authorization: Bearer <accessToken>`. The user always comes from the token, so you can only see and change your own basket and favorites.

Basket rules: quantity 1–`Basket:MaxQuantity` (default 99) per line, at most `Basket:MaxDistinctProducts` (default 50) different products, and no more than DummyJSON's `stock`. Both limits are checked when the app starts; an out-of-range value stops it. Breaking one → 400. An unknown product → 404. Two requests editing the same basket at the same moment → 409.

**Categories are an enum** (`ProductCategory`, in Domain), matching DummyJSON's 24 categories. Use the name in the query string, e.g. `?category=HomeDecoration`; matching ignores case. A value that isn't in the enum returns 400. Products, basket items and favorites return `category` as that name. If DummyJSON ever adds a category the enum doesn't know, those products come back as `Unknown` and a warning is logged.

The enum is **generated** from DummyJSON by a small .NET tool. It pulls all products and uses the categories they actually carry:

```bash
dotnet run --project tools/GenerateProductCategories              # rewrite ProductCategory.cs
dotnet run --project tools/GenerateProductCategories -- -check    # exit 1 if it's out of date (for CI)
```

The database stores the category as **text** (the enum name), and only the code checks that it's valid; there's no lookup table. So the generator never renames or removes a name, because stored rows would stop parsing. A category DummyJSON drops is kept and marked, and a new one is appended. No migration is needed after regenerating.

**Paging:** `page` (1-based, default 1) and `pageSize` (1–100, default 30), e.g. `?page=2&pageSize=10` returns items 11–20. Every list response says where you are:

```json
{ "items": [ ... ], "page": 2, "pageSize": 10, "total": 194, "totalPages": 20 }
```

A page past the end returns empty `items` with the real `total`/`totalPages`.

**Search within a category** (`?category=Smartphones&search=pro`): DummyJSON has no endpoint for this, and its search ignores `category`. So the API first runs DummyJSON's search for **all** hits (`limit=0`; the whole catalogue is 194 products, cached per search term), then keeps the hits whose `ProductCategory` matches, and sorts and pages them itself. `total` counts the matches in that category. Because the search is still DummyJSON's, the results are exactly what `?search=` alone returns, narrowed to one category.

**Caching (HybridCache, two levels):** DummyJSON responses are cached by request URL.

| Level | Where | Lifetime | Shared between API instances? |
|---|---|---|---|
| L1 | each instance's memory | `LocalCacheSeconds` (60 s) | no |
| L2 | Postgres table `CacheEntries` | `CacheMinutes` (10 min) | **yes** |

A request checks L1, then L2, and only then calls DummyJSON. So with several instances, DummyJSON is called about once per URL and not once per instance, and the cache survives restarts. Simultaneous misses for the same URL within one instance share a single DummyJSON call. "Not found" is cached as well (the catalogue is static), so repeated requests for unknown ids don't each hit DummyJSON. Queries are normalized before they become a key: the search term is trimmed and lower-cased, and `sortBy`/`order` are case-insensitive, so `?sortBy=Price&order=DESC` and `?sortBy=price&order=desc` share one entry. `search` is capped at 100 characters. Keys carry a version (`dummyjson:v1:`) so a deploy that changes the cached shape doesn't read old rows. Expired rows are deleted every 30 minutes by a background job. If the database is unreachable, the cache falls back to L1 and DummyJSON, so browsing keeps working. Only product data is cached; baskets and favorites always read the database, which is their source of truth.

If DummyJSON is down or times out, the API returns **503**, not 404.

In Swagger, call `login`, copy `accessToken`, click **Authorize** and paste it (without `Bearer `).

## Errors

Every error is RFC 7807 problem JSON with a human-readable `detail`, a machine-readable `code`, and, where useful, the numbers behind it, so a frontend can show its own text or offer a fix (e.g. "add 1 instead"):

```json
{
  "status": 400,
  "title": "Invalid request",
  "detail": "Only 3 of 'Essence Mascara Lash Princess' are in stock and you already have 2 in your basket, so you can add at most 1 more.",
  "code": "basket.insufficient_stock",
  "productId": 1, "requested": 2, "inStock": 3, "inBasket": 2, "maxAddable": 1,
  "traceId": "00-..."
}
```

| Status | `code` | When | Extra fields |
|---|---|---|---|
| 400 | `basket.insufficient_stock` | more than DummyJSON's stock (what's already in the basket counts on add) | `requested`, `inStock`, `inBasket`, `maxAddable` |
| 400 | `basket.out_of_stock` | stock is 0 | `productId`, `inStock` |
| 400 | `basket.quantity_limit_exceeded` | above `Basket:MaxQuantity` for one product (also when adding to an existing line) | `requested`, `inBasket`, `maxQuantity`, `maxAddable` |
| 400 | `basket.product_limit_reached` | basket already holds `Basket:MaxDistinctProducts` different products | `distinctProducts`, `maxDistinctProducts` |
| 400 | `basket.invalid_quantity` | quantity below 1 (on update the message points to DELETE) | `requested` |
| 400 | `product.invalid_query` | bad `search` (over 100 chars), `page`, `pageSize`, `sortBy`, `order` or `category` | `parameter` |
| 400 | `product.invalid_id` | product id ≤ 0 | `productId` |
| 400 | `auth.invalid_email` / `auth.invalid_username` / `auth.invalid_password` | register input | `minLength`, `maxLength` |
| 401 | `auth.failed` | wrong email or password (same message for both) | |
| 401 | `auth.token_missing` / `auth.token_invalid` / `auth.token_expired` | no, bad or expired bearer token | |
| 403 | `auth.forbidden` | authenticated but not allowed | |
| 404 | `product.not_found` / `basket.item_not_found` / `favorite.not_found` / `auth.user_not_found` | | `productId` |
| 409 | `auth.email_taken` / `basket.concurrent_update` | duplicate email / two edits of one basket at once | |
| 429 | `auth.rate_limited` | too many login/register attempts from one IP (fixed window); also sets `Retry-After` | `retryAfterSeconds` |
| 503 | `product.catalog_unavailable` | DummyJSON down or timing out | |
| 500 | `server_error` | anything unexpected; details are only in the log (look up the `traceId`) | |

## Auth

Plain JWT, no ASP.NET Core Identity:

- **Passwords:** PBKDF2-HMAC-SHA512, 210,000 iterations, random 16-byte salt. It uses .NET's built-in `Rfc2898DeriveBytes.Pbkdf2` with a constant-time compare. The stored format is `v1.{iterations}.{salt}.{key}`, so the iteration count can be raised later without breaking existing hashes.
- **User ids are Guids** (`Guid.CreateVersion7()`), not sequential numbers. The id is in the token, and a JWT is readable by anyone who holds it, so a counter would tell users how many accounts exist and roughly where theirs is. Responses carry no ids for the user or the basket at all; the server always takes the user from the token. Baskets and basket items keep int keys internally.
- **Tokens:** HS256-signed. Claims are `sub` (user id, a Guid), `email`, `unique_name` and `jti`. They're valid for `Jwt:ExpiryMinutes`, and there are no refresh tokens. The same `JwtOptions` drive both signing and validation.

## Tests

```bash
dotnet test
```
