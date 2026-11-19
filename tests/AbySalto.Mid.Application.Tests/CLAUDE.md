# AbySalto.Mid.Application.Tests

xUnit + NSubstitute + EF Core InMemory (`TestDbContext`) + `FakeTimeProvider`. Run with `dotnet test` from the repo root (outside the Claude sandbox).

## Conventions

- Folders mirror the features: `Auth/`, `Baskets/` (plural, see root CLAUDE.md), `Favorites/`, `Products/`.
- Test names describe behaviour: `Adding_the_same_product_again_merges_into_one_line`.
- Services are tested with a real `TestDbContext` and a substituted `IProductClient` / `ICurrentUser`. Use the real `Pbkdf2PasswordHasher` in auth tests (a mock would hide register/login mismatches).
- User ids are Guids (`Guid.CreateVersion7()`); get ids from the data store, not from API responses (DTOs carry none).
- Pin time with `FakeTimeProvider`; never use `DateTime.Now`.
- Tests needing the network (DummyJSON) don't belong here; the category enum's freshness is checked with the generator's `-check`.

## Limits of EF InMemory

It doesn't enforce unique indexes, check constraints or the basket's concurrency token, so the 409 race/concurrency paths aren't covered here. Those need a real Postgres (e.g. Testcontainers).
