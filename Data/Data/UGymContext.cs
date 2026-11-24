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

        public DbSet<Usuario> Usuarios { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Usuario>().HasIndex(x => x.NomeNormalizado).IsUnique();
            modelBuilder.Entity<CheckIn>().HasIndex(x => new { x.AlunoId, x.DiaLocal })
                .IsUnique().HasFilter("[DiaLocal] IS NOT NULL AND [Permitido] = 1");
            modelBuilder.Entity<CheckIn>().HasIndex(x => new { x.AlunoId, x.DataHora });
            modelBuilder.Entity<Pagamento>().Property(x => x.Valor).HasPrecision(18, 2);
        }
    }
}
