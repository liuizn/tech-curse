using System.ComponentModel.DataAnnotations;

namespace TechCurse.src.Application.DTOs;
public record EnrollmentInputDto([Required] int CourseId, int StudentId);