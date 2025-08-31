namespace ABD_Project.Modelos;

/// <summary>
/// Modelo para mostrar información básica de errores.
/// </summary>
public class ModeloVistaError
{
    public string? RequestId { get; set; }

    /// <summary>Indica si se debe mostrar el identificador de la solicitud.</summary>
    public bool MostrarRequestId => !string.IsNullOrEmpty(RequestId);
}
