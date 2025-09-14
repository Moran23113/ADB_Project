using System.Linq;

public interface IEspecializacionEerService
{
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

        var padresSin = _repositorio.ObtenerPadresSinHijo(conexion, entidadPadre, entidadesHija);
        info.EsTotal = !padresSin.Any();

        var inter = _repositorio.ObtenerIntersecciones(conexion, entidadPadre, entidadesHija);
        info.EsDisjunta = inter == 0;

        return info;
    }
}

