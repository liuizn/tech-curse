using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Entities; 

namespace TechCurse.src.Application.Interfaces;

public interface IStudentRepository
{
    Task<(IEnumerable<Student> Items, int TotalCount)> GetPagedAsync(PaginationParamsDto searchParams);
    Task<IEnumerable<Student>> GetAllAsync();
    Task<Student?> GetByIdAsync(int id);
    Task<Student?> GetByEmailAsync(string email);
    Task<IEnumerable<CourseStudentOutputDto>> GetCoursesAsync(Student student);
    Task AddAsync(Student student);
    Task UpdateAsync(Student student);
    Task DeleteAsync(Student student);
    Task<bool> EmailExistsAsync(string email);
    Task<bool> StudentIsActiveAsync(Student student);
}