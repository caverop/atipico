using Atipico.Application.Common;

namespace Atipico.Application.Tests
{
    // El parser es el unico pedazo de la dirección de entrega donde puede haber un error de
    // verdad: el resto son columnas anulables. Es logica pura, asi que se prueba entera.
    public class UbicacionCompartidaTest
    {
        // Los formatos de docs/direccion-entrega.md §1, que son los que produce WhatsApp
        // segun por donde abra el mesero la tarjeta de ubicacion.
        [Theory]
        [InlineData("https://maps.google.com/maps?q=-17.783241,-63.182140")]
        [InlineData("https://www.google.com/maps/search/?api=1&query=-17.783241,-63.182140")]
        [InlineData("https://www.google.com/maps/@-17.783241,-63.182140,17z")]
        [InlineData("-17.783241,-63.182140")]
        [InlineData("-17.783241, -63.182140")]
        [InlineData("Llegó esto: https://maps.google.com/maps?q=-17.783241,-63.182140 ¿sirve?")]
        public void ExtraeElPuntoDeLosFormatosDeWhatsApp(string texto)
        {
            var pudo = UbicacionCompartida.TryExtraer(texto, out var lat, out var lng);

            Assert.True(pudo);
            Assert.Equal(-17.783241m, lat);
            Assert.Equal(-63.182140m, lng);
        }

        // El ",17z" del nivel de zoom va pegado a la longitud y no tiene parte decimal: no
        // debe confundirse con un tercer numero ni cortar el par.
        [Fact]
        public void ElNivelDeZoomNoSeConfundeConUnaCoordenada()
        {
            UbicacionCompartida.TryExtraer("https://www.google.com/maps/@-17.783241,-63.182140,17z", out _, out var lng);

            Assert.Equal(-63.182140m, lng);
        }

        // Un enlace corto no trae las coordenadas. No es un error: el texto se guarda igual
        // y una persona lo abre (§2 del spec).
        [Theory]
        [InlineData("https://maps.app.goo.gl/AbCdEfGhIjK")]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("la casa verde de la esquina")]
        [InlineData("mi telefono es 78945612")]
        public void DevuelveFalseCuandoNoHayPunto(string? texto)
        {
            var pudo = UbicacionCompartida.TryExtraer(texto, out var lat, out var lng);

            Assert.False(pudo);
            Assert.Equal(0m, lat);
            Assert.Equal(0m, lng);
        }

        // Un par fuera de rango no es un punto: se saltea y se sigue buscando, en vez de
        // devolver algo que la base va a rechazar con ck_pedido_latitud.
        [Fact]
        public void SalteaUnParFueraDeRangoYSigueBuscando()
        {
            var pudo = UbicacionCompartida.TryExtraer(
                "version 200.5,300.75 del mapa — el punto es -17.783241,-63.182140",
                out var lat, out var lng);

            Assert.True(pudo);
            Assert.Equal(-17.783241m, lat);
            Assert.Equal(-63.182140m, lng);
        }

        [Theory]
        [InlineData(-17.78, -63.18, false)]   // Santa Cruz
        [InlineData(-16.5, -68.15, false)]    // La Paz
        [InlineData(-63.18, -17.78, true)]    // invertidas: el error clasico
        [InlineData(0, 0, true)]              // GPS sin señal: pasa los rangos, no es Bolivia
        [InlineData(40.71, -74.0, true)]      // Nueva York
        public void DetectaLosPuntosQueNoEstanEnBolivia(double lat, double lng, bool esperado)
        {
            Assert.Equal(esperado, UbicacionCompartida.FueraDeBolivia((decimal)lat, (decimal)lng));
        }

        [Theory]
        [InlineData(-90, -180, true)]
        [InlineData(90, 180, true)]
        [InlineData(-90.1, 0, false)]
        [InlineData(0, 180.1, false)]
        public void ValidaLosRangosDelSistemaDeCoordenadas(double lat, double lng, bool esperado)
        {
            Assert.Equal(esperado, UbicacionCompartida.EnRango((decimal)lat, (decimal)lng));
        }

        // El enlace se arma con InvariantCulture: si tomara la cultura del servidor, una que
        // use coma decimal generaria "q=-17,783241,-63,182140" y el mapa abriria en cualquier
        // lado.
        [Fact]
        public void ElEnlaceUsaPuntoDecimalSiempre()
        {
            var enlace = UbicacionCompartida.EnlaceMapa(-17.783241m, -63.182140m);

            Assert.Equal("https://www.google.com/maps?q=-17.783241,-63.182140", enlace);
        }
    }
}
