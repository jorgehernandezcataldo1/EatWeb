namespace EatWeb.Models;

public static class Roles
{
    public const string Admin = nameof(Admin);
    public const string Garzon = nameof(Garzon);
    public const string AdminOGarzon = $"{Admin},{Garzon}";
}
