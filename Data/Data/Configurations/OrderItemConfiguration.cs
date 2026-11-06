using Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Data.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_Id", "\"Id\" > 0");
            table.HasCheckConstraint("CK_OrderItems_Quantity", "\"Quantity\" BETWEEN 1 AND 2147483647");
            table.HasCheckConstraint("CK_OrderItems_UnitPrice", "typeof(\"UnitPrice\") = 'integer' AND \"UnitPrice\" > 0");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.UnitPrice).HasConversion(ScaledDecimalConverters.Money)
            .HasColumnType("INTEGER");
        builder.Property(entity => entity.DishNameFixed).IsRequired().HasMaxLength(100);
        builder.Property(entity => entity.PortionNameFixed).IsRequired().HasMaxLength(100);
        builder.Property(entity => entity.PortionDescriptionFixed).IsRequired().HasMaxLength(500);
        builder.HasOne(entity => entity.Order).WithMany(entity => entity.Items)
            .HasForeignKey(entity => entity.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(entity => entity.MenuItem).WithMany(entity => entity.OrderItems)
            .HasForeignKey(entity => entity.MenuItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => new { entity.OrderId, entity.MenuItemId })
            .IsUnique().HasDatabaseName("UX_OrderItems_OrderId_MenuItemId");
        builder.HasIndex(entity => entity.MenuItemId).HasDatabaseName("IX_OrderItems_MenuItemId");
    }
}
