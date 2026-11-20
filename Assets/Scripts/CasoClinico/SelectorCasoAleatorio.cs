using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Elige un <see cref="FichaCaso"/> al azar del pool y arranca el caso.
/// Antecede a <see cref="GestorCasoClinico.IniciarCaso"/> — no lo modifica ni
/// toca MotorEvaluacion / InteraccionPaciente.
///
/// La elección es SILENCIOSA: el jugador no debe poder saber qué caso salió
/// (ni el nombre/diagnóstico). Solo se registra en consola para depuración.
/// </summary>
public class SelectorCasoAleatorio : MonoBehaviour
{
    [Tooltip("Por ahora solo CasoEjemploPlaceholder. El pool real (7 casos) se " +
             "agrega aquí cuando estén redactados y validados — sin tocar código.")]
    [SerializeField] private List<FichaCaso> poolDeCasos = new List<FichaCaso>();

    [SerializeField] private GestorCasoClinico gestorCasoClinico;

    [Header("Protocolo de estudio")]
    [Tooltip("Vacío = caso aleatorio del pool (como siempre). Con un idCaso del pool (p. ej. CASO-SCA) se juega siempre ese caso. " +
             "Si el id no está en el pool se avisa en el log y se usa el aleatorio. No cambia el pool.")]
    [SerializeField] private string idCasoFijo = "";

    /// <summary>Caso que se jugará con el id fijo; null si idCasoFijo está vacío o no está en el pool (en ese caso avisa).</summary>
    private FichaCaso BuscarCasoFijo()
    {
        if (string.IsNullOrWhiteSpace(idCasoFijo)) return null;
        string id = idCasoFijo.Trim();
        foreach (var caso in poolDeCasos)
            if (caso != null && string.Equals(caso.idCaso, id, System.StringComparison.OrdinalIgnoreCase)) return caso;
        Debug.LogWarning("[SelectorCasoAleatorio] idCasoFijo '" + id + "' no está en el pool; se usa el caso aleatorio.", this);
        return null;
    }

    public void SeleccionarYComenzar(SubmodoCaso submodo)
    {
        if (gestorCasoClinico == null)
        {
            Debug.LogError("[SelectorCasoAleatorio] Falta referencia a GestorCasoClinico.", this);
            return;
        }
        if (poolDeCasos == null || poolDeCasos.Count == 0)
        {
            Debug.LogError("[SelectorCasoAleatorio] poolDeCasos está vacío.", this);
            return;
        }

        FichaCaso casoElegido = BuscarCasoFijo();
        int indice = casoElegido != null ? poolDeCasos.IndexOf(casoElegido) : Random.Range(0, poolDeCasos.Count);
        if (casoElegido == null) casoElegido = poolDeCasos[indice];

        // Log de depuración — invisible para el jugador (no aparece en ningún panel).
        Debug.Log($"[SelectorCasoAleatorio] Caso {indice + 1}/{poolDeCasos.Count} " +
                  $"('{casoElegido.nombreCaso}') · submodo {submodo}.");

        gestorCasoClinico.IniciarCaso(casoElegido, submodo);
    }
}
