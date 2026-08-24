using System.Reflection;
using TechCurse.Api.Controllers;
using TechCurse.Application.Features.Courses.Commands.CreateCourse;
using TechCurse.Domain.Entities;
using TechCurse.Infrastructure.Data;

namespace TechCurse.ArchitectureTests.Common;

public static class ArchitectureConstants
{
    public const string DomainNamespace = "TechCurse.Domain";
    public const string ApplicationNamespace = "TechCurse.Application";
    public const string InfrastructureNamespace = "TechCurse.Infrastructure";
    public const string ApiNamespace = "TechCurse.Api";

    public static readonly Assembly DomainAssembly = typeof(Course).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(CreateCourseCommand).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(TechCurseContext).Assembly;
    public static readonly Assembly ApiAssembly = typeof(CourseController).Assembly;
}
