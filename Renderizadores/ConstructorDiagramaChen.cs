using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ABD_Project.Modelos;
using ABD_Project.Utilidades;

/// <summary>
/// Renderizador que genera un diagrama ER en notación Chen
/// utilizando la sintaxis de Mermaid (flowchart).
/// Pinta entidades, atributos y relaciones indicando cardinalidad.
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
        "EER_UserChoices"
    };

        // Encabezado y estilos
        sb.AppendLine("flowchart LR");
        sb.AppendLine("classDef entidad fill:#eef,stroke:#334,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef relacion fill:#ffe,stroke:#a66,stroke-width:2px;");
        sb.AppendLine("classDef atributo fill:#eef,stroke:#557;");
        sb.AppendLine("classDef clave font-weight:bold,text-decoration:underline;");
        sb.AppendLine("classDef unico stroke-dasharray:3 2;");

        // -------- Entidades --------
        foreach (var t in s.Tablas)
        {
            if (ocultas.Contains(t.Nombre)) continue; // 👈 filtra EER_UserChoices
            if (s.TablasUnionMuchosAMuchos.Contains(t.Nombre)) continue;

            var entId = TextoMermaid.San(t.Nombre);
            sb.AppendLine($"  {entId}[{TextoMermaid.Esc(t.Nombre)}]:::entidad");

            foreach (var c in s.Columnas.Where(x => x.Tabla == t.Nombre))
            {
                var attrId = $"{entId}__{TextoMermaid.San(c.Nombre)}";
                sb.AppendLine($"  {attrId}(({TextoMermaid.Esc(c.Nombre)})):::atributo");

                if (c.EsPk) sb.AppendLine($"  class {attrId} clave;");
                else if (c.EsUnicoCandidato) sb.AppendLine($"  class {attrId} unico;");

                sb.AppendLine($"  {attrId} --- {entId}");
            }
        }

        // -------- Relaciones binarias (FKs) --------
        int r = 0;
        foreach (var fk in s.LlavesForaneas)
        {
            if (ocultas.Contains(fk.TablaPadre) || ocultas.Contains(fk.TablaHija)) continue; // 👈 filtra
            if (s.TablasUnionMuchosAMuchos.Contains(fk.TablaHija)) continue;

            var relId = $"REL_{r++}_{TextoMermaid.San(fk.Nombre)}";
            sb.AppendLine($"  {relId}{{{{{TextoMermaid.Esc(fk.Nombre)}}}}}:::relacion");
            sb.AppendLine($"  {TextoMermaid.San(fk.TablaPadre)} -- \"1\" --> {relId}");

            string mult = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "1" : "0..1")
                : (fk.HijaTodasNoNulas ? "1..N" : "0..N");

            if (fk.HijaTodasNoNulas)
                sb.AppendLine($"  {relId} -- \"{mult}\" --> {TextoMermaid.San(fk.TablaHija)}");
            else
                sb.AppendLine($"  {relId} -. \"{mult}\" .-> {TextoMermaid.San(fk.TablaHija)}");
        }

        // -------- Relaciones M:N --------
        foreach (var jt in s.TablasUnionMuchosAMuchos)
        {
            if (ocultas.Contains(jt)) continue; // 👈 filtra
            var padres = s.LlavesForaneas
                .Where(f => f.TablaHija == jt)
                .Select(f => f.TablaPadre)
                .Distinct()
                .ToList();

            if (padres.Count == 2)
            {
                var relId = $"MN_{TextoMermaid.San(jt)}";
                sb.AppendLine($"  {relId}{{{{{TextoMermaid.Esc(jt)}}}}}:::relacion");

                sb.AppendLine($"  {TextoMermaid.San(padres[0])} -- \"1..N\" --> {relId}");
                sb.AppendLine($"  {relId} -- \"1..N\" --> {TextoMermaid.San(padres[1])}");

                var fkCols = new HashSet<string>(
                    s.LlavesForaneas.Where(f => f.TablaHija == jt)
                        .SelectMany(f => f.ColumnasHijaCsv.Split(',').Select(x => x.Trim())),
                    StringComparer.OrdinalIgnoreCase);

                var attrsRelacion = s.Columnas.Where(c => c.Tabla == jt && !fkCols.Contains(c.Nombre));
                foreach (var c in attrsRelacion)
                {
                    var aid = $"{TextoMermaid.San(jt)}__{TextoMermaid.San(c.Nombre)}";
                    sb.AppendLine($"  {aid}(({TextoMermaid.Esc(c.Nombre)})):::atributo");
                    if (c.EsPk) sb.AppendLine($"  class {aid} clave;");
                    sb.AppendLine($"  {aid} --- {relId}");
                }
            }
        }

        foreach (var w in s.EntidadesDebiles)
            if (!ocultas.Contains(w)) // 👈 también aquí
                sb.AppendLine($"  %% {w} es ENTIDAD DEBIL (PK incluye FK)");

        return sb.ToString();
    }

}
