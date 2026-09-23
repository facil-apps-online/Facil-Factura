using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UglyToad.PdfPig;

namespace Fel.Infrastructure.Services
{
    /// <summary>
    /// Extrae los datos de un RUT (Formulario 001 de la DIAN) en PDF, para prellenar el alta de un
    /// Tenant sin tener que transcribirlos a mano.
    /// </summary>
    /// <remarks>
    /// A diferencia de <see cref="DianResolutionParserService"/>, acá NO sirve aplanar el texto y
    /// buscar con expresiones regulares. El RUT es un formulario de casillas: el texto plano
    /// devuelve todas las etiquetas ("35. Razón social", "41. Dirección principal") mezcladas y
    /// separadas de sus valores, de modo que no hay forma de saber qué valor pertenece a qué
    /// casilla. Hay que leerlo por coordenadas.
    ///
    /// Dos particularidades del formulario que condicionan el algoritmo:
    ///
    /// 1. Los valores no están siempre en el mismo sitio respecto de su etiqueta. En unas casillas
    ///    van justo debajo (razón social, dirección) y en otras en la misma línea a la derecha
    ///    (correo, teléfono). Por eso cada casilla declara dónde buscar.
    /// 2. Los campos numéricos se imprimen con un carácter por casilla, así que llegan como
    ///    "9 0 1 9 6 6 6 1 2" y hay que quitarles los espacios.
    ///
    /// El anclaje es a la ETIQUETA, no a coordenadas fijas: se localiza "35." en la página y se
    /// busca su valor en relación con ella. Así el parser sobrevive a que la DIAN mueva el formato,
    /// mientras conserve la numeración de casillas, que está fijada por resolución.
    /// </remarks>
    public class DianRutParserService
    {
        public class ParsedRutData
        {
            public bool IsSuccess { get; set; }
            public string ErrorMessage { get; set; } = string.Empty;

            public string TaxId { get; set; } = string.Empty;              // Casilla 5
            public string VerificationDigit { get; set; } = string.Empty;  // Casilla 6
            public string TaxpayerType { get; set; } = string.Empty;       // Casilla 24
            public string LegalName { get; set; } = string.Empty;          // Casilla 35
            public string CommercialName { get; set; } = string.Empty;     // Casilla 36
            public string Country { get; set; } = string.Empty;            // Casilla 38
            public string Department { get; set; } = string.Empty;         // Casilla 39
            public string City { get; set; } = string.Empty;               // Casilla 40
            public string Address { get; set; } = string.Empty;            // Casilla 41
            public string Email { get; set; } = string.Empty;              // Casilla 42
            public string Phone { get; set; } = string.Empty;              // Casilla 44
            public string EconomicActivity { get; set; } = string.Empty;   // Casilla 46 (CIIU principal)

            /// <summary>Casilla 53, como "48 - Impuesto sobre las ventas - IVA".</summary>
            public List<string> Responsibilities { get; set; } = new();

            /// <summary>Códigos de la casilla 53 ya separados, para poder consultarlos.</summary>
            public List<string> ResponsibilityCodes { get; set; } = new();

            /// <summary>
            /// La 52 es "Facturador electrónico". Si no está, el contribuyente todavía no figura
            /// como obligado en su RUT — no impide crear el Tenant, pero conviene advertirlo.
            /// </summary>
            public bool IsElectronicInvoicer => ResponsibilityCodes.Contains("52");
        }

        // Un trozo de texto contiguo de la página, con su posición.
        private sealed record Fragmento(double X, double Y, string Texto);

        // Dónde buscar el valor respecto de la etiqueta de la casilla.
        private enum Donde { Debajo, MismaLinea, Cualquiera }

        public async Task<ParsedRutData> ParsePdfAsync(Stream pdfStream)
        {
            var result = new ParsedRutData { IsSuccess = false };

            try
            {
                List<Fragmento> fragmentos;

                using (var document = PdfDocument.Open(pdfStream))
                {
                    var pagina = document.GetPages().FirstOrDefault();
                    if (pagina == null)
                    {
                        result.ErrorMessage = "El PDF no tiene páginas.";
                        return result;
                    }
                    fragmentos = ArmarFragmentos(pagina);
                }

                if (fragmentos.Count == 0)
                {
                    result.ErrorMessage = "El PDF no contiene texto legible. Si es un escaneo, hay que "
                                        + "descargar el RUT en PDF desde el portal de la DIAN en vez de escanearlo.";
                    return result;
                }

                // NIT y DV se leen juntos a propósito. Las casillas de dígitos del formulario se
                // extienden más allá de donde arranca la etiqueta "6. DV", así que ningún corte
                // por coordenadas los separa de forma fiable. En cambio el DV se puede CALCULAR a
                // partir del NIT, así que se lee el bloque completo de dígitos y se parte donde el
                // cálculo cuadra: eso separa bien y de paso valida que la lectura fue correcta.
                var bloqueNit = SoloDigitos(Valor(fragmentos, 5, 180, Donde.Debajo));
                if (bloqueNit.Length >= 2)
                {
                    var nitSinDv = bloqueNit[..^1];
                    if (CalcularDv(nitSinDv) == bloqueNit[^1].ToString())
                    {
                        result.TaxId = nitSinDv;
                        result.VerificationDigit = bloqueNit[^1].ToString();
                    }
                    else
                    {
                        // No cuadró: se asume que el bloque es solo el NIT y el DV se calcula.
                        result.TaxId = bloqueNit;
                        result.VerificationDigit = CalcularDv(bloqueNit);
                    }
                }
                result.TaxpayerType = Valor(fragmentos, 24, 140, Donde.Debajo);
                result.LegalName = Valor(fragmentos, 35, 300, Donde.Debajo);
                result.CommercialName = Valor(fragmentos, 36, 280, Donde.Debajo);
                // País, departamento y ciudad traen delante su código DANE en casillas de dígitos
                // ("1 6 9 COLOMBIA"). Se conserva solo el nombre, que es lo que usa el Tenant.
                result.Country = SinCodigoDelante(Valor(fragmentos, 38, 160, Donde.Debajo));
                result.Department = SinCodigoDelante(Valor(fragmentos, 39, 180, Donde.Debajo));
                result.City = SinCodigoDelante(Valor(fragmentos, 40, 180, Donde.Debajo));
                result.Address = Valor(fragmentos, 41, 300, Donde.Debajo);
                result.Email = Valor(fragmentos, 42, 300, Donde.Cualquiera);

                // El teléfono se limita a 10 dígitos: en Colombia un móvil tiene 10 y un fijo con
                // indicativo 10 también, así que más que eso significa que se coló la casilla vecina.
                var telefono = SoloDigitos(Valor(fragmentos, 44, 220, Donde.Cualquiera, invadeSiguiente: 60));
                result.Phone = telefono.Length > 10 ? telefono[..10] : telefono;

                // El CIIU son exactamente 4 dígitos; la casilla siguiente es una fecha.
                var ciiu = SoloDigitos(Valor(fragmentos, 46, 60, Donde.Debajo));
                result.EconomicActivity = ciiu.Length >= 4 ? ciiu[..4] : ciiu;

                // Casilla 53: las descripciones vienen ya legibles ("48 - Impuesto sobre las
                // ventas - IVA"), así que basta reconocerlas por su forma en vez de cruzarlas con
                // los códigos sueltos que el formulario imprime arriba en casillas separadas.
                foreach (var f in fragmentos.OrderByDescending(f => f.Y))
                {
                    var m = Regex.Match(f.Texto, @"^(\d{2})\s*-\s*(.+)$");
                    if (!m.Success) continue;
                    result.Responsibilities.Add(f.Texto.Trim());
                    result.ResponsibilityCodes.Add(m.Groups[1].Value);
                }

                if (string.IsNullOrWhiteSpace(result.TaxId) || string.IsNullOrWhiteSpace(result.LegalName))
                {
                    result.ErrorMessage = "No se pudieron leer el NIT y la razón social. ¿El archivo es "
                                        + "realmente un RUT (Formulario 001) descargado del portal de la DIAN?";
                    return result;
                }

                // El nombre comercial suele venir vacío; en ese caso el comercial es la razón social.
                if (string.IsNullOrWhiteSpace(result.CommercialName))
                    result.CommercialName = result.LegalName;

                result.IsSuccess = true;
                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"No se pudo leer el PDF: {ex.Message}";
                return result;
            }
        }

        /// <summary>
        /// Reconstruye los bloques de texto de la página. PdfPig entrega palabras sueltas, así que
        /// se agrupan por línea y, dentro de cada línea, se cortan donde hay un hueco horizontal
        /// grande — que es lo que separa una casilla de la siguiente.
        /// </summary>
        private static List<Fragmento> ArmarFragmentos(UglyToad.PdfPig.Content.Page pagina)
        {
            const double toleranciaLinea = 3.0;   // dos palabras a menos de esto son la misma línea
            // Deliberadamente pequeño: es preferible partir de más y volver a unir al leer cada
            // casilla, que unir de más y que el NIT se trague el dígito de verificación de la
            // casilla vecina. Con un umbral grande eso es exactamente lo que pasaba.
            const double huecoEntreCasillas = 4.0;

            var palabras = pagina.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .OrderByDescending(w => w.BoundingBox.Bottom)
                .ThenBy(w => w.BoundingBox.Left)
                .ToList();

            var fragmentos = new List<Fragmento>();
            var lineaActual = new List<UglyToad.PdfPig.Content.Word>();

            void CerrarLinea()
            {
                if (lineaActual.Count == 0) return;
                var enOrden = lineaActual.OrderBy(w => w.BoundingBox.Left).ToList();
                var acumulado = new List<UglyToad.PdfPig.Content.Word> { enOrden[0] };

                for (int i = 1; i < enOrden.Count; i++)
                {
                    var hueco = enOrden[i].BoundingBox.Left - acumulado[^1].BoundingBox.Right;
                    if (hueco > huecoEntreCasillas)
                    {
                        fragmentos.Add(AFragmento(acumulado));
                        acumulado = new List<UglyToad.PdfPig.Content.Word>();
                    }
                    acumulado.Add(enOrden[i]);
                }
                if (acumulado.Count > 0) fragmentos.Add(AFragmento(acumulado));
                lineaActual.Clear();
            }

            foreach (var w in palabras)
            {
                if (lineaActual.Count > 0 &&
                    Math.Abs(lineaActual[0].BoundingBox.Bottom - w.BoundingBox.Bottom) > toleranciaLinea)
                {
                    CerrarLinea();
                }
                lineaActual.Add(w);
            }
            CerrarLinea();

            return fragmentos;
        }

        private static Fragmento AFragmento(List<UglyToad.PdfPig.Content.Word> palabras) =>
            new(palabras[0].BoundingBox.Left,
                palabras[0].BoundingBox.Bottom,
                string.Join(" ", palabras.Select(p => p.Text)).Trim());

        private static bool EsEtiqueta(string t) => Regex.IsMatch(t, @"^\d{1,3}\s*\.");

        /// <param name="invadeSiguiente">
        /// Puntos que se permite avanzar más allá de donde empieza la casilla siguiente. Hace falta
        /// en los campos de dígitos, cuyas cajitas se dibujan desbordando la etiqueta vecina: sin
        /// esto el teléfono se lee truncado.
        /// </param>
        private static string Valor(List<Fragmento> fragmentos, int casilla, double anchoMax, Donde donde, double invadeSiguiente = 0)
        {
            var etiqueta = fragmentos.FirstOrDefault(f => Regex.IsMatch(f.Texto, $@"^{casilla}\s*\."));
            if (etiqueta == null) return string.Empty;

            // El borde derecho real de una casilla es donde empieza la siguiente. Calcularlo así,
            // en vez de con un ancho fijo, es lo que evita que un campo invada al vecino: el NIT
            // (casilla 5) termina donde arranca el DV (casilla 6), sea cual sea la maqueta.
            var siguiente = fragmentos
                .Where(f => EsEtiqueta(f.Texto) && Math.Abs(f.Y - etiqueta.Y) <= 4 && f.X > etiqueta.X)
                .OrderBy(f => f.X)
                .FirstOrDefault();

            var bordeDerecho = siguiente != null
                ? Math.Min(siguiente.X - 2 + invadeSiguiente, etiqueta.X + anchoMax)
                : etiqueta.X + anchoMax;

            var enLinea = new List<Fragmento>();
            var abajo = new List<Fragmento>();

            foreach (var f in fragmentos)
            {
                if (EsEtiqueta(f.Texto)) continue;
                if (f.X < etiqueta.X - 12 || f.X > bordeDerecho) continue;

                var dy = etiqueta.Y - f.Y;
                if (Math.Abs(dy) <= 4 && f.X > etiqueta.X) enLinea.Add(f);
                else if (dy > 4 && dy <= 22) abajo.Add(f);
            }

            // Se prefiere el valor de la misma línea (correo, teléfono) y si no el de abajo
            // (razón social, dirección). Los trozos se vuelven a unir por orden horizontal, que es
            // lo que reconstruye "9 0 1 9 6 6 6 1 2" partido en casillas.
            var elegidos = donde switch
            {
                Donde.Debajo => abajo,
                Donde.MismaLinea => enLinea,
                _ => enLinea.Count > 0 ? enLinea : abajo
            };

            if (elegidos.Count == 0) return string.Empty;

            // Si hay varias filas candidatas debajo, quedarse solo con la más cercana.
            if (elegidos == abajo)
            {
                var filaMasCercana = elegidos.Min(f => etiqueta.Y - f.Y);
                elegidos = elegidos.Where(f => Math.Abs((etiqueta.Y - f.Y) - filaMasCercana) <= 4).ToList();
            }

            return string.Join(" ", elegidos.OrderBy(f => f.X).Select(f => f.Texto)).Trim();
        }

        private static string SoloDigitos(string valor) =>
            string.IsNullOrEmpty(valor) ? string.Empty : Regex.Replace(valor, @"\D", "");

        /// <summary>
        /// Quita los códigos DANE que el formulario imprime en casillas de dígitos junto al nombre.
        /// Se limpian por ambos lados porque, según la casilla, el código cae antes o después.
        /// </summary>
        private static string SinCodigoDelante(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return string.Empty;
            var limpio = Regex.Replace(valor, @"^[\d\s]+", "");
            limpio = Regex.Replace(limpio, @"[\s\d]+$", "");
            return limpio.Trim();
        }

        /// <summary>
        /// Dígito de verificación del NIT según la DIAN: suma ponderada por primos, de derecha a
        /// izquierda, módulo 11.
        /// </summary>
        public static string CalcularDv(string nit)
        {
            if (string.IsNullOrWhiteSpace(nit) || !nit.All(char.IsDigit)) return string.Empty;

            int[] pesos = { 3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71 };
            long suma = 0;
            for (int i = 0; i < nit.Length && i < pesos.Length; i++)
            {
                var digito = nit[nit.Length - 1 - i] - '0';
                suma += (long)digito * pesos[i];
            }

            var residuo = suma % 11;
            return (residuo is 0 or 1 ? residuo : 11 - residuo).ToString();
        }
    }
}
