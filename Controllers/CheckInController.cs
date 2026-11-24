using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using uGYM.API.Data;
using uGYM.API.DTOs;
using uGYM.API.Services;
namespace uGYM.API.Controllers;
[ApiController, Route("api/CheckIn"), Authorize]
public class CheckInController(UGymContext db) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarCheckInDto dto)
    {
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            // Serializes check-ins with each other and status updates, including legacy rows.
            var aluno = await db.Alunos.FromSqlInterpolated($"SELECT * FROM [Alunos] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {dto.AlunoId}").SingleOrDefaultAsync();
            if (aluno is null) return NotFound(new { mensagem = "Aluno não encontrado." });
            if (!aluno.Ativo) return Conflict(new { mensagem = "Aluno inativo. Ative a matrícula antes do check-in." });
            var agora = DateTime.UtcNow;
            var dia = RelogioAcademia.Dia(agora);
            var inicio = RelogioAcademia.InicioUtc(dia);
            var fim = RelogioAcademia.InicioUtc(dia.AddDays(1));
            if (await db.CheckIns.AnyAsync(x => x.AlunoId == dto.AlunoId && x.Permitido && x.DataHora >= inicio && x.DataHora < fim))
                return Conflict(new { mensagem = "Este aluno já fez check-in hoje." });
            var entrada = new CheckIn { AlunoId = dto.AlunoId, DataHora = agora, DiaLocal = dia, Permitido = true,
                OperadorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!) };
            db.CheckIns.Add(entrada);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            return Created($"/api/CheckIn/aluno/{dto.AlunoId}", new EntradaDto(entrada.Id, entrada.AlunoId, agora, entrada.OperadorId));
        }
        catch (DbUpdateException e) when (e.InnerException is SqlException { Number: 2601 or 2627 })
        { return Conflict(new { mensagem = "Este aluno já fez check-in hoje." }); }
        catch (SqlException e) when (e.Number == 1205)
        { return Conflict(new { mensagem = "Outra operação está em andamento. Tente novamente." }); }
    }
    [HttpGet("resumo")]
    public async Task<IActionResult> Resumo()
    {
        var dia = RelogioAcademia.Dia(DateTime.UtcNow);
        var inicio = RelogioAcademia.InicioUtc(dia);
        var fim = RelogioAcademia.InicioUtc(dia.AddDays(1));
        return Ok(new { totalAlunos = await db.Alunos.CountAsync(), alunosAtivos = await db.Alunos.CountAsync(x => x.Ativo),
            checkInsHoje = await db.CheckIns.CountAsync(x => x.Permitido && x.DataHora >= inicio && x.DataHora < fim), diaLocal = dia });
    }
    [HttpGet("aluno/{alunoId:int}/resumo")]
    public async Task<IActionResult> ResumoAluno(int alunoId)
    {
        if (!await db.Alunos.AnyAsync(x => x.Id == alunoId)) return NotFound(new { mensagem = "Aluno não encontrado." });
        var dia = RelogioAcademia.Dia(DateTime.UtcNow);
        var mes = RelogioAcademia.InicioUtc(new DateOnly(dia.Year, dia.Month, 1));
        var fimMes = RelogioAcademia.InicioUtc(new DateOnly(dia.Year, dia.Month, 1).AddMonths(1));
        var q = db.CheckIns.AsNoTracking().Where(x => x.AlunoId == alunoId && x.Permitido);
        var ultimo = await q.MaxAsync(x => (DateTime?)x.DataHora);
        return Ok(new ResumoAlunoDto(await q.CountAsync(), await q.CountAsync(x => x.DataHora >= mes && x.DataHora < fimMes),
            ultimo.HasValue ? RelogioAcademia.ComoUtc(ultimo.Value) : null));
    }
    [HttpGet("aluno/{alunoId:int}")]
    public async Task<IActionResult> Historico(int alunoId, string periodo = "todos", int pagina = 1, int tamanho = 20)
    {
        if (pagina < 1 || pagina > 100000 || tamanho is < 1 or > 100 || !new[] { "hoje", "7dias", "mes", "todos" }.Contains(periodo))
            return BadRequest(new { mensagem = "Filtro ou paginação inválidos." });
        if (!await db.Alunos.AnyAsync(x => x.Id == alunoId)) return NotFound(new { mensagem = "Aluno não encontrado." });
        var dia = RelogioAcademia.Dia(DateTime.UtcNow);
        DateOnly? de = periodo switch { "hoje" => dia, "7dias" => dia.AddDays(-6), "mes" => new(dia.Year, dia.Month, 1), _ => null };
        var q = db.CheckIns.AsNoTracking().Where(x => x.AlunoId == alunoId && x.Permitido);
        if (de.HasValue)
        {
            var inicio = RelogioAcademia.InicioUtc(de.Value);
            var fim = RelogioAcademia.InicioUtc(dia.AddDays(1));
            q = q.Where(x => x.DataHora >= inicio && x.DataHora < fim);
        }
        var total = await q.CountAsync();
        var rows = await q.OrderByDescending(x => x.DataHora).ThenByDescending(x => x.Id).Skip((pagina - 1) * tamanho).Take(tamanho).ToListAsync();
        return Ok(new { total, pagina, tamanho, itens = rows.Select(x => new EntradaDto(x.Id, x.AlunoId, RelogioAcademia.ComoUtc(x.DataHora), x.OperadorId)) });
    }
}
