using CardsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardsApi.Infrastructure.Data.Configurations;

public class PinAccessLogConfiguration : IEntityTypeConfiguration<PinAccessLog>
{
    public void Configure(EntityTypeBuilder<PinAccessLog> e)
    {
        e.ToTable("pin_access_logs");
        e.HasKey(p => p.Id);
        e.Property(p => p.Id).HasColumnName("id");
        e.Property(p => p.CardId).HasColumnName("card_id");
        e.Property(p => p.UserId).HasColumnName("user_id");
        e.Property(p => p.AccessedAt).HasColumnName("accessed_at");
        e.Property(p => p.Ip).HasColumnName("ip");
        e.HasIndex(p => new { p.CardId, p.AccessedAt });
    }
}
