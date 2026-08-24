using MediatR;

namespace TechCurse.src.Application.Features.Students.Commands.UpdateStudent;

public record UpdateStudentCommand(int Id, string Nome) : IRequest;
