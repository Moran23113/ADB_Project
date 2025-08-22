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

        // Encabezado y estilos (clases Mermaid)
        sb.AppendLine("flowchart LR"); // Left-to-Right
        sb.AppendLine("classDef entidad fill:#eef,stroke:#334,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef relacion fill:#ffe,stroke:#a66,stroke-width:2px;");
        sb.AppendLine("classDef atributo fill:#eef,stroke:#557;");
        sb.AppendLine("classDef clave font-weight:bold,text-decoration:underline;");
        sb.AppendLine("classDef unico stroke-dasharray:3 2;");

        // =========================
        // Entidades + atributos
        // =========================
        foreach (var t in s.Tablas)
        {
            // No dibujar como entidad si es tabla puente M:N (esa se dibuja como rombo/relación más abajo).
            if (s.TablasUnionMuchosAMuchos.Contains(t.Nombre)) continue;

            // Nodo de entidad (cuadro)
            sb.AppendLine($"  {San(t.Nombre)}[\"{Esc(t.Nombre)}\"]:::entidad");

            // Atributos (óvalos) y enlace con la entidad
            foreach (var c in s.Columnas.Where(x => x.Tabla == t.Nombre))
            {
                var attrId = $"{San(t.Nombre)}__{San(c.Nombre)}";
                sb.AppendLine($"  {attrId}((\"{Esc(c.Nombre)}\")):::atributo");

                // Estilos: clave primaria subrayada, candidato único punteado
                if (c.EsPk) sb.AppendLine($"  class {attrId} clave;");
                else if (c.EsUnicoCandidato) sb.AppendLine($"  class {attrId} unico;");

                // Conexión atributo — entidad
                sb.AppendLine($"  {attrId} --- {San(t.Nombre)}");
            }
        }

        // ====================================
        // Relaciones binarias desde las FKs
        // ====================================
        int r = 0;
        foreach (var fk in s.LlavesForaneas)
        {
            // Si la tabla hija es una tabla puente M:N, se representa como relación más abajo
            if (s.TablasUnionMuchosAMuchos.Contains(fk.TablaHija)) continue;

            // Nodo de relación (rombo)
            var relId = $"REL_{r++}_{San(fk.Nombre)}";
            sb.AppendLine($"  {relId}{{\"{Esc(fk.Nombre)}\"}}:::relacion");

            // Lado padre siempre 1 (PK)
            sb.AppendLine($"  {San(fk.TablaPadre)} -- \"1\" --> {relId}");

            // Cardinalidad del lado hijo:
            // - Si la FK es única en hija => 1 ó 0..1 (según nulabilidad).
            // - Si la FK no es única => 1..N ó 0..N (según nulabilidad).
            string mult = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "1" : "0..1")
                : (fk.HijaTodasNoNulas ? "1..N" : "0..N");

            // Si la FK es NOT NULL, línea sólida; si permite NULL, línea punteada (opcionalidad visual).
            if (fk.HijaTodasNoNulas)
                sb.AppendLine($"  {relId} -- \"{Esc(mult)}\" --> {San(fk.TablaHija)}");
            else
                sb.AppendLine($"  {relId} -. \"{Esc(mult)}\" .-> {San(fk.TablaHija)}");
        }

        // ===================================================
        // Relaciones M:N (tabla de unión como relación Chen)
        // ===================================================
        foreach (var jt in s.TablasUnionMuchosAMuchos)
        {
            // Detecta las dos tablas padres a las que referencia la tabla puente
            var padres = s.LlavesForaneas
                .Where(f => f.TablaHija == jt)
                .Select(f => f.TablaPadre)
                .Distinct()
                .ToList();

            // Solo si hay exactamente 2 lados (M:N clásico)
            if (padres.Count == 2)
            {
                var relId = $"MN_{San(jt)}";

                // El nombre de la relación es el de la tabla puente
                sb.AppendLine($"  {relId}{{\"{Esc(jt)}\"}}:::relacion");

                // Ambos lados 1..N
                sb.AppendLine($"  {San(padres[0])} -- \"1..N\" --> {relId}");
                sb.AppendLine($"  {relId} -- \"1..N\" --> {San(padres[1])}");

                // Atributos propios de la tabla puente (los que no son FK) se dibujan como atributos de la relación
                var fkCols = new HashSet<string>(
                    s.LlavesForaneas.Where(f => f.TablaHija == jt)
                                    .SelectMany(f => f.ColumnasHijaCsv.Split(',').Select(x => x.Trim())),
                    StringComparer.OrdinalIgnoreCase);

                var attrsRelacion = s.Columnas.Where(c => c.Tabla == jt && !fkCols.Contains(c.Nombre));
                foreach (var c in attrsRelacion)
                {
                    var aid = $"{San(jt)}__{San(c.Nombre)}";
                    sb.AppendLine($"  {aid}((\"{Esc(c.Nombre)}\")):::atributo");
                    if (c.EsPk) sb.AppendLine($"  class {aid} clave;");
                    sb.AppendLine($"  {aid} --- {relId}");
                }
            }
        }

        // Marcas informativas para entidades débiles (PK que incluye FK)
        foreach (var w in s.EntidadesDebiles)
            sb.AppendLine($"  %% {w} es ENTIDAD DEBIL (PK incluye FK)");

        return sb.ToString();
    }
}
