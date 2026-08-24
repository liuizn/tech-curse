using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Students.Commands.CreateStudent;

public record CreateStudentCommand(string Nome, string Email) : IRequest<StudentOutputDto>;
