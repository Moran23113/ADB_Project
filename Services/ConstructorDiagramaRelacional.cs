using System;
using System.Text;
using System.Collections.Generic;

public class ConstructorDiagramaRelacional
{
    private static string San(string? id)
    {
        var src = string.IsNullOrEmpty(id) ? "X" : id;
        var sb = new StringBuilder(src.Length);
        foreach (var ch in src)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        var s = sb.ToString();
        if (s.Length == 0 || !char.IsLetter(s[0])) s = "N_" + s;
        return s.Length > 60 ? s[..60] : s;
    }

    private static string Limpiar(string t)
    {
        var sb = new StringBuilder(t.Length);
        foreach (var ch in t)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        return sb.ToString();
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
        _ => Limpiar(t) // simplifica tipos raros para Mermaid
    };
    /// Genera Mermaid erDiagram (crow’s foot) a partir de InstantaneaEsquema.
    public string Construir(InstantaneaEsquema s)
    {
        var ocultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EER_UserChoices" };
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        var fksPorTabla = IndiceFksPorTabla(s);
        AgregarTablas(sb, s, ocultas, fksPorTabla);
        AgregarRelaciones(sb, s, ocultas);
        return sb.ToString();
    }

    private static Dictionary<string, HashSet<string>> IndiceFksPorTabla(InstantaneaEsquema s)
    {
        var dict = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in s.LlavesForaneas)
        {
            if (!dict.TryGetValue(fk.TablaHija, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                dict[fk.TablaHija] = set;
            }
            var cols = fk.ColumnasHijaCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var col in cols)
                set.Add(col);
        }
        return dict;
    }

    private static void AgregarTablas(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas, Dictionary<string, HashSet<string>> fksPorTabla)
    {
        foreach (var tabla in s.Tablas)
        {
            var t = tabla.Nombre;
            if (ocultas.Contains(t)) continue;

            fksPorTabla.TryGetValue(t, out var fkCols);
            fkCols ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            sb.AppendLine($"  {San(t)} {{");
            foreach (var c in s.Columnas)
            {
                if (c.Tabla != t) continue;
                var tipo = MapTipo(c.Tipo);
                var flags = new List<string>();
                if (c.EsPk) flags.Add("PK");
                if (fkCols.Contains(c.Nombre)) flags.Add("FK");
                if (c.EsUnicoCandidato && !c.EsPk) flags.Add("UK");
                var flagTxt = flags.Count > 0 ? " " + string.Join(" ", flags) : "";
                sb.AppendLine($"    {Esc(tipo)} {Esc(c.Nombre)}{flagTxt}");
            }
            sb.AppendLine("  }");
        }
    }

    private static void AgregarRelaciones(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var fk in s.LlavesForaneas)
        {
            if (ocultas.Contains(fk.TablaPadre) || ocultas.Contains(fk.TablaHija)) continue;

            var left = "||";
            var right = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "||" : "o|")
                : (fk.HijaTodasNoNulas ? "|{" : "o{");

            var etiqueta = Esc(fk.Nombre);
            sb.AppendLine($"  {San(fk.TablaPadre)} {left}--{right} {San(fk.TablaHija)} : \"{etiqueta}\"");
        }
    }
}
