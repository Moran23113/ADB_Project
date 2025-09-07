using System;
using System.Text.RegularExpressions;

namespace TuProyecto.Services
{
    public static class TraductorSimple
    {
        // AR -> SQL
        public static string ARtoSQL(string ar)
        {
            ar = ar.Trim();

            // Selección: σ_{cond}(R)
            var m = Regex.Match(ar, @"σ_\{(.+)\}\((.+)\)");
            if (m.Success)
                return $"SELECT * FROM {m.Groups[2].Value} WHERE {m.Groups[1].Value}";

            // Proyección: π_{a,b}(R)
            m = Regex.Match(ar, @"π_\{(.+)\}\((.+)\)");
            if (m.Success)
                return $"SELECT {m.Groups[1].Value} FROM {m.Groups[2].Value}";

            // Join: R ⋈_{cond} S
            m = Regex.Match(ar, @"(.+)\s*⋈_\{(.+)\}\s*(.+)");
            if (m.Success)
                return $"SELECT * FROM {m.Groups[1].Value} JOIN {m.Groups[3].Value} ON {m.Groups[2].Value}";

            // Unión: R ∪ S
            m = Regex.Match(ar, @"(.+)\s*∪\s*(.+)");
            if (m.Success)
                return $"SELECT * FROM {m.Groups[1].Value} UNION SELECT * FROM {m.Groups[2].Value}";

            // Diferencia: R − S
            m = Regex.Match(ar, @"(.+)\s*−\s*(.+)");
            if (m.Success)
                return $"SELECT * FROM {m.Groups[1].Value} EXCEPT SELECT * FROM {m.Groups[2].Value}";

            // División: DIV(A; by[b]; keep[k])
            m = Regex.Match(ar, @"DIV\((.+); by\[(.+)\]; keep\[(.+)\]\)");
            if (m.Success)
            {
                var A = m.Groups[1].Value;
                var by = m.Groups[2].Value;
                var keep = m.Groups[3].Value;
                return $@"
SELECT DISTINCT {keep}
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

        // SQL -> AR
        public static string SQLtoAR(string sql)
        {
            sql = sql.Trim();

            // Selección: SELECT cols FROM R WHERE cond
            var m = Regex.Match(sql, @"SELECT (.+) FROM (\w+) WHERE (.+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string cols = m.Groups[1].Value.Trim();
                string rel = m.Groups[2].Value.Trim();
                string cond = m.Groups[3].Value.Trim();
                return $"σ_{{{cond}}}(π_{{{cols}}}({rel}))";
            }

            // Proyección: SELECT cols FROM R
            m = Regex.Match(sql, @"SELECT (.+) FROM (\w+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string cols = m.Groups[1].Value.Trim();
                string rel = m.Groups[2].Value.Trim();
                return $"π_{{{cols}}}({rel})";
            }

            // JOIN: FROM R JOIN S ON cond
            m = Regex.Match(sql, @"FROM (\w+) JOIN (\w+) ON (.+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string left = m.Groups[1].Value.Trim();
                string right = m.Groups[2].Value.Trim();
                string cond = m.Groups[3].Value.Trim();
                return $"{left} ⋈_{{{cond}}} {right}";
            }

            // UNION
            m = Regex.Match(sql, @"(.+) UNION (.+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return $"({SQLtoAR(m.Groups[1].Value)}) ∪ ({SQLtoAR(m.Groups[2].Value)})";
            }

            // EXCEPT
            m = Regex.Match(sql, @"(.+) EXCEPT (.+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return $"({SQLtoAR(m.Groups[1].Value)}) − ({SQLtoAR(m.Groups[2].Value)})";
            }

            return "-- consulta no reconocida";
        }

    }
}
