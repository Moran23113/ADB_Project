// Models (pueden ir arriba del mismo archivo)
using Microsoft.Data.SqlClient;

public record InfoTabla(string Nombre);
public record InfoColumna(string Tabla, string Nombre, string Tipo, bool EsNulo, bool EsPk, bool EsUnicoCandidato);
public record InfoLlaveForanea(string Nombre, string TablaPadre, string TablaHija,
    string ColumnasPadreCsv, string ColumnasHijaCsv, bool HijaEsUnica, bool HijaTodasNoNulas);

public class InstantaneaEsquema
{
    public List<InfoTabla> Tablas { get; } = new();
    public List<InfoColumna> Columnas { get; } = new();
    public List<InfoLlaveForanea> LlavesForaneas { get; } = new();
    public HashSet<string> TablasUnionMuchosAMuchos { get; } = new();
    public HashSet<string> EntidadesDebiles { get; } = new();
}


public class LectorEsquemaSql
{
    private readonly string _cnnMaestra;
    public LectorEsquemaSql(IConfiguration cfg) => _cnnMaestra = cfg.GetConnectionString("SqlMaestra")!;
    private static string CnnDeBD(string cnnMaestra, string nombreBD)
    { var csb = new SqlConnectionStringBuilder(cnnMaestra) { InitialCatalog = nombreBD }; return csb.ToString(); }

    public async Task<InstantaneaEsquema> LeerAsync(string nombreBD)
    {
        var s = new InstantaneaEsquema();
        using var cn = new SqlConnection(CnnDeBD(_cnnMaestra, nombreBD));
        await cn.OpenAsync();

        // Tablas
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sys.tables WHERE is_ms_shipped=0 ORDER BY name;";
            using var rd = await cmd.ExecuteReaderAsync(); while (await rd.ReadAsync()) s.Tablas.Add(new InfoTabla(rd.GetString(0)));
        }

        // Columnas + PK + Únicos
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
WITH pkcols AS (
  SELECT ic.object_id, ic.column_id
  FROM sys.key_constraints pk
  JOIN sys.index_columns ic ON ic.object_id = pk.parent_object_id AND ic.index_id = pk.unique_index_id
  WHERE pk.[type] = 'PK'
),
uqcols AS (
  SELECT ic.object_id, ic.column_id
  FROM sys.indexes i
  JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
  WHERE i.is_unique = 1 AND i.is_primary_key = 0
)
SELECT t.name, c.name, ty.name, c.is_nullable,
       CASE WHEN pk.object_id IS NULL THEN 0 ELSE 1 END,
       CASE WHEN uq.object_id IS NULL THEN 0 ELSE 1 END
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN pkcols pk ON pk.object_id = t.object_id AND pk.column_id = c.column_id
LEFT JOIN uqcols uq ON uq.object_id = t.object_id AND uq.column_id = c.column_id
ORDER BY t.name, c.column_id;";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                s.Columnas.Add(new InfoColumna(rd.GetString(0), rd.GetString(1), rd.GetString(2),
                                               rd.GetBoolean(3), rd.GetInt32(4) == 1, rd.GetInt32(5) == 1));
        }

        // FKs + (1–1 / 1–N) + participación (NOT NULL / NULL)
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
WITH fkcols AS (
  SELECT fk.object_id AS fk_id, fk.name AS FK_Nombre,
         rt.name AS TablaPadre, t.name AS TablaHija,
         pc.name AS ColPadre, cc.name AS ColHija, cc.column_id
  FROM sys.foreign_keys fk
  JOIN sys.tables t  ON t.object_id  = fk.parent_object_id
  JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
  JOIN sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
  JOIN sys.columns pc ON pc.object_id = fkc.referenced_object_id AND pc.column_id = fkc.referenced_column_id
  JOIN sys.columns cc ON cc.object_id = fkc.parent_object_id     AND cc.column_id = fkc.parent_column_id
),
fkg AS (
  SELECT fk_id, MAX(FK_Nombre) AS FK_Nombre, MAX(TablaPadre) AS TablaPadre, MAX(TablaHija) AS TablaHija,
         STRING_AGG(ColPadre, ', ') WITHIN GROUP (ORDER BY column_id) AS ColsPadre,
         STRING_AGG(ColHija , ', ') WITHIN GROUP (ORDER BY column_id) AS ColsHija
  FROM fkcols GROUP BY fk_id
),
uniq_hija AS (
  SELECT t.name AS Tabla, STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Cols
  FROM sys.indexes i
  JOIN sys.tables t ON t.object_id = i.object_id
  JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
  JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
  WHERE i.is_unique = 1
  GROUP BY t.name
),
fk_nullable AS (
  SELECT fkc.constraint_object_id AS fk_id, MIN(CASE WHEN c.is_nullable = 1 THEN 1 ELSE 0 END) AS AllNotNull
  FROM sys.foreign_key_columns fkc
  JOIN sys.columns c ON c.object_id = fkc.parent_object_id AND c.column_id = fkc.parent_column_id
  GROUP BY fkc.constraint_object_id
)
SELECT g.FK_Nombre, g.TablaPadre, g.TablaHija, g.ColsPadre, g.ColsHija,
       CASE WHEN EXISTS (SELECT 1 FROM uniq_hija uh WHERE uh.Tabla = g.TablaHija AND uh.Cols = g.ColsHija) THEN 1 ELSE 0 END AS HijaUnica,
       AllNotNull
FROM fkg g
JOIN fk_nullable n ON n.fk_id = g.fk_id
ORDER BY g.TablaPadre, g.TablaHija;";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                s.LlavesForaneas.Add(new InfoLlaveForanea(
                    rd.GetString(0), rd.GetString(1), rd.GetString(2),
                    rd.GetString(3), rd.GetString(4),
                    rd.GetInt32(5) == 1, rd.GetInt32(6) == 1));
        }

        // Tablas de unión M:N (heurística)
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
WITH pkcols AS (
  SELECT t.object_id, t.name AS Tabla, kc.name AS PKName, COUNT(*) AS PKCols
  FROM sys.key_constraints kc
  JOIN sys.tables t ON t.object_id = kc.parent_object_id
  WHERE kc.[type] = 'PK'
  GROUP BY t.object_id, t.name, kc.name
),
fkcount AS (
  SELECT t.object_id, COUNT(DISTINCT fk.object_id) AS FKs, COUNT(DISTINCT rt.name) AS TablasReferenciadas
  FROM sys.tables t
  JOIN sys.foreign_keys fk ON fk.parent_object_id = t.object_id
  JOIN sys.tables rt ON rt.object_id = fk.referenced_object_id
  GROUP BY t.object_id
)
SELECT p.Tabla
FROM pkcols p
JOIN fkcount f ON f.object_id = p.object_id
WHERE p.PKCols = 2 AND f.FKs >= 2 AND f.TablasReferenciadas = 2;";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) s.TablasUnionMuchosAMuchos.Add(rd.GetString(0));
        }

        // Entidades débiles (PK incluye FK)
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT t.name AS TablaDebil
FROM sys.tables t
JOIN sys.key_constraints pk ON pk.parent_object_id = t.object_id AND pk.[type] = 'PK'
JOIN sys.foreign_keys fk ON fk.parent_object_id = t.object_id
WHERE EXISTS (
  SELECT 1
  FROM sys.foreign_key_columns fkc
  JOIN sys.index_columns ic ON ic.object_id = fkc.parent_object_id AND ic.index_id = pk.unique_index_id
  WHERE fkc.parent_object_id = t.object_id AND ic.column_id = fkc.parent_column_id
);";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync()) s.EntidadesDebiles.Add(rd.GetString(0));
        }

        return s;
    }
}
