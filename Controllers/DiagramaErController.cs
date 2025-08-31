using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Linq;

#region Controlador: Diagrama ER / EER
/// <summary>
/// Controlador MVC responsable de:
/// 1) Subir un .bak, restaurarlo temporalmente, leer su esquema,
/// 2) Construir el diagrama ER (Chen) y detectar jerarquías EER,
/// 3) Renderizar Mermaid para ER y EER,
/// 4) Guardar/Aplicar elecciones del usuario para disyunción/totalidad de EER,
/// 5) Eliminar la BD restaurada.
/// </summary>
/// <remarks>
/// Flujo principal:
/// - GET /DiagramaEr/Subir → muestra formulario de carga.
/// - POST /DiagramaEr/Subir(.bak) → restaura, lee, construye diagramas, muestra Resultado.
/// - POST /DiagramaEr/GuardarChoices → guarda elecciones EER en la BD restaurada y re-renderiza.
/// - POST /DiagramaEr/EliminarBase → borra la BD restaurada.
/// 
/// Dependencias inyectadas:
/// - <see cref="ServicioRestauracionSql"/>: Restaura/Elimina bases desde .bak.
/// - <see cref="LectorEsquemaSql"/>: Lee el esquema (tablas, columnas, FKs, pks).
/// - <see cref="ConstructorDiagramaChen"/>: Genera Mermaid para ER (Chen).
/// 
/// Notas:
/// - Las elecciones EER se guardan en la BD restaurada (tabla interna controlada por EERChoicesRestored).
/// - El límite de carga está configurado en 1GB en este controlador (RequestSizeLimit).
/// </remarks>
public class DiagramaErController : Controller
{
    private readonly IWebHostEnvironment _entorno;
    private readonly IConfiguration _cfg;
    private readonly ServicioRestauracionSql _restaurador;
    private readonly LectorEsquemaSql _lector;
    private readonly ConstructorDiagramaChen _constructor;


    /// <summary>
    /// Crea una nueva instancia del controlador.
    /// </summary>
    public DiagramaErController(
        IWebHostEnvironment entorno,
        IConfiguration cfg,
        ServicioRestauracionSql restaurador,
        LectorEsquemaSql lector,
        ConstructorDiagramaChen constructor)
    {
        _entorno = entorno;
        _cfg = cfg;
        _restaurador = restaurador;
        _lector = lector;
        _constructor = constructor;
    }

    /// <summary>
    /// Muestra el formulario para subir un archivo .bak.
    /// </summary>
    [HttpGet]
    public IActionResult Subir() => View();

    /// <summary>
    /// Sube un archivo .bak, lo restaura en una BD temporal, lee su esquema
    /// y construye los diagramas ER (Chen) y EER (Mermaid).
    /// </summary>
    /// <param name="archivoBak">Archivo .bak de la base a analizar.</param>
    /// <returns>Vista Resultado con los diagramas y formulario de ambigüedades EER.</returns>
    [HttpPost]
    [RequestSizeLimit(1_000_000_000)] // 1 GB (ajustar según política)
    public async Task<IActionResult> Subir(IFormFile archivoBak)
    {
        // Validación de entrada.
        if (archivoBak == null || archivoBak.Length == 0)
        {
            ModelState.AddModelError("", "Sube un archivo .bak");
            return View();
        }

        // Carpeta destino para almacenar temporalmente el .bak subido.
        var carpeta = _cfg["Upload:Carpeta"] ?? "App_Data/Uploads";
        var raiz = Path.Combine(_entorno.ContentRootPath, carpeta);
        Directory.CreateDirectory(raiz);

        // Nombre único del .bak en disco.
        var rutaBak = Path.Combine(raiz, $"{Guid.NewGuid():N}.bak");

        // Guardar el archivo a disco.
        using (var fs = System.IO.File.Create(rutaBak))
            await archivoBak.CopyToAsync(fs);

        string nombreBD = "";
        try
        {
            // 1) Restaurar la BD desde el .bak (nombre aleatorio prefijado con "ER").
            nombreBD = await _restaurador.RestaurarAsync(rutaBak, "ER");

            // 2) Leer el esquema de la BD restaurada (tablas, columnas, PKs, FKs...).
            var instantanea = await _lector.LeerAsync(nombreBD);

            // 2.b) Construir cadena de conexión hacia la BD restaurada.
            //     (Se usa para leer/guardar elecciones EER en esa misma BD).
            var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, nombreBD);

            // 3) ER (Chen): generar Mermaid del modelo relacional.
            ViewBag.NombreBD = nombreBD;
            ViewBag.Mermaid = _constructor.Construir(instantanea);

            // 4) EER: detectar jerarquías de subtipos mediante heurística PK=FK (FK UNIQUE).
            var jerarquias = InferenciaEER.DetectarJerarquias(instantanea);

            // 4.b) Aplicar elecciones guardadas previamente (si existen) desde la BD restaurada.
            var elecciones = await EERChoicesRestored.LoadChoicesAsync(cnnRestaurada);
            foreach (var j in jerarquias)
            {
                var subsCsv = string.Join(",", j.Subtipos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                var c = elecciones.FirstOrDefault(x =>
                    x.sup.Equals(j.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                    x.subs.Equals(subsCsv, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(c.sup))
                {
                    j.Disyuncion = c.dis.Equals("Exclusive", StringComparison.OrdinalIgnoreCase)
                        ? EerDisjointness.Exclusive : EerDisjointness.Overlapping;
                    j.Totalidad = c.tot.Equals("Total", StringComparison.OrdinalIgnoreCase)
                        ? EerTotalness.Total : EerTotalness.Partial;
                    j.Evidencia = "Elección del usuario aplicada.";
                }
            }

            // 4.c) Render EER en Mermaid (con etiqueta "especializacion").
            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            // 4.d) Filtrar jerarquías que siguen ambiguas (mostrar formulario para resolver).
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(j => j.Disyuncion == EerDisjointness.Ambiguous || j.Totalidad == EerTotalness.Ambiguous)
                .ToList();

            return View("Resultado");
        }
        catch (Exception ex)
        {
            // Nota: evitar revelar detalles sensibles (rutas, cadenas, etc.) en producción.
            ModelState.AddModelError("", $"Error: {ex.Message}");
            return View();
        }
        finally
        {
            // Limpieza: eliminar el .bak temporal.
            try { System.IO.File.Delete(rutaBak); } catch { /* swallow logging si aplica */ }
        }
    }

    /// <summary>
    /// Elimina la base de datos restaurada temporalmente.
    /// </summary>
    /// <param name="nombreBD">Nombre de la BD a eliminar.</param>
    [HttpPost]
    public async Task<IActionResult> EliminarBase(string nombreBD)
    {
        try
        {
            await _restaurador.EliminarBaseDatosAsync(nombreBD);
            TempData["msg"] = $"BD {nombreBD} eliminada.";
        }
        catch (Exception ex)
        {
            TempData["msg"] = $"No se pudo eliminar: {ex.Message}";
        }
        return RedirectToAction("Subir");
    }

    /// <summary>
    /// Guarda las elecciones del usuario para Disyunción/Totalidad de cada jerarquía EER
    /// directamente en la BD restaurada y re-renderiza los diagramas.
    /// </summary>
    /// <param name="NombreBD">Nombre de la BD restaurada (hidden en la vista).</param>
    /// <param name="Disyuncion">Mapa: clave de jerarquía → "Exclusive" | "Overlapping".</param>
    /// <param name="Totalidad">Mapa: clave de jerarquía → "Total" | "Partial".</param>
    /// <param name="SubtypesCsv">Mapa: clave de jerarquía → subtipos (CSV normalizado y ordenado).</param>
    /// <returns>Vista Resultado actualizada.</returns>
    [HttpPost]
    public async Task<IActionResult> GuardarChoices(
        string NombreBD,
        Dictionary<string, string> Disyuncion,
        Dictionary<string, string> Totalidad,
        Dictionary<string, string> SubtypesCsv)
    {
        try
        {
            // 1) Guardar elecciones en la BD restaurada (tabla interna de elecciones).
            var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(_cfg, NombreBD);
            await EERChoicesRestored.SaveChoicesAsync(cnnRestaurada, Disyuncion, Totalidad, SubtypesCsv);

            // 2) Releer esquema y reconstruir diagramas (ER + EER) tras aplicar elecciones.
            var instantanea = await _lector.LeerAsync(NombreBD);

            ViewBag.NombreBD = NombreBD;
            ViewBag.Mermaid = _constructor.Construir(instantanea);

            var jerarquias = InferenciaEER.DetectarJerarquias(instantanea);

            var elecciones = await EERChoicesRestored.LoadChoicesAsync(cnnRestaurada);
            foreach (var j in jerarquias)
            {
                var subsCsv = string.Join(",", j.Subtipos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
                var c = elecciones.FirstOrDefault(x =>
                    x.sup.Equals(j.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                    x.subs.Equals(subsCsv, StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrEmpty(c.sup))
                {
                    j.Disyuncion = c.dis.Equals("Exclusive", StringComparison.OrdinalIgnoreCase)
                        ? EerDisjointness.Exclusive : EerDisjointness.Overlapping;
                    j.Totalidad = c.tot.Equals("Total", StringComparison.OrdinalIgnoreCase)
                        ? EerTotalness.Total : EerTotalness.Partial;
                    j.Evidencia = "Elección del usuario aplicada.";
                }
            }

            ViewBag.MermaidEER = InferenciaEER.RenderMermaidEER(jerarquias);

            // Idealmente ya no hay ambigüedades; si quedan, se vuelven a mostrar.
            ViewBag.JerarquiasAmbiguas = jerarquias
                .Where(j => j.Disyuncion == EerDisjointness.Ambiguous || j.Totalidad == EerTotalness.Ambiguous)
                .ToList();

            TempData["msg"] = "Elecciones EER guardadas y aplicadas.";
            return View("Resultado");
        }
        catch (Exception ex)
        {
            TempData["msg"] = "Error guardando elecciones: " + ex.Message;
            return RedirectToAction("Subir");
        }
    }
}
#endregion
