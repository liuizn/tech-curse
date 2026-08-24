using MediatR;
using TechCurse.Application.DTOs;
using TechCurse.Application.Interfaces;
using TechCurse.Domain.Enums;
using TechCurse.Domain.Exceptions;

namespace TechCurse.Application.Features.Students.Queries.GetStudentById;

public class GetStudentByIdQueryHandler : IRequestHandler<GetStudentByIdQuery, StudentOutputDto>
{
    private readonly IStudentRepository _studentRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetStudentByIdQueryHandler(
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

    public async Task<StudentOutputDto> Handle(GetStudentByIdQuery request, CancellationToken cancellationToken)
    {
        // Bug Fix: Ignoramos o cache de item para leitura direta pois a validação de segurança precisa do IdentityUserId
        // A implementação antiga tinha uma falha grave de segurança que pulava a checagem se o DTO (que não possui IdentityUserId)
        // já estivesse no Cache. O BD será sempre consultado para garantir a segurança. O lookup por PK é leve o suficiente.

        var student = await _studentRepository.GetByIdAsync(request.Id);
        
        if (student == null || student.IsDeleted)
        {
            throw new NotFoundException("Estudante não encontrado.");
        }

        ValidateRoleAccess(student.IdentityUserId);

        return new StudentOutputDto(student.StudentId, student.Nome, student.Email, student.DataCadastro);
    }
}
