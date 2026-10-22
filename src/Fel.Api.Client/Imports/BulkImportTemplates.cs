using System;
using ClosedXML.Excel;
using Fel.Api.Client.Models;

namespace Fel.Api.Client.Imports
{
    // Plantillas de las cargas masivas: las mismas columnas que usan los clientes en sus Excel, dos filas de ejemplo y una hoja de instrucciones.
    public static class BulkImportTemplates
    {
        private static void Dropdown(IXLWorksheet sheet, int column, params string[] options)
        {
            var validation = sheet.Range(2, column, 1000, column).CreateDataValidation();
            validation.List(string.Join(",", options));
            // Solo sugiere: un valor distinto no se bloquea, la vista previa de la carga lo revisa.
            validation.ErrorStyle = XLErrorStyle.Information;
            validation.ShowErrorMessage = false;
        }

        private static void Instructions(XLWorkbook workbook, string title, string intro, string[][] rows)
        {
            var sheet = workbook.Worksheets.Add("Instrucciones");
            sheet.Cell(1, 1).Value = title;
            sheet.Cell(1, 1).Style.Font.Bold = true;
            sheet.Cell(1, 1).Style.Font.FontSize = 14;
            sheet.Cell(2, 1).Value = intro;
            ExcelTemplateHelper.WriteHeaderRow(sheet, 4, "Columna", "Obligatoria", "Qué va", "Valores válidos / notas");
            for (var i = 0; i < rows.Length; i++)
                for (var j = 0; j < rows[i].Length; j++)
                    sheet.Cell(5 + i, j + 1).Value = rows[i][j];
            sheet.Columns().AdjustToContents();
            sheet.Column(3).Width = 55;
            sheet.Column(4).Width = 70;
            sheet.Range(5, 1, 4 + rows.Length, 4).Style.Alignment.WrapText = true;
            sheet.Range(5, 1, 4 + rows.Length, 4).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
            sheet.SheetView.FreezeRows(4);
        }

        public static byte[] Customers()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Terceros");
            ExcelTemplateHelper.WriteHeaderRow(sheet, 1, "TIPO", "IDENTIFICACION", "DIGITO_VERIFICACION", "TIPO_IDENTIFICACION", "NOMBRE", "OTROS_NOMBRES", "APELLIDO",
                "SEGUNDO_APELLIDO", "RAZON_SOCIAL", "RESPONSABLE_IVA", "REGIMEN", "CORREO", "TELEFONO", "DEPARTAMENTO", "CIUDAD", "DIRECCION", "CATEGORIA");

            string[][] examples =
            {
                new[] { "JURIDICA", "900123456", "", "NIT", "", "", "", "", "EJEMPLO COMERCIAL S.A.S.", "TRUE", "ORDINARIO", "facturacion@ejemplo.com", "3001234567", "ANTIOQUIA", "MEDELLIN", "CARRERA 43A # 1-50", "CLIENTE" },
                new[] { "NATURAL", "1032456789", "", "CC", "JUAN", "CARLOS", "PEREZ", "GOMEZ", "", "FALSE", "", "juan.perez@correo.com", "3109876543", "BOGOTA", "BOGOTA, D.C.", "CALLE 10 # 20-30", "CLIENTE/PROVEEDOR" }
            };
            for (var r = 0; r < examples.Length; r++)
                for (var c = 0; c < examples[r].Length; c++)
                    sheet.Cell(2 + r, c + 1).SetValue(examples[r][c]);

            Dropdown(sheet, 1, "NATURAL", "JURIDICA");
            Dropdown(sheet, 4, "CC", "NIT", "CE", "TI", "RC", "NUIP", "TE", "PEP", "PPT", "PASAPORTE", "IE");
            Dropdown(sheet, 10, "TRUE", "FALSE");
            Dropdown(sheet, 11, "ORDINARIO", "SIMPLE", "AUTORRETENEDOR", "GRAN_CONTRIBUYENTE");
            Dropdown(sheet, 17, "CLIENTE", "PROVEEDOR", "CLIENTE/PROVEEDOR", "NOMINA", "MANDANTE");
            sheet.Columns().AdjustToContents();
            // Los números de identificación y teléfono son texto: sin esto Excel les quita ceros y los muestra en notación científica.
            foreach (var col in new[] { 2, 3, 13 }) sheet.Column(col).Style.NumberFormat.Format = "@";

            Instructions(workbook, "Carga masiva de terceros",
                "Llena la hoja \"Terceros\" (las dos filas de ejemplo se pueden borrar). Las columnas se reconocen por su nombre, en cualquier orden; las que no uses puedes dejarlas vacías o quitarlas.",
                new[]
                {
                    new[] { "TIPO", "No", "Persona natural o jurídica", "NATURAL o JURIDICA. Solo se usa para avisar si no coincide con el documento." },
                    new[] { "IDENTIFICACION", "Sí", "Número de documento, sin puntos ni dígito de verificación", "Para un NIT puede venir con el dígito (900123456-7)." },
                    new[] { "DIGITO_VERIFICACION", "No", "Dígito de verificación del NIT", "Si falta se calcula; si no coincide se avisa y se usa el calculado." },
                    new[] { "TIPO_IDENTIFICACION", "Sí", "Tipo de documento", "CC, NIT, CE, TI, RC, NUIP, TE, PEP (Permiso Especial de Permanencia), PPT (Permiso por Protección Temporal), PASAPORTE, IE. También sirve el código DIAN (13, 31...)." },
                    new[] { "NOMBRE / OTROS_NOMBRES / APELLIDO / SEGUNDO_APELLIDO", "Una de estas o RAZON_SOCIAL", "Nombres y apellidos de una persona natural", "Se arma el nombre completo con ellos." },
                    new[] { "RAZON_SOCIAL", "Una de estas o NOMBRE", "Razón social de una empresa", "Si el documento es NIT, es el nombre que se guarda." },
                    new[] { "RESPONSABLE_IVA", "No", "Si es responsable de IVA", "TRUE o FALSE. Sin dato se avisa." },
                    new[] { "REGIMEN", "No", "Régimen del tercero", "ORDINARIO, SIMPLE, AUTORRETENEDOR o GRAN_CONTRIBUYENTE. Vacío = ORDINARIO." },
                    new[] { "CORREO", "No", "Correo para enviar los documentos", "Sin correo se avisa." },
                    new[] { "TELEFONO", "No", "Teléfono", "Se limpian los espacios y guiones." },
                    new[] { "DEPARTAMENTO / CIUDAD", "No", "Ubicación", "Se buscan en el catálogo de municipios de la DIAN sin importar tildes ni mayúsculas. Si no se reconocen, se avisa." },
                    new[] { "DIRECCION", "No", "Dirección", "Si falta, en la vista previa eliges: la dirección del cliente, la por defecto para terceros nuevos o dejarla vacía." },
                    new[] { "CATEGORIA", "No", "Rol del tercero", "CLIENTE, PROVEEDOR, CLIENTE/PROVEEDOR (se crean los dos roles), NOMINA (empleado) o MANDANTE. Vacío = CLIENTE. Cualquier otro valor lo decides en la vista previa." }
                });

            return ExcelTemplateHelper.ToBytes(workbook);
        }

        public static byte[] Invoices()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Facturas");
            string[] headers =
            {
                "NUMERO", "TIPO_MEDIO_DE_PAGO", "MEDIO_DE_PAGO", "FECHA_VENCIMIENTO", "ORDEN_DE_COMPRA", "OBSERVACIONES",
                "CLIENTE_TIPO_IDENTIFICATION", "CLIENTE_IDENTIFICATION", "CLIENTE_NOMBRE", "CLIENTE_TIPO", "CLIENTE_DIRECCION", "CLIENTE_TELEFONO", "CLIENTE_CIUDAD",
                "CLIENTE_DEPARTAMENTO", "CLIENTE_CORREO", "CLIENTE_TAX_LEVEL", "CLIENTE_REGIMEN",
                "ITEM_REFERENCIA", "ITEM_DESCRIPCION", "ITEM_CANTIDAD", "ITEM_PRECIO", "ITEM_DESCUENTO%", "IVA%", "RET_FUENTE%", "RET_ICA%", "RET_IVA%",
                "DESCUENTO_GLOBAL", "DESCUENTO_GLOBAL_DESCRIPCION"
            };
            ExcelTemplateHelper.WriteHeaderRow(sheet, 1, headers);

            string[][] examples =
            {
                new[] { "1", "DEBITO", "TRANSFERENCIA", "", "", "Pago del mes de octubre", "CC", "1032456789", "JUAN CARLOS PEREZ GOMEZ", "PERSONA_NATURAL", "CALLE 10 # 20-30", "3109876543", "Bogota, D.C.", "BOGOTA",
                    "juan.perez@correo.com", "NO_RESPONSABLE_DE_IVA", "ORDINARIO", "PENSION", "Pensión octubre", "1", "335000", "", "", "", "", "", "", "" },
                new[] { "2", "CREDITO", "", "", "OC-100", "", "NIT", "900123456", "EJEMPLO COMERCIAL S.A.S.", "PERSONA_JURIDICA", "CARRERA 43A # 1-50", "3001234567", "Medellin", "ANTIOQUIA",
                    "facturacion@ejemplo.com", "RESPONSABLE_DE_IVA", "ORDINARIO", "SRV-001", "Servicio de asesoría", "2", "150000", "", "19", "", "", "", "", "" },
                new[] { "2", "CREDITO", "", "", "OC-100", "", "NIT", "900123456", "EJEMPLO COMERCIAL S.A.S.", "PERSONA_JURIDICA", "CARRERA 43A # 1-50", "3001234567", "Medellin", "ANTIOQUIA",
                    "facturacion@ejemplo.com", "RESPONSABLE_DE_IVA", "ORDINARIO", "SRV-002", "Soporte mensual", "1", "80000", "10", "19", "", "", "", "", "" }
            };
            // La fecha de vencimiento de la factura a crédito se escribe 30 días después de hoy; el ejemplo se ajusta al descargar la plantilla.
            var due = DateTime.Today.AddDays(30).ToString("dd/MM/yyyy");
            for (var r = 0; r < examples.Length; r++)
                for (var c = 0; c < examples[r].Length; c++)
                {
                    var value = headers[c] == "FECHA_VENCIMIENTO" && examples[r][1] == "CREDITO" ? due : examples[r][c];
                    sheet.Cell(2 + r, c + 1).SetValue(value);
                }

            Dropdown(sheet, 2, "DEBITO", "CREDITO");
            Dropdown(sheet, 3, "TRANSFERENCIA", "CASH", "DEBIT_CARD", "CREDIT_CARD", "MUTUAL_AGREEMENT");
            Dropdown(sheet, 7, "CC", "NIT", "CE", "TI", "RC", "NUIP", "TE", "PEP", "PPT", "PASAPORTE", "IE");
            sheet.Columns().AdjustToContents();
            foreach (var col in new[] { 4, 8, 12 }) sheet.Column(col).Style.NumberFormat.Format = "@";

            Instructions(workbook, "Carga masiva de facturas",
                "Llena la hoja \"Facturas\" (los tres renglones de ejemplo se pueden borrar): cada fila es una línea y las filas con el mismo NUMERO forman una sola factura. Las columnas se reconocen por su nombre, en cualquier orden; también puedes subir un archivo CSV (separado por ; o ,). Las facturas quedan como BORRADORES con la resolución que elijas; se emiten después desde la lista.",
                new[]
                {
                    new[] { "NUMERO", "No", "Agrupa las líneas de una misma factura", "No es el número de la factura: el consecutivo lo asigna el sistema al emitir. Sin NUMERO, cada fila es una factura." },
                    new[] { "TIPO_MEDIO_DE_PAGO", "Sí", "Forma de pago", "DEBITO (Contado) o CREDITO." },
                    new[] { "MEDIO_DE_PAGO", "Contado", "Medio de pago", "TRANSFERENCIA (o BANK_TRANSFER), CASH (efectivo), DEBIT_CARD, CREDIT_CARD, MUTUAL_AGREEMENT. Si no se reconoce lo eliges en la vista previa. En crédito no se pide." },
                    new[] { "FECHA_VENCIMIENTO", "Crédito", "Vencimiento de la factura a crédito (dd/mm/aaaa)", "Debe ser posterior a hoy. De contado no se usa: vence el día de la emisión." },
                    new[] { "FECHA_EXPEDICION", "No", "Fecha de emisión del archivo", "Se ignora: las facturas se crean con la fecha de hoy (hora de Colombia)." },
                    new[] { "ORDEN_DE_COMPRA / OBSERVACIONES", "No", "Orden de compra del cliente y observaciones de la factura", "" },
                    new[] { "CLIENTE_TIPO_IDENTIFICATION / CLIENTE_IDENTIFICATION", "Sí", "Documento del cliente", "CC, NIT, CE, TI, RC, NUIP, TE, PEP, PPT, PASAPORTE, IE o el código DIAN. Si el tercero no existe se crea con rol Cliente." },
                    new[] { "CLIENTE_NOMBRE", "Sí", "Nombre o razón social", "O CLIENTE_PRIMER_NOMBRE y CLIENTE_APELLIDO para armar el nombre de una persona natural." },
                    new[] { "CLIENTE_TIPO / CLIENTE_TAX_LEVEL / CLIENTE_REGIMEN", "No", "PERSONA_NATURAL o PERSONA_JURIDICA; RESPONSABLE_DE_IVA o NO_RESPONSABLE_DE_IVA; ORDINARIO, SIMPLE, AUTORRETENEDOR o GRAN_CONTRIBUYENTE", "Solo se usan al crear el tercero." },
                    new[] { "CLIENTE_DIRECCION / TELEFONO / CIUDAD / DEPARTAMENTO / CORREO", "No", "Datos del tercero nuevo", "La ciudad se busca en el catálogo de municipios de la DIAN. Si falta la dirección eliges qué hacer en la vista previa." },
                    new[] { "ITEM_REFERENCIA", "Sí", "Código del producto", "Si el producto existe la línea toma sus impuestos y su unidad; si no existe se crea." },
                    new[] { "ITEM_DESCRIPCION", "No", "Texto de la línea", "Sin descripción se usa el nombre del producto. Si el producto es nuevo, también es su nombre." },
                    new[] { "ITEM_CANTIDAD / ITEM_PRECIO", "Sí", "Cantidad y precio unitario antes de impuestos", "El precio acepta 335000, 335,000 o 335.000." },
                    new[] { "ITEM_DESCUENTO%", "No", "Descuento de la línea, en %", "" },
                    new[] { "IVA%", "No", "IVA de la línea, solo para productos nuevos", "Producto nuevo sin IVA: la línea sale Excluida y el producto queda con \"impuestos por completar\". Producto existente: se usa su IVA." },
                    new[] { "RET_FUENTE% / RET_ICA% / RET_IVA%", "No", "Retenciones de la línea, en %", "" },
                    new[] { "DESCUENTO_GLOBAL / DESCUENTO_GLOBAL_DESCRIPCION", "No", "Descuento general de la factura (valor en pesos) y su motivo", "Se toma de la primera línea de la factura que lo traiga." }
                });

            return ExcelTemplateHelper.ToBytes(workbook);
        }

        public static byte[] Products()
        {
            using var workbook = new XLWorkbook();
            var sheet = workbook.Worksheets.Add("Productos");
            ExcelTemplateHelper.WriteHeaderRow(sheet, 1, "REF", "DESCRIPCION", "CATEGORIA", "PRECIO_DE_VENTA_UNITARIO", "PRECIO_INCLUYE_IMPUESTOS", "UNIDAD", "IVA%", "IMP_CONSUMO%", "IMP_LICOR", "CONTROL_INVENTARIO");

            sheet.Cell(2, 1).SetValue("SRV-001");
            sheet.Cell(2, 2).SetValue("Servicio de asesoría");
            sheet.Cell(2, 4).Value = 150000;
            sheet.Cell(2, 5).SetValue("FALSE");
            sheet.Cell(2, 6).SetValue("EA");
            sheet.Cell(2, 7).Value = 19;
            sheet.Cell(2, 10).SetValue("FALSE");
            sheet.Cell(3, 1).SetValue("REST-01");
            sheet.Cell(3, 2).SetValue("Almuerzo ejecutivo");
            sheet.Cell(3, 3).SetValue("Restaurante");
            sheet.Cell(3, 4).Value = 25000;
            sheet.Cell(3, 5).SetValue("TRUE");
            sheet.Cell(3, 6).SetValue("EA");
            sheet.Cell(3, 8).Value = 8;
            sheet.Cell(3, 10).SetValue("FALSE");

            Dropdown(sheet, 5, "TRUE", "FALSE");
            Dropdown(sheet, 10, "TRUE", "FALSE");
            sheet.Columns().AdjustToContents();

            Instructions(workbook, "Carga masiva de productos",
                "Llena la hoja \"Productos\" (las dos filas de ejemplo se pueden borrar). Las columnas se reconocen por su nombre, en cualquier orden. La carga es fiel: lo que escribas se guarda tal cual y lo que dejes vacío no se inventa.",
                new[]
                {
                    new[] { "REF", "Sí", "Código o referencia del producto", "No se puede repetir: si ya existe un producto con ese código, la fila se omite." },
                    new[] { "DESCRIPCION", "Sí", "Nombre del producto", "" },
                    new[] { "CATEGORIA", "No", "Categoría", "Todavía no se guarda; se ignora." },
                    new[] { "PRECIO_DE_VENTA_UNITARIO", "No", "Precio por unidad", "Vacío o 0 se carga en 0 con aviso." },
                    new[] { "PRECIO_INCLUYE_IMPUESTOS", "No", "Si el precio ya trae el IVA", "TRUE o FALSE. Vacío = FALSE." },
                    new[] { "UNIDAD", "No", "Unidad de medida", "EA o UN (Unidad), KGM, LBR, HUR... Vacío = Unidad." },
                    new[] { "IVA%", "No", "Tarifa de IVA", "19, 5 o 10. Si queda vacía o en 0 el producto se carga con \"impuestos por completar\": no se asume ningún tratamiento y luego puedes asignarlos en bloque." },
                    new[] { "IMP_CONSUMO%", "No", "Impuesto al consumo", "Por ejemplo 8. Un producto con impuesto al consumo y sin IVA no queda pendiente." },
                    new[] { "IMP_LICOR", "No", "Impuesto al consumo de licores", "Porcentaje." },
                    new[] { "CONTROL_INVENTARIO", "No", "Control de inventario", "Todavía no se usa; se ignora." }
                });

            return ExcelTemplateHelper.ToBytes(workbook);
        }
    }
}
