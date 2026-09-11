/// <summary>
/// Implementado por scripts cuyo estado determina la etiqueta visible de un
/// ítem de <see cref="GestorMenuPausa"/> (p. ej. <see cref="BotonMaestro"/>,
/// cuya etiqueta cambia entre "Cambiar a modo Simulación"/"...Calibración"
/// según el modo actual).
///
/// Se consulta solo al abrir el menú de pausa (y al refrescar tras una
/// acción), nunca por frame.
/// </summary>
public interface IEtiquetaDinamica
{
    string ObtenerEtiqueta();
}
