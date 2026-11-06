using Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Data.Configurations;

public class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems", table =>
        {
            table.HasCheckConstraint("CK_MenuItems_Id", "\"Id\" > 0");
            table.HasCheckConstraint("CK_MenuItems_Price", "typeof(\"Price\") = 'integer' AND \"Price\" > 0");
            table.HasCheckConstraint("CK_MenuItems_IsAvailable", "\"IsAvailable\" IN (0, 1)");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.PortionName).IsRequired().HasMaxLength(100);
        builder.Property(entity => entity.PortionDescription).IsRequired().HasMaxLength(500);
        builder.Property(entity => entity.Price).HasConversion(ScaledDecimalConverters.Money).HasColumnType("INTEGER");
        builder.HasOne(entity => entity.Dish).WithMany(entity => entity.MenuItems)
            .HasForeignKey(entity => entity.DishId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.DishId).HasDatabaseName("IX_MenuItems_DishId");
    }
}
