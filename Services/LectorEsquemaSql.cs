// Models (pueden ir arriba del mismo archivo)
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Información mínima de una tabla para el diagrama.
/// </summary>
/// <param name="Nombre">Nombre lógico de la tabla.</param>
public record InfoTabla(string Nombre);

/// <summary>
/// Columna con metadatos relevantes para ER/EER.
/// </summary>
/// <param name="Tabla">Nombre de la tabla a la que pertenece.</param>
/// <param name="Nombre">Nombre de la columna.</param>
/// <param name="Tipo">Tipo de datos (user type).</param>
/// <param name="EsNulo">Indica si admite NULL.</param>
/// <param name="EsPk">Indica si forma parte de la clave primaria.</param>
/// <param name="EsUnicoCandidato">Indica si participa en alguna restricción/índice único (no PK).</param>
public record InfoColumna(string Tabla, string Nombre, string Tipo, bool EsNulo, bool EsPk, bool EsUnicoCandidato);

/// <summary>
/// Llave foránea agrupada (posiblemente multicolumna).
/// </summary>
/// <param name="Nombre">Nombre de la FK.</param>
/// <param name="TablaPadre">Tabla referenciada (lado 1).</param>
/// <param name="TablaHija">Tabla que contiene la FK (lado N/0..1).</param>
/// <param name="ColumnasPadreCsv">Columnas PK/UK referenciadas (en orden).</param>
/// <param name="ColumnasHijaCsv">Columnas FK en la hija (en orden).</param>
/// <param name="HijaEsUnica">True si el conjunto de columnas hija está cubierto por un índice único.</param>
/// <param name="HijaTodasNoNulas">True si todas las columnas FK en la hija son NOT NULL.</param>
public record InfoLlaveForanea(
    string Nombre, string TablaPadre, string TablaHija,
    string ColumnasPadreCsv, string ColumnasHijaCsv,
    bool HijaEsUnica, bool HijaTodasNoNulas);

/// <summary>
/// Contenedor con toda la instantánea necesaria para construir el diagrama.
/// </summary>
public class InstantaneaEsquema
{
    /// <summary>Tablas del esquema (excluye del sistema).</summary>
    public List<InfoTabla> Tablas { get; } = new();

    /// <summary>Todas las columnas con flags de PK/Único/Nulabilidad.</summary>
    public List<InfoColumna> Columnas { get; } = new();

    /// <summary>Llaves foráneas (agrupadas) con cardinalidad/participación inferida.</summary>
    public List<InfoLlaveForanea> LlavesForaneas { get; } = new();

    /// <summary>Nombres de tablas puente (M:N) identificadas por heurística.</summary>
    public HashSet<string> TablasUnionMuchosAMuchos { get; } = new();

    /// <summary>Entidades débiles (PK incluye alguna FK) detectadas por heurística.</summary>
    public HashSet<string> EntidadesDebiles { get; } = new();
}

/// <summary>
/// Lector de metadatos desde SQL Server, basado en catálogos del sistema.
/// Devuelve una <see cref="InstantaneaEsquema"/> lista para alimentar el constructor del diagrama.
/// </summary>
public class LectorEsquemaSql
{
    private readonly string _cnnMaestra;

    /// <summary>
    /// Crea el lector con una cadena de conexión "maestra" (server/credenciales),
    /// desde la cual se apuntará dinámicamente al catálogo de la BD restaurada.
    /// </summary>
    public LectorEsquemaSql(IConfiguration cfg) => _cnnMaestra = cfg.GetConnectionString("SqlMaestra")!;

    /// <summary>
    /// Construye una cadena de conexión a una BD específica reutilizando servidor/credenciales de la cadena maestra.
    /// </summary>
    private static string CnnDeBD(string cnnMaestra, string nombreBD)
    {
        var csb = new SqlConnectionStringBuilder(cnnMaestra) { InitialCatalog = nombreBD };
        return csb.ToString();
    }

    /// <summary>
    /// Lee el esquema (tablas, columnas, FKs, heurísticas) de la base indicada.
    /// </summary>
    /// <param name="nombreBD">Nombre de la base restaurada.</param>
    /// <returns>Instantánea del esquema para diagrama ER/EER.</returns>
    public async Task<InstantaneaEsquema> LeerAsync(string nombreBD)
    {
        var s = new InstantaneaEsquema();

        using var cn = new SqlConnection(CnnDeBD(_cnnMaestra, nombreBD));
        await cn.OpenAsync();

        // =========================
        // 1) TABLAS (excluye las del sistema)
        // =========================
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sys.tables WHERE is_ms_shipped = 0 ORDER BY name;";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                s.Tablas.Add(new InfoTabla(rd.GetString(0)));
        }

        // =========================
        // 2) COLUMNAS + flags PK/UNIQUE
        //    - pkcols: columnas que conforman la PK.
        //    - uqcols: columnas que participan en índices únicos (no PK).
        // =========================
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
WITH pkcols AS (
  -- Columnas que conforman la clave primaria
  SELECT ic.object_id, ic.column_id
  FROM sys.key_constraints pk
  JOIN sys.index_columns ic
    ON ic.object_id = pk.parent_object_id
   AND ic.index_id  = pk.unique_index_id
  WHERE pk.[type] = 'PK'
),
uqcols AS (
  -- Columnas que participan en algún índice único (no PK)
  SELECT ic.object_id, ic.column_id
  FROM sys.indexes i
  JOIN sys.index_columns ic
    ON ic.object_id = i.object_id
   AND ic.index_id  = i.index_id
  WHERE i.is_unique = 1 AND i.is_primary_key = 0
)
SELECT t.name, c.name, ty.name, c.is_nullable,
       CASE WHEN pk.object_id IS NULL THEN 0 ELSE 1 END AS EsPk,
       CASE WHEN uq.object_id IS NULL THEN 0 ELSE 1 END AS EsUnico
FROM sys.tables t
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types   ty ON ty.user_type_id = c.user_type_id
LEFT JOIN pkcols pk ON pk.object_id = t.object_id AND pk.column_id = c.column_id
LEFT JOIN uqcols uq ON uq.object_id = t.object_id AND uq.column_id = c.column_id
ORDER BY t.name, c.column_id;";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                s.Columnas.Add(new InfoColumna(
                    rd.GetString(0),             // tabla
                    rd.GetString(1),             // columna
                    rd.GetString(2),             // tipo
                    rd.GetBoolean(3),            // es nulo
                    rd.GetInt32(4) == 1,         // es pk
                    rd.GetInt32(5) == 1));       // es único (candidato)
            }
        }

        // =========================
        // 3) FKs agrupadas + cardinalidad y participación
        //    - fkg: agrupa por FK (posibles multicolumna) y mantiene orden.
        //    - uniq_hija: pares (tabla, columnas) que están bajo algún índice único.
        //    - fk_nullable: ***FIX*** calcula si TODAS las columnas FK son NOT NULL.
        // =========================
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
WITH fkcols AS (
  -- Desglosa cada FK en sus columnas padre/hija
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
  -- Agrupa columnas por FK manteniendo el orden original
  SELECT fk_id,
         MAX(FK_Nombre)   AS FK_Nombre,
         MAX(TablaPadre)  AS TablaPadre,
         MAX(TablaHija)   AS TablaHija,
         STRING_AGG(ColPadre, ', ') WITHIN GROUP (ORDER BY column_id) AS ColsPadre,
         STRING_AGG(ColHija , ', ') WITHIN GROUP (ORDER BY column_id) AS ColsHija
  FROM fkcols
  GROUP BY fk_id
),
uniq_hija AS (
  -- Conjuntos de columnas hija cubiertos por algún índice único
  -- Nota: i.is_unique incluye PKs; para excluirlas: 'AND i.is_primary_key = 0'
  SELECT t.name AS Tabla,
         STRING_AGG(c.name, ', ') WITHIN GROUP (ORDER BY ic.key_ordinal) AS Cols
  FROM sys.indexes i
  JOIN sys.tables t        ON t.object_id = i.object_id
  JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
  JOIN sys.columns c        ON c.object_id  = ic.object_id AND c.column_id = ic.column_id
  WHERE i.is_unique = 1
  GROUP BY t.name
),
fk_nullable AS (
  -- 1 solo si todas las columnas FK son NOT NULL (participación total)
  SELECT fkc.constraint_object_id AS fk_id,
         MIN(CASE WHEN c.is_nullable = 0 THEN 1 ELSE 0 END) AS AllNotNull
  FROM sys.foreign_key_columns fkc
  JOIN sys.columns c
    ON c.object_id   = fkc.parent_object_id
   AND c.column_id   = fkc.parent_column_id
  GROUP BY fkc.constraint_object_id
)
SELECT g.FK_Nombre, g.TablaPadre, g.TablaHija, g.ColsPadre, g.ColsHija,
       CASE WHEN EXISTS (
         SELECT 1 FROM uniq_hija uh
         WHERE uh.Tabla = g.TablaHija AND uh.Cols = g.ColsHija
       ) THEN 1 ELSE 0 END AS HijaUnica,
       n.AllNotNull
FROM fkg g
JOIN fk_nullable n ON n.fk_id = g.fk_id
ORDER BY g.TablaPadre, g.TablaHija;";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
            {
                s.LlavesForaneas.Add(new InfoLlaveForanea(
                    rd.GetString(0),             // nombre FK
                    rd.GetString(1),             // tabla padre
                    rd.GetString(2),             // tabla hija
                    rd.GetString(3),             // cols padre csv
                    rd.GetString(4),             // cols hija csv
                    rd.GetInt32(5) == 1,         // hija es única
                    rd.GetInt32(6) == 1));       // ***YA CORRECTO*** todas NOT NULL
            }
        }

        // =========================
        // 4) Tablas de unión M:N (heurística)
        //    - PK de 2 columnas, al menos 2 FKs, y referencia exactamente a 2 tablas distintas.
        // =========================
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
WITH pkcols AS (
  -- Cuenta columnas de la PK por tabla
  SELECT t.object_id, t.name AS Tabla, kc.name AS PKName, COUNT(*) AS PKCols
  FROM sys.key_constraints kc
  JOIN sys.tables t ON t.object_id = kc.parent_object_id
  WHERE kc.[type] = 'PK'
  GROUP BY t.object_id, t.name, kc.name
),
fkcount AS (
  -- Número de FKs y tablas referenciadas por cada tabla
  SELECT t.object_id,
         COUNT(DISTINCT fk.object_id) AS FKs,
         COUNT(DISTINCT rt.name)      AS TablasReferenciadas
  FROM sys.tables t
  JOIN sys.foreign_keys fk   ON fk.parent_object_id = t.object_id
  JOIN sys.tables       rt   ON rt.object_id        = fk.referenced_object_id
  GROUP BY t.object_id
)
SELECT p.Tabla
FROM pkcols p
JOIN fkcount f ON f.object_id = p.object_id
WHERE p.PKCols = 2 AND f.FKs >= 2 AND f.TablasReferenciadas = 2;";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                s.TablasUnionMuchosAMuchos.Add(rd.GetString(0));
        }

        // =========================
        // 5) Entidades débiles (PK incluye FK)
        // =========================
        using (var cmd = cn.CreateCommand())
        {
            cmd.CommandText = @"
SELECT t.name AS TablaDebil
FROM sys.tables t
JOIN sys.key_constraints pk ON pk.parent_object_id = t.object_id AND pk.[type] = 'PK'
JOIN sys.foreign_keys fk    ON fk.parent_object_id = t.object_id
WHERE EXISTS (
  SELECT 1
  FROM sys.foreign_key_columns fkc
  JOIN sys.index_columns ic
    ON ic.object_id = fkc.parent_object_id
   AND ic.index_id  = pk.unique_index_id
  WHERE fkc.parent_object_id = t.object_id
    AND ic.column_id         = fkc.parent_column_id
);";
            using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                s.EntidadesDebiles.Add(rd.GetString(0));
        }

        return s;
    }
}
