using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using uGYM.API.Data;
using uGYM.API.Models;

namespace uGYM.API.Controllers
{
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
        public async Task<ActionResult<List<Aluno>>> BuscarAlunos()
        {
            var alunos = await _context.Alunos.ToListAsync();
            return Ok(alunos);
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

            return Ok(aluno);
        }

        // POST: api/Aluno
        [HttpPost]
        public async Task<ActionResult> CadastrarAluno(Aluno aluno)
        {
            _context.Alunos.Add(aluno);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensagem = "Aluno cadastrado com sucesso!"
            });
        }
    }
}