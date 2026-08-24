using MediatR;
using TechCurse.src.Application.DTOs;

namespace TechCurse.src.Application.Features.Students.Queries.GetStudents;

public record GetStudentsQuery(PaginationParamsDto SearchParams) : IRequest<PagedResultDto<StudentOutputDto>>;
