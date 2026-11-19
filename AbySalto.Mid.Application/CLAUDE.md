# AbySalto.Mid.Application

Use cases: one service per feature (`IAuthService`/`AuthService`, `IBasketService`, `IFavoriteService`, `IProductService`), DTOs, and the interfaces Infrastructure implements. References Domain only, plus EF Core (for `DbSet` in `IAppDbContext`) and DI abstractions.

## Conventions

- **Feature folders:** `Auth/`, `Baskets/`, `Favorites/`, `Products/`, each with `Dtos/`. Shared: `Common/Dtos`, `Common/Exceptions`, `Common/Interfaces`.
- **Services are `internal sealed`**, registered scoped in `DependencyInjection.cs`; interfaces are public. Tests see internals via `InternalsVisibleTo`.
- **DTOs are records.** Map entity → DTO explicitly in the service (`ToDto`); never return an entity. No ids for users or baskets in responses.
- **Ownership:** the user comes only from `ICurrentUser.UserId` (Guid, from the JWT). Never accept a user id in a request.
- **Product data** (price, title, stock, category) comes from `IProductClient`, never from the client.
- **Validation** of input happens in the service and throws `ValidationException`; business rules stay in Domain.
- **Exceptions → status:** `ValidationException` 400, `AuthenticationFailedException` 401, `NotFoundException` 404, `ConflictException` 409, `ProductCatalogUnavailableException` 503. When converting a `DbUpdateException`, pass it as the inner exception so the cause is logged.
- Exception messages are shown to clients: never put emails, usernames or other PII in them.
- `IAppDbContext` is the only DB access; the concrete `AppDbContext` stays in Infrastructure.
