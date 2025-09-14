using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;



public enum EerDisjointness { Exclusiva, Solapada, Ambiguous }

public enum EerTotalness { Total, Parcial, Ambiguous }





public class JerarquiaEer
{
    public string Supertipo { get; init; } = "";
    public List<string> Subtipos { get; } = new();
    public EerDisjointness Disyuncion { get; set; } = EerDisjointness.Ambiguous;
    public EerTotalness Totalidad { get; set; } = EerTotalness.Ambiguous;
    public string? Evidencia { get; set; }
}






public static class InferenciaEER
{
    
    
    
    
    
    
    public static List<JerarquiaEer> DetectarJerarquias(InstantaneaEsquema s)
    {
        
        var pkPorTabla = s.Tablas.ToDictionary(
            t => t.Nombre,
            t => s.Columnas.Where(c => c.Tabla == t.Nombre && c.EsPk).Select(c => c.Nombre).ToList(),
            StringComparer.OrdinalIgnoreCase);

        
        var candidatos = s.LlavesForaneas.Where(fk =>
        {
            if (!fk.HijaEsUnica) return false;
            if (!pkPorTabla.TryGetValue(fk.TablaHija, out var pkCols) || pkCols.Count == 0) return false;

            var fkCols = fk.ColumnasHijaCsv.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            return pkCols.SequenceEqual(fkCols, StringComparer.OrdinalIgnoreCase);
        }).ToList();

        
        var grupos = candidatos.GroupBy(x => x.TablaPadre, StringComparer.OrdinalIgnoreCase);
        var lista = new List<JerarquiaEer>();

        foreach (var g in grupos)
        {
            var j = new JerarquiaEer { Supertipo = g.Key };
            foreach (var fk in g) j.Subtipos.Add(fk.TablaHija);

            
            var disc = s.Columnas.FirstOrDefault(c =>
                c.Tabla.Equals(j.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                (c.Nombre.Equals("Tipo", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Discriminator", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Categoria", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Clase", StringComparison.OrdinalIgnoreCase) ||
                 c.Nombre.Equals("Subtipo", StringComparison.OrdinalIgnoreCase)));

            if (disc is not null && !disc.EsNulo)
            {
                j.Disyuncion = EerDisjointness.Exclusiva;
                j.Totalidad = EerTotalness.Ambiguous;
                j.Evidencia = $"Discriminador {disc.Nombre} en {j.Supertipo} (NOT NULL).";
            }
            else
            {
                j.Disyuncion = j.Subtipos.Count > 1 ? EerDisjointness.Solapada : EerDisjointness.Ambiguous;
                j.Totalidad = EerTotalness.Ambiguous;
                j.Evidencia = "Subtipos detectados por patrón PK=FK (FK UNIQUE).";
            }

            lista.Add(j);
        }

        return lista;
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
        {
            var tagDis = h.Disyuncion switch
            {
                EerDisjointness.Exclusiva => "exclusiva",
                EerDisjointness.Solapada => "solapada",
                _ => "ambiguous"
            };
            var tagTot = h.Totalidad switch
            {
                EerTotalness.Total => "total",
                EerTotalness.Parcial => "parcial",
                _ => "ambiguous"
            };

            var supId = MermaidUtils.Sanitizar(h.Supertipo);
            sb.AppendLine($"{supId}[\"{MermaidUtils.Escapar(h.Supertipo)} ({tagDis},{tagTot})\"]:::super");

            var genId = $"GEN_{k++}_{supId}";
            sb.AppendLine($"{genId}[[especializacion]]:::note");
            sb.AppendLine($"{supId} --> {genId}");

            foreach (var sub in h.Subtipos.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var subId = MermaidUtils.Sanitizar(sub);
                sb.AppendLine($"{subId}(\"{MermaidUtils.Escapar(sub)}\"):::sub");
                sb.AppendLine($"{genId} --> {subId}");
            }

            if (!string.IsNullOrWhiteSpace(h.Evidencia))
            {
                var noteId = $"NOTE_{supId}";
                sb.AppendLine($"{noteId}[\"{MermaidUtils.Escapar(h.Evidencia)}\"]:::note");
                sb.AppendLine($"{noteId} -.-> {supId}");
            }
        }
        return sb.ToString();
    }
}
