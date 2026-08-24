using TechCurse.src.Domain.Entities;

namespace TechCurse.src.Application.DTOs;

public record CoursePostDto(string Titulo, string Descricao, string Categoria, int CargaHoraria);
public record CoursePutDto(int Id, string Titulo, string Descricao, string Categoria, int CargaHoraria);
public record CourseOutputDto(int Id, string Titulo, string Descricao, string Categoria, int CargaHoraria, DateTime DataCriacao);