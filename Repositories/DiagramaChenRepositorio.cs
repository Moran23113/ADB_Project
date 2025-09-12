using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

public interface IDiagramaChenRepositorio
{
    string Construir(InstantaneaEsquema esquema);
}

public class DiagramaChenRepositorio : IDiagramaChenRepositorio
{
    private static readonly HashSet<string> TablasIgnoradas =
        new(StringComparer.OrdinalIgnoreCase) { "EER_UserChoices" };

    public string Construir(InstantaneaEsquema esquema)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        sb.AppendLine("classDef entidad fill:#eef,stroke:#334,stroke-width:1px,rx:8,ry:8;");
        sb.AppendLine("classDef relacion fill:#ffe,stroke:#a66,stroke-width:2px;");
        sb.AppendLine("classDef atributo fill:#eef,stroke:#557;");
        sb.AppendLine("classDef clave font-weight:bold,text-decoration:underline;");
        sb.AppendLine("classDef unico stroke-dasharray:3 2;");

        RenderizarEntidades(sb, esquema);
        RenderizarRelacionesBinarias(sb, esquema);
        RenderizarRelacionesMuchosAMuchos(sb, esquema);

        return sb.ToString();
    }

    private static void RenderizarEntidades(StringBuilder sb, InstantaneaEsquema esquema)
    {
        foreach (var tabla in esquema.Tablas)
        {
            if (TablasIgnoradas.Contains(tabla.Nombre)) continue;
            if (esquema.TablasUnionMuchosAMuchos.Contains(tabla.Nombre)) continue;

            var idEntidad = MermaidUtils.Sanitizar(tabla.Nombre);
            sb.AppendLine($"  {idEntidad}[{MermaidUtils.Escapar(tabla.Nombre)}]:::entidad");

            foreach (var columna in esquema.Columnas.Where(x => x.Tabla == tabla.Nombre))
            {
                var idAtributo = $"{idEntidad}__{MermaidUtils.Sanitizar(columna.Nombre)}";
                sb.AppendLine($"  {idAtributo}(({MermaidUtils.Escapar(columna.Nombre)})):::atributo");

                if (columna.EsPk) sb.AppendLine($"  class {idAtributo} clave;");
                else if (columna.EsUnicoCandidato) sb.AppendLine($"  class {idAtributo} unico;");

                sb.AppendLine($"  {idAtributo} --- {idEntidad}");
            }
        }
    }

    private static void RenderizarRelacionesBinarias(StringBuilder sb, InstantaneaEsquema esquema)
    {
        int contador = 0;
        foreach (var relacion in esquema.LlavesForaneas)
        {
            if (TablasIgnoradas.Contains(relacion.TablaPadre) || TablasIgnoradas.Contains(relacion.TablaHija)) continue;
            if (esquema.TablasUnionMuchosAMuchos.Contains(relacion.TablaHija)) continue;

            var idRelacion = $"REL_{contador++}_{MermaidUtils.Sanitizar(relacion.Nombre)}";
            sb.AppendLine($"  {idRelacion}{{{{{MermaidUtils.Escapar(relacion.Nombre)}}}}}:::relacion");
            sb.AppendLine($"  {MermaidUtils.Sanitizar(relacion.TablaPadre)} -- \"1\" --> {idRelacion}");

            string multiplicidad = relacion.HijaEsUnica
                ? (relacion.HijaTodasNoNulas ? "1" : "0..1")
                : (relacion.HijaTodasNoNulas ? "1..N" : "0..N");

            if (relacion.HijaTodasNoNulas)
                sb.AppendLine($"  {idRelacion} -- \"{multiplicidad}\" --> {MermaidUtils.Sanitizar(relacion.TablaHija)}");
            else
                sb.AppendLine($"  {idRelacion} -. \"{multiplicidad}\" .-> {MermaidUtils.Sanitizar(relacion.TablaHija)}");
        }
    }

    private static void RenderizarRelacionesMuchosAMuchos(StringBuilder sb, InstantaneaEsquema esquema)
    {
        foreach (var tablaPuente in esquema.TablasUnionMuchosAMuchos)
        {
            if (TablasIgnoradas.Contains(tablaPuente)) continue;

            var padres = esquema.LlavesForaneas
                .Where(f => f.TablaHija == tablaPuente)
                .Select(f => f.TablaPadre)
                .Distinct()
                .ToList();

            if (padres.Count == 2)
            {
                var idRelacion = $"MN_{MermaidUtils.Sanitizar(tablaPuente)}";
                sb.AppendLine($"  {idRelacion}{{{{{MermaidUtils.Escapar(tablaPuente)}}}}}:::relacion");

                sb.AppendLine($"  {MermaidUtils.Sanitizar(padres[0])} -- \"1..N\" --> {idRelacion}");
                sb.AppendLine($"  {idRelacion} -- \"1..N\" --> {MermaidUtils.Sanitizar(padres[1])}");

                var columnasFk = new HashSet<string>(
                    esquema.LlavesForaneas.Where(f => f.TablaHija == tablaPuente)
                        .SelectMany(f => f.ColumnasHijaCsv.Split(',').Select(x => x.Trim())),
                    StringComparer.OrdinalIgnoreCase);

                var atributosRelacion = esquema.Columnas.Where(c => c.Tabla == tablaPuente && !columnasFk.Contains(c.Nombre));
                foreach (var columna in atributosRelacion)
                {
                    var idAtributo = $"{MermaidUtils.Sanitizar(tablaPuente)}__{MermaidUtils.Sanitizar(columna.Nombre)}";
                    sb.AppendLine($"  {idAtributo}(({MermaidUtils.Escapar(columna.Nombre)})):::atributo");
                    if (columna.EsPk) sb.AppendLine($"  class {idAtributo} clave;");
                    sb.AppendLine($"  {idAtributo} --- {idRelacion}");
                }
            }
        }
    }

}
