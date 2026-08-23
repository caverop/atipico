using Atipico.Application.Common;
using System.Globalization;

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

        // Google Maps copia quince decimales; la columna guarda seis. Se redondea al leer y no
        // al guardar, para que la pantalla no muestre un valor que la base va a cambiar: eso
        // se lee como que la aplicación perdió datos.
        [Theory]
        [InlineData("-17.763152976587406, -63.19892908691274", -17.763153, -63.198929)]
        [InlineData("-17.7631535, -63.1989285", -17.763154, -63.198929)]   // AwayFromZero, como Postgres
        [InlineData("-17.783241, -63.182140", -17.783241, -63.182140)]     // ya venía en seis
        public void RedondeaALosSeisDecimalesDeLaColumna(string texto, double esperadoLat, double esperadoLng)
        {
            Assert.True(UbicacionCompartida.TryExtraer(texto, out var lat, out var lng));
            Assert.Equal((decimal)esperadoLat, lat);
            Assert.Equal((decimal)esperadoLng, lng);
        }

        // Lo que se muestra tiene que ser exactamente lo que va a la base: si volver a leer
        // el texto formateado diera otro número, el campo cambiaría solo al recargar.
        [Fact]
        public void LoQueSeMuestraEsLoQueSeGuarda()
        {
            UbicacionCompartida.TryExtraer("-17.763152976587406, -63.19892908691274", out var lat, out var lng);
            var texto = UbicacionCompartida.Formatear(lat, lng);

            Assert.True(UbicacionCompartida.TryExtraer(texto, out var lat2, out var lng2));
            Assert.Equal(lat, lat2);
            Assert.Equal(lng, lng2);
        }

        // El punto de referencia se pega en un solo campo y tiene que volver a leerse igual:
        // si Formatear y TryExtraer no cierran, el valor por defecto se corrompe al primer
        // guardado.
        [Fact]
        public void ElPuntoPorDefectoSobreviveIdaYVuelta()
        {
            var texto = UbicacionCompartida.Formatear(
                UbicacionCompartida.LatitudPorDefecto, UbicacionCompartida.LongitudPorDefecto);

            Assert.True(UbicacionCompartida.TryExtraer(texto, out var lat, out var lng));
            Assert.Equal(UbicacionCompartida.LatitudPorDefecto, lat);
            Assert.Equal(UbicacionCompartida.LongitudPorDefecto, lng);
        }

        // Seis decimales, los que admite numeric(9,6). Con mas, Postgres redondea y la
        // comparacion por igualdad de EsReferenciaPorDefecto deja de dar.
        [Fact]
        public void ElPuntoPorDefectoTieneLaPrecisionDeLaColumna()
        {
            Assert.Equal(6, decimal.GetBits(UbicacionCompartida.LatitudPorDefecto)[3] >> 16 & 0xFF);
            Assert.Equal(6, decimal.GetBits(UbicacionCompartida.LongitudPorDefecto)[3] >> 16 & 0xFF);
        }

        [Theory]
        [InlineData(-17.763701, -63.199920, true)]
        [InlineData(-17.783241, -63.182140, false)]
        [InlineData(-17.763701, -63.182140, false)]   // solo una coincide
        public void ReconoceSiElPuntoSigueSiendoElDeReferencia(double lat, double lng, bool esperado)
        {
            Assert.Equal(esperado, UbicacionCompartida.EsReferenciaPorDefecto((decimal)lat, (decimal)lng));
        }

        [Fact]
        public void UnPedidoSinPuntoNoEsElDeReferencia()
        {
            Assert.False(UbicacionCompartida.EsReferenciaPorDefecto(null, null));
        }

        // El error de docs/mapa-entrega.md §3.1: bbox va longitud primero y marker latitud
        // primero. Invertirlos dibuja un mapa impecable de otro lugar, que a ojo no se
        // distingue de uno bien. Por eso se fija la URL entera y no solo "que contenga".
        [Fact]
        public void ElEmbedRespetaElOrdenDeCadaParametro()
        {
            var embed = UbicacionCompartida.EmbedMapa(-17.783241m, -63.182140m);

            Assert.Equal(
                "https://www.openstreetmap.org/export/embed.html"
                + "?bbox=-63.183640,-17.784741,-63.180640,-17.781741"
                + "&layer=mapnik&marker=-17.783241,-63.182140",
                embed);
        }

        // La caja mide el doble del margen por lado. Si alguien cambia la constante, esta
        // prueba avisa cuánto quedó en metros antes de que el mapa salga inservible.
        [Fact]
        public void LaCajaDelEmbedRondaLosTrescientosMetros()
        {
            var embed = UbicacionCompartida.EmbedMapa(-17.783241m, -63.182140m);

            var bbox = embed.Split("bbox=")[1].Split('&')[0].Split(',');
            var ladoEnGrados = decimal.Parse(bbox[3], CultureInfo.InvariantCulture)
                             - decimal.Parse(bbox[1], CultureInfo.InvariantCulture);

            // 1 grado de latitud ~ 111 km.
            var ladoEnMetros = ladoEnGrados * 111_000m;

            Assert.InRange(ladoEnMetros, 250m, 450m);
        }

        // Con una cultura de coma decimal, "-17.78" saldria "-17,78" y el bbox — que separa
        // sus valores con coma — quedaria con siete campos en vez de cuatro.
        [Fact]
        public void ElEmbedNoSeRompeConUnaCulturaDeComaDecimal()
        {
            var anterior = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("es-BO");
                var embed = UbicacionCompartida.EmbedMapa(-17.783241m, -63.182140m);

                var bbox = embed.Split("bbox=")[1].Split('&')[0];

                Assert.Equal(4, bbox.Split(',').Length);
                Assert.DoesNotContain("=-17,", embed);
            }
            finally
            {
                CultureInfo.CurrentCulture = anterior;
            }
        }
    }
}
