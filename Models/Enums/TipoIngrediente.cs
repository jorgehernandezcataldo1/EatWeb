namespace EatWeb.Models.Enums;

public static class TipoIngrediente
{
    public const string Incluido = nameof(Incluido);
    public const string Extra = nameof(Extra);

    public static IEnumerable<string> Todos() =>
        new[] { Incluido, Extra };
}
