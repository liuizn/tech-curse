using System.Reflection;
using TechCurse.src.API.Controllers;
using TechCurse.src.Application.Features.Courses.Commands.CreateCourse;
using TechCurse.src.Domain.Entities;
using TechCurse.src.Infrastructure.Data;

namespace TechCurse.Test.Architecture.Common;

public static class ArchitectureConstants
{
    public const string DomainNamespace = "TechCurse.src.Domain";
    public const string ApplicationNamespace = "TechCurse.src.Application";
    public const string InfrastructureNamespace = "TechCurse.src.Infrastructure";
    public const string ApiNamespace = "TechCurse.src.API";

    public static readonly Assembly DomainAssembly = typeof(Course).Assembly;
    public static readonly Assembly ApplicationAssembly = typeof(CreateCourseCommand).Assembly;
    public static readonly Assembly InfrastructureAssembly = typeof(TechCurseContext).Assembly;
    public static readonly Assembly ApiAssembly = typeof(CourseController).Assembly;
}
