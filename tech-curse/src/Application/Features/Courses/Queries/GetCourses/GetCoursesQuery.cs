using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Courses.Queries.GetCourses;

public record GetCoursesQuery(CoursePaginationParamsDto SearchParams) : IRequest<PagedResultDto<CourseOutputDto>>;
