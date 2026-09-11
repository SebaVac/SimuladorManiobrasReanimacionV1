/// <summary>
/// Implementado por scripts cuyo estado determina si un ítem de
/// <see cref="GestorMenuPausa"/> debe mostrarse en el menú de pausa
/// (p. ej. <see cref="CalibradorPosicion"/>, cuyo ítem "ajustar sensor de pecho"
/// solo tiene sentido durante la calibración).
///
/// Se consulta solo al abrir el menú de pausa (y al refrescar tras una acción),
/// nunca por frame. Si el proveedor es null o no implementa esta interfaz, el
/// ítem se muestra siempre.
/// </summary>
public interface IItemMenuCondicional
{
    bool DebeMostrarseEnMenu();
}
