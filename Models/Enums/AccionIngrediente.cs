namespace EatWeb.Models.Enums;

public static class AccionIngrediente
{
    public const string Quitar = nameof(Quitar);
    public const string Agregar = nameof(Agregar);

    public static IEnumerable<string> Todos() =>
        new[] { Quitar, Agregar };
}
