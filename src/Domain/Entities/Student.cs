using Microsoft.AspNetCore.Identity;

namespace TechCurse.Domain.Entities;

public class Student
{
    public int StudentId { get; set; }
    public string Nome { get; set; }
    public string Email { get; set; }
    public string IdentityUserId { get; set; }
    public IdentityUser IdentityUser { get; set; }
    public DateTime DataCadastro { get; set; }
    public ICollection<Enrollment> Enrollments { get; set; } = new List<Enrollment>();
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
