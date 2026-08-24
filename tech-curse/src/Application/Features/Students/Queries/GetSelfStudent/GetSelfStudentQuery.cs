using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Students.Queries.GetSelfStudent;

public record GetSelfStudentQuery() : IRequest<StudentOutputDto>;
