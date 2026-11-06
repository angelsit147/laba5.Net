using Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Data.Configurations;

public class LoyaltyLevelConfiguration : IEntityTypeConfiguration<LoyaltyLevel>
{
    public void Configure(EntityTypeBuilder<LoyaltyLevel> builder)
    {
        builder.ToTable("LoyaltyLevels", table =>
        {
            table.HasCheckConstraint("CK_LoyaltyLevels_Id", "\"Id\" > 0");
            table.HasCheckConstraint("CK_LoyaltyLevels_DiscountPercent", "\"DiscountPercent\" BETWEEN 0 AND 10000");
            table.HasCheckConstraint("CK_LoyaltyLevels_MinOrders", "\"MinOrders\" BETWEEN 0 AND 2147483647");
            table.HasCheckConstraint("CK_LoyaltyLevels_MinTotalSpent", "typeof(\"MinTotalSpent\") = 'integer' AND \"MinTotalSpent\" >= 0");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.Name).IsRequired().HasMaxLength(100).UseCollation("BINARY");
        builder.Property(entity => entity.DiscountPercent).HasConversion(ScaledDecimalConverters.Percent)
            .HasColumnType("INTEGER");
        builder.Property(entity => entity.MinTotalSpent).HasConversion(ScaledDecimalConverters.Money)
            .HasColumnType("INTEGER");
        builder.HasIndex(entity => entity.Name).IsUnique().HasDatabaseName("UX_LoyaltyLevels_Name");
    }
}
