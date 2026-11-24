namespace uGYM.API.Services;
public static class RelogioAcademia
{
    public static readonly TimeZoneInfo Fuso = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
    public static DateOnly Dia(DateTime utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Fuso));
    public static DateTime InicioUtc(DateOnly dia) => TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue), Fuso);
    public static DateTime ComoUtc(DateTime valor) => DateTime.SpecifyKind(valor, DateTimeKind.Utc);
}
