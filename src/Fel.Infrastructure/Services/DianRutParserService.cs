using System;
using System.Collections.Generic;
using System.Globalization;
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
            public string LegalName { get; set; } = string.Empty;          // Casilla 35, o los 4 campos de abajo concatenados si es persona natural
            public string CommercialName { get; set; } = string.Empty;     // Casilla 36

            // Persona natural: la 35 (Razón social) viene vacía a propósito — el nombre está repartido
            // en estas 4 casillas en su lugar. Persona jurídica: quedan vacías, solo se llena LegalName.
            public string FirstName { get; set; } = string.Empty;      // Casilla 33
            public string SecondName { get; set; } = string.Empty;     // Casilla 34
            public string FirstLastName { get; set; } = string.Empty;  // Casilla 31
            public string SecondLastName { get; set; } = string.Empty; // Casilla 32
            public string Country { get; set; } = string.Empty;            // Casilla 38
            public string Department { get; set; } = string.Empty;         // Casilla 39
            public string DepartmentCode { get; set; } = string.Empty;     // Casilla 39, código DANE (2 dígitos)
            public string City { get; set; } = string.Empty;               // Casilla 40
            public string MunicipalityCode { get; set; } = string.Empty;   // Casilla 40, código DANE (3 dígitos)

            // Código DANE completo del municipio (5 dígitos: departamento + municipio), tal como lo
            // usa Client/Customer.CityCode — el mismo que trae impreso el RUT junto al nombre, no
            // uno recalculado por coincidencia de nombres.
            public string CityCode => DepartmentCode.Length == 2 && MunicipalityCode.Length == 3
                ? DepartmentCode + MunicipalityCode
                : string.Empty;
            public string Address { get; set; } = string.Empty;            // Casilla 41
            public string Email { get; set; } = string.Empty;              // Casilla 42
            public string Phone { get; set; } = string.Empty;              // Casilla 44
            public string EconomicActivity { get; set; } = string.Empty;   // Casilla 46 (CIIU principal)

            // Hoja de representación del RUT (casillas 98-110). Es opcional: muchos RUT se
            // cargan solo con la hoja principal y eso no debe impedir crear el contribuyente.
            public string RepresentativeRepresentationCode { get; set; } = string.Empty;
            public string RepresentativeDocumentType { get; set; } = string.Empty;
            public string RepresentativeDocumentNumber { get; set; } = string.Empty;
            public string RepresentativeFirstLastName { get; set; } = string.Empty;
            public string RepresentativeSecondLastName { get; set; } = string.Empty;
            public string RepresentativeFirstName { get; set; } = string.Empty;
            public string RepresentativeOtherNames { get; set; } = string.Empty;
            public string RepresentativeNit { get; set; } = string.Empty;
            public string RepresentativeLegalName { get; set; } = string.Empty;
            public DateTime? RepresentativeStartDate { get; set; }

            /// <summary>Casilla 53, como "48 - Impuesto sobre las ventas - IVA".</summary>
            public List<string> Responsibilities { get; set; } = new();

            /// <summary>Códigos de la casilla 53 ya separados, para poder consultarlos.</summary>
            public List<string> ResponsibilityCodes { get; set; } = new();

            /// <summary>
            /// La 52 es "Facturador electrónico". Si no está, el contribuyente todavía no figura
            /// como obligado en su RUT — no impide crear el Tenant, pero conviene advertirlo.
            /// </summary>
            public bool IsElectronicInvoicer => ResponsibilityCodes.Contains("52");

            // 13 = Gran Contribuyente, 09 = Agente Retenedor de IVA, 15 = Autorretenedor de renta —
            // la representación gráfica de la factura debe declarar estas tres explícitamente.
            public bool IsGranContribuyente => ResponsibilityCodes.Contains("13");
            public bool IsAgenteRetenedorIva => ResponsibilityCodes.Contains("09");
            public bool IsAutorretenedorRenta => ResponsibilityCodes.Contains("15");
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
                List<Fragmento>? fragmentosRepresentacion = null;

                using (var document = PdfDocument.Open(pdfStream))
                {
                    var paginas = document.GetPages().ToList();
                    var pagina = paginas.FirstOrDefault();
                    if (pagina == null)
                    {
                        result.ErrorMessage = "El PDF no tiene páginas.";
                        return result;
                    }
                    fragmentos = ArmarFragmentos(pagina);

                    // La representación suele estar en la hoja 3, pero no asumimos que el PDF
                    // siempre tenga tres hojas ni que el formato conserve exactamente el mismo
                    // orden. Se inspeccionan las páginas restantes y se toma la primera que tenga
                    // un valor reconocible en la casilla 98.
                    foreach (var paginaExtra in paginas.Skip(1))
                    {
                        var candidatos = ArmarFragmentos(paginaExtra);
                        if (!string.IsNullOrWhiteSpace(Valor(candidatos, 98, 180, Donde.Debajo)))
                        {
                            fragmentosRepresentacion = candidatos;
                            break;
                        }
                    }
                }

                if (fragmentos.Count == 0)
                {
                    result.ErrorMessage = "El PDF no contiene texto legible. Si es un escaneo, hay que "
                                        + "descargar el RUT en PDF desde el portal de la DIAN en vez de escanearlo.";
                    return result;
                }

                // La casilla 6 (DV) nunca se alcanza a leer: Valor() corta el borde derecho de la
                // casilla 5 justo donde arranca la etiqueta "6." (sin invadeSiguiente), así que
                // bloqueNit es siempre solo el NIT. Antes había acá una heurística que intentaba
                // adivinar si el último dígito leído era el DV comparándolo contra CalcularDv() del
                // resto — como esa comparación nunca tiene el DV real disponible, era pura lotería:
                // para WORLD MARKETING JG PREFERENCIAL coincidió que CalcularDv("90113243") == "6"
                // (el 9° dígito del NIT real, no un DV), y el NIT de 9 dígitos quedó mal partido en
                // 8+1. El DV siempre se calcula, nunca se lee.
                var bloqueNit = SoloDigitos(Valor(fragmentos, 5, 180, Donde.Debajo));
                if (bloqueNit.Length >= 2)
                {
                    result.TaxId = bloqueNit;
                    result.VerificationDigit = CalcularDv(bloqueNit);
                }
                result.TaxpayerType = Valor(fragmentos, 24, 140, Donde.Debajo);
                result.LegalName = Valor(fragmentos, 35, 300, Donde.Debajo);
                result.CommercialName = Valor(fragmentos, 36, 280, Donde.Debajo);

                // Persona natural: la 35 viene vacía y el nombre se arma con la 31-34. Se leen
                // siempre (no solo cuando la 35 está vacía) porque no cuesta nada y así el
                // formulario del admin recibe los 4 campos sueltos también para RUT de persona
                // natural con datos parciales.
                result.FirstLastName = Valor(fragmentos, 31, 160, Donde.Debajo);
                result.SecondLastName = Valor(fragmentos, 32, 160, Donde.Debajo);
                result.FirstName = Valor(fragmentos, 33, 160, Donde.Debajo);
                result.SecondName = Valor(fragmentos, 34, 200, Donde.Debajo);

                if (string.IsNullOrWhiteSpace(result.LegalName))
                {
                    result.LegalName = string.Join(" ", new[] { result.FirstName, result.SecondName, result.FirstLastName, result.SecondLastName }
                        .Where(s => !string.IsNullOrWhiteSpace(s)));
                }
                // País, departamento y ciudad traen delante su código DANE en casillas de dígitos
                // ("1 6 9 COLOMBIA"). Se conserva solo el nombre, que es lo que usa el Tenant.
                result.Country = SinCodigoDelante(Valor(fragmentos, 38, 160, Donde.Debajo));

                // El código DANE de casillas 39/40 no está pegado al nombre: vive en una columna fija
                // del formulario, bastante más a la derecha (confirmado con un RUT real: "Valle del
                // Cauca" termina ~X250, su código "76" cae en X371-386, y recién en X393 empieza el
                // nombre de la casilla 40). El ancho de las demás casillas de texto (160-200) se
                // quedaba corto y truncaba el código a un solo dígito.
                var departamentoCrudo = Valor(fragmentos, 39, 195, Donde.Debajo);
                result.Department = SinCodigoDelante(departamentoCrudo);
                result.DepartmentCode = CodigoAlFinal(departamentoCrudo);

                var municipioCrudo = Valor(fragmentos, 40, 220, Donde.Debajo);
                result.City = SinCodigoDelante(municipioCrudo);
                result.MunicipalityCode = CodigoAlFinal(municipioCrudo);
                result.Address = Valor(fragmentos, 41, 300, Donde.Debajo);
                result.Email = Valor(fragmentos, 42, 300, Donde.Cualquiera);

                // El teléfono se limita a 10 dígitos: en Colombia un móvil tiene 10 y un fijo con
                // indicativo 10 también, así que más que eso significa que se coló la casilla vecina.
                var telefono = SoloDigitos(Valor(fragmentos, 44, 220, Donde.Cualquiera, invadeSiguiente: 60));
                result.Phone = telefono.Length > 10 ? telefono[..10] : telefono;

                // El CIIU son exactamente 4 dígitos; la casilla siguiente es una fecha.
                var ciiu = SoloDigitos(Valor(fragmentos, 46, 60, Donde.Debajo));
                result.EconomicActivity = ciiu.Length >= 4 ? ciiu[..4] : ciiu;

                // La hoja de representación es opcional. Cualquier casilla ausente queda vacía
                // y el resultado principal sigue siendo válido.
                if (fragmentosRepresentacion != null)
                {
                    result.RepresentativeRepresentationCode = NormalizarCodigoConEspacios(Valor(fragmentosRepresentacion, 98, 180, Donde.Debajo));
                    result.RepresentativeDocumentType = NormalizarTipoDocumento(Valor(fragmentosRepresentacion, 100, 180, Donde.Debajo));
                    result.RepresentativeDocumentNumber = SoloDigitos(Valor(fragmentosRepresentacion, 101, 180, Donde.Debajo));
                    result.RepresentativeFirstLastName = Valor(fragmentosRepresentacion, 104, 160, Donde.Debajo);
                    result.RepresentativeSecondLastName = Valor(fragmentosRepresentacion, 105, 160, Donde.Debajo);
                    result.RepresentativeFirstName = Valor(fragmentosRepresentacion, 106, 160, Donde.Debajo);
                    result.RepresentativeOtherNames = Valor(fragmentosRepresentacion, 107, 200, Donde.Debajo);
                    result.RepresentativeNit = SoloDigitos(Valor(fragmentosRepresentacion, 108, 180, Donde.Debajo));
                    result.RepresentativeLegalName = Valor(fragmentosRepresentacion, 110, 300, Donde.Debajo);

                    // La casilla 99 está a la derecha de la hoja y la fecha ocupa ocho dígitos
                    // separados; 180 puntos corta los últimos dígitos en el RUT de SoFactory.
                    var rawStartDate = Valor(fragmentosRepresentacion, 99, 240, Donde.Debajo);
                    if (TryParseRutDate(rawStartDate, out var startDate))
                        result.RepresentativeStartDate = startDate;
                }

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

        private static string NormalizarTipoDocumento(string value)
        {
            var normalized = Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim().ToUpperInvariant();
            if (normalized.Contains("CÉDULA") || normalized.Contains("CEDULA")) return "CC";
            if (normalized.Contains("PASAPORTE")) return "PAS";
            if (normalized.Contains("EXTRANJERÍA") || normalized.Contains("EXTRANJERIA")) return "CE";
            if (normalized.Contains("NIT")) return "NIT";
            return (value ?? string.Empty).Trim();
        }

        private static bool TryParseRutDate(string value, out DateTime date)
        {
            var digits = SoloDigitos(value);
            if (digits.Length == 8 && DateTime.TryParseExact(digits, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
                return true;

            return DateTime.TryParse(value, CultureInfo.GetCultureInfo("es-CO"), DateTimeStyles.None, out date);
        }

        private static string NormalizarCodigoConEspacios(string value)
        {
            var match = Regex.Match(value ?? string.Empty, @"((?:\d\s*)+)$");
            return match.Success ? Regex.Replace(match.Groups[1].Value, @"\s+", string.Empty) : (value ?? string.Empty).Trim();
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
        /// El complemento de <see cref="SinCodigoDelante"/>: en vez de descartar el código DANE que
        /// el formulario imprime junto al nombre (departamento/municipio), lo extrae. Los dígitos
        /// vienen uno por casilla ("7 6"), así que se les quitan los espacios.
        /// </summary>
        private static string CodigoAlFinal(string valor)
        {
            if (string.IsNullOrEmpty(valor)) return string.Empty;
            var m = Regex.Match(valor, @"([\d\s]+)$");
            return m.Success ? Regex.Replace(m.Groups[1].Value, @"\s+", "") : string.Empty;
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
