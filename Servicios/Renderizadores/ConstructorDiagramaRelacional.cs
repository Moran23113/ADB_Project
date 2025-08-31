using ABD_Project.Servicios.Modelos;
using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace ABD_Project.Servicios.Renderizadores;

public class ConstructorDiagramaRelacional
{
    private static readonly Regex _idBad = new(@"[^A-Za-z0-9_]", RegexOptions.Compiled);

    private static string San(string? id)
    {
        var s = id ?? "X";
        s = _idBad.Replace(s, "_");
        if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0])) s = "N_" + s;
        if (s.Length > 60) s = s[..60];
        return s;
    }

    // Escapar caracteres que rompen erDiagram, incluidas comillas
    private static string Esc(string? txt)
    {
        if (string.IsNullOrEmpty(txt)) return "";
        return txt.Replace("\r", " ")
                  .Replace("\n", " ")
                  .Replace("\"", "&quot;")
                  .Replace("<", "&lt;")
                  .Replace(">", "&gt;")
                  .Replace("{", "\\{").Replace("}", "\\}")
                  .Replace("[", "&#91;").Replace("]", "&#93;");
    }

    private static string MapTipo(string t) => t switch
    {
        "int" => "int",
        "bigint" => "bigint",
        "smallint" => "smallint",
        "bit" => "bit",
        "date" => "date",
        "datetime" or "datetime2" => "datetime2",
        "decimal" or "numeric" => "decimal",
        "float" => "float",
        "varchar" or "nvarchar" => "varchar",
        _ => _idBad.Replace(t, "_") // simplifica tipos raros para Mermaid
    };

    /// Genera Mermaid erDiagram (crow’s foot) a partir de InstantaneaEsquema.
    public string Construir(InstantaneaEsquema s)
    {
        var ocultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EER_UserChoices" };
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        // --------- Índice: columnas FK por tabla (maneja FKs compuestas) ----------
        var fksPorTabla = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in s.LlavesForaneas)
        {
            if (!fksPorTabla.TryGetValue(fk.TablaHija, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                fksPorTabla[fk.TablaHija] = set;
            }

            foreach (var col in fk.ColumnasHijaCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                set.Add(col);
        }

        // ------------------------- Tablas + columnas ------------------------------
        foreach (var t in s.Tablas.Select(x => x.Nombre))
        {
            if (ocultas.Contains(t)) continue;

            // Hash de FKs de esta tabla (si no tiene, set vacío)
            fksPorTabla.TryGetValue(t, out var fkCols);
            fkCols ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            sb.AppendLine($"  {San(t)} {{");
            foreach (var c in s.Columnas.Where(c => c.Tabla == t))
            {
                var tipo = MapTipo(c.Tipo);

                // Flags: PK / FK / UK (sin paréntesis; Mermaid los muestra como texto a la derecha)
                var flags = new List<string>();
                if (c.EsPk) flags.Add("PK");
                if (fkCols.Contains(c.Nombre)) flags.Add("FK");
                if (c.EsUnicoCandidato && !c.EsPk) flags.Add("UK"); // si ya es PK, no repito UK

                var flagTxt = flags.Count > 0 ? " " + string.Join(" ", flags) : "";
                sb.AppendLine($"    {Esc(tipo)} {Esc(c.Nombre)}{flagTxt}");
            }
            sb.AppendLine("  }");
        }

        // --------------------------- Relaciones (patas) ---------------------------
        foreach (var fk in s.LlavesForaneas)
        {
            if (ocultas.Contains(fk.TablaPadre) || ocultas.Contains(fk.TablaHija)) continue;

            var left = "||"; // padre: exactamente 1
            string right = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "||" : "o|")   // 1:1 o 0..1
                : (fk.HijaTodasNoNulas ? "|{" : "o{");  // 1:N o 0..N

            var etiqueta = Esc(fk.Nombre);
            sb.AppendLine($"  {San(fk.TablaPadre)} {left}--{right} {San(fk.TablaHija)} : \"{etiqueta}\"");
        }

        return sb.ToString();
    }
}
