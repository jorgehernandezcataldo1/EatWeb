namespace EatWeb.Services;

/// <summary>
/// Se coloca en controladores/acciones que requieren una capacidad comercial.
/// Los permisos de personal siguen comprobándose por [Authorize] y membresía.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequiereFuncionalidadAttribute : Attribute
{
    public string Clave { get; }
    public RequiereFuncionalidadAttribute(string clave) => Clave = clave;
}
