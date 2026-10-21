using System.Collections.Generic;
using UnityEngine;

/// <summary>Un examen del catálogo global: nombre (id), categoría de presentación y el
/// resultado que devuelve cuando el caso NO lo trae en su propia lista.</summary>
[System.Serializable]
public class EntradaCatalogoExamen
{
    public string nombreExamen;
    public string categoria;
    [TextArea] public string resultadoNormal;
}

/// <summary>
/// Catálogo ampliado de exámenes complementarios. En modo Evaluación el Panel 6 muestra
/// TODO el catálogo (en este orden fijo, igual para todos los casos) para que el jugador
/// pueda equivocarse; en Tutorial se mantiene la lista corta de cada caso.
///
/// Resolución del resultado y de la clasificación de un examen X en un caso C:
///   - Si <c>C.examenesDisponibles</c> contiene X: se usan SU resultado y SU clasificación
///     (los de la ficha: pertinentes, e "indiferentes" con resultado anormal coherente).
///   - Si no: <see cref="EntradaCatalogoExamen.resultadoNormal"/> y NoPertinente (resta).
/// El orden de las entradas define el orden de categorías (por primera aparición) y el
/// orden dentro de cada categoría. No depende del caso: nada delata cuáles son los suyos.
/// </summary>
[CreateAssetMenu(fileName = "CatalogoExamenes", menuName = "RazonamientoClinico/Catálogo de Exámenes")]
public class CatalogoExamenes : ScriptableObject
{
    public List<EntradaCatalogoExamen> entradas = new List<EntradaCatalogoExamen>();

    /// <summary>Categorías en orden de primera aparición (se llena <paramref name="destino"/>).</summary>
    public void ObtenerCategorias(List<string> destino)
    {
        destino.Clear();
        for (int i = 0; i < entradas.Count; i++)
        {
            var e = entradas[i];
            if (e == null || string.IsNullOrEmpty(e.categoria)) continue;
            if (!destino.Contains(e.categoria)) destino.Add(e.categoria);
        }
    }

    public EntradaCatalogoExamen Buscar(string nombreExamen)
    {
        for (int i = 0; i < entradas.Count; i++)
            if (entradas[i] != null && entradas[i].nombreExamen == nombreExamen) return entradas[i];
        return null;
    }

    /// <summary>Resultado de <paramref name="nombreExamen"/> en <paramref name="caso"/>
    /// (ficha primero, catálogo después). Devuelve cadena vacía si no existe en ninguno.</summary>
    public string ResolverResultado(FichaCaso caso, string nombreExamen)
    {
        var enCaso = BuscarEnCaso(caso, nombreExamen);
        if (enCaso != null) return enCaso.resultado;
        var enCatalogo = Buscar(nombreExamen);
        return enCatalogo != null ? enCatalogo.resultadoNormal : string.Empty;
    }

    /// <summary>Clasificación de <paramref name="nombreExamen"/> en <paramref name="caso"/>:
    /// la de la ficha si lo trae; NoPertinente en cualquier otro caso.</summary>
    public ClasificacionExamen Clasificar(FichaCaso caso, string nombreExamen)
    {
        var enCaso = BuscarEnCaso(caso, nombreExamen);
        return enCaso != null ? enCaso.clasificacion : ClasificacionExamen.NoPertinente;
    }

    private static ExamenComplementario BuscarEnCaso(FichaCaso caso, string nombreExamen)
    {
        if (caso == null || caso.examenesDisponibles == null) return null;
        for (int i = 0; i < caso.examenesDisponibles.Count; i++)
        {
            var e = caso.examenesDisponibles[i];
            if (e != null && e.nombreExamen == nombreExamen) return e;
        }
        return null;
    }
}
