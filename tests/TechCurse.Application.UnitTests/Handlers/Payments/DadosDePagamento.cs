using TechCurse.Domain.Entities;

namespace TechCurse.Application.UnitTests.Handlers.Payments;

public static class DadosDePagamento
{
    public const int CursoId = 7;
    public const string CursoTitulo = "Curso de Teste";

    public static Enrollment MatriculaComCurso(int enrollmentId = 1, int studentId = 2)
    {
        return new Enrollment
        {
            EnrollmentId = enrollmentId,
            StudentId = studentId,
            CourseId = CursoId,
            Course = new Course
            {
                CourseId = CursoId,
                Titulo = CursoTitulo,
                Descricao = "Descrição",
                Categoria = "Tech",
                CargaHoraria = 10
            }
        };
    }
}
