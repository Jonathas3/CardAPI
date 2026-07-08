using CardsApi.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CardsApi.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("users");
        e.HasKey(u => u.Id);
        e.Property(u => u.Id).HasColumnName("id");
        e.Property(u => u.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        e.Property(u => u.Username).HasColumnName("username").HasMaxLength(100).IsRequired();
        e.HasIndex(u => u.Username).IsUnique();
        e.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        e.Property(u => u.CreatedAt).HasColumnName("created_at");
    }
}
