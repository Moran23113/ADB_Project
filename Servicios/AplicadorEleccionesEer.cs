using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ABD_Project.Modelos;

/// <summary>
/// Aplica las elecciones del usuario (persistidas) sobre las jerarquías EER detectadas.
/// </summary>
public static class AplicadorEleccionesEer
{
    /// <summary>
    /// Carga elecciones (sup, subs, dis, tot) y sobreescribe Disyunción/Totalidad
    /// en la lista de jerarquías. Marca la evidencia como “Elección del usuario aplicada.”.
    /// </summary>
    /// <param name="loadChoices">Función que retorna la lista persistida de elecciones.</param>
    /// <param name="jerarquias">Jerarquías detectadas a modificar in place.</param>
    public static async Task AplicarEleccionesAsync(
        Func<Task<List<(string sup, string subs, string dis, string tot)>>> cargarElecciones,
        List<JerarquiaEer> jerarquias)
    {
        var choices = await cargarElecciones();

        foreach (var j in jerarquias)
        {
            var subsCsv = string.Join(",", j.Subtipos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var c = choices.FirstOrDefault(x =>
                x.sup.Equals(j.Supertipo, StringComparison.OrdinalIgnoreCase) &&
                x.subs.Equals(subsCsv, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrEmpty(c.sup))
            {
                j.Disyuncion = c.dis.Equals("Exclusive", StringComparison.OrdinalIgnoreCase)
                    ? EerDisyuncion.Exclusiva : EerDisyuncion.Solapada;
                j.Totalidad = c.tot.Equals("Total", StringComparison.OrdinalIgnoreCase)
                    ? EerTotalidad.Total : EerTotalidad.Parcial;
                j.Evidencia = "Elección del usuario aplicada.";
            }
        }
    }
}
