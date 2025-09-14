using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;




public static class InferenciaEER_Overrides
{
    
    
    
    
    
    
    public static async Task AplicarOverridesAsync(
        Func<Task<List<(string sup, string subs, string dis, string tot)>>> loadChoices,
        List<JerarquiaEer> jerarquias)
    {
        var choices = await loadChoices();

        foreach (var j in jerarquias)
        {
            var subsCsv = string.Join(",", j.Subtipos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            var c = choices.FirstOrDefault(x =>
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
