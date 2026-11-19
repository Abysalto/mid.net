using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AbySalto.Mid.Infrastructure.Persistence.Migrations
{
    public partial class SeedInitialData : Migration
    {
        private static readonly DateTime SeededAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly Guid DemoUserId = new("01937c8e-5a00-7000-8000-000000000001");
        private static readonly Guid JaneUserId = new("01937c8e-5a00-7000-8000-000000000002");

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Users",
                columns: ["Id", "Email", "Username", "PasswordHash", "CreatedAt"],
                values: new object[,]
                {
                    { DemoUserId, "demo@abysalto.local", "demo", "v1.210000.cSlqgOHzpht35gaeoKGq1g==.nH6tZYLtdSOXQfjOBp30up5ShlkuFSdsuBV1ZZEnYKI=", SeededAt },
                    { JaneUserId, "jane@abysalto.local", "jane", "v1.210000.h+ubt5nUENuWrh+GGfMezA==.S+e4L0ng3X1nrK97Ug1WLie6MV1G0mUQO+wcWpHlbn4=", SeededAt },
                });

            migrationBuilder.InsertData(
                table: "Baskets",
                columns: ["Id", "UserId", "CreatedAt", "UpdatedAt"],
                values: new object[] { 1, DemoUserId, SeededAt, SeededAt });

            migrationBuilder.InsertData(
                table: "BasketItems",
                columns: ["Id", "BasketId", "ProductId", "ProductTitle", "Thumbnail", "Category", "Quantity", "UnitPrice", "AddedAt"],
                values: new object[,]
                {
                    { 1, 1, 1, "Essence Mascara Lash Princess", "https://cdn.dummyjson.com/product-images/beauty/essence-mascara-lash-princess/thumbnail.webp", "Beauty", 2, 9.99m, SeededAt },
                    { 2, 1, 6, "Calvin Klein CK One", "https://cdn.dummyjson.com/product-images/fragrances/calvin-klein-ck-one/thumbnail.webp", "Fragrances", 1, 49.99m, SeededAt },
                });

            migrationBuilder.InsertData(
                table: "Favorites",
                columns: ["UserId", "ProductId", "ProductTitle", "Thumbnail", "Category", "CreatedAt"],
                values: new object[] { DemoUserId, 16, "Apple", "https://cdn.dummyjson.com/product-images/groceries/apple/thumbnail.webp", "Groceries", SeededAt });

        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(table: "Favorites", keyColumns: ["UserId", "ProductId"], keyValues: new object[] { DemoUserId, 16 });
            migrationBuilder.DeleteData(table: "BasketItems", keyColumn: "Id", keyValues: new object[] { 1, 2 });
            migrationBuilder.DeleteData(table: "Baskets", keyColumn: "Id", keyValue: 1);
            migrationBuilder.DeleteData(table: "Users", keyColumn: "Id", keyValues: new object[] { DemoUserId, JaneUserId });
        }
    }
}
