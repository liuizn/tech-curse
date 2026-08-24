using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Courses.Commands.CreateCourse;

public record CreateCourseCommand(string Titulo, string Descricao, string Categoria, int CargaHoraria) : IRequest<CourseOutputDto>;
