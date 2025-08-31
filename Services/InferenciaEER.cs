using System;
using System.Collections.Generic;
using System.Text;

#region Modelos EER
public enum EerDisjointness { Exclusive, Overlapping, Ambiguous }
public enum EerTotalness { Total, Partial, Ambiguous }

public class JerarquiaEer
{
    public string Supertipo { get; init; } = "";
    public List<string> Subtipos { get; } = new();
    public EerDisjointness Disyuncion { get; set; } = EerDisjointness.Ambiguous;
    public EerTotalness Totalidad { get; set; } = EerTotalness.Ambiguous;
    public string? Evidencia { get; set; }
}
#endregion

public static class InferenciaEER
{
    public static List<JerarquiaEer> DetectarJerarquias(InstantaneaEsquema s)
    {
        var pkPorTabla = ConstruirPkPorTabla(s);
        var candidatos = BuscarCandidatos(s, pkPorTabla);
        return AgruparJerarquias(candidatos, s);
    }

    private static Dictionary<string, List<string>> ConstruirPkPorTabla(InstantaneaEsquema s)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in s.Tablas)
        {
            var cols = new List<string>();
            foreach (var c in s.Columnas)
                if (c.Tabla == t.Nombre && c.EsPk)
                    cols.Add(c.Nombre);
            dict[t.Nombre] = cols;
        }
        return dict;
    }

    private static List<InfoLlaveForanea> BuscarCandidatos(InstantaneaEsquema s, Dictionary<string, List<string>> pkPorTabla)
    {
        var list = new List<InfoLlaveForanea>();
        foreach (var fk in s.LlavesForaneas)
        {
            if (!fk.HijaEsUnica) continue;
            if (!pkPorTabla.TryGetValue(fk.TablaHija, out var pkCols) || pkCols.Count == 0) continue;
            var fkCols = fk.ColumnasHijaCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (pkCols.Count != fkCols.Length) continue;
            bool igual = true;
            for (int i = 0; i < pkCols.Count; i++)
                if (!pkCols[i].Equals(fkCols[i], StringComparison.OrdinalIgnoreCase))
                { igual = false; break; }
            if (igual) list.Add(fk);
        }
        return list;
    }

    private static List<JerarquiaEer> AgruparJerarquias(List<InfoLlaveForanea> candidatos, InstantaneaEsquema s)
    {
        var porPadre = new Dictionary<string, List<InfoLlaveForanea>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in candidatos)
        {
            if (!porPadre.TryGetValue(fk.TablaPadre, out var list))
            {
                list = new List<InfoLlaveForanea>();
                porPadre[fk.TablaPadre] = list;
            }
            list.Add(fk);
        }

        var resultado = new List<JerarquiaEer>();
        foreach (var kv in porPadre)
            resultado.Add(CrearJerarquia(kv.Key, kv.Value, s));
        return resultado;
    }

    private static JerarquiaEer CrearJerarquia(string padre, List<InfoLlaveForanea> fks, InstantaneaEsquema s)
    {
        var j = new JerarquiaEer { Supertipo = padre };
        foreach (var fk in fks) j.Subtipos.Add(fk.TablaHija);

        var disc = BuscarDiscriminador(padre, s);
        if (disc is not null && !disc.EsNulo)
        {
            j.Disyuncion = EerDisjointness.Exclusive;
            j.Evidencia = $"Discriminador {disc.Nombre} en {padre} (NOT NULL).";
        }
        else
        {
            j.Disyuncion = j.Subtipos.Count > 1 ? EerDisjointness.Overlapping : EerDisjointness.Ambiguous;
            j.Evidencia = "Subtipos detectados por patrón PK=FK (FK UNIQUE).";
        }

        j.Totalidad = EerTotalness.Ambiguous;
        return j;
    }

    private static InfoColumna? BuscarDiscriminador(string sup, InstantaneaEsquema s)
    {
        foreach (var c in s.Columnas)
        {
            if (!c.Tabla.Equals(sup, StringComparison.OrdinalIgnoreCase)) continue;
            var n = c.Nombre;
            if (n.Equals("Tipo", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Discriminator", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Categoria", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Clase", StringComparison.OrdinalIgnoreCase) ||
                n.Equals("Subtipo", StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    public static string RenderMermaidEER(IReadOnlyList<JerarquiaEer> hs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart TB");
        sb.AppendLine("classDef super fill:#e8f4ff,stroke:#2a6fb3,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef sub fill:#eef9f0,stroke:#2b8a3e,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef note fill:#fff7e6,stroke:#b37400,stroke-dasharray:3 2;");

        int k = 0;
        foreach (var h in hs)
            RenderJerarquia(sb, h, ref k);
        return sb.ToString();
    }

    private static string SanId(string s)
    {
        var sb = new StringBuilder();
        foreach (var ch in s)
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
        if (sb.Length == 0 || !char.IsLetter(sb[0])) sb.Insert(0, "N_");
        var r = sb.ToString();
        return r.Length > 60 ? r[..60] : r;
    }

    private static string EscTxt(string? s) =>
        (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");

    private static void RenderJerarquia(StringBuilder sb, JerarquiaEer h, ref int k)
    {
        var tagDis = h.Disyuncion switch
        {
            EerDisjointness.Exclusive => "exclusive",
            EerDisjointness.Overlapping => "overlapping",
            _ => "ambiguous",
        };
        var tagTot = h.Totalidad switch
        {
            EerTotalness.Total => "total",
            EerTotalness.Partial => "partial",
            _ => "ambiguous",
        };

        var supId = SanId(h.Supertipo);
        sb.AppendLine($"{supId}[\"{EscTxt(h.Supertipo)} ({tagDis},{tagTot})\"]:::super");

        var genId = $"GEN_{k++}_{supId}";
        sb.AppendLine($"{genId}[[especializacion]]:::note");
        sb.AppendLine($"{genId} --> {supId}");

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sub in h.Subtipos)
        {
            if (!vistos.Add(sub)) continue;
            var subId = SanId(sub);
            sb.AppendLine($"{subId}(\"{EscTxt(sub)}\"):::sub");
            sb.AppendLine($"{subId} -->|is a| {genId}");
        }

        if (!string.IsNullOrWhiteSpace(h.Evidencia))
        {
            var noteId = $"NOTE_{supId}";
            sb.AppendLine($"{noteId}[\"{EscTxt(h.Evidencia)}\"]:::note");
            sb.AppendLine($"{noteId} -.-> {supId}");
        }
    }
}
