using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Students.Queries.GetStudentEnrollments;

public record GetStudentEnrollmentsQuery(int Id) : IRequest<IEnumerable<CourseStudentOutputDto>>;
