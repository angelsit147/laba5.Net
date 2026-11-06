using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Data.Migrations
{

    public partial class InitialCreate : Migration
    {

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Dishes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dishes", x => x.Id);
                    table.CheckConstraint("CK_Dishes_Id", "\"Id\" > 0");
                    table.CheckConstraint("CK_Dishes_IsAvailable", "\"IsAvailable\" IN (0, 1)");
                });

            migrationBuilder.CreateTable(
                name: "Ingredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ingredients", x => x.Id);
                    table.CheckConstraint("CK_Ingredients_Id", "\"Id\" > 0");
                });

            migrationBuilder.CreateTable(
                name: "LoyaltyLevels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false, collation: "BINARY"),
                    DiscountPercent = table.Column<int>(type: "INTEGER", nullable: false),
                    MinOrders = table.Column<int>(type: "INTEGER", nullable: false),
                    MinTotalSpent = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoyaltyLevels", x => x.Id);
                    table.CheckConstraint("CK_LoyaltyLevels_DiscountPercent", "\"DiscountPercent\" BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_LoyaltyLevels_Id", "\"Id\" > 0");
                    table.CheckConstraint("CK_LoyaltyLevels_MinOrders", "\"MinOrders\" BETWEEN 0 AND 2147483647");
                    table.CheckConstraint("CK_LoyaltyLevels_MinTotalSpent", "typeof(\"MinTotalSpent\") = 'integer' AND \"MinTotalSpent\" >= 0");
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DishId = table.Column<int>(type: "INTEGER", nullable: false),
                    PortionName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PortionDescription = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Price = table.Column<long>(type: "INTEGER", nullable: false),
                    IsAvailable = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.CheckConstraint("CK_MenuItems_Id", "\"Id\" > 0");
                    table.CheckConstraint("CK_MenuItems_IsAvailable", "\"IsAvailable\" IN (0, 1)");
                    table.CheckConstraint("CK_MenuItems_Price", "typeof(\"Price\") = 'integer' AND \"Price\" > 0");
                    table.ForeignKey(
                        name: "FK_MenuItems_Dishes_DishId",
                        column: x => x.DishId,
                        principalTable: "Dishes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DishIngredients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DishId = table.Column<int>(type: "INTEGER", nullable: false),
                    IngredientId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DishIngredients", x => x.Id);
                    table.CheckConstraint("CK_DishIngredients_Id", "\"Id\" > 0");
                    table.ForeignKey(
                        name: "FK_DishIngredients_Dishes_DishId",
                        column: x => x.DishId,
                        principalTable: "Dishes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DishIngredients_Ingredients_IngredientId",
                        column: x => x.IngredientId,
                        principalTable: "Ingredients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    LoyaltyCode = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, collation: "BINARY"),
                    LoyaltyLevelId = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                    table.CheckConstraint("CK_Customers_Id", "\"Id\" > 0");
                    table.ForeignKey(
                        name: "FK_Customers_LoyaltyLevels_LoyaltyLevelId",
                        column: x => x.LoyaltyLevelId,
                        principalTable: "LoyaltyLevels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Orders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    CustomerId = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    OrderType = table.Column<int>(type: "INTEGER", nullable: false),
                    DiscountPercentFixed = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Orders", x => x.Id);
                    table.CheckConstraint("CK_Orders_DiscountPercentFixed", "\"DiscountPercentFixed\" IS NULL OR \"DiscountPercentFixed\" BETWEEN 0 AND 10000");
                    table.CheckConstraint("CK_Orders_GuestDiscount", "\"CustomerId\" IS NOT NULL OR \"DiscountPercentFixed\" IS NULL OR \"DiscountPercentFixed\" = 0");
                    table.CheckConstraint("CK_Orders_Id", "\"Id\" > 0");
                    table.CheckConstraint("CK_Orders_OrderType", "\"OrderType\" IN (0, 1, 2)");
                    table.CheckConstraint("CK_Orders_Status", "\"Status\" IN (0, 1, 2, 3)");
                    table.CheckConstraint("CK_Orders_StatusDiscount", "(\"Status\" = 0 AND \"DiscountPercentFixed\" IS NULL) OR (\"Status\" IN (1, 2) AND \"DiscountPercentFixed\" IS NOT NULL) OR \"Status\" = 3");
                    table.ForeignKey(
                        name: "FK_Orders_Customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "Customers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "OrderItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OrderId = table.Column<int>(type: "INTEGER", nullable: false),
                    MenuItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantity = table.Column<int>(type: "INTEGER", nullable: false),
                    UnitPrice = table.Column<long>(type: "INTEGER", nullable: false),
                    DishNameFixed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PortionNameFixed = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    PortionDescriptionFixed = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderItems", x => x.Id);
                    table.CheckConstraint("CK_OrderItems_Id", "\"Id\" > 0");
                    table.CheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" BETWEEN 1 AND 2147483647");
                    table.CheckConstraint("CK_OrderItems_UnitPrice", "typeof(\"UnitPrice\") = 'integer' AND \"UnitPrice\" > 0");
                    table.ForeignKey(
                        name: "FK_OrderItems_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderItems_Orders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "Orders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Customers_LoyaltyLevelId",
                table: "Customers",
                column: "LoyaltyLevelId");

            migrationBuilder.CreateIndex(
                name: "UX_Customers_LoyaltyCode",
                table: "Customers",
                column: "LoyaltyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DishIngredients_IngredientId",
                table: "DishIngredients",
                column: "IngredientId");

            migrationBuilder.CreateIndex(
                name: "UX_DishIngredients_DishId_IngredientId",
                table: "DishIngredients",
                columns: new[] { "DishId", "IngredientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UX_LoyaltyLevels_Name",
                table: "LoyaltyLevels",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_DishId",
                table: "MenuItems",
                column: "DishId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderItems_MenuItemId",
                table: "OrderItems",
                column: "MenuItemId");

            migrationBuilder.CreateIndex(
                name: "UX_OrderItems_OrderId_MenuItemId",
                table: "OrderItems",
                columns: new[] { "OrderId", "MenuItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_CustomerId_Status",
                table: "Orders",
                columns: new[] { "CustomerId", "Status" });
        }


        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DishIngredients");

            migrationBuilder.DropTable(
                name: "OrderItems");

            migrationBuilder.DropTable(
                name: "Ingredients");

            migrationBuilder.DropTable(
                name: "MenuItems");

            migrationBuilder.DropTable(
                name: "Orders");

            migrationBuilder.DropTable(
                name: "Dishes");

            migrationBuilder.DropTable(
                name: "Customers");

            migrationBuilder.DropTable(
                name: "LoyaltyLevels");
        }
    }
}
