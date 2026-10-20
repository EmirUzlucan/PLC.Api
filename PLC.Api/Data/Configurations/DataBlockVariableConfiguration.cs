using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class DataBlockVariableConfiguration
    : IEntityTypeConfiguration<DataBlockVariable>
{
    public void Configure(
        EntityTypeBuilder<DataBlockVariable> builder)
    {
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Name)
            .IsRequired();

        builder.Property(v => v.Path)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(v => v.TypeDefinition)
            .IsRequired();

        builder.HasOne(v => v.DataBlock)
            .WithMany(db => db.Variables)
            .HasForeignKey(v => v.DataBlockId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(v => v.Parent)
            .WithMany(v => v.Children)
            .HasForeignKey(v => v.ParentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(v => new
        {
            v.DataBlockId,
            v.Path
        })
        .IsUnique();
    }
}