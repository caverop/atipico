using Atipico.Domain.Interfaces;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using EntityTable = Atipico.Web.Components.Shared.EntityTable<Atipico.Web.Tests.EntityTableDetalleTests.Cosa>;

namespace Atipico.Web.Tests;

/// <summary>
/// SCRUM-13, ampliación §8.2 de specs/rol-delivery.md — CASO 10 del plan §10.1.
///
/// La ampliación separa "poder ABRIR la ficha de una fila" de "poder escribir" con dos
/// parámetros nuevos (<c>DetalleRoles</c>, <c>DetalleLabel</c>), donde <c>DetalleRoles</c> cae
/// en <c>WriteRoles</c> si no se declara. Este archivo fija el comportamiento SIN declararlos:
/// es la red que protege a las otras ocho grillas de la aplicación, que no van a pasar ninguno
/// de los dos.
///
/// Se prueba contra <c>EntityTable</c> directamente con una entidad de prueba y no apoyándose
/// en una grilla real: las páginas existentes traen su propio cableado (filtros, turno, orden)
/// que no tiene nada que ver con lo que acá importa, y con una entidad propia las columnas y
/// los índices quedan bajo control del test.
///
/// A diferencia de los casos 7–9 y 11, este test NACE EN VERDE: describe lo que la grilla ya
/// hace hoy. Es deliberado — su valor es fallar el día que <c>DetalleRolesCsv</c> reemplace a
/// <c>RolesCsv</c> de más y encienda "+ Nuevo" o el botón de borrar para quien solo mira.
/// </summary>
public class EntityTableDetalleTests : BunitContext
{
    public sealed class Cosa : IEntity
    {
        public long Id { get; set; }
        public string Nombre { get; set; } = "";
    }

    private const long IdCosa = 3;

    private IRenderedComponent<EntityTable> RenderizarGrilla(string rol)
    {
        var auth = this.AddAuthorization();
        auth.SetAuthorized($"{rol.ToLowerInvariant()}-de-prueba");
        auth.SetRoles(rol);

        return Render<EntityTable>(parameters => parameters
            .Add(p => p.Title, "Cosas")
            .Add(p => p.Items, [new Cosa { Id = IdCosa, Nombre = "Tres" }])
            .Add(p => p.Columns,
            [
                ("Id", (Func<Cosa, object?>)(c => c.Id)),
                ("Nombre", (Func<Cosa, object?>)(c => c.Nombre)),
            ])
            .Add(p => p.CreateUrl, "/cosas/nuevo")
            .Add(p => p.EditUrlPrefix, "/cosas")
            .Add(p => p.WriteRoles, ["Admin"]));
        // DetalleRoles y DetalleLabel NO se pasan: ese es el punto del caso 10.
    }

    [Fact]
    public void SinDetalleRoles_UnRolQueEscribe_VeAbrirCrearYBorrar()
    {
        var cut = RenderizarGrilla("Admin");

        // "+ Nuevo" sigue gobernado por WriteRoles.
        Assert.Contains(cut.FindAll("a"), a => a.TextContent.Trim() == "+ Nuevo");

        // Encabezado: las dos columnas más la columna vacía de acciones.
        Assert.Equal(3, cut.FindAll("table.table thead th").Count);

        // Celda de acciones: el enlace a la ficha, etiquetado "Editar" por defecto, y el
        // botón de borrar.
        var accion = cut.Find($"table.table tbody tr a[href='/cosas/{IdCosa}']");
        Assert.Equal("Editar", accion.TextContent.Trim());
        Assert.Contains(cut.FindAll("table.table tbody tr button"), b => b.TextContent.Trim() == "Eliminar");

        // Móvil: la tarjeta entera es un enlace a la ficha.
        Assert.Equal($"/cosas/{IdCosa}", cut.Find(".entity-cards .entity-card-link").GetAttribute("href"));
        Assert.Empty(cut.FindAll(".entity-card-plana"));
    }

    [Fact]
    public void SinDetalleRoles_UnRolQueNoEscribe_NoVeNiAbrirNiCrearNiBorrar()
    {
        var cut = RenderizarGrilla("Cocinero");

        Assert.DoesNotContain(cut.FindAll("a"), a => a.TextContent.Trim() == "+ Nuevo");

        // Sin permiso no hay columna de acciones, ni en el <th> ni en el <td>: los dos tienen
        // que moverse juntos o la tabla queda con una columna de diferencia entre encabezado y
        // cuerpo (§8.2).
        Assert.Equal(2, cut.FindAll("table.table thead th").Count);
        Assert.Equal(2, cut.FindAll("table.table tbody tr td").Count);
        Assert.Empty(cut.FindAll($"table.table tbody tr a[href='/cosas/{IdCosa}']"));

        // Móvil: la fila queda plana, sin enlace.
        Assert.Empty(cut.FindAll(".entity-card-link"));
        Assert.Single(cut.FindAll(".entity-cards .entity-card-plana"));
    }
}
