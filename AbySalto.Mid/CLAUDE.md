# AbySalto.Mid (WebApi)

HTTP layer: controllers, JWT bearer setup, Swagger, error handling. `Program.cs` → `AddPresentation()` (`DependencyInjection.cs`) + `AddApplication()` + `AddInfrastructure()`.

## Conventions

- **Thin controllers:** one service call per action, `[ProducesResponseType]` for every status the service can produce. No logic, no `NotFound()`/`BadRequest()` with hand-made bodies: throw Application exceptions instead.
- **Auth:** `[Authorize]` at class level for user-owned resources (`BasketController`, `FavoritesController`). No user or basket id in routes; the user is `ICurrentUser` (Guid from `sub`; `MapInboundClaims = false`).
- **Errors:** `Errors/ApiExceptionHandler` (a singleton, so no scoped dependencies: read the user from `HttpContext`) maps exceptions to ProblemDetails, logs each failure once with user id + traceId, and redacts DB exception text (`ExceptionRedaction`). 500s return a generic message. JwtBearer `OnChallenge`/`OnForbidden` write the same ProblemDetails shape.
- **JSON:** enums serialize as names (`JsonStringEnumConverter`).
- Don't name a folder `Infrastructure` here: it collides with the Infrastructure project's namespace.

## Configuration

- `appsettings*.json` hold non-secret settings only. Claude is **denied** reading them; ask the user for a value if needed.
- Secrets (`Jwt:Key`, `ConnectionStrings:DefaultConnection`) come from user-secrets in Development (UserSecretsId in the csproj) and env vars in production. Claude is denied reading user secrets too.
- `JwtOptions` are validated on start: the app refuses to start without `Jwt:Key`.
- Logging categories that would re-log raw DB exception text (EF `Update`/`Query`/`Database.Command`, `ExceptionHandlerMiddleware`) are set to `None`. Don't re-enable them in an environment file.
