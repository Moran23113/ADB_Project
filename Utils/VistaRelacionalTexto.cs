using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public static class VistaRelacionalTexto
{
    public static string Construir(InstantaneaEsquema esquema, bool comoHtml = false)
    {
        var sb = new StringBuilder();

        // Columnas FK por tabla (reutiliza tu lógica)
        var fksPorTabla = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in esquema.LlavesForaneas)
        {
            if (!fksPorTabla.TryGetValue(fk.TablaHija, out var set))
                fksPorTabla[fk.TablaHija] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in fk.ColumnasHijaCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                set.Add(c);
        }

        // Ordena por nombre de tabla para salida estable
        foreach (var tabla in esquema.Tablas.Select(t => t.Nombre).OrderBy(n => n))
        {
            // Omitir tablas auxiliares del modelo EER (generalización/especialización)
            if (tabla.StartsWith("EER_", StringComparison.OrdinalIgnoreCase))
                continue;

            var cols = esquema.Columnas.Where(c => c.Tabla == tabla).ToList();

            // Orden: PK primero, luego UK, luego el resto (estético)
            var ordenadas = cols
                .OrderByDescending(c => c.EsPk)
                .ThenByDescending(c => c.EsUnicoCandidato && !c.EsPk)
                .ThenBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string Formatear(string nombre, bool esPk, bool esFk, bool esUk)
            {
                // HTML bonito (subrayado PK, cursiva FK, [UK] sufijo)
                if (comoHtml)
                {
                    var n = nombre;
                    if (esPk) n = $"<u>{n}</u>";
                    if (esFk) n = $"<i>{n}</i>";
                    if (esUk && !esPk) n += " <small>[UK]</small>";
                    if (esPk && esFk) n += " <small>[FK]</small>"; // PK=FK visible
                    return n;
                }
                // Texto plano
                var tags = new List<string>();
                if (esPk) tags.Add("PK");
                if (esFk) tags.Add("FK");
                if (esUk && !esPk) tags.Add("UK");
                return tags.Count == 0 ? nombre : $"{nombre} [{string.Join(", ", tags)}]";
            }

            var fkSet = fksPorTabla.TryGetValue(tabla, out var s) ? s : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var atributos = ordenadas.Select(c =>
                Formatear(c.Nombre, c.EsPk, fkSet.Contains(c.Nombre), c.EsUnicoCandidato)
            );

            // Línea tipo: TABLA(col1, col2, ...)
            if (comoHtml)
                sb.AppendLine($"<div><b>{tabla}</b>({string.Join(", ", atributos)})</div>");
            else
                sb.AppendLine($"{tabla}({string.Join(", ", atributos)})");
        }

        return sb.ToString();
    }
}
