using System.ComponentModel.DataAnnotations;
namespace uGYM.API.DTOs;
public record LoginDto([Required, StringLength(80)] string Usuario, [Required, StringLength(256)] string Senha);
public record RegistrarCheckInDto([Range(1, int.MaxValue)] int AlunoId);
public record EntradaDto(int Id, int AlunoId, DateTime DataHora, int? OperadorId);
public record ResumoAlunoDto(int Total, int MesAtual, DateTime? UltimoCheckIn);
