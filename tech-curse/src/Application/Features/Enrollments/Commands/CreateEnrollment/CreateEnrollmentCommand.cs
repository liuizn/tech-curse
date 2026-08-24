using MediatR;

namespace TechCurse.src.Application.Features.Enrollments.Commands.CreateEnrollment;

public record CreateEnrollmentCommand(int StudentId, int CourseId) : IRequest;
