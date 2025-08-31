using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Construye un diagrama ER en notación Chen usando sintaxis de Mermaid (flowchart),
/// a partir de una instantánea del esquema (tablas, columnas, FKs, etc.).
/// </summary>
public class ConstructorDiagramaChen
{
    /// <summary>
    /// Construye el diagrama Mermaid (flowchart LR) en notación tipo Chen.
    /// </summary>
    /// <param name="s">Instantánea del esquema (tablas, columnas, FKs, tablas puente, etc.).</param>
    /// <returns>Texto Mermaid listo para renderizar.</returns>
    public string Construir(InstantaneaEsquema s)
    {
        var sb = new StringBuilder();

        // Tablas internas que no deben dibujarse
        var ocultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "EER_UserChoices",
        };

        // Encabezado y estilos
        sb.AppendLine("flowchart LR");
        sb.AppendLine("classDef entidad fill:#eef,stroke:#334,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef relacion fill:#ffe,stroke:#a66,stroke-width:2px;");
        sb.AppendLine("classDef atributo fill:#eef,stroke:#557;");
        sb.AppendLine("classDef clave font-weight:bold,text-decoration:underline;");
        sb.AppendLine("classDef unico stroke-dasharray:3 2;");

        RenderEntidades(sb, s, ocultas);
        RenderRelacionesBinarias(sb, s, ocultas);
        RenderRelacionesMN(sb, s, ocultas);
        RenderEntidadesDebiles(sb, s, ocultas);

        return sb.ToString();
    }

    /// <summary>
    /// Pinta las entidades y sus atributos a partir de las tablas.
    /// </summary>
    private static void RenderEntidades(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var t in s.Tablas)
        {
            if (ocultas.Contains(t.Nombre)) continue;
            if (s.TablasUnionMuchosAMuchos.Contains(t.Nombre)) continue;

            var entId = MermaidUtils.SanitizeId(t.Nombre);
            sb.AppendLine($"  {entId}[{MermaidUtils.EscapeText(t.Nombre)}]:::entidad");

            foreach (var c in s.Columnas.Where(x => x.Tabla == t.Nombre))
            {
                var attrId = $"{entId}__{MermaidUtils.SanitizeId(c.Nombre)}";
                sb.AppendLine($"  {attrId}(({MermaidUtils.EscapeText(c.Nombre)})):::atributo");

                if (c.EsPk) sb.AppendLine($"  class {attrId} clave;");
                else if (c.EsUnicoCandidato) sb.AppendLine($"  class {attrId} unico;");

                sb.AppendLine($"  {attrId} --- {entId}");
            }
        }
    }

    /// <summary>
    /// Dibuja las relaciones binarias basadas en llaves foráneas.
    /// </summary>
    private static void RenderRelacionesBinarias(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        int r = 0;
        foreach (var fk in s.LlavesForaneas)
        {
            if (ocultas.Contains(fk.TablaPadre) || ocultas.Contains(fk.TablaHija)) continue;
            if (s.TablasUnionMuchosAMuchos.Contains(fk.TablaHija)) continue;

            var relId = $"REL_{r++}_{MermaidUtils.SanitizeId(fk.Nombre)}";
            sb.AppendLine($"  {relId}{{{{{MermaidUtils.EscapeText(fk.Nombre)}}}}}:::relacion");
            sb.AppendLine($"  {MermaidUtils.SanitizeId(fk.TablaPadre)} -- \"1\" --> {relId}");

            string mult = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "1" : "0..1")
                : (fk.HijaTodasNoNulas ? "1..N" : "0..N");

            if (fk.HijaTodasNoNulas)
                sb.AppendLine($"  {relId} -- \"{mult}\" --> {MermaidUtils.SanitizeId(fk.TablaHija)}");
            else
                sb.AppendLine($"  {relId} -. \"{mult}\" .-> {MermaidUtils.SanitizeId(fk.TablaHija)}");
        }
    }

    /// <summary>
    /// Representa las relaciones muchos a muchos a partir de tablas puente.
    /// </summary>
    private static void RenderRelacionesMN(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var jt in s.TablasUnionMuchosAMuchos)
        {
            if (ocultas.Contains(jt)) continue;

            var padres = s.LlavesForaneas
                .Where(f => f.TablaHija == jt)
                .Select(f => f.TablaPadre)
                .Distinct()
                .ToList();

            if (padres.Count == 2)
            {
                var relId = $"MN_{MermaidUtils.SanitizeId(jt)}";
                sb.AppendLine($"  {relId}{{{{{MermaidUtils.EscapeText(jt)}}}}}:::relacion");

                sb.AppendLine($"  {MermaidUtils.SanitizeId(padres[0])} -- \"1..N\" --> {relId}");
                sb.AppendLine($"  {relId} -- \"1..N\" --> {MermaidUtils.SanitizeId(padres[1])}");

                var fkCols = new HashSet<string>(
                    s.LlavesForaneas.Where(f => f.TablaHija == jt)
                        .SelectMany(f => f.ColumnasHijaCsv.Split(',').Select(x => x.Trim())),
                    StringComparer.OrdinalIgnoreCase);

                var attrsRelacion = s.Columnas.Where(c => c.Tabla == jt && !fkCols.Contains(c.Nombre));
                foreach (var c in attrsRelacion)
                {
                    var aid = $"{MermaidUtils.SanitizeId(jt)}__{MermaidUtils.SanitizeId(c.Nombre)}";
                    sb.AppendLine($"  {aid}(({MermaidUtils.EscapeText(c.Nombre)})):::atributo");
                    if (c.EsPk) sb.AppendLine($"  class {aid} clave;");
                    sb.AppendLine($"  {aid} --- {relId}");
                }
            }
        }
    }

    /// <summary>
    /// Anota en el diagrama las entidades débiles detectadas.
    /// </summary>
    private static void RenderEntidadesDebiles(StringBuilder sb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var w in s.EntidadesDebiles)
            if (!ocultas.Contains(w))
                sb.AppendLine($"  %% {w} es ENTIDAD DEBIL (PK incluye FK)");
    }
}
