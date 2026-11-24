using System.ComponentModel.DataAnnotations;
using uGYM.API.Services;

namespace uGYM.API.DTOs;

public class CadastrarAlunoDto : IValidatableObject
{
    [Required(ErrorMessage = "Informe o nome do aluno.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 150 caracteres.")]
    public string Nome { get; set; } = "";

    [Required(ErrorMessage = "Informe o CPF do aluno.")]
    [StringLength(20, ErrorMessage = "Informe um CPF válido.")]
    public string CPF { get; set; } = "";

    [StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string? Email { get; set; }

    [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
    public string? Telefone { get; set; }

    [Required(ErrorMessage = "Informe o plano do aluno.")]
    [StringLength(120, ErrorMessage = "O plano deve ter no máximo 120 caracteres.")]
    public string Plano { get; set; } = "";

    [StringLength(120, ErrorMessage = "O instrutor deve ter no máximo 120 caracteres.")]
    public string? Instrutor { get; set; }

    [StringLength(2000, ErrorMessage = "As observações devem ter no máximo 2000 caracteres.")]
    public string? Observacoes { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!string.IsNullOrWhiteSpace(Nome) && Nome.Trim().Length < 2)
            yield return new("O nome deve ter pelo menos 2 caracteres.", new[] { nameof(Nome) });
        if (!string.IsNullOrWhiteSpace(CPF) && !DocumentoCpf.Valido(CPF))
            yield return new("Informe um CPF válido, com 11 dígitos e verificadores corretos.", new[] { nameof(CPF) });
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email.Trim()))
            yield return new("Informe um e-mail válido ou deixe o campo vazio.", new[] { nameof(Email) });
    }
}
