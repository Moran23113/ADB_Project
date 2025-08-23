using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Construye un diagrama ER en notación Chen usando sintaxis de Mermaid (flowchart),
/// a partir de una instantánea del esquema (tablas, columnas, FKs, etc.).
/// - Pinta entidades (cuadros), atributos (óvalos) y relaciones (rombos).
/// - Dibuja cardinalidades 1, 0..1, 1..N, 0..N según la unicidad y nulabilidad de las FKs.
/// - Trata tablas puente (M:N) como relaciones con atributos.
/// </summary>
public class ConstructorDiagramaChen
{
    // Regex para sanear IDs de nodos en Mermaid (solo letras, números y guion bajo).
    private static readonly Regex _idBad = new(@"[^A-Za-z0-9_]", RegexOptions.Compiled);

    /// <summary>
    /// Sanea un identificador para que sea válido en Mermaid:
    /// - Reemplaza caracteres no permitidos por "_".
    /// - Si no inicia con letra, antepone "N_".
    /// - Limita a 60 caracteres (Mermaid puede romper con IDs larguísimos).
    /// </summary>
    private static string San(string? id)
    {
        var s = id ?? "X";
        s = _idBad.Replace(s, "_");
        if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0]))
            s = "N_" + s;
        if (s.Length > 60) s = s[..60];
        return s;
    }

    /// <summary>
    /// Escapa texto para que no rompa el parser de Mermaid:
    /// - Backslashes, comillas, saltos de línea y llaves.
    /// </summary>
    private static string Esc(string? txt)
    {
        if (string.IsNullOrEmpty(txt)) return "";
        return txt
            .Replace("\\", "\\\\")   // backslash
            .Replace("\"", "\\\"")   // comillas
            .Replace("\r", " ")      // CR
            .Replace("\n", " ")      // LF
            .Replace("{", "\\{")     // llaves
            .Replace("}", "\\}");    // llaves
    }

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

            var entId = San(t.Nombre);
            sb.AppendLine($"  {entId}[{Esc(t.Nombre)}]:::entidad");

            foreach (var c in s.Columnas.Where(x => x.Tabla == t.Nombre))
            {
                var attrId = $"{entId}__{San(c.Nombre)}";
                sb.AppendLine($"  {attrId}(({Esc(c.Nombre)})):::atributo");

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

            var relId = $"REL_{r++}_{San(fk.Nombre)}";
            sb.AppendLine($"  {relId}{{{{{Esc(fk.Nombre)}}}}}:::relacion");
            sb.AppendLine($"  {San(fk.TablaPadre)} -- \"1\" --> {relId}");

            string mult = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "1" : "0..1")
                : (fk.HijaTodasNoNulas ? "1..N" : "0..N");

            if (fk.HijaTodasNoNulas)
                sb.AppendLine($"  {relId} -- \"{mult}\" --> {San(fk.TablaHija)}");
            else
                sb.AppendLine($"  {relId} -. \"{mult}\" .-> {San(fk.TablaHija)}");
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
                var relId = $"MN_{San(jt)}";
                sb.AppendLine($"  {relId}{{{{{Esc(jt)}}}}}:::relacion");

                sb.AppendLine($"  {San(padres[0])} -- \"1..N\" --> {relId}");
                sb.AppendLine($"  {relId} -- \"1..N\" --> {San(padres[1])}");

                var fkCols = new HashSet<string>(
                    s.LlavesForaneas.Where(f => f.TablaHija == jt)
                        .SelectMany(f => f.ColumnasHijaCsv.Split(',').Select(x => x.Trim())),
                    StringComparer.OrdinalIgnoreCase);

                var attrsRelacion = s.Columnas.Where(c => c.Tabla == jt && !fkCols.Contains(c.Nombre));
                foreach (var c in attrsRelacion)
                {
                    var aid = $"{San(jt)}__{San(c.Nombre)}";
                    sb.AppendLine($"  {aid}(({Esc(c.Nombre)})):::atributo");
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
