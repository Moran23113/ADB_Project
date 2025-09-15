using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

/// <summary>
/// Define cómo transformar el esquema restaurado en un modelo relacional descrito en texto plano o HTML.
/// </summary>
public interface IModeloRelacionalTextoRepositorio
{
    /// <summary>
    /// Construye la representación textual del modelo relacional.
    /// </summary>
    /// <param name="esquema">Instantánea del esquema que contiene tablas, columnas y relaciones.</param>
    /// <param name="comoHtml">Si es verdadero, genera etiquetas HTML para mostrarlo en la vista.</param>
    string Construir(InstantaneaEsquema esquema, bool comoHtml = false);
}

public class ModeloRelacionalTextoRepositorio : IModeloRelacionalTextoRepositorio
{
    public string Construir(InstantaneaEsquema esquema, bool comoHtml = false)
    {
        // Se utiliza StringBuilder para producir un listado detallado de tablas y atributos.
        var sb = new StringBuilder();

        // Se agrupan las columnas que pertenecen a claves foráneas por tabla para etiquetar atributos como FK.
        var fksPorTabla = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in esquema.LlavesForaneas)
        {
            if (!fksPorTabla.TryGetValue(fk.TablaHija, out var set))
                fksPorTabla[fk.TablaHija] = set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var c in fk.ColumnasHijaCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                set.Add(c);
        }

        // Se ordenan las tablas por nombre para presentar un modelo estable y fácil de leer.
        foreach (var tabla in esquema.Tablas.Select(t => t.Nombre).OrderBy(n => n))
        {
            if (tabla.StartsWith("EER_", StringComparison.OrdinalIgnoreCase))
                continue;

            var cols = esquema.Columnas.Where(c => c.Tabla == tabla).ToList();

            // Ordena atributos resaltando primero las claves primarias y únicas para facilitar la lectura.
            var ordenadas = cols
                .OrderByDescending(c => c.EsPk)
                .ThenByDescending(c => c.EsUnicoCandidato && !c.EsPk)
                .ThenBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase)
                .ToList();

            string Formatear(string nombre, bool esPk, bool esFk, bool esUk)
            {
                if (comoHtml)
                {
                    // En modo HTML se agregan etiquetas para subrayar PKs, cursiva para FKs y marcas para únicos.
                    var n = nombre;
                    if (esPk) n = $"<u>{n}</u>";
                    if (esFk) n = $"<i>{n}</i>";
                    if (esUk && !esPk) n += " <small>[UK]</small>";
                    if (esPk && esFk) n += " <small>[FK]</small>";
                    return n;
                }
                // En modo texto plano se agregan sufijos entre corchetes con las etiquetas correspondientes.
                var tags = new List<string>();
                if (esPk) tags.Add("PK");
                if (esFk) tags.Add("FK");
                if (esUk && !esPk) tags.Add("UK");
                return tags.Count == 0 ? nombre : $"{nombre} [{string.Join(", ", tags)}]";
            }

            var fkSet = fksPorTabla.TryGetValue(tabla, out var s) ? s : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Cada atributo se formatea con su etiqueta y se concatena como parte de la lista de columnas.
            var atributos = ordenadas.Select(c =>
                Formatear(c.Nombre, c.EsPk, fkSet.Contains(c.Nombre), c.EsUnicoCandidato)
            );

            if (comoHtml)
                // En HTML se envuelve la salida en un <div> para facilitar el estilo en la vista.
                sb.AppendLine($"<div><b>{tabla}</b>({string.Join(", ", atributos)})</div>");
            else
                sb.AppendLine($"{tabla}({string.Join(", ", atributos)})");
        }

        return sb.ToString();
    }
}

