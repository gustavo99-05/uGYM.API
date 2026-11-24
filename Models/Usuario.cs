using System.ComponentModel.DataAnnotations;
namespace uGYM.API.Models;
public class Usuario
{
    public int Id { get; set; }
    [MaxLength(80)] public string Nome { get; set; } = "";
    [MaxLength(80)] public string NomeNormalizado { get; set; } = "";
    public string SenhaHash { get; set; } = "";
    public bool Ativo { get; set; } = true;
}
