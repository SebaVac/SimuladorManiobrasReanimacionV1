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

        int indice = Random.Range(0, poolDeCasos.Count);
        FichaCaso casoElegido = poolDeCasos[indice];

        // Log de depuración — invisible para el jugador (no aparece en ningún panel).
        Debug.Log($"[SelectorCasoAleatorio] Caso {indice + 1}/{poolDeCasos.Count} " +
                  $"('{casoElegido.nombreCaso}') · submodo {submodo}.");

        gestorCasoClinico.IniciarCaso(casoElegido, submodo);
    }
}
