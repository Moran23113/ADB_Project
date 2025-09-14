using System.Linq;

public interface IGeneralizacionEerService
{
    GeneralizacionInfo AnalizarGeneralizacion(string conexion, string entidadPadre, params string[] entidadesHija);
}

public class GeneralizacionEerService : IGeneralizacionEerService
{
    private readonly IGeneralizacionEerRepositorio _repositorio;

    public GeneralizacionEerService(IGeneralizacionEerRepositorio repositorio)
    {
        _repositorio = repositorio;
    }

    public GeneralizacionInfo AnalizarGeneralizacion(string conexion, string entidadPadre, params string[] entidadesHija)
    {
        var info = new GeneralizacionInfo();

        var padresSin = _repositorio.ObtenerPadresSinHijo(conexion, entidadPadre, entidadesHija);
        info.EsTotal = !padresSin.Any();

        var inter = _repositorio.ObtenerIntersecciones(conexion, entidadPadre, entidadesHija);
        info.EsDisjunta = inter == 0;

        return info;
    }
}
