using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
public class ConstructorDiagramaChen
{
    private static readonly Regex _idBad = new(@"[^A-Za-z0-9_]", RegexOptions.Compiled);
    private static string San(string? id)
    {
        var s = id ?? "X";
        s = _idBad.Replace(s, "_");        
        if (string.IsNullOrEmpty(s) || !char.IsLetter(s[0]))
            s = "N_" + s;                    
        if (s.Length > 60) s = s[..60];      
        return s;
    }

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
        sb.AppendLine("flowchart LR");
        sb.AppendLine("classDef entidad fill:#eef,stroke:#334,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef relacion fill:#ffe,stroke:#a66,stroke-width:2px;");
        sb.AppendLine("classDef atributo fill:#eef,stroke:#557;");
        sb.AppendLine("classDef clave font-weight:bold,text-decoration:underline;");
        sb.AppendLine("classDef unico stroke-dasharray:3 2;");

        // Entidades + atributos
        foreach (var t in s.Tablas)
        {
            if (s.TablasUnionMuchosAMuchos.Contains(t.Nombre)) continue;
            sb.AppendLine($"  {San(t.Nombre)}[\"{Esc(t.Nombre)}\"]:::entidad");

            foreach (var c in s.Columnas.Where(x => x.Tabla == t.Nombre))
            {
                var attrId = $"{San(t.Nombre)}__{San(c.Nombre)}";
                sb.AppendLine($"  {attrId}((\"{Esc(c.Nombre)}\")):::atributo");
                if (c.EsPk) sb.AppendLine($"  class {attrId} clave;");
                else if (c.EsUnicoCandidato) sb.AppendLine($"  class {attrId} unico;");
                sb.AppendLine($"  {attrId} --- {San(t.Nombre)}");
            }
        }

        // Relaciones (rombo) a partir de FKs
        int r = 0;
        foreach (var fk in s.LlavesForaneas)
        {
            if (s.TablasUnionMuchosAMuchos.Contains(fk.TablaHija)) continue;
            var relId = $"REL_{r++}_{San(fk.Nombre)}";
            sb.AppendLine($"  {relId}{{\"{Esc(fk.Nombre)}\"}}:::relacion");
            sb.AppendLine($"  {San(fk.TablaPadre)} -- \"1\" --> {relId}");

            string mult = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "1" : "0..1")
                : (fk.HijaTodasNoNulas ? "1..N" : "0..N");
            if (fk.HijaTodasNoNulas)
                sb.AppendLine($"  {relId} -- \"{Esc(mult)}\" --> {San(fk.TablaHija)}");
            else
                sb.AppendLine($"  {relId} -. \"{Esc(mult)}\" .-> {San(fk.TablaHija)}");

        }

        // M:N como relación con atributos (tabla de unión)
        foreach (var jt in s.TablasUnionMuchosAMuchos)
        {
            var padres = s.LlavesForaneas.Where(f => f.TablaHija == jt).Select(f => f.TablaPadre).Distinct().ToList();
            if (padres.Count == 2)
            {
                var relId = $"MN_{San(jt)}";
                sb.AppendLine($"  {relId}{{\"{Esc(jt)}\"}}:::relacion");
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
                    sb.AppendLine($"  {aid}((\"{Esc(c.Nombre)}\")):::atributo");
                    if (c.EsPk) sb.AppendLine($"  class {aid} clave;");
                    sb.AppendLine($"  {aid} --- {relId}");
                }
            }
        }

        foreach (var w in s.EntidadesDebiles)
            sb.AppendLine($"  %% {w} es ENTIDAD DEBIL (PK incluye FK)");

        return sb.ToString();
    }

}
