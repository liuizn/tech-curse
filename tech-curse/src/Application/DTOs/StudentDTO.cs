using TechCurse.src.Domain.Entities;

namespace TechCurse.src.Application.DTOs;

public record StudentPostDto(string Nome, string Email);
public record StudentPutDto(string Nome);
public record StudentOutputDto(int Id, string Nome, string Email, DateTime DataCadastro);
public record CourseStudentOutputDto(int CourseId, string Titulo, string Descricao, string Categoria, bool MatriculaAtiva);