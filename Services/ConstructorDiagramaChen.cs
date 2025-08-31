using System;
using System.Collections.Generic;

public class ConstructorDiagramaChen
{
    public string Construir(InstantaneaEsquema s)
    {
        var mb = new MermaidBuilder();
        var ocultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EER_UserChoices" };
        AgregarEncabezado(mb);
        AgregarEntidades(mb, s, ocultas);
        AgregarRelacionesBinarias(mb, s, ocultas);
        AgregarRelacionesMN(mb, s, ocultas);
        AgregarEntidadesDebiles(mb, s, ocultas);
        return mb.Build();
    }

    private static void AgregarEncabezado(MermaidBuilder mb)
    {
        mb.AddRaw("flowchart LR");
        mb.AddRaw("classDef entidad fill:#eef,stroke:#334,stroke-width:1px,rx:8,ry:8;");
        mb.AddRaw("classDef relacion fill:#ffe,stroke:#a66,stroke-width:2px;");
        mb.AddRaw("classDef atributo fill:#eef,stroke:#557;");
        mb.AddRaw("classDef clave font-weight:bold,text-decoration:underline;");
        mb.AddRaw("classDef unico stroke-dasharray:3 2;");
    }

    private static void AgregarEntidades(MermaidBuilder mb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var t in s.Tablas)
        {
            if (ocultas.Contains(t.Nombre)) continue;
            if (s.TablasUnionMuchosAMuchos.Contains(t.Nombre)) continue;

            var entId = MermaidUtils.SanitizeId(t.Nombre);
            mb.AddRaw($"  {entId}[{MermaidUtils.EscapeText(t.Nombre)}]:::entidad");

            foreach (var c in s.Columnas)
            {
                if (c.Tabla != t.Nombre) continue;
                var attrId = $"{entId}__{MermaidUtils.SanitizeId(c.Nombre)}";
                mb.AddRaw($"  {attrId}(({MermaidUtils.EscapeText(c.Nombre)})):::atributo");
                if (c.EsPk) mb.AddRaw($"  class {attrId} clave;");
                else if (c.EsUnicoCandidato) mb.AddRaw($"  class {attrId} unico;");
                mb.AddRaw($"  {attrId} --- {entId}");
            }
        }
    }

    private static void AgregarRelacionesBinarias(MermaidBuilder mb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        int r = 0;
        foreach (var fk in s.LlavesForaneas)
        {
            if (ocultas.Contains(fk.TablaPadre) || ocultas.Contains(fk.TablaHija)) continue;
            if (s.TablasUnionMuchosAMuchos.Contains(fk.TablaHija)) continue;

            var relId = $"REL_{r++}_{MermaidUtils.SanitizeId(fk.Nombre)}";
            mb.AddRaw($"  {relId}{{{{{MermaidUtils.EscapeText(fk.Nombre)}}}}}:::relacion");
            mb.AddRaw($"  {MermaidUtils.SanitizeId(fk.TablaPadre)} -- \"1\" --> {relId}");

            var mult = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "1" : "0..1")
                : (fk.HijaTodasNoNulas ? "1..N" : "0..N");

            var flecha = fk.HijaTodasNoNulas ? "--" : "-.";
            var cola = fk.HijaTodasNoNulas ? "-->" : ".->";
            mb.AddRaw($"  {relId} {flecha} \"{mult}\" {cola} {MermaidUtils.SanitizeId(fk.TablaHija)}");
        }
    }

    private static void AgregarRelacionesMN(MermaidBuilder mb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var jt in s.TablasUnionMuchosAMuchos)
        {
            if (ocultas.Contains(jt)) continue;

            var padres = new List<string>();
            foreach (var fk in s.LlavesForaneas)
                if (fk.TablaHija == jt && !padres.Contains(fk.TablaPadre))
                    padres.Add(fk.TablaPadre);

            if (padres.Count != 2) continue;

            var relId = $"MN_{MermaidUtils.SanitizeId(jt)}";
            mb.AddRaw($"  {relId}{{{{{MermaidUtils.EscapeText(jt)}}}}}:::relacion");
            mb.AddRaw($"  {MermaidUtils.SanitizeId(padres[0])} -- \"1..N\" --> {relId}");
            mb.AddRaw($"  {relId} -- \"1..N\" --> {MermaidUtils.SanitizeId(padres[1])}");

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
                var aid = $"{MermaidUtils.SanitizeId(jt)}__{MermaidUtils.SanitizeId(c.Nombre)}";
                mb.AddRaw($"  {aid}(({MermaidUtils.EscapeText(c.Nombre)})):::atributo");
                if (c.EsPk) mb.AddRaw($"  class {aid} clave;");
                mb.AddRaw($"  {aid} --- {relId}");
            }
        }
    }

    private static void AgregarEntidadesDebiles(MermaidBuilder mb, InstantaneaEsquema s, HashSet<string> ocultas)
    {
        foreach (var w in s.EntidadesDebiles)
            if (!ocultas.Contains(w))
                mb.AddRaw($"  %% {w} es ENTIDAD DEBIL (PK incluye FK)");
    }
}
