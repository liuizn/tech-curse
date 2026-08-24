using MediatR;
using TechCurse.src.Application.DTOs;
using TechCurse.src.Application.Interfaces;
using TechCurse.src.Domain.Enums;
using TechCurse.src.Domain.Exceptions;

namespace TechCurse.src.Application.Features.Students.Queries.GetStudentEnrollments;

public class GetStudentEnrollmentsQueryHandler : IRequestHandler<GetStudentEnrollmentsQuery, IEnumerable<CourseStudentOutputDto>>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetStudentEnrollmentsQueryHandler(
        IStudentRepository studentRepository, 
        ICurrentUserService currentUserService)
    {
        _studentRepository = studentRepository;
        _currentUserService = currentUserService;
    }

    private void ValidateRoleAccess(string targetIdentityUserId)
    {
        var currentUserId = _currentUserService.GetUserId();
        var isAdmin = _currentUserService.IsInRole(UserRole.Admin);

        if (currentUserId != targetIdentityUserId && !isAdmin)
        {
            throw new NotAllowedException("Você não possui permissão suficiente para acessar este registro.");
        }
    }

    public async Task<IEnumerable<CourseStudentOutputDto>> Handle(GetStudentEnrollmentsQuery request, CancellationToken cancellationToken)
    {
        var student = await _studentRepository.GetByIdAsync(request.Id);
        
        if (student == null || student.IsDeleted)
        {
            throw new NotFoundException("Estudante não encontrado.");
        }

        ValidateRoleAccess(student.IdentityUserId);

        return await _studentRepository.GetCoursesAsync(student);
    }
}
