using System;
using System.Collections.Generic;
using System.Text;

public class ConstructorDiagramaChen
{
    /// <summary>Sanea un identificador para Mermaid.</summary>
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

    /// <summary>
    /// Escapa texto para que no rompa el parser de Mermaid:
    /// backslashes, comillas, saltos de línea y llaves.
    /// </summary>
    private static string Esc(string? txt)
    {
        if (string.IsNullOrEmpty(txt)) return "";
        return txt
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("{", "\\{")
            .Replace("}", "\\}");
    }

    public string Construir(InstantaneaEsquema s)
    {
        var sb = new StringBuilder();
        var ocultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EER_UserChoices" };
        AgregarEncabezado(sb);
        AgregarEntidades(sb, s, ocultas);
        AgregarRelacionesBinarias(sb, s, ocultas);
        AgregarRelacionesMN(sb, s, ocultas);
        AgregarEntidadesDebiles(sb, s, ocultas);
        return sb.ToString();
    }

    private static void AgregarEncabezado(StringBuilder sb)
    {
        sb.AppendLine("flowchart LR");
        sb.AppendLine("classDef entidad fill:#eef,stroke:#334,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef relacion fill:#ffe,stroke:#a66,stroke-width:2px;");
        sb.AppendLine("classDef atributo fill:#eef,stroke:#557;");
        sb.AppendLine("classDef clave font-weight:bold,text-decoration:underline;");
        sb.AppendLine("classDef unico stroke-dasharray:3 2;");
    }

    private static void AgregarEntidades(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var t in s.Tablas)
        {
            if (ocultas.Contains(t.Nombre)) continue;
            if (s.TablasUnionMuchosAMuchos.Contains(t.Nombre)) continue;

            var entId = San(t.Nombre);
            sb.AppendLine($"  {entId}[{Esc(t.Nombre)}]:::entidad");

            foreach (var c in s.Columnas)
            {
                if (c.Tabla != t.Nombre) continue;
                var attrId = $"{entId}__{San(c.Nombre)}";
                sb.AppendLine($"  {attrId}(({Esc(c.Nombre)})):::atributo");
                if (c.EsPk) sb.AppendLine($"  class {attrId} clave;");
                else if (c.EsUnicoCandidato) sb.AppendLine($"  class {attrId} unico;");
                sb.AppendLine($"  {attrId} --- {entId}");
            }
        }
    }

    private static void AgregarRelacionesBinarias(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        int r = 0;
        foreach (var fk in s.LlavesForaneas)
        {
            if (ocultas.Contains(fk.TablaPadre) || ocultas.Contains(fk.TablaHija)) continue;
            if (s.TablasUnionMuchosAMuchos.Contains(fk.TablaHija)) continue;

            var relId = $"REL_{r++}_{San(fk.Nombre)}";
            sb.AppendLine($"  {relId}{{{{{Esc(fk.Nombre)}}}}}:::relacion");
            sb.AppendLine($"  {San(fk.TablaPadre)} -- \"1\" --> {relId}");

            var mult = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "1" : "0..1")
                : (fk.HijaTodasNoNulas ? "1..N" : "0..N");

            var flecha = fk.HijaTodasNoNulas ? "--" : "-.";
            var cola = fk.HijaTodasNoNulas ? "-->" : ".->";
            sb.AppendLine($"  {relId} {flecha} \"{mult}\" {cola} {San(fk.TablaHija)}");
        }
    }

    private static void AgregarRelacionesMN(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var jt in s.TablasUnionMuchosAMuchos)
        {
            if (ocultas.Contains(jt)) continue;

            var padres = new List<string>();
            foreach (var fk in s.LlavesForaneas)
                if (fk.TablaHija == jt && !padres.Contains(fk.TablaPadre))
                    padres.Add(fk.TablaPadre);

            if (padres.Count != 2) continue;

            var relId = $"MN_{San(jt)}";
            sb.AppendLine($"  {relId}{{{{{Esc(jt)}}}}}:::relacion");
            sb.AppendLine($"  {San(padres[0])} -- \"1..N\" --> {relId}");
            sb.AppendLine($"  {relId} -- \"1..N\" --> {San(padres[1])}");

            var fkCols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var fk in s.LlavesForaneas)
            {
                if (fk.TablaHija != jt) continue;
                var cols = fk.ColumnasHijaCsv.Split(',', StringSplitOptions.RemoveEmptyEntries);
                foreach (var col in cols) fkCols.Add(col.Trim());
            }

            foreach (var c in s.Columnas)
            {
                if (c.Tabla != jt || fkCols.Contains(c.Nombre)) continue;
                var aid = $"{San(jt)}__{San(c.Nombre)}";
                sb.AppendLine($"  {aid}(({Esc(c.Nombre)})):::atributo");
                if (c.EsPk) sb.AppendLine($"  class {aid} clave;");
                sb.AppendLine($"  {aid} --- {relId}");
            }
        }
    }

    private static void AgregarEntidadesDebiles(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var w in s.EntidadesDebiles)
            if (!ocultas.Contains(w))
                sb.AppendLine($"  %% {w} es ENTIDAD DEBIL (PK incluye FK)");
    }
}
