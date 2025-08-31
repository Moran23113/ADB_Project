namespace ABD_Project.Servicios.Modelos;

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
