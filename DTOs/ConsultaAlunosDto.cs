using System.ComponentModel.DataAnnotations;

namespace uGYM.API.DTOs;

public class ConsultaAlunosDto
{
    [Range(1, 100000, ErrorMessage = "A página deve estar entre 1 e 100000.")]
    public int Pagina { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
    public int Tamanho { get; set; } = 20;

    [StringLength(150, ErrorMessage = "A busca deve ter no máximo 150 caracteres.")]
    public string? Busca { get; set; }

    public bool? Ativo { get; set; }

    [RegularExpression("^(mobile|desktop)$", ErrorMessage = "A visão deve ser mobile ou desktop.")]
    public string? Visao { get; set; }
}
