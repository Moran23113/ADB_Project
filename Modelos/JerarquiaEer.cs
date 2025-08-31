namespace ABD_Project.Modelos;

/// <summary>Estado de disyunción en la jerarquía EER.</summary>
public enum EerDisyuncion { Exclusiva, Solapada, Ambigua }

/// <summary>Estado de totalidad en la jerarquía EER.</summary>
public enum EerTotalidad { Total, Parcial, Ambigua }

/// <summary>
/// Jerarquía de especialización (EER) detectada.
/// </summary>
public class JerarquiaEer
{
    public string Supertipo { get; init; } = string.Empty;
    public List<string> Subtipos { get; } = new();
    public EerDisyuncion Disyuncion { get; set; } = EerDisyuncion.Ambigua;
    public EerTotalidad Totalidad { get; set; } = EerTotalidad.Ambigua;
    public string? Evidencia { get; set; }
}
