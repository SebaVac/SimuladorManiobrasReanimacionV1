/// <summary>
/// Implementado por paneles diegéticos de escena que deben poder ocultarse y
/// restaurarse desde un punto central sin conocerlo (hoy: <see cref="PuenteVisibilidadPausa"/>,
/// al abrir/cerrar el menú de pausa — <see cref="GestorMenuPausa"/> tampoco conoce a
/// estos paneles, solo dispara sus eventos <c>AlAbrirMenu</c>/<c>AlCerrarMenu</c>).
///
/// El panel es responsable de recordar su propio estado real (según su propia
/// máquina de estados / lógica de visibilidad) para restaurarlo correctamente:
/// <see cref="OcultarTemporalmente"/> solo debe forzar la visibilidad a "oculto"
/// sin perder ese estado recordado, y <see cref="RestaurarVisibilidad"/> debe
/// volver exactamente a lo que el panel tenía calculado (visible u oculto) en el
/// momento de ocultarse — nunca asumir "siempre mostrar de nuevo".
/// </summary>
public interface IPanelOcultable
{
    void OcultarTemporalmente();
    void RestaurarVisibilidad();
}
