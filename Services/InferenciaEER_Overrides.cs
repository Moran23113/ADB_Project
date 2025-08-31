using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Aplica las elecciones del usuario (persistidas) sobre las jerarquías EER detectadas.
/// </summary>
public static class InferenciaEER_Overrides
{
    /// <summary>
    /// Carga elecciones (sup, subs, dis, tot) y sobreescribe Disyunción/Totalidad
    /// en la lista de jerarquías. Marca la evidencia como “Elección del usuario aplicada.”.
    /// </summary>
    /// <param name="cargarElecciones">Función que retorna la lista persistida de elecciones.</param>
    /// <param name="jerarquias">Jerarquías detectadas a modificar in place.</param>
    public static async Task AplicarOverridesAsync(
        Func<Task<List<(string sup, string subs, string dis, string tot)>>> cargarElecciones,
        List<JerarquiaEer> jerarquias)
    {
        var elecciones = await cargarElecciones();

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
    }
}
