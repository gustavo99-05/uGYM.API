public class CheckIn
{
    public int Id { get; set; }

    public int AlunoId { get; set; }

    public DateTime DataHora { get; set; }

    public bool Permitido { get; set; }

    // Nullable to preserve historical rows without inventing their timezone.
    public DateOnly? DiaLocal { get; set; }
    public int? OperadorId { get; set; }
}
