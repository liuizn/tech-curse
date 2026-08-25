using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
using TechCurse.Domain.Entities;

namespace TechCurse.Infrastructure.Data;

/// <summary>
/// Contexto principal da aplicação. Implementa <see cref="IDataProtectionKeyContext"/>
/// para que o chaveiro do ASP.NET Data Protection viva no banco, e não no disco
/// efêmero do container — sem isso, todo redeploy invalida cookies e tokens de
/// reset de senha, e cada réplica geraria um chaveiro próprio.
/// </summary>
public class TechCurseContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext
{
    public TechCurseContext(DbContextOptions<TechCurseContext> options) : base(options)
    {
    }

    public DbSet<Course> Courses { get; set; } = null!;
    public DbSet<Student> Students { get; set; } = null!;
    public DbSet<Enrollment> Enrollments { get; set; } = null!;
    public DbSet<Payment> Payments { get; set; } = null!;

    /// <summary>
    /// Tabela do chaveiro do Data Protection. Gerenciada inteiramente pelo
    /// framework — a aplicação nunca lê ou escreve aqui diretamente.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(TechCurseContext).Assembly);

        builder.Entity<Student>().HasQueryFilter(s => !s.IsDeleted);
    }
}
