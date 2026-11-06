
using System;
using Data.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace Data.Migrations
{
    [DbContext(typeof(RestaurantDbContext))]
    partial class RestaurantDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "8.0.33");

            modelBuilder.Entity("Data.Entities.Customer", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("LastName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<string>("LoyaltyCode")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("TEXT")
                        .UseCollation("BINARY");

                    b.Property<int>("LoyaltyLevelId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PhoneNumber")
                        .HasMaxLength(32)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("LoyaltyCode")
                        .IsUnique()
                        .HasDatabaseName("UX_Customers_LoyaltyCode");

                    b.HasIndex("LoyaltyLevelId")
                        .HasDatabaseName("IX_Customers_LoyaltyLevelId");

                    b.ToTable("Customers", null, t =>
                        {
                            t.HasCheckConstraint("CK_Customers_Id", "\"Id\" > 0");
                        });
                });

            modelBuilder.Entity("Data.Entities.Dish", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Description")
                        .HasMaxLength(1000)
                        .HasColumnType("TEXT");

                    b.Property<bool>("IsAvailable")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Dishes", null, t =>
                        {
                            t.HasCheckConstraint("CK_Dishes_Id", "\"Id\" > 0");

                            t.HasCheckConstraint("CK_Dishes_IsAvailable", "\"IsAvailable\" IN (0, 1)");
                        });
                });

            modelBuilder.Entity("Data.Entities.DishIngredient", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("DishId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("IngredientId")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("IngredientId")
                        .HasDatabaseName("IX_DishIngredients_IngredientId");

                    b.HasIndex("DishId", "IngredientId")
                        .IsUnique()
                        .HasDatabaseName("UX_DishIngredients_DishId_IngredientId");

                    b.ToTable("DishIngredients", null, t =>
                        {
                            t.HasCheckConstraint("CK_DishIngredients_Id", "\"Id\" > 0");
                        });
                });

            modelBuilder.Entity("Data.Entities.Ingredient", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Ingredients", null, t =>
                        {
                            t.HasCheckConstraint("CK_Ingredients_Id", "\"Id\" > 0");
                        });
                });

            modelBuilder.Entity("Data.Entities.LoyaltyLevel", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("DiscountPercent")
                        .HasColumnType("INTEGER");

                    b.Property<int>("MinOrders")
                        .HasColumnType("INTEGER");

                    b.Property<long>("MinTotalSpent")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT")
                        .UseCollation("BINARY");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique()
                        .HasDatabaseName("UX_LoyaltyLevels_Name");

                    b.ToTable("LoyaltyLevels", null, t =>
                        {
                            t.HasCheckConstraint("CK_LoyaltyLevels_DiscountPercent", "\"DiscountPercent\" BETWEEN 0 AND 10000");

                            t.HasCheckConstraint("CK_LoyaltyLevels_Id", "\"Id\" > 0");

                            t.HasCheckConstraint("CK_LoyaltyLevels_MinOrders", "\"MinOrders\" BETWEEN 0 AND 2147483647");

                            t.HasCheckConstraint("CK_LoyaltyLevels_MinTotalSpent", "typeof(\"MinTotalSpent\") = 'integer' AND \"MinTotalSpent\" >= 0");
                        });
                });

            modelBuilder.Entity("Data.Entities.MenuItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<int>("DishId")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("IsAvailable")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PortionDescription")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<string>("PortionName")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<long>("Price")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("DishId")
                        .HasDatabaseName("IX_MenuItems_DishId");

                    b.ToTable("MenuItems", null, t =>
                        {
                            t.HasCheckConstraint("CK_MenuItems_Id", "\"Id\" > 0");

                            t.HasCheckConstraint("CK_MenuItems_IsAvailable", "\"IsAvailable\" IN (0, 1)");

                            t.HasCheckConstraint("CK_MenuItems_Price", "typeof(\"Price\") = 'integer' AND \"Price\" > 0");
                        });
                });

            modelBuilder.Entity("Data.Entities.Order", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("TEXT");

                    b.Property<int?>("CustomerId")
                        .HasColumnType("INTEGER");

                    b.Property<int?>("DiscountPercentFixed")
                        .HasColumnType("INTEGER");

                    b.Property<int>("OrderType")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Status")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("CustomerId", "Status")
                        .HasDatabaseName("IX_Orders_CustomerId_Status");

                    b.ToTable("Orders", null, t =>
                        {
                            t.HasCheckConstraint("CK_Orders_DiscountPercentFixed", "\"DiscountPercentFixed\" IS NULL OR \"DiscountPercentFixed\" BETWEEN 0 AND 10000");

                            t.HasCheckConstraint("CK_Orders_GuestDiscount", "\"CustomerId\" IS NOT NULL OR \"DiscountPercentFixed\" IS NULL OR \"DiscountPercentFixed\" = 0");

                            t.HasCheckConstraint("CK_Orders_Id", "\"Id\" > 0");

                            t.HasCheckConstraint("CK_Orders_OrderType", "\"OrderType\" IN (0, 1, 2)");

                            t.HasCheckConstraint("CK_Orders_Status", "\"Status\" IN (0, 1, 2, 3)");

                            t.HasCheckConstraint("CK_Orders_StatusDiscount", "(\"Status\" = 0 AND \"DiscountPercentFixed\" IS NULL) OR (\"Status\" IN (1, 2) AND \"DiscountPercentFixed\" IS NOT NULL) OR \"Status\" = 3");
                        });
                });

            modelBuilder.Entity("Data.Entities.OrderItem", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("DishNameFixed")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int>("MenuItemId")
                        .HasColumnType("INTEGER");

                    b.Property<int>("OrderId")
                        .HasColumnType("INTEGER");

                    b.Property<string>("PortionDescriptionFixed")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("TEXT");

                    b.Property<string>("PortionNameFixed")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("TEXT");

                    b.Property<int>("Quantity")
                        .HasColumnType("INTEGER");

                    b.Property<long>("UnitPrice")
                        .HasColumnType("INTEGER");

                    b.HasKey("Id");

                    b.HasIndex("MenuItemId")
                        .HasDatabaseName("IX_OrderItems_MenuItemId");

                    b.HasIndex("OrderId", "MenuItemId")
                        .IsUnique()
                        .HasDatabaseName("UX_OrderItems_OrderId_MenuItemId");

                    b.ToTable("OrderItems", null, t =>
                        {
                            t.HasCheckConstraint("CK_OrderItems_Id", "\"Id\" > 0");

                            t.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" BETWEEN 1 AND 2147483647");

                            t.HasCheckConstraint("CK_OrderItems_UnitPrice", "typeof(\"UnitPrice\") = 'integer' AND \"UnitPrice\" > 0");
                        });
                });

            modelBuilder.Entity("Data.Entities.Customer", b =>
                {
                    b.HasOne("Data.Entities.LoyaltyLevel", "LoyaltyLevel")
                        .WithMany("Customers")
                        .HasForeignKey("LoyaltyLevelId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("LoyaltyLevel");
                });

            modelBuilder.Entity("Data.Entities.DishIngredient", b =>
                {
                    b.HasOne("Data.Entities.Dish", "Dish")
                        .WithMany("DishIngredients")
                        .HasForeignKey("DishId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Data.Entities.Ingredient", "Ingredient")
                        .WithMany("DishIngredients")
                        .HasForeignKey("IngredientId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Dish");

                    b.Navigation("Ingredient");
                });

            modelBuilder.Entity("Data.Entities.MenuItem", b =>
                {
                    b.HasOne("Data.Entities.Dish", "Dish")
                        .WithMany("MenuItems")
                        .HasForeignKey("DishId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("Dish");
                });

            modelBuilder.Entity("Data.Entities.Order", b =>
                {
                    b.HasOne("Data.Entities.Customer", "Customer")
                        .WithMany("Orders")
                        .HasForeignKey("CustomerId")
                        .OnDelete(DeleteBehavior.ClientNoAction);

                    b.Navigation("Customer");
                });

            modelBuilder.Entity("Data.Entities.OrderItem", b =>
                {
                    b.HasOne("Data.Entities.MenuItem", "MenuItem")
                        .WithMany("OrderItems")
                        .HasForeignKey("MenuItemId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.HasOne("Data.Entities.Order", "Order")
                        .WithMany("Items")
                        .HasForeignKey("OrderId")
                        .OnDelete(DeleteBehavior.Restrict)
                        .IsRequired();

                    b.Navigation("MenuItem");

                    b.Navigation("Order");
                });

            modelBuilder.Entity("Data.Entities.Customer", b =>
                {
                    b.Navigation("Orders");
                });

            modelBuilder.Entity("Data.Entities.Dish", b =>
                {
                    b.Navigation("DishIngredients");

                    b.Navigation("MenuItems");
                });

            modelBuilder.Entity("Data.Entities.Ingredient", b =>
                {
                    b.Navigation("DishIngredients");
                });

            modelBuilder.Entity("Data.Entities.LoyaltyLevel", b =>
                {
                    b.Navigation("Customers");
                });

            modelBuilder.Entity("Data.Entities.MenuItem", b =>
                {
                    b.Navigation("OrderItems");
                });

            modelBuilder.Entity("Data.Entities.Order", b =>
                {
                    b.Navigation("Items");
                });
#pragma warning restore 612, 618
        }
    }
}
