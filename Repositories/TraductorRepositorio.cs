using System.Text.RegularExpressions;

public interface ITraductorRepositorio
{
    string AlgebraRelacionalASql(string entrada);
    string SqlAAlgebraRelacional(string entrada);
}

public class TraductorRepositorio : ITraductorRepositorio
{
    private static readonly Regex Seleccion = new(@"^σ_\{(.+?)\}\((.+)\)$", RegexOptions.Compiled);
    private static readonly Regex Proyeccion = new(@"^π_\{(.+?)\}\((.+)\)$", RegexOptions.Compiled);
    private static readonly Regex Join = new(@"^(.+)\s*⋈_\{(.+?)\}\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex Union = new(@"^(.+)\s*∪\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex Diferencia = new(@"^(.+)\s*−\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex Division = new(@"DIV\((.+); by\[(.+)\]; keep\[(.+)\]\)", RegexOptions.Compiled);

    private static readonly Regex SqlSeleccion = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))? WHERE (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlProyeccion = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlJoinWhere = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))? JOIN (\w+)(?:\s+(\w+))? ON (.+) WHERE (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlJoin = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))? JOIN (\w+)(?:\s+(\w+))? ON (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlUnion = new(@"^(.+) UNION (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlExcept = new(@"^(.+) EXCEPT (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string AlgebraRelacionalASql(string ar)
    {
        ar = ar.Trim().TrimEnd(';');

        var seleccion = Seleccion.Match(ar);
        if (seleccion.Success)
        {
            var cuerpo = seleccion.Groups[2].Value.Trim();
            var proyeccionInterna = Proyeccion.Match(cuerpo);
            if (proyeccionInterna.Success)
            {
                var cuerpoProy = proyeccionInterna.Groups[2].Value.Trim();
                var joinInterno = Join.Match(cuerpoProy);
                if (joinInterno.Success)
                    return $"SELECT {proyeccionInterna.Groups[1].Value} FROM {joinInterno.Groups[1].Value.Trim()} JOIN {joinInterno.Groups[3].Value.Trim()} ON {joinInterno.Groups[2].Value.Trim()} WHERE {seleccion.Groups[1].Value}";
                return $"SELECT {proyeccionInterna.Groups[1].Value} FROM {cuerpoProy} WHERE {seleccion.Groups[1].Value}";
            }

            var joinInterno2 = Join.Match(cuerpo);
            if (joinInterno2.Success)
                return $"SELECT * FROM {joinInterno2.Groups[1].Value.Trim()} JOIN {joinInterno2.Groups[3].Value.Trim()} ON {joinInterno2.Groups[2].Value.Trim()} WHERE {seleccion.Groups[1].Value}";

            return $"SELECT * FROM {cuerpo} WHERE {seleccion.Groups[1].Value}";
        }

        var proyeccion = Proyeccion.Match(ar);
        if (proyeccion.Success)
        {
            var cuerpo = proyeccion.Groups[2].Value.Trim();
            var seleccionInterna = Seleccion.Match(cuerpo);
            if (seleccionInterna.Success)
                return $"SELECT {proyeccion.Groups[1].Value} FROM {seleccionInterna.Groups[2].Value} WHERE {seleccionInterna.Groups[1].Value}";

            var joinInterno = Join.Match(cuerpo);
            if (joinInterno.Success)
                return $"SELECT {proyeccion.Groups[1].Value} FROM {joinInterno.Groups[1].Value.Trim()} JOIN {joinInterno.Groups[3].Value.Trim()} ON {joinInterno.Groups[2].Value.Trim()}";

            return $"SELECT {proyeccion.Groups[1].Value} FROM {cuerpo}";
        }

        var join = Join.Match(ar);
        if (join.Success)
            return $"SELECT * FROM {join.Groups[1].Value.Trim()} JOIN {join.Groups[3].Value.Trim()} ON {join.Groups[2].Value.Trim()}";

        var union = Union.Match(ar);
        if (union.Success)
            return $"SELECT * FROM {union.Groups[1].Value.Trim()} UNION SELECT * FROM {union.Groups[2].Value.Trim()}";

        var diferencia = Diferencia.Match(ar);
        if (diferencia.Success)
            return $"SELECT * FROM {diferencia.Groups[1].Value.Trim()} EXCEPT SELECT * FROM {diferencia.Groups[2].Value.Trim()}";

        var division = Division.Match(ar);
        if (division.Success)
        {
            var A = division.Groups[1].Value.Trim();
            var by = division.Groups[2].Value.Trim();
            var keep = division.Groups[3].Value.Trim();
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
        sql = sql.Trim().TrimEnd(';');

        var union = SqlUnion.Match(sql);
        if (union.Success)
            return $"({SqlAAlgebraRelacional(union.Groups[1].Value)}) ∪ ({SqlAAlgebraRelacional(union.Groups[2].Value)})";

        var except = SqlExcept.Match(sql);
        if (except.Success)
            return $"({SqlAAlgebraRelacional(except.Groups[1].Value)}) − ({SqlAAlgebraRelacional(except.Groups[2].Value)})";

        var joinWhere = SqlJoinWhere.Match(sql);
        if (joinWhere.Success)
        {
            var t1 = joinWhere.Groups[2].Value.Trim();
            var a1 = joinWhere.Groups[3].Value.Trim();
            var t2 = joinWhere.Groups[4].Value.Trim();
            var a2 = joinWhere.Groups[5].Value.Trim();
            var left = string.IsNullOrEmpty(a1) ? t1 : $"{t1} {a1}";
            var right = string.IsNullOrEmpty(a2) ? t2 : $"{t2} {a2}";
            return $"σ_{{{joinWhere.Groups[7].Value.Trim()}}}(π_{{{joinWhere.Groups[1].Value.Trim()}}}({left} ⋈_{{{joinWhere.Groups[6].Value.Trim()}}} {right}))";
        }

        var seleccion = SqlSeleccion.Match(sql);
        if (seleccion.Success)
        {
            var t = seleccion.Groups[2].Value.Trim();
            var a = seleccion.Groups[3].Value.Trim();
            var table = string.IsNullOrEmpty(a) ? t : $"{t} {a}";
            return $"σ_{{{seleccion.Groups[4].Value.Trim()}}}(π_{{{seleccion.Groups[1].Value.Trim()}}}({table}))";
        }

        var join = SqlJoin.Match(sql);
        if (join.Success)
        {
            var t1 = join.Groups[2].Value.Trim();
            var a1 = join.Groups[3].Value.Trim();
            var t2 = join.Groups[4].Value.Trim();
            var a2 = join.Groups[5].Value.Trim();
            var left = string.IsNullOrEmpty(a1) ? t1 : $"{t1} {a1}";
            var right = string.IsNullOrEmpty(a2) ? t2 : $"{t2} {a2}";
            return $"π_{{{join.Groups[1].Value.Trim()}}}({left} ⋈_{{{join.Groups[6].Value.Trim()}}} {right})";
        }

        var proyeccion = SqlProyeccion.Match(sql);
        if (proyeccion.Success)
        {
            var t = proyeccion.Groups[2].Value.Trim();
            var a = proyeccion.Groups[3].Value.Trim();
            var table = string.IsNullOrEmpty(a) ? t : $"{t} {a}";
            return $"π_{{{proyeccion.Groups[1].Value.Trim()}}}({table})";
        }

        return "-- consulta no reconocida";
    }
}
