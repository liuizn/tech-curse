using MediatR;

namespace TechCurse.src.Application.Features.Courses.Commands.DeleteCourse;

public record DeleteCourseCommand(int Id) : IRequest<Unit>;
