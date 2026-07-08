using CardsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardsApi.Infrastructure.Data.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> e)
    {
        e.ToTable("cards");
        e.HasKey(c => c.Id);
        e.Property(c => c.Id).HasColumnName("id");
        e.Property(c => c.UserId).HasColumnName("user_id");
        e.Property(c => c.CardholderName).HasColumnName("cardholder_name").HasMaxLength(200).IsRequired();
        e.Property(c => c.Nickname).HasColumnName("nickname").HasMaxLength(100).IsRequired();
        e.Property(c => c.Brand).HasColumnName("brand").HasMaxLength(50).IsRequired();
        e.Property(c => c.CardNumberEncrypted).HasColumnName("card_number_encrypted").IsRequired();
        e.Property(c => c.CardNumberFirst4).HasColumnName("card_number_first4").HasMaxLength(4).IsRequired();
        e.Property(c => c.CardNumberLast4).HasColumnName("card_number_last4").HasMaxLength(4).IsRequired();
        e.Property(c => c.PinEncrypted).HasColumnName("pin_encrypted").IsRequired();
        e.Property(c => c.ExpirationDate).HasColumnName("expiration_date");
        e.Property(c => c.CreditLimit).HasColumnName("credit_limit").HasColumnType("numeric(14,2)");
        e.Property(c => c.Status)
            .HasColumnName("status")
            .IsRequired();
        e.Property(c => c.CreatedAt).HasColumnName("created_at");
        e.Property(c => c.UpdatedAt).HasColumnName("updated_at");
        e.Property(c => c.IsDeleted).HasColumnName("is_deleted");
        e.Property(c => c.DeletedAt).HasColumnName("deleted_at");

        e.HasOne(c => c.User)
            .WithMany(u => u.Cards)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        e.HasIndex(c => new { c.UserId, c.IsDeleted, c.CreatedAt });
        e.HasIndex(c => new { c.UserId, c.ExpirationDate });

        e.HasQueryFilter(c => !c.IsDeleted);

        e.ToTable(t => t.HasCheckConstraint("ck_cards_status", "status IN (0, 1, 2)"));
        e.ToTable(t => t.HasCheckConstraint("ck_cards_credit_limit", "credit_limit >= 0"));
    }
}
