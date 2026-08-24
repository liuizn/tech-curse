using TechCurse.src.Application.DTOs;
using TechCurse.src.Domain.Entities; 

namespace TechCurse.src.Application.Interfaces;

public interface IEnrollmentRepository
{
    Task<Enrollment?> GetByIdAsync(int id);
    Task<Enrollment?> GetByStudentCourseAsync(int studentId, int courseId);
    Task<bool> EnrollmentIsActiveAsync(int id);
    Task<bool> EnrollmentIsActiveAsync(int studentId, int courseId);    
    Task AddAsync(Enrollment enrollment);
}