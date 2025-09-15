using System.Linq;

/// <summary>
/// Servicio de alto nivel para combinar consultas de especialización y exponer un resultado simplificado.
/// </summary>
public interface IEspecializacionEerService
{
    /// <summary>
    /// Determina si una jerarquía es total o parcial y si es disjunta o solapada.
    /// </summary>
    EspecializacionInfo AnalizarEspecializacion(string conexion, string entidadPadre, params string[] entidadesHija);
}

public class EspecializacionEerService : IEspecializacionEerService
{
    private readonly IEspecializacionEerRepositorio _repositorio;

    public EspecializacionEerService(IEspecializacionEerRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public EspecializacionInfo AnalizarEspecializacion(string conexion, string entidadPadre, params string[] entidadesHija)
    {
        var info = new EspecializacionInfo();

        // Si no existe ningún registro del supertipo sin representación en subtipos, la especialización es total.
        var padresSin = _repositorio.ObtenerPadresSinHijo(conexion, entidadPadre, entidadesHija);
        info.EsTotal = !padresSin.Any();

        // Se cuentan las intersecciones para inferir si los subtipos se solapan.
        var inter = _repositorio.ObtenerIntersecciones(conexion, entidadPadre, entidadesHija);
        info.EsDisjunta = inter == 0;

        return info;
    }
}

