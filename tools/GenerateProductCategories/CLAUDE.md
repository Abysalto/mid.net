# GenerateProductCategories

Dev tool (net9 console, **not** in the solution) that regenerates `AbySalto.Mid.Domain/Enums/ProductCategory.cs` from the categories DummyJSON's products actually use (`/products?limit=0`).

```bash
dotnet run --project tools/GenerateProductCategories              # rewrite the enum
dotnet run --project tools/GenerateProductCategories -- -check    # exit 1 if stale, 2 if DummyJSON unreachable
```

## Rules it enforces

- Existing names are never renamed or removed: the DB stores the name as text. A category DummyJSON drops is kept with a comment.
- New categories are appended; numbers are never reused.
- Name ↔ slug must round-trip with `ProductCategoryExtensions` (`HomeDecoration` ↔ `home-decoration`); a slug that can't fails loudly.

## Gotchas

- XML comments in the csproj can't contain `--`; usage text lives in `Program.cs`.
- DummyJSON is sometimes slow; the tool retries 3 times.
