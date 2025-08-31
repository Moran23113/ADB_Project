namespace ABD_Project.Modelos;

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
