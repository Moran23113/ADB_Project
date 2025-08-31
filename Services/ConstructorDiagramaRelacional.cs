using System;
using System.Collections.Generic;

public class ConstructorDiagramaRelacional
{
    private static string Limpiar(string t)
    {
        var sb = new System.Text.StringBuilder(t.Length);
        foreach (var ch in t)
            sb.Append(char.IsLetterOrDigit(ch) || ch == '_' ? ch : '_');
        return sb.ToString();
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
        _ => Limpiar(t)
    };

    /// Genera Mermaid erDiagram (crow’s foot) a partir de InstantaneaEsquema.
    public string Construir(InstantaneaEsquema s)
    {
        var ocultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EER_UserChoices" };
        var mb = new MermaidBuilder();
        mb.AddRaw("erDiagram");

        var fksPorTabla = IndiceFksPorTabla(s);
        AgregarTablas(mb, s, ocultas, fksPorTabla);
        AgregarRelaciones(mb, s, ocultas);
        return mb.Build();
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

    private static void AgregarTablas(MermaidBuilder mb, InstantaneaEsquema s, HashSet<string> ocultas, Dictionary<string, HashSet<string>> fksPorTabla)
    {
        foreach (var tabla in s.Tablas)
        {
            var t = tabla.Nombre;
            if (ocultas.Contains(t)) continue;

            fksPorTabla.TryGetValue(t, out var fkCols);
            fkCols ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var tid = MermaidUtils.SanitizeId(t);
            mb.AddRaw($"  {tid} {{");
            foreach (var c in s.Columnas)
            {
                if (c.Tabla != t) continue;
                var tipo = MapTipo(c.Tipo);
                var flags = new List<string>();
                if (c.EsPk) flags.Add("PK");
                if (fkCols.Contains(c.Nombre)) flags.Add("FK");
                if (c.EsUnicoCandidato && !c.EsPk) flags.Add("UK");
                var flagTxt = flags.Count > 0 ? " " + string.Join(" ", flags) : string.Empty;
                mb.AddRaw($"    {MermaidUtils.EscapeText(tipo)} {MermaidUtils.EscapeText(c.Nombre)}{flagTxt}");
            }
            mb.AddRaw("  }");
        }
    }

    private static void AgregarRelaciones(MermaidBuilder mb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var fk in s.LlavesForaneas)
        {
            if (ocultas.Contains(fk.TablaPadre) || ocultas.Contains(fk.TablaHija)) continue;

            var left = "||";
            var right = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "||" : "o|")
                : (fk.HijaTodasNoNulas ? "|{" : "o{");

            var etiqueta = MermaidUtils.EscapeText(fk.Nombre);
            mb.AddRaw($"  {MermaidUtils.SanitizeId(fk.TablaPadre)} {left}--{right} {MermaidUtils.SanitizeId(fk.TablaHija)} : \"{etiqueta}\"");
        }
    }
}
