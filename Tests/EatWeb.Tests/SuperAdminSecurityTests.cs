using System.Reflection;
using EatWeb.Controllers;
using EatWeb.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace EatWeb.Tests;

public class SuperAdminSecurityTests
{
    [Fact]
    public void ControladorSoloPermiteRolSuperAdmin()
    {
        var authorize = typeof(SuperAdminController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(authorize);
        Assert.Equal(Roles.SuperAdmin, authorize!.Roles);
        Assert.DoesNotContain(Roles.AdminCadena, authorize.Roles!);
        Assert.DoesNotContain(Roles.AdminRestaurante, authorize.Roles!);
    }

    [Theory]
    [InlineData("GuardarCadena")]
    [InlineData("GuardarRestaurante")]
    [InlineData("GuardarMiembroRestaurante")]
    [InlineData("GuardarMiembroCadena")]
    [InlineData("EliminarMiembroCadena")]
    [InlineData("CrearUsuario")]
    [InlineData("EstadoUsuario")]
    public void CambiosExigenAntiforgery(string accion)
    {
        var metodo = typeof(SuperAdminController).GetMethod(accion);
        Assert.NotNull(metodo);
        Assert.NotNull(metodo!.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
        Assert.NotNull(metodo.GetCustomAttribute<HttpPostAttribute>());
    }
}
