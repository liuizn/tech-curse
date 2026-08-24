using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Courses.Queries.GetCourseById;

public record GetCourseByIdQuery(int Id) : IRequest<CourseOutputDto>;
