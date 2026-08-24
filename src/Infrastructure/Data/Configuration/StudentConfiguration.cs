using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TechCurse.Domain.Entities;

namespace TechCurse.Infrastructure.Data.Configuration;

public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        // Garante que a coluna de e-mail seja única no banco de dados
        builder.HasIndex(s => s.Email)
               .IsUnique();

        // Opcional, mas recomendado: definir um tamanho máximo para o campo
        builder.Property(s => s.Email)
               .IsRequired()
               .HasMaxLength(150);
    }
}
