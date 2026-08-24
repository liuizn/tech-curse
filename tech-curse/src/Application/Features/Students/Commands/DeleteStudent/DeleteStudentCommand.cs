using MediatR;

namespace TechCurse.src.Application.Features.Students.Commands.DeleteStudent;

public record DeleteStudentCommand(int Id) : IRequest;
