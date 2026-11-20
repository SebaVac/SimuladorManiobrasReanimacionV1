# Plataforma de Urgencias en Realidad Virtual

Plataforma de entrenamiento en medicina de urgencias para **Meta Quest 3**, desarrollada en Unity como proyecto de Seminario de Título (Escuela de Ingeniería Informática, PUCV).

> ⚠️ **Proyecto académico, no es un dispositivo médico.** El contenido clínico es un borrador pendiente de validación formal por un profesional de la salud. No debe usarse para formación clínica real ni para tomar decisiones médicas.

## Módulos

| Módulo | Escena | Descripción |
|---|---|---|
| **Menú de inicio** | `SceneInicio` | Pantalla de navegación hacia los dos módulos. |
| **RCP** | `SceneSimulador` | Entrenamiento de reanimación cardiopulmonar con *hand tracking* nativo. Mide profundidad (objetivo 5,0–6,0 cm), frecuencia (objetivo 100–120/min, con metrónomo a 110) y postura de manos, y al terminar una sesión de 2 minutos muestra un panel de resultados. |
| **Razonamiento Clínico Basado en Casos** | `SceneDiagnostico` | El usuario revisa un caso (examen físico, anamnesis, exámenes complementarios), registra diagnóstico y manejo, y recibe un debriefing con puntaje sobre 100. 7 casos: SCA, EPA, Neumonía, Bronquitis aguda, EPOC, TBC y SARS. |

## Requisitos

- **Unity 2022.3.62f3** (2022.3 LTS) con el módulo **Android Build Support** (incluye OpenJDK y Android SDK/NDK).
- **Meta XR All-in-One SDK 81.0.0** y **Oculus XR Plugin 4.5.2** (se instalan solos desde `Packages/manifest.json`).
- Render pipeline: URP 14.0.12. TextMeshPro 3.0.7.
- **git** instalado en el equipo: `manifest.json` incluye una dependencia por git (ver «Dependencias»).
- Dispositivo: **Meta Quest 3** con modo desarrollador activado y seguimiento de manos habilitado.

## Cómo abrir y compilar

1. Clona el repositorio:
   ```bash
   git clone <URL-DEL-REPOSITORIO>
   ```
2. Abre la carpeta con **Unity Hub** usando la versión 2022.3.62f3. La primera vez Unity regenera `Library/` y descarga los paquetes (puede tardar varios minutos).
3. Abre `Assets/Scenes/SceneInicio.unity`.
4. Build Settings: las escenas deben estar en este orden: **0** `SceneInicio`, **1** `SceneSimulador`, **2** `SceneDiagnostico`. Plataforma **Android**, scripting backend **IL2CPP**, arquitectura **ARM64** (así vienen configurados en `ProjectSettings`; mínimo y objetivo: Android API 32).
5. Con el Quest 3 conectado por USB: *File → Build Settings → Build And Run* (o *Build* para generar la APK).

Nota: el *Project Setup Tool* de Meta puede proponer activar «Scene Support» y anclas espaciales. Este proyecto **no** las usa y el `AndroidManifest.xml` versionado no las declara; no apliques esa corrección salvo que quieras añadir esos permisos.

## Cómo instalar una APK en el visor

Las APK no se incluyen en el repositorio. Con una APK generada:

- **Meta Quest Developer Hub:** conecta el visor, acepta «Permitir depuración USB» dentro del visor, arrastra el `.apk` a la ventana y ábrela desde *Biblioteca → Aplicaciones → Orígenes desconocidos*.
- **adb:**
  ```bash
  adb install -r NombreDelArchivo.apk
  ```

## Uso de cada módulo

Descrito a partir del código; lo marcado como **no verificado** no se ha comprobado en el visor para esta documentación.

### RCP (`SceneSimulador`)
- Al cargar la escena aparece un **panel de introducción** que pide el código del participante (ver «Protocolo de estudio»); al pulsar «Comenzar» la escena queda en modo **Calibración** (`BotonMaestro`): se ajusta la posición del maniquí. El cambio entre Calibración y Simulación se hace desde el ítem «Cambiar de modo» del menú de pausa.
- En Simulación, `LogicaRCP` mide la profundidad de cada compresión respecto de la altura inicial de las manos. Una compresión se cuenta como correcta si llega a 5,0–6,0 cm; el HUD muestra la profundidad en color mientras se empuja. Un metrónomo (110 por minuto) marca el ritmo.
- La sesión (`SesionRCP`) empieza con la primera compresión y dura 2 minutos; al terminar, `PanelResultadosRCP` muestra los resultados (compresiones, profundidad, frecuencia, postura, receso). Con el protocolo de estudio activo el botón es «Siguiente sesión» o «Finalizar»; sin él, se puede repetir la sesión o volver a Calibración.
- El menú de pausa permite reiniciar la sesión y volver al menú principal.
- La escena usa solo seguimiento de manos. **No verificado:** el gesto exacto con que se abre el menú de pausa y la sensación de uso con manos reales.

### Razonamiento Clínico (`SceneDiagnostico`)
- Flujo de un caso (`GestorCasoClinico`): Introducción → Examen físico → Anamnesis → Exámenes complementarios → Registro de diagnóstico y manejo → Evaluación → Debriefing. Desde Anamnesis, Exámenes complementarios y Registro hay un botón secundario `« VOLVER` (`BotonVolverFase`) a la fase anterior: no abandona el caso ni borra nada (historial, exámenes pedidos, textos de diagnóstico y manejo, duración) y no escribe filas en el CSV; desde el examen físico no se retrocede. (La hoja de notas se eliminó el 2026-11-19.)
- Hay dos submodos (Tutorial y Evaluación) y el paciente puede estar sentado o acostado (`PosePaciente`).
- **Anamnesis:** el estudiante escribe preguntas con el teclado del visor. El sistema busca primero las preguntas clave del caso; si no coincide ninguna, busca una intención del catálogo (`CatalogoIntenciones`) y el paciente responde con su historia; si nada coincide, responde que no entendió y se anota la pregunta. Solo las preguntas clave suman puntaje. Es determinista, sin IA ni conexión a red.
- **Examen físico, exámenes y registro:** se seleccionan por toque; el diagnóstico y el manejo se escriben como texto libre.
- **Debriefing:** puntaje total sobre 100 con desglose por componente (diagnóstico 30, manejo 25, exámenes 20, anamnesis 15, examen físico 10; configurable en `ConfigPuntaje.asset`).
- **No verificado:** el teclado nativo del visor y el uso con controles Touch o con manos en cada panel.

## Protocolo de estudio

La versión 1.1.0 incorpora un protocolo para pruebas con usuarios (los cuestionarios previos y posteriores —SUS, VRCQ, mareo, HMSAM2— se aplican **fuera** de la aplicación):

- **Panel de introducción** antes de cada módulo (RCP y Razonamiento Clínico), con un **código anónimo del participante** (letras, números y guion; máximo 12; no se guarda el nombre). El código se guarda en el dispositivo y lo comparten los módulos.
- **RCP:** cada participante hace 3 sesiones seguidas de 2 minutos (`sesionesPorParticipante`, de 1 a 5; `modoProtocolo` activa o desactiva todo el protocolo en el objeto `Panel_ResultadosRCP`). Al terminar cada sesión se escribe una fila en `resultados_rcp.csv` dentro de `Application.persistentDataPath` del visor, con las métricas crudas de la sesión; una sesión abandonada a mitad se registra como `Descartada` y no cuenta como sesión realizada.
- Los datos se extraen con Meta Quest Developer Hub o `adb pull`. Diccionario de columnas, extracción, importación a Excel y limitaciones: [`docs/protocolo_estudio.md`](docs/protocolo_estudio.md). La ruta del archivo en el visor es la que corresponde al nombre de paquete y **no está verificada en el dispositivo**.
- **Razonamiento Clínico:** una sola pasada por el módulo. Con `forzarEvaluacion` (objeto `Panel_IntroduccionDiagnostico`, true) «Comenzar» inicia un caso en modo **Evaluación** sin pasar por el panel de selección de submodo; el caso lo sigue eligiendo el azar del pool (`idCasoFijo` en `SelectorCasoAleatorio` lo fija; vacío = aleatorio) y se registra siempre. Con `unaSolaPasada` (objeto `Panel_Debriefing`, true) tras el Debriefing solo queda «Finalizar», que vuelve a `SceneInicio`. Al terminar el caso se escribe una fila en `resultados_diagnostico.csv` (puntaje y desglose, diagnóstico y manejo escritos, preguntas, regiones y exámenes, errores detectados); un caso abandonado queda como `Abandonado` con los datos parciales. El Debriefing muestra el código y «Resultados guardados» o «No se pudieron guardar».

> ⚠️ Desinstalar la aplicación borra los datos guardados: extrae el CSV antes.

## Estructura

```
Assets/
  Scenes/        SceneInicio, SceneSimulador, SceneDiagnostico
  Scripts/       Lógica de RCP (LogicaRCP, SesionRCP, MetronomoRCP...), módulo clínico (CasoClinico/),
                 entorno, entrada (controles Touch) y alineación inicial del jugador
  Editor/        Herramientas de editor: generadores (Construir*), configuradores y validación
  Enviroment/    Ambientación hospitalaria (assets de terceros)
  Materiales/    Materiales propios
  Plugins/       Modelo de terceros (Banana Man) y AndroidManifest.xml
docs/            Documentos de validación clínica; capturas en docs/capturas/
Packages/        Dependencias (manifest.json y packages-lock.json)
ProjectSettings/ Configuración del proyecto Unity
```

Los casos clínicos son `ScriptableObject` en `Assets/Scripts/CasoClinico/FichasEjemplo/`.

> ⚠️ Los menús `Tools/Razonamiento Clínico/Anamnesis/1. Crear catálogo, historias y mapeo` (`ConstruirAnamnesisHistoria`) y los demás generadores de `Assets/Editor` (`Construir*`) **regeneran datos o partes de las escenas**. No los ejecutes sobre el proyecto ya armado salvo que quieras rehacer lo que generan: pueden sobrescribir cambios hechos a mano. Los menús `Tools/Temporal/...` son herramientas de depuración y no hacen falta para usar el proyecto.

## Documentación de validación clínica

En `docs/` hay documentos generados desde los datos de los casos (preguntas, hallazgos, errores frecuentes, matriz de exámenes, conjuntos de palabras clave de manejo, historia del paciente y control anti-fuga), pensados para que un profesional los revise. Empieza por [`docs/README_validacion.md`](docs/README_validacion.md).

## Limitaciones conocidas

Comprobadas en el código actual:

- **Anamnesis sin negaciones:** el matching quita tildes y signos y tolera un error de tipeo en palabras de 5 o más letras, pero no entiende negaciones: «¿no le duele?» coincide con la misma raíz que «¿le duele?» (`MotorEvaluacion.CoincidenciaConjuntos`).
- **BPM en vivo:** el valor que se muestra durante la simulación se calcula solo con las compresiones que llegan a 5,0–6,0 cm (`LogicaRCP.RegistrarCompresion`).
- **Frecuencia de los resultados:** `SesionRCP` promedia los intervalos entre el inicio de todos los ciclos de compresión, sin excluir pausas, por lo que una pausa larga baja la frecuencia media.
- **HUD de profundidad:** solo muestra un valor mientras se empuja (desde 2 cm); fuera de eso muestra «--».
- **Errores frecuentes sin condición:** 5 errores frecuentes de los casos tienen la condición vacía y hoy no se detectan solos (ver `docs/errores_sin_condicion.md`).
- **Umbrales provisionales:** los colores del semáforo del panel de resultados de RCP son provisionales (`PanelResultadosRCP`), y los pesos de puntaje del módulo clínico son configurables y no han sido validados.
- **Contenido clínico sin validación formal:** casos, respuestas y conjuntos de palabras clave son un borrador.

## Dependencias por git

`Packages/manifest.json` incluye `com.coplaydev.unity-mcp` desde `https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main`, fijado por `packages-lock.json` al commit `4ce7dd3cc54e37e2ed6dc59cb5a047f3dccb3f50`. Es una herramienta de editor; ningún script del proyecto la usa. El resto de los paquetes viene del registro de Unity o es parte del editor.

## Créditos y assets de terceros

| Recurso | Ubicación | Origen | Licencia |
|---|---|---|---|
| Hospital Assets | `Assets/Enviroment/HospitalAssets` | [Atomic Realm](https://atomicrealm.itch.io/hospital-assets) | por confirmar |
| Banana Man | `Assets/Plugins/Banana Yellow Games` | Banana Yellow Games | por confirmar |
| TextMesh Pro (recursos y ejemplos) | `Assets/TextMesh Pro` | Unity (paquete TextMesh Pro); los ejemplos incluyen fuentes con su archivo OFL | ver archivos junto a cada fuente; resto por confirmar |
| Meta XR All-in-One SDK 81.0.0 y Oculus XR Plugin | paquetes (`Packages/manifest.json`) y ajustes en `Assets/MetaXR`, `Assets/Oculus`, `Assets/XR`, `Assets/Resources` | Meta | por confirmar |
| Universal Render Pipeline 14 | paquete y `Assets/Settings` | Unity | por confirmar |
| unity-mcp (CoplayDev) | paquete por git | CoplayDev | por confirmar |

> Los assets de terceros conservan su licencia original. Revisa las condiciones de cada uno antes de reutilizarlos o redistribuirlos.

## Autores

_Completar: nombres de los autores y, si corresponde, profesores guía._

## Licencia

_Pendiente de definir._ Mientras no se agregue un archivo `LICENSE`, todos los derechos están reservados por los autores.
