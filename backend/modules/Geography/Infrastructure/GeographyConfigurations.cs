using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SeniorConnect.Modules.Geography.Domain;

namespace SeniorConnect.Modules.Geography.Infrastructure;

public sealed class AustrianAdministrativeUnitConfiguration : IEntityTypeConfiguration<AustrianAdministrativeUnit>
{
    public void Configure(EntityTypeBuilder<AustrianAdministrativeUnit> builder)
    {
        builder.ToTable("austrian_administrative_units", t =>
        {
            t.HasCheckConstraint("ck_austria_active", "is_active IN (true, false)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BundeslandCode).HasColumnName("bundesland_code").IsRequired();
        builder.Property(x => x.BundeslandName).HasColumnName("bundesland_name").IsRequired();
        builder.Property(x => x.BezirkCode).HasColumnName("bezirk_code").IsRequired();
        builder.Property(x => x.BezirkName).HasColumnName("bezirk_name").IsRequired();
        builder.Property(x => x.GemeindeCode).HasColumnName("gemeinde_code").IsRequired();
        builder.Property(x => x.GemeindeName).HasColumnName("gemeinde_name").IsRequired();
        builder.Property(x => x.PostalCode).HasColumnName("postal_code").IsRequired();
        builder.Property(x => x.LocalityName).HasColumnName("locality_name").IsRequired();
        builder.Property(x => x.Latitude).HasColumnName("latitude").HasColumnType("numeric(9,6)").IsRequired();
        builder.Property(x => x.Longitude).HasColumnName("longitude").HasColumnType("numeric(9,6)").IsRequired();
        builder.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();

        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc").HasColumnType("timestamptz").HasDefaultValueSql("now()").IsRequired();
        builder.Property(x => x.UpdatedBy).HasColumnName("updated_by");

        builder.HasIndex(x => x.PostalCode).HasDatabaseName("ix_austria_geo_plz");
        builder.HasIndex(x => x.GemeindeName).HasDatabaseName("ix_austria_geo_gemeinde");
        builder.HasIndex(x => new { x.Latitude, x.Longitude }).HasDatabaseName("ix_austria_geo_coords");
        builder.HasIndex(x => x.BundeslandCode).HasDatabaseName("ix_austria_bundesland");
        builder.HasIndex(x => x.BezirkCode).HasDatabaseName("ix_austria_bezirk");
        builder.HasIndex(x => x.GemeindeCode).HasDatabaseName("ix_austria_gemeinde_code");
    }
}