using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#region Modelos EER

/// <summary>Estado de disyunción en la jerarquía EER.</summary>
public enum EerDisjointness { Exclusive, Overlapping, Ambiguous }
/// <summary>Estado de totalidad en la jerarquía EER.</summary>
public enum EerTotalness { Total, Partial, Ambiguous }

/// <summary>
/// Jerarquía de especialización (EER) detectada:
/// Supertipo, lista de Subtipos y heurísticas de Disyunción/Totalidad.
/// </summary>
public class JerarquiaEer
{
    public string Supertipo { get; init; } = "";
    public List<string> Subtipos { get; } = new();
    public EerDisjointness Disyuncion { get; set; } = EerDisjointness.Ambiguous;
    public EerTotalness Totalidad { get; set; } = EerTotalness.Ambiguous;
    public string? Evidencia { get; set; }
}

#endregion

/// <summary>
/// Motor de inferencia EER: detecta jerarquías por patrón PK=FK (FK UNIQUE) y
/// renderiza un diagrama Mermaid con etiqueta de “especialización”.
/// </summary>
public static class InferenciaEER
{
    /// <summary>
    /// Detecta jerarquías de especialización (subtipos) por el patrón PK=FK:
    /// la FK en la hija es única y coincide (en orden) con la PK de la hija.
    /// </summary>
    /// <param name="s">Instantánea del esquema con tablas, columnas y FKs agrupadas.</param>
    /// <returns>Lista de <see cref="JerarquiaEer"/> detectadas.</returns>
    public static List<JerarquiaEer> DetectarJerarquias(InstantaneaEsquema s)
    {
        // PK por tabla (para comparar con columnas de las FK en hija)
        var pkPorTabla = s.Tablas.ToDictionary(
            t => t.Nombre,
            t => s.Columnas.Where(c => c.Tabla == t.Nombre && c.EsPk).Select(c => c.Nombre).ToList(),
            StringComparer.OrdinalIgnoreCase);

        // Candidatos a subtipo: FK única en hija y columnas FK == PK(hija)
        var candidatos = s.LlavesForaneas.Where(fk =>
        {
            if (!fk.HijaEsUnica) return false;
            if (!pkPorTabla.TryGetValue(fk.TablaHija, out var pkCols) || pkCols.Count == 0) return false;

            var fkCols = fk.ColumnasHijaCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return pkCols.SequenceEqual(fkCols, StringComparer.OrdinalIgnoreCase);
        }).ToList();

        // Agrupar por padre → Supertipo; hijas → Subtipos
        var grupos = candidatos.GroupBy(x => x.TablaPadre, StringComparer.OrdinalIgnoreCase);
        var lista = new List<JerarquiaEer>();

        foreach (var g in grupos)
        {
            var j = new JerarquiaEer { Supertipo = g.Key };
            foreach (var fk in g) j.Subtipos.Add(fk.TablaHija);

            // Heurística: discriminador NOT NULL en el supertipo → Disyunción exclusiva.
            var disc = s.Columnas.FirstOrDefault(c =>
                c.Tabla.Equals(j.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                (c.Nombre.Equals("Tipo", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Discriminator", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Categoria", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Clase", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Subtipo", StringComparison.OrdinalIgnoreCase)));

            if (disc is not null && !disc.EsNulo)
            {
                j.Disyuncion = EerDisjointness.Exclusive;
                j.Totalidad = EerTotalness.Ambiguous;
                j.Evidencia = $"Discriminador {disc.Nombre} en {j.Supertipo} (NOT NULL).";
            }
            else
            {
                j.Disyuncion = j.Subtipos.Count > 1 ? EerDisjointness.Overlapping : EerDisjointness.Ambiguous;
                j.Totalidad = EerTotalness.Ambiguous;
                j.Evidencia = "Subtipos detectados por patrón PK=FK (FK UNIQUE).";
            }

            lista.Add(j);
        }

        return lista;
    }

    /// <summary>
    /// Renderiza Mermaid (flowchart TB) para jerarquías EER con nodo “especialización”.
    /// </summary>
    public static string RenderMermaidEER(IReadOnlyList<JerarquiaEer> hs)
    {
        // Sanear IDs Mermaid
        string San(string s)
        {
            var x = new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
            if (x.Length == 0 || !char.IsLetter(x[0])) x = "N_" + x;
            return x.Length > 60 ? x[..60] : x;
        }
        // Escapar texto para Mermaid
        string Esc(string? s) =>
            (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", " ").Replace("\n", " ");

        var sb = new StringBuilder();
        sb.AppendLine("flowchart TB");
        sb.AppendLine("classDef super fill:#e8f4ff,stroke:#2a6fb3,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef sub fill:#eef9f0,stroke:#2b8a3e,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef note fill:#fff7e6,stroke:#b37400,stroke-dasharray:3 2;");

        int k = 0;
        foreach (var h in hs)
        {
            var tagDis = h.Disyuncion switch
            {
                EerDisjointness.Exclusive => "exclusive",
                EerDisjointness.Overlapping => "overlapping",
                _ => "ambiguous"
            };
            var tagTot = h.Totalidad switch
            {
                EerTotalness.Total => "total",
                EerTotalness.Partial => "partial",
                _ => "ambiguous"
            };

            var supId = San(h.Supertipo);
            sb.AppendLine($"{supId}[\"{Esc(h.Supertipo)} ({tagDis},{tagTot})\"]:::super");

            var genId = $"GEN_{k++}_{supId}";
            sb.AppendLine($"{genId}[[especializacion]]:::note");
            sb.AppendLine($"{genId} --> {supId}");

            foreach (var sub in h.Subtipos.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var subId = San(sub);
                sb.AppendLine($"{subId}(\"{Esc(sub)}\"):::sub");
                sb.AppendLine($"{subId} -->|is a| {genId}");
            }

            if (!string.IsNullOrWhiteSpace(h.Evidencia))
            {
                var noteId = $"NOTE_{supId}";
                sb.AppendLine($"{noteId}[\"{Esc(h.Evidencia)}\"]:::note");
                sb.AppendLine($"{noteId} -.-> {supId}");
            }
        }
        return sb.ToString();
    }
}
