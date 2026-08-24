using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Students.Queries.GetStudentById;

public record GetStudentByIdQuery(int Id) : IRequest<StudentOutputDto>;
