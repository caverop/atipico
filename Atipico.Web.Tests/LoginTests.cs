using Atipico.Web.Components.Pages;
using Bunit;

namespace Atipico.Web.Tests;

/// <summary>
/// CA-2 de specs/pruebas-blazor-marca-login.md: el encabezado del login muestra la marca
/// del producto. Solo se mira el texto del &lt;h1&gt;; el CSS queda fuera de alcance (§2.3).
/// </summary>
public class LoginTests : BunitContext
{
    [Fact]
    public void Login_MuestraLaMarcaAtipicoEnElEncabezado()
    {
        var cut = Render<Login>();

        var encabezado = cut.Find("h1");

        Assert.Equal("ATIPICO3", encabezado.TextContent);
    }
}
