using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using ABD_Project.Models;

public class AnalisisBakService
{
    private readonly ServicioRestauracionSql _restaurador;
    private readonly LectorEsquemaSql _lector;
    private readonly ConstructorDiagramaChen _constructor;

    public AnalisisBakService(
        ServicioRestauracionSql restaurador,
        LectorEsquemaSql lector,
        ConstructorDiagramaChen constructor)
    {
        _restaurador = restaurador;
        _lector = lector;
        _constructor = constructor;
    }

    public async Task<DiagramResultViewModel> AnalizarBakAsync(string rutaBak, IConfiguration cfg)
    {
        string nombreBD = string.Empty;
        try
        {
            nombreBD = await _restaurador.RestaurarAsync(rutaBak, "ER");
            return await GenerarDesdeBdAsync(nombreBD, cfg);
        }
        finally
        {
            try { File.Delete(rutaBak); } catch { }
        }
    }

    public async Task<DiagramResultViewModel> GenerarDesdeBdAsync(string nombreBD, IConfiguration cfg)
    {
        var snap = await _lector.LeerAsync(nombreBD);
        var cnnRestaurada = EERChoicesRestored.BuildCnnToRestoredDb(cfg, nombreBD);
        var mermaid = _constructor.Construir(snap);
        var jerarquias = InferenciaEER.DetectarJerarquias(snap);

        await InferenciaEER_Overrides.AplicarOverridesAsync(
            () => EERChoicesRestored.LoadChoicesAsync(cnnRestaurada),
            jerarquias);

        return new DiagramResultViewModel
        {
            NombreBD = nombreBD,
            MermaidEr = mermaid,
            MermaidEer = InferenciaEER.RenderMermaidEER(jerarquias),
            JerarquiasAmbiguas = jerarquias
                .Where(j => j.Disyuncion == EerDisjointness.Ambiguous || j.Totalidad == EerTotalness.Ambiguous)
                .ToList()
        };
    }
}
