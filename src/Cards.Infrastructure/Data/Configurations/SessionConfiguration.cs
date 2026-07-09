using Cards.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Cards.Infrastructure.Data.Configurations;

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> e)
    {
        e.ToTable("sessions");
        e.HasKey(s => s.Id);
        e.Property(s => s.Id).HasColumnName("id");
        e.Property(s => s.UserId).HasColumnName("user_id");
        e.Property(s => s.CreatedAt).HasColumnName("created_at");
        e.Property(s => s.ExpiresAt).HasColumnName("expires_at");
        e.Property(s => s.RevokedAt).HasColumnName("revoked_at");
        e.Property(s => s.ReplacedBySessionId).HasColumnName("replaced_by_session_id");

        e.HasOne(s => s.User)
            .WithMany(u => u.Sessions)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        e.HasIndex(s => new { s.UserId, s.ExpiresAt });
    }
}
