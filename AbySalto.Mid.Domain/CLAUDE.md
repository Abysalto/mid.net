# AbySalto.Mid.Domain

Plain C#: entities, their rules, enums. **No package references and no project references.** No EF attributes, no ASP.NET, no DTOs.

## Conventions

- Entities have **private setters** and a private parameterless constructor for EF (`private Basket() { } // EF Core`). State changes go through methods that enforce the rules.
- Rules that only need the entity's own data live on the entity: `Basket` merges duplicate lines and enforces the limits in `BasketLimits` (distinct products, quantity per line). The **values** are configuration (Application's `BasketOptions`, section `Basket`); the service passes them in, because Domain can't read config. Violations throw `DomainException` (→ 400).
- `BasketItem` is created and changed only through `Basket` (internal ctor/mutators), so the basket's rules always run. Its collection is the `_items` backing field (EF is configured to use it).
- Methods take `DateTime now` instead of reading the clock, so tests control time.
- `User.Id` is a Guid set in the constructor (`Guid.CreateVersion7()`), never a counter. `User.NormalizeEmail` is the single definition of email comparison; lookups must use it too.
- Basket items and favorites keep a **snapshot** of the product (title, thumbnail, category, unit price) taken when added.

## ProductCategory (generated)

`Enums/ProductCategory.cs` is **generated** by `tools/GenerateProductCategories` from DummyJSON's products. Don't edit it by hand. The DB stores the **name** as text, so a name must never be renamed or removed. `ProductCategoryExtensions` converts name ↔ DummyJSON slug (`HomeDecoration` ↔ `home-decoration`); an unknown slug maps to `Unknown`.
