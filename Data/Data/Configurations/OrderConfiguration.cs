using Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders", table =>
        {
            table.HasCheckConstraint("CK_Orders_Id", "\"Id\" > 0");
            table.HasCheckConstraint("CK_Orders_Status", "\"Status\" IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_Orders_OrderType", "\"OrderType\" IN (0, 1, 2)");
            table.HasCheckConstraint("CK_Orders_DiscountPercentFixed", "\"DiscountPercentFixed\" IS NULL OR \"DiscountPercentFixed\" BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_Orders_StatusDiscount", "(\"Status\" = 0 AND \"DiscountPercentFixed\" IS NULL) OR (\"Status\" IN (1, 2) AND \"DiscountPercentFixed\" IS NOT NULL) OR \"Status\" = 3");
            table.HasCheckConstraint("CK_Orders_GuestDiscount", "\"CustomerId\" IS NOT NULL OR \"DiscountPercentFixed\" IS NULL OR \"DiscountPercentFixed\" = 0");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.CreatedAt).HasConversion(
            value => RequireUtc(value),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc)).HasColumnType("TEXT");
        builder.Property(entity => entity.Status).HasConversion<int>();
        builder.Property(entity => entity.OrderType).HasConversion<int>();
        builder.Property(entity => entity.DiscountPercentFixed).HasConversion(ScaledDecimalConverters.Percent)
            .HasColumnType("INTEGER");
        builder.HasOne(entity => entity.Customer).WithMany(entity => entity.Orders)
            .HasForeignKey(entity => entity.CustomerId).OnDelete(DeleteBehavior.ClientNoAction);
        builder.HasIndex(entity => new { entity.CustomerId, entity.Status })
            .HasDatabaseName("IX_Orders_CustomerId_Status");
    }

    private static DateTime RequireUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("CreatedAt must be UTC.", nameof(value));
        }

        return value;
    }
}
