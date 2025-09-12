using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

public interface IDiagramaRelacionalRepositorio
{
    string Construir(InstantaneaEsquema esquema);
}

public class DiagramaRelacionalRepositorio : IDiagramaRelacionalRepositorio
{
    private static readonly Regex IdentificadorInvalido =
        new(@"[^A-Za-z0-9_]", RegexOptions.Compiled);

    private static string MapearTipo(string tipo) => tipo switch
    {
        "int" => "int",
        "bigint" => "bigint",
        "smallint" => "smallint",
        "bit" => "bit",
        "date" => "date",
        "datetime" or "datetime2" => "datetime2",
        "decimal" or "numeric" => "decimal",
        "float" => "float",
        "varchar" or "nvarchar" => "varchar",
        _ => IdentificadorInvalido.Replace(tipo, "_")
    };

    public string Construir(InstantaneaEsquema esquema)
    {
        var tablasOcultas = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EER_UserChoices" };
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        var fkPorTabla = ObtenerColumnasFk(esquema);

        foreach (var tabla in esquema.Tablas.Select(t => t.Nombre))
        {
            if (tablasOcultas.Contains(tabla)) continue;

            fkPorTabla.TryGetValue(tabla, out var columnasFk);
            columnasFk ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            sb.AppendLine($"  {MermaidUtils.Sanitizar(tabla)} {{");
            foreach (var columna in esquema.Columnas.Where(c => c.Tabla == tabla))
            {
                var tipo = MapearTipo(columna.Tipo);
                var flags = new List<string>();
                if (columna.EsPk) flags.Add("PK");
                if (columnasFk.Contains(columna.Nombre)) flags.Add("FK");
                if (columna.EsUnicoCandidato && !columna.EsPk) flags.Add("UK");
                var sufijo = flags.Count > 0 ? " " + string.Join(" ", flags) : string.Empty;
                sb.AppendLine($"    {MermaidUtils.Escapar(tipo)} {MermaidUtils.Escapar(columna.Nombre)}{sufijo}");
            }
            sb.AppendLine("  }");
        }

        foreach (var fk in esquema.LlavesForaneas)
        {
            if (tablasOcultas.Contains(fk.TablaPadre) || tablasOcultas.Contains(fk.TablaHija)) continue;

            var padre = "||";
            var hija = fk.HijaEsUnica
                ? (fk.HijaTodasNoNulas ? "||" : "o|")
                : (fk.HijaTodasNoNulas ? "|{" : "o{");

            sb.AppendLine($"  {MermaidUtils.Sanitizar(fk.TablaPadre)} {padre}--{hija} {MermaidUtils.Sanitizar(fk.TablaHija)} : \"{MermaidUtils.Escapar(fk.Nombre)}\"");
        }

        return sb.ToString();
    }

    private static Dictionary<string, HashSet<string>> ObtenerColumnasFk(InstantaneaEsquema esquema)
    {
        var resultado = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var fk in esquema.LlavesForaneas)
        {
            if (!resultado.TryGetValue(fk.TablaHija, out var columnas))
            {
                columnas = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                resultado[fk.TablaHija] = columnas;
            }

            foreach (var columna in fk.ColumnasHijaCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                columnas.Add(columna);
        }

        return resultado;
    }
}
