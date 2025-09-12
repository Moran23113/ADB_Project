using System.Text.RegularExpressions;

public interface ITraductorRelacional
{
    string AlgebraRelacionalASql(string entrada);
    string SqlAAlgebraRelacional(string entrada);
}

public class TraductorRelacional : ITraductorRelacional
{
    private static readonly Regex Seleccion = new(@"σ_\{(.+)\}\((.+)\)", RegexOptions.Compiled);
    private static readonly Regex Proyeccion = new(@"π_\{(.+)\}\((.+)\)", RegexOptions.Compiled);
    private static readonly Regex Join = new(@"(.+)\s*⋈_\{(.+)\}\s*(.+)", RegexOptions.Compiled);
    private static readonly Regex Union = new(@"(.+)\s*∪\s*(.+)", RegexOptions.Compiled);
    private static readonly Regex Diferencia = new(@"(.+)\s*−\s*(.+)", RegexOptions.Compiled);
    private static readonly Regex Division = new(@"DIV\((.+); by\[(.+)\]; keep\[(.+)\]\)", RegexOptions.Compiled);

    private static readonly Regex SqlSeleccion = new(@"SELECT (.+) FROM (\w+) WHERE (.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlProyeccion = new(@"SELECT (.+) FROM (\w+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlJoin = new(@"FROM (\w+) JOIN (\w+) ON (.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlUnion = new(@"(.+) UNION (.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlExcept = new(@"(.+) EXCEPT (.+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string AlgebraRelacionalASql(string ar)
    {
        ar = ar.Trim();

        var seleccion = Seleccion.Match(ar);
        if (seleccion.Success)
            return $"SELECT * FROM {seleccion.Groups[2].Value} WHERE {seleccion.Groups[1].Value}";

        var proyeccion = Proyeccion.Match(ar);
        if (proyeccion.Success)
            return $"SELECT {proyeccion.Groups[1].Value} FROM {proyeccion.Groups[2].Value}";

        var join = Join.Match(ar);
        if (join.Success)
            return $"SELECT * FROM {join.Groups[1].Value} JOIN {join.Groups[3].Value} ON {join.Groups[2].Value}";

        var union = Union.Match(ar);
        if (union.Success)
            return $"SELECT * FROM {union.Groups[1].Value} UNION SELECT * FROM {union.Groups[2].Value}";

        var diferencia = Diferencia.Match(ar);
        if (diferencia.Success)
            return $"SELECT * FROM {diferencia.Groups[1].Value} EXCEPT SELECT * FROM {diferencia.Groups[2].Value}";

        var division = Division.Match(ar);
        if (division.Success)
        {
            var A = division.Groups[1].Value;
            var by = division.Groups[2].Value;
            var keep = division.Groups[3].Value;
            return $@"SELECT DISTINCT {keep}
FROM {A} AS A
WHERE NOT EXISTS (
  SELECT 1 FROM BY_{A} AS B
  WHERE NOT EXISTS (
    SELECT 1 FROM {A} AS A2
    WHERE A2.{keep} = A.{keep} AND A2.{by} = B.{by}
  )
)";
        }

        return "-- operador no reconocido";
    }

    public string SqlAAlgebraRelacional(string sql)
    {
        sql = sql.Trim();

        var seleccion = SqlSeleccion.Match(sql);
        if (seleccion.Success)
            return $"σ_{{{seleccion.Groups[3].Value.Trim()}}}(π_{{{seleccion.Groups[1].Value.Trim()}}}({seleccion.Groups[2].Value.Trim()}))";

        var proyeccion = SqlProyeccion.Match(sql);
        if (proyeccion.Success)
            return $"π_{{{proyeccion.Groups[1].Value.Trim()}}}({proyeccion.Groups[2].Value.Trim()})";

        var join = SqlJoin.Match(sql);
        if (join.Success)
            return $"{join.Groups[1].Value.Trim()} ⋈_{{{join.Groups[3].Value.Trim()}}} {join.Groups[2].Value.Trim()}";

        var union = SqlUnion.Match(sql);
        if (union.Success)
            return $"({SqlAAlgebraRelacional(union.Groups[1].Value)}) ∪ ({SqlAAlgebraRelacional(union.Groups[2].Value)})";

        var except = SqlExcept.Match(sql);
        if (except.Success)
            return $"({SqlAAlgebraRelacional(except.Groups[1].Value)}) − ({SqlAAlgebraRelacional(except.Groups[2].Value)})";

        return "-- consulta no reconocida";
    }
}
