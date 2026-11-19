# AbySalto.Mid

.NET 9 Web API: products proxied from DummyJSON (cached), users/baskets/favorites in PostgreSQL via EF Core (Npgsql), plain JWT auth (no ASP.NET Identity). Setup, config and endpoints: `README.md`. Design: `docs/ARCHITECTURE.md`. Each project has its own `CLAUDE.md` with layer rules.

## Layout (Clean Architecture, dependencies point inward)

| Project | Role | May reference |
|---|---|---|
| `AbySalto.Mid.Domain` | entities, business rules, enums | nothing |
| `AbySalto.Mid.Application` | services per feature, DTOs, interfaces | Domain |
| `AbySalto.Mid.Infrastructure` | EF Core, DummyJSON client, hashing, JWT | Application, Domain |
| `AbySalto.Mid` (WebApi) | controllers, auth wiring, error handling | Application, Infrastructure |
| `tests/AbySalto.Mid.Application.Tests` | xUnit + NSubstitute + EF InMemory | all |
| `tools/GenerateProductCategories` | dev tool, not in the sln | nothing |

Services per feature, not CQRS/MediatR. Features are folders: `Auth`, `Baskets` (plural: a `Basket` namespace shadows the entity), `Favorites`, `Products`.

## Commands

```bash
dotnet build
dotnet test
dotnet run --project AbySalto.Mid                                  # needs Postgres + user-secrets, see README
dotnet ef migrations add <Name> -p AbySalto.Mid.Infrastructure -s AbySalto.Mid -o Persistence/Migrations
dotnet run --project tools/GenerateProductCategories [-- -check]   # regenerate the ProductCategory enum
```

- Run `dotnet build/test/run/ef` **outside the Claude sandbox**: NuGet restore and MSBuild hang inside it.
- `dotnet ef` needs `ConnectionStrings__DefaultConnection` and `Jwt__Key` set (any dummy values) to build the host; it doesn't connect except for `database update` / `migrations remove`.

## Rules that apply everywhere

- **Secrets:** never in `appsettings*.json` or code. Dev: user-secrets. Prod: env vars (`Jwt__Key`, `ConnectionStrings__DefaultConnection`). Claude is denied reading `appsettings*.json` and user secrets (`.claude/settings.json`); ask the user if a config value is needed.
- **No PII in logs:** identify users by id (Guid) only, never email/username. DB exception messages are redacted (they contain row values).
- **No sequential ids to the client:** user ids are Guids; responses carry no user or basket id. The user always comes from the JWT `sub` claim, never from a route or body.
- **Package versions:** keep every `Microsoft.Extensions.*` / EF Core package on the same 9.0.x. NU1605 (downgrade) is an error and shows as "Build FAILED" with 0 errors when output is filtered.
- **Errors:** throw the Application exceptions (`ValidationException` 400, `NotFoundException` 404, `ConflictException` 409, ...); `ApiExceptionHandler` turns them into ProblemDetails. Don't return bare `NotFound()`/`BadRequest()`.
- **Time:** inject `TimeProvider`, store UTC (`GetUtcNow().UtcDateTime`).
- Keep `README.md` in sync when endpoints, config or setup change.
