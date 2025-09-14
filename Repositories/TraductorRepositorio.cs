using System.Text.RegularExpressions;

public interface ITraductorRepositorio
{
    string AlgebraRelacionalASql(string entrada);
    string SqlAAlgebraRelacional(string entrada);
}

public class TraductorRepositorio : ITraductorRepositorio
{
    private static readonly Regex SeleccionRegex = new(@"^σ_\{(.+?)\}\((.+)\)$", RegexOptions.Compiled);
    private static readonly Regex ProyeccionRegex = new(@"^π_\{(.+?)\}\((.+)\)$", RegexOptions.Compiled);
    private static readonly Regex UnionNaturalRegex = new(@"^(.+)\s*⋈_\{(.+?)\}\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex UnionRegex = new(@"^(.+)\s*∪\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex DiferenciaRegex = new(@"^(.+)\s*−\s*(.+)$", RegexOptions.Compiled);
    private static readonly Regex DivisionRegex = new(@"DIV\((.+); by\[(.+)\]; keep\[(.+)\]\)", RegexOptions.Compiled);

    private static readonly Regex SqlSeleccionRegex = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))? WHERE (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlProyeccionRegex = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlUnionNaturalWhereRegex = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))? JOIN (\w+)(?:\s+(\w+))? ON (.+) WHERE (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlUnionNaturalRegex = new(@"^SELECT (.+) FROM (\w+)(?:\s+(\w+))? JOIN (\w+)(?:\s+(\w+))? ON (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlUnionRegex = new(@"^(.+) UNION (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SqlExceptoRegex = new(@"^(.+) EXCEPT (.+)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string AlgebraRelacionalASql(string algebra)
    {
        algebra = algebra.Trim().TrimEnd(';');

        var seleccion = SeleccionRegex.Match(algebra);
        if (seleccion.Success)
        {
            var cuerpo = seleccion.Groups[2].Value.Trim();
            var proyeccionInterna = ProyeccionRegex.Match(cuerpo);
            if (proyeccionInterna.Success)
            {
                var cuerpoProyeccion = proyeccionInterna.Groups[2].Value.Trim();
                var unionInterna = UnionNaturalRegex.Match(cuerpoProyeccion);
                if (unionInterna.Success)
                    return $"SELECT {proyeccionInterna.Groups[1].Value} FROM {unionInterna.Groups[1].Value.Trim()} JOIN {unionInterna.Groups[3].Value.Trim()} ON {unionInterna.Groups[2].Value.Trim()} WHERE {seleccion.Groups[1].Value}";
                return $"SELECT {proyeccionInterna.Groups[1].Value} FROM {cuerpoProyeccion} WHERE {seleccion.Groups[1].Value}";
            }

            var unionInternaDentro = UnionNaturalRegex.Match(cuerpo);
            if (unionInternaDentro.Success)
                return $"SELECT * FROM {unionInternaDentro.Groups[1].Value.Trim()} JOIN {unionInternaDentro.Groups[3].Value.Trim()} ON {unionInternaDentro.Groups[2].Value.Trim()} WHERE {seleccion.Groups[1].Value}";

            return $"SELECT * FROM {cuerpo} WHERE {seleccion.Groups[1].Value}";
        }

        var proyeccion = ProyeccionRegex.Match(algebra);
        if (proyeccion.Success)
        {
            var cuerpo = proyeccion.Groups[2].Value.Trim();
            var seleccionInterna = SeleccionRegex.Match(cuerpo);
            if (seleccionInterna.Success)
                return $"SELECT {proyeccion.Groups[1].Value} FROM {seleccionInterna.Groups[2].Value} WHERE {seleccionInterna.Groups[1].Value}";

            var unionInterna = UnionNaturalRegex.Match(cuerpo);
            if (unionInterna.Success)
                return $"SELECT {proyeccion.Groups[1].Value} FROM {unionInterna.Groups[1].Value.Trim()} JOIN {unionInterna.Groups[3].Value.Trim()} ON {unionInterna.Groups[2].Value.Trim()}";

            return $"SELECT {proyeccion.Groups[1].Value} FROM {cuerpo}";
        }

        var unionNatural = UnionNaturalRegex.Match(algebra);
        if (unionNatural.Success)
            return $"SELECT * FROM {unionNatural.Groups[1].Value.Trim()} JOIN {unionNatural.Groups[3].Value.Trim()} ON {unionNatural.Groups[2].Value.Trim()}";

        var unionConjuntos = UnionRegex.Match(algebra);
        if (unionConjuntos.Success)
            return $"SELECT * FROM {unionConjuntos.Groups[1].Value.Trim()} UNION SELECT * FROM {unionConjuntos.Groups[2].Value.Trim()}";

        var diferencia = DiferenciaRegex.Match(algebra);
        if (diferencia.Success)
            return $"SELECT * FROM {diferencia.Groups[1].Value.Trim()} EXCEPT SELECT * FROM {diferencia.Groups[2].Value.Trim()}";

        var division = DivisionRegex.Match(algebra);
        if (division.Success)
        {
            var tabla = division.Groups[1].Value.Trim();
            var por = division.Groups[2].Value.Trim();
            var conservar = division.Groups[3].Value.Trim();
            return $@"SELECT DISTINCT {conservar}
FROM {tabla} AS A
WHERE NOT EXISTS (
  SELECT 1 FROM BY_{tabla} AS B
  WHERE NOT EXISTS (
    SELECT 1 FROM {tabla} AS A2
    WHERE A2.{conservar} = A.{conservar} AND A2.{por} = B.{por}
  )
)";
        }

        return "-- operador no reconocido";
    }

    public string SqlAAlgebraRelacional(string sql)
    {
        sql = sql.Trim().TrimEnd(';');

        var unionSql = SqlUnionRegex.Match(sql);
        if (unionSql.Success)
            return $"({SqlAAlgebraRelacional(unionSql.Groups[1].Value)}) ∪ ({SqlAAlgebraRelacional(unionSql.Groups[2].Value)})";

        var exceptoSql = SqlExceptoRegex.Match(sql);
        if (exceptoSql.Success)
            return $"({SqlAAlgebraRelacional(exceptoSql.Groups[1].Value)}) − ({SqlAAlgebraRelacional(exceptoSql.Groups[2].Value)})";

        var unionNaturalWhereSql = SqlUnionNaturalWhereRegex.Match(sql);
        if (unionNaturalWhereSql.Success)
        {
            var tabla1 = unionNaturalWhereSql.Groups[2].Value.Trim();
            var alias1 = unionNaturalWhereSql.Groups[3].Value.Trim();
            var tabla2 = unionNaturalWhereSql.Groups[4].Value.Trim();
            var alias2 = unionNaturalWhereSql.Groups[5].Value.Trim();
            var izquierda = string.IsNullOrEmpty(alias1) ? tabla1 : $"{tabla1} {alias1}";
            var derecha = string.IsNullOrEmpty(alias2) ? tabla2 : $"{tabla2} {alias2}";
            return $"σ_{{{unionNaturalWhereSql.Groups[7].Value.Trim()}}}(π_{{{unionNaturalWhereSql.Groups[1].Value.Trim()}}}({izquierda} ⋈_{{{unionNaturalWhereSql.Groups[6].Value.Trim()}}} {derecha}))";
        }

        var seleccion = SqlSeleccionRegex.Match(sql);
        if (seleccion.Success)
        {
            var tabla = seleccion.Groups[2].Value.Trim();
            var alias = seleccion.Groups[3].Value.Trim();
            var tablaResuelta = string.IsNullOrEmpty(alias) ? tabla : $"{tabla} {alias}";
            return $"σ_{{{seleccion.Groups[4].Value.Trim()}}}(π_{{{seleccion.Groups[1].Value.Trim()}}}({tablaResuelta}))";
        }

        var unionNaturalSql = SqlUnionNaturalRegex.Match(sql);
        if (unionNaturalSql.Success)
        {
            var tabla1 = unionNaturalSql.Groups[2].Value.Trim();
            var alias1 = unionNaturalSql.Groups[3].Value.Trim();
            var tabla2 = unionNaturalSql.Groups[4].Value.Trim();
            var alias2 = unionNaturalSql.Groups[5].Value.Trim();
            var izquierda = string.IsNullOrEmpty(alias1) ? tabla1 : $"{tabla1} {alias1}";
            var derecha = string.IsNullOrEmpty(alias2) ? tabla2 : $"{tabla2} {alias2}";
            return $"π_{{{unionNaturalSql.Groups[1].Value.Trim()}}}({izquierda} ⋈_{{{unionNaturalSql.Groups[6].Value.Trim()}}} {derecha})";
        }

        var proyeccionSql = SqlProyeccionRegex.Match(sql);
        if (proyeccionSql.Success)
        {
            var tabla = proyeccionSql.Groups[2].Value.Trim();
            var alias = proyeccionSql.Groups[3].Value.Trim();
            var tablaResuelta = string.IsNullOrEmpty(alias) ? tabla : $"{tabla} {alias}";
            return $"π_{{{proyeccionSql.Groups[1].Value.Trim()}}}({tablaResuelta})";
        }

        return "-- consulta no reconocida";
    }
}

