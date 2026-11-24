using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using uGYM.API.Data;
using uGYM.API.DTOs;
using uGYM.API.Models;
using uGYM.API.Services;

namespace uGYM.API.Controllers
{
    [Microsoft.AspNetCore.Authorization.Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class AlunoController : ControllerBase
    {
        private readonly UGymContext _context;

        public AlunoController(UGymContext context)
        {
            _context = context;
        }

        // GET: api/Aluno
        [HttpGet]
        public async Task<IActionResult> BuscarAlunos([FromQuery] ConsultaAlunosDto consulta, CancellationToken cancellationToken)
        {
            var query = _context.Alunos.AsNoTracking();
            if (consulta.Ativo.HasValue) query = query.Where(x => x.Ativo == consulta.Ativo.Value);
            if (!string.IsNullOrWhiteSpace(consulta.Busca))
            {
                var busca = consulta.Busca.Trim();
                query = query.Where(x => EF.Functions.Collate(x.Nome, "Latin1_General_100_CI_AI").Contains(busca));
            }
            var total = await query.CountAsync(cancellationToken);
            var pagina = query.OrderBy(x => x.Nome).ThenBy(x => x.Id)
                .Skip((consulta.Pagina - 1) * consulta.Tamanho).Take(consulta.Tamanho);
            if (consulta.Visao == "mobile")
                return Ok(new { total, consulta.Pagina, consulta.Tamanho,
                    itens = await pagina.Select(x => new { x.Id, x.Nome, x.Plano, x.Ativo }).ToListAsync(cancellationToken) });
            return Ok(new { total, consulta.Pagina, consulta.Tamanho, itens = await pagina.ToListAsync(cancellationToken) });
        }

        // GET: api/Aluno/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Aluno>> BuscarAlunoPorId(int id)
        {
            var aluno = await _context.Alunos.FindAsync(id);

            if (aluno == null)
            {
                return NotFound(new { mensagem = "Aluno não encontrado." });
            }

            aluno.DataCadastro = DateTime.SpecifyKind(aluno.DataCadastro, DateTimeKind.Utc);
            return Ok(aluno);
        }

        // POST: api/Aluno
        [HttpPost]
        public async Task<ActionResult> CadastrarAluno(CadastrarAlunoDto dados)
        {
            var cpf = DocumentoCpf.Normalizar(dados.CPF);
            try
            {
                await using var transacao = await _context.Database.BeginTransactionAsync();
                // Serialize registrations of this CPF without changing legacy records or the schema.
                // The lock is released automatically on commit/rollback, including early returns.
                var recurso = "uGYM:cadastro-aluno:" + cpf;
                await _context.Database.ExecuteSqlInterpolatedAsync($"""
                    DECLARE @resultado int;
                    EXEC @resultado = sys.sp_getapplock @Resource = {recurso},
                        @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = 5000;
                    IF @resultado < 0 THROW 51001, 'Cadastro concorrente. Tente novamente.', 1;
                    """);
                if (await _context.Alunos.AnyAsync(x => x.CPF.Replace(".", "").Replace("-", "").Replace(" ", "") == cpf))
                    return Conflict(new { mensagem = "Já existe um aluno cadastrado com este CPF." });

                var aluno = new Aluno
                {
                    Nome = dados.Nome.Trim(), CPF = cpf, Email = dados.Email?.Trim() ?? "",
                    Telefone = dados.Telefone?.Trim() ?? "", Plano = dados.Plano.Trim(),
                    Instrutor = dados.Instrutor?.Trim() ?? "", Observacoes = dados.Observacoes?.Trim() ?? "",
                    Ativo = true, DataCadastro = DateTime.UtcNow
                };
                _context.Alunos.Add(aluno);
                await _context.SaveChangesAsync();
                await transacao.CommitAsync();
                return Ok(new { mensagem = "Aluno cadastrado com sucesso!" });
            }
            catch (SqlException ex) when (ex.Number == 51001)
            {
                return Conflict(new { mensagem = "Outro cadastro está em andamento para este CPF. Tente novamente." });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult> AtualizarStatusAluno(
            [FromRoute] int id,
            [FromBody] AtualizarStatusAlunoDto dados)
        {
            if (!dados.Ativo.HasValue)
            {
                return BadRequest(new
                {
                    mensagem = "A propriedade 'ativo' é obrigatória e deve ser true ou false."
                });
            }

            var aluno = await _context.Alunos.FindAsync(id);

            if (aluno == null)
            {
                return NotFound(new
                {
                    mensagem = "Aluno não encontrado."
                });
            }

            aluno.Ativo = dados.Ativo.Value;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensagem = "Status do aluno atualizado com sucesso!",
                id = aluno.Id,
                ativo = aluno.Ativo
            });
        }
    }
}
