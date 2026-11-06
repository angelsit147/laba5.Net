using Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Data.Data.Configurations;

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers", table =>
        {
            table.HasCheckConstraint("CK_Customers_Id", "\"Id\" > 0");
        });
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(entity => entity.LastName).IsRequired().HasMaxLength(100);
        builder.Property(entity => entity.PhoneNumber).HasMaxLength(32);
        builder.Property(entity => entity.LoyaltyCode).IsRequired().HasMaxLength(32).UseCollation("BINARY");
        builder.HasOne(entity => entity.LoyaltyLevel).WithMany(entity => entity.Customers)
            .HasForeignKey(entity => entity.LoyaltyLevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(entity => entity.LoyaltyCode).IsUnique().HasDatabaseName("UX_Customers_LoyaltyCode");
        builder.HasIndex(entity => entity.LoyaltyLevelId).HasDatabaseName("IX_Customers_LoyaltyLevelId");
    }
}
