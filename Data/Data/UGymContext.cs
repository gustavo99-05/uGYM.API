using Microsoft.EntityFrameworkCore;
using uGYM.API.Models;

namespace uGYM.API.Data
{
    public class UGymContext : DbContext
    {
        public UGymContext(DbContextOptions<UGymContext> options)
            : base(options)
        {
        }

        public DbSet<Aluno> Alunos { get; set; }

        public DbSet<Pagamento> Pagamentos { get; set; }

        public DbSet<CheckIn> CheckIns { get; set; }
    }
}