# Protocolo de estudio (versión 1.1.0) — datos de RCP y de Razonamiento Clínico

Este documento describe qué guarda la aplicación durante las pruebas con usuarios, cómo extraerlo y cómo interpretarlo. Las secciones 1 a 6 cubren la infraestructura común (código del participante y escritura de resultados) y el módulo de **RCP** (`resultados_rcp.csv`); la sección 7 cubre el módulo de **Razonamiento Clínico** (`resultados_diagnostico.csv`). Ambos archivos comparten las seis primeras columnas y el formato.

> ⚠️ Prototipo académico. Los resultados sirven para comparar sesiones de un mismo participante dentro del estudio; los umbrales de color del panel de resultados son provisionales y no equivalen a una certificación en RCP.

## 1. Qué pasa en una prueba de RCP

1. Al cargar `SceneSimulador` aparece el **panel de introducción**: explica qué se hará y qué se guarda, y pide el **código del participante** (campo que abre el teclado nativo del visor; muestra el código usado antes). «Comenzar» queda gris y deshabilitado mientras el código esté vacío. Mientras el panel esté visible la lógica de RCP está en pausa: **no se cuentan compresiones**.
2. Al pulsar «Comenzar» el panel se oculta y la escena queda en **Calibración** (como antes de esta versión). El participante calibra al paciente y pasa a Simulación con el ítem de pausa «Cambiar de modo».
3. Cada **sesión dura 2 minutos** y empieza con la primera compresión. Al terminar aparece el panel de resultados con «Sesión k de N» y el código.
4. El participante hace **N sesiones** seguidas (`sesionesPorParticipante`, 3 por defecto, de 1 a 5). El botón del panel es «Siguiente sesión» (equivale a Repetir sesión, sin recalibrar) y en la última sesión «Finalizar», que muestra el cierre («Terminaste las N sesiones. Gracias.») y un botón «Volver al menú principal».
5. En modo protocolo no aparece «Volver a calibración»; queda el ítem de pausa «Cambiar de modo».

Parámetros en el Inspector (objeto `Panel_ResultadosRCP`, componente `PanelResultadosRCP`): `modoProtocolo` (true) y `sesionesPorParticipante` (3). En el objeto `Panel_IntroduccionRCP`: `mostrarIntroduccion` (true). Con `modoProtocolo` en false el comportamiento es el anterior (Repetir sesión / Volver a calibración) **y no se guarda nada**.

### Qué cuenta como cada fila
- **Completa**: una fila por sesión terminada (nunca duplicada, aunque el panel se oculte y vuelva a aparecer por la pausa).
- **Descartada**: la sesión estaba en curso y se abandonó (ítem «Reiniciar sesión», «Cambiar de modo» o «Salir al menú principal»). La fila trae lo acumulado hasta ese momento; `duracionSeg` es el **tiempo transcurrido**, no 120. **No avanza k**: la siguiente sesión repite el mismo número y parte de cero.
- Si la sesión ya había terminado y el participante usa «Reiniciar sesión» o «Cambiar de modo» desde el menú de pausa, no se escribe fila Descartada: se pasa a la sesión siguiente (tras la última empieza una ronda nueva en k = 1).
- k se reinicia al **cambiar de código** y al **volver al menú principal** (al reentrar a la escena empieza en 1; el código se conserva).
- Si la aplicación se cierra o el visor se apaga a mitad de una sesión **no** se escribe fila Descartada (no hay evento que lo avise).

## 2. Código del participante

Se guarda en `PlayerPrefs` (clave `codigoParticipante`), así que persiste entre ejecuciones y es el mismo para todos los módulos. Normalización: mayúsculas, sin tildes (Á→A, Ñ→N), solo letras A–Z, números 0–9 y guion «-», máximo 12 caracteres; si queda vacío, no es válido y se conserva el código anterior. Es anónimo: **no se pide ni se guarda el nombre**. Si por alguna razón no hay código, las filas llevan `SIN-CODIGO`.

## 3. Archivo `resultados_rcp.csv`

Formato: UTF-8 sin BOM, separador coma, decimales con **punto**, fin de línea CRLF, una fila por sesión, siempre con *append* (la cabecera se escribe solo si el archivo no existe o está vacío). Los campos de texto con comas, comillas o saltos de línea van entre comillas dobles (comillas duplicadas). Los números decimales llevan 2 decimales.

| # | Columna | Tipo | Significado |
|---|---|---|---|
| 1 | `codigoParticipante` | texto | Código normalizado del participante (`SIN-CODIGO` si no había). |
| 2 | `fechaHoraIso` | texto | Fecha y hora local con desfase, ISO 8601 (`2026-11-17T13:37:29-03:00`). Es el momento en que se escribió la fila. |
| 3 | `versionApp` | texto | `Application.version` (1.1.0). |
| 4 | `modulo` | texto | `RCP`. |
| 5 | `dispositivoEntrada` | texto | `Manos` (el módulo RCP solo usa seguimiento de manos). |
| 6 | `estado` | texto | `Completa` o `Descartada` (ver arriba). |
| 7 | `sesionK` | entero | Número de la sesión dentro de la ronda (1…N). |
| 8 | `sesionesTotales` | entero | N configurado (`sesionesPorParticipante`). |
| 9 | `duracionSeg` | decimal | Duración de la sesión (120,00 en las Completas; tiempo transcurrido en las Descartadas). |
| 10 | `intentos` | entero | Ciclos de compresión de cualquier profundidad (>2 cm). |
| 11 | `validas` | entero | Ciclos cuya profundidad **máxima** quedó entre 5,0 y 6,0 cm (igual a `correctas`). |
| 12 | `insuficientes` | entero | Ciclos con profundidad máxima < 5,0 cm. |
| 13 | `correctas` | entero | Ciclos con profundidad máxima de 5,0 a 6,0 cm. |
| 14 | `excesivas` | entero | Ciclos con profundidad máxima > 6,0 cm. |
| 15 | `profundidadMediaCm` | decimal | Media de la profundidad máxima de todos los ciclos, en cm. |
| 16 | `frecuenciaMediaCpm` | decimal | 60 / intervalo medio entre inicios de ciclos consecutivos, **incluye las pausas** (0 si hubo menos de 2 compresiones). |
| 17 | `pctIntervalosEnRango` | decimal | % de intervalos entre ciclos que equivalen a 100–120 por minuto. |
| 18 | `segPosturaIncorrecta` | decimal | Segundos de la sesión con las manos fuera de la posición correcta. |
| 19 | `salidasDeZona` | entero | Veces que las manos salieron de la zona de compresión. |
| 20 | `recoilIncompleto` | entero | Compresiones válidas cuyo retorno (recoil) no llegó a quedar por debajo de 1,5 cm de profundidad antes de la compresión siguiente. |
| 21 | `recoilEvaluado` | entero | Compresiones válidas cuyo recoil llegó a evaluarse (denominador de la columna anterior). |
| 22 | `validasConteoHud` | entero | Conteo «Total» del HUD durante la sesión. Puede superar a `validas`: un ciclo excesivo que cruza 5–6 cm camino a su máximo se cuenta ahí como válido. En una fila Descartada se lee hasta el último fotograma de la sesión y puede omitir una compresión del fotograma final. |

### Comparar la sesión 1 con la sesión 3
Para cada participante: filtra `estado = Completa` y `codigoParticipante`, ordena por `fechaHoraIso` y compara las columnas de las sesiones 1 y 3 (`validas/intentos`, `profundidadMediaCm`, `frecuenciaMediaCpm`, `pctIntervalosEnRango`, `recoilIncompleto/recoilEvaluado`). Si una sesión se repitió (hay filas `Descartada` del mismo `sesionK`), usa la `Completa` posterior. Si un participante hizo más de una ronda (k = 1 de nuevo), distíngalas por fecha.

## 4. Extraer los datos del visor

El archivo queda en `Application.persistentDataPath`, que en un Quest corresponde, según el nombre de paquete de la APK, a:

```
/sdcard/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/resultados_rcp.csv
```

**Ruta no verificada en el dispositivo** (sale del nombre de paquete y de la convención de Unity; confírmala con el visor).

- **Meta Quest Developer Hub:** conecta el visor y usa el explorador de archivos de la pestaña del dispositivo para navegar a esa carpeta y descargar el archivo.
- **adb:**
  ```bash
  adb pull /sdcard/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/resultados_rcp.csv .
  ```

> ⚠️ **Desinstalar la aplicación borra estos datos.** Extrae el archivo antes de desinstalar o de reinstalar. Instalar una versión nueva con el mismo paquete (`adb install -r`) conserva los datos, pero si la instalación falla por firma y hay que desinstalar primero, se pierden.

## 5. Importar a Excel (u otra herramienta)

- **Excel:** *Datos → Obtener datos → Desde texto/CSV*, origen de archivo **UTF-8**, delimitador **coma**, y configura la configuración regional de la columna como inglés (punto decimal) para las columnas decimales. Abrir el CSV con doble clic en un Excel configurado en español puede interpretar mal el punto decimal.
- **Python/pandas:** `pd.read_csv("resultados_rcp.csv")` (UTF-8 y coma son los valores por defecto).
- La cabecera aparece una sola vez al inicio del archivo.

## 6. Limitaciones de la RCP (versión del 21-oct)

- **Profundidad relativa a la altura de entrada:** la profundidad de cada compresión se mide contra la altura de las manos en el momento de entrar a la zona (`alturaInicialManos`), no contra una referencia fija del paciente. Dos participantes que entran a distinta altura no son comparables en milímetros absolutos; sí lo son sus cambios entre sesiones del mismo participante.
- **BPM en vivo:** el valor que se muestra durante la simulación se calcula solo con las compresiones que llegan a 5,0–6,0 cm (no se guarda en el CSV).
- **Frecuencia de los resultados:** `frecuenciaMediaCpm` promedia los intervalos entre **todos** los ciclos, incluidas las pausas, por lo que una pausa larga la reduce.
- **Contenido no verificado con manos reales** para este documento: el comportamiento exacto de la calibración y del teclado nativo en el visor.

## 7. Razonamiento Clínico (`SceneDiagnostico`) — `resultados_diagnostico.csv`

### 7.1 Qué pasa en una prueba
1. Al cargar `SceneDiagnostico` aparece el **panel de introducción** (en el slot principal, como los demás paneles): explica que el participante atenderá **un caso** (examinar, preguntar, pedir exámenes y registrar diagnóstico e indicaciones), qué se guarda, pide el **código del participante** (el mismo de RCP: campo que abre el teclado nativo y muestra el código previo) y tiene «Comenzar», gris y con ayuda mientras el código esté vacío. Mientras esté pendiente, el panel de selección de submodo (Tutorial / Evaluación) permanece oculto.
2. Con `forzarEvaluacion` en true (por defecto), «Comenzar» **inicia un caso en modo Evaluación** sin pasar por el panel de selección de submodo (equivale a tocar la tarjeta «Evaluación»). Con `forzarEvaluacion` en false, «Comenzar» solo muestra el panel de selección de siempre.
3. El **caso** lo elige `SelectorCasoAleatorio` entre los 7 del pool, al azar; el caso jugado **siempre se registra** (`idCaso`). Para fijar el caso, escribe su id (p. ej. `CASO-SCA`) en el campo `idCasoFijo` de ese componente; un id que no esté en el pool avisa en el log (`[SelectorCasoAleatorio] idCasoFijo '…' no está en el pool`) y usa el aleatorio. No se cambió el pool.
4. Tras el Debriefing, con `unaSolaPasada` en true (por defecto) solo queda el botón **«Finalizar»**, que vuelve a `SceneInicio`; no aparecen «Reintentar este caso» ni «Volver a selección de caso». El Debriefing muestra el código del participante y una línea discreta «Resultados guardados» / «No se pudieron guardar».
5. Si en modo forzado el caso se abandona («Salir del caso», «Atrás» desde la introducción del caso) y la escena vuelve a su estado inicial, reaparece la introducción del estudio (con el código ya cargado) en lugar del panel de selección de submodo, para que no se pueda elegir Tutorial.
6. En Anamnesis, Exámenes complementarios y Registro hay un botón secundario **`« VOLVER`** que lleva a la fase anterior (Registro → Exámenes → Anamnesis → Examen físico); desde el examen físico no se retrocede. Volver **no abandona el caso**: conserva el historial de anamnesis, los exámenes pedidos, los textos de diagnóstico y manejo escritos y la duración, no emite el evento de abandono, no escribe filas y no cambia el puntaje (con las mismas acciones el resultado es el mismo que sin volver). Con el menú de pausa abierto no responde. La hoja de notas ya no existe (eliminada el 2026-11-19).

Parámetros en el Inspector: `mostrarIntroduccion` y `forzarEvaluacion` (objeto `Panel_IntroduccionDiagnostico`, ambos true), `unaSolaPasada` (objeto `Panel_Debriefing`, true), `idCasoFijo` (objeto `SelectorCasoAleatorio`, vacío = aleatorio) y `guardarResultados` (objeto `Registro_CasoClinico`, true). Con `mostrarIntroduccion` en false la escena arranca como antes (`forzarEvaluacion` solo actúa a través de la introducción).

### 7.2 Qué cuenta como cada fila
- **Completo**: una fila por caso que llega al Debriefing, con el `ResultadoCaso` exacto del motor de evaluación. Una sola por caso.
- **Abandonado**: un caso a medias que se deja: «Salir del caso», «Atrás» desde la introducción del caso, «Reiniciar caso actual» o «Abandonar y volver al menú principal» del menú de pausa. El botón `« VOLVER` de fase **no** cuenta como abandono. La fila lleva los datos parciales hasta ese momento; **las columnas de puntaje, `diagnosticoCorrecto` y `erroresDetectados` quedan vacías** (no hubo evaluación). «Reiniciar caso actual» registra el intento que se descarta y el caso nuevo parte de cero.
- Un caso ya terminado (Debriefing) nunca genera fila Abandonado. Si la aplicación se cierra o el visor se apaga a mitad de un caso **no** se escribe nada.

### 7.3 Columnas (35)

| # | Columna | Tipo | Significado |
|---|---|---|---|
| 1–6 | `codigoParticipante`, `fechaHoraIso`, `versionApp`, `modulo`, `dispositivoEntrada`, `estado` | texto | Las mismas que en RCP. `modulo` = `Diagnostico`; `estado` = `Completo` o `Abandonado`; `dispositivoEntrada` = `Manos`, `Controles` o `Mixto` (el usado en el caso). |
| 7 | `idCaso` | texto | Caso jugado (`CASO-SCA`, `CASO-EPA`, `CASO-NAC`, `CASO-BRONQ`, `CASO-EPOC`, `CASO-TBC`, `CASO-SARS`). |
| 8 | `submodo` | texto | `Evaluacion` (en modo forzado siempre) o `Tutorial`. |
| 9 | `pose` | texto | Pose del paciente: `Sentado` o `Acostado`. |
| 10 | `duracionSeg` | decimal | Segundos desde que arrancó el caso hasta que terminó o se abandonó, **sin contar el tiempo con el menú de pausa abierto**. |
| 11–12 | `puntajeTotal`, `puntajeMaximo` | entero | Puntaje redondeado y máximo posible del caso (vacíos si Abandonado). En Tutorial el motor también los calcula, aunque el Debriefing no los muestre. |
| 13–22 | `diagnosticoObtenido/Maximo`, `manejoObtenido/Maximo`, `examenesObtenido/Maximo`, `anamnesisObtenido/Maximo`, `examenFisicoObtenido/Maximo` | decimal | Puntos obtenidos y máximos de cada componente (un componente que no aplica al caso tiene máximo 0). |
| 23 | `penalizacionErrores` | decimal | Puntos que restan los errores frecuentes detectados (ya con tope). |
| 24 | `diagnosticoCorrecto` | true/false | Si el diagnóstico escrito coincide con el esperado. |
| 25 | `diagnosticoEscrito` | texto | Diagnóstico tal como se entregó a la evaluación (en Abandonado, lo último escrito). Una sola línea: saltos de línea y tabulaciones pasan a espacio. |
| 26 | `manejoEscrito` | texto | Indicaciones / manejo (mismo tratamiento). |
| 27 | `preguntasClaveReconocidas` | entero | Preguntas clave del caso reconocidas (las que puntúan), distintas. |
| 28 | `intercambiosReconocidos` | entero | Intercambios de anamnesis distintos reconocidos: preguntas clave más intenciones del catálogo que responden con la historia sin puntuar. |
| 29 | `preguntasSinReconocer` | entero | Veces que el participante escribió una pregunta y el paciente contestó con la respuesta genérica del caso («Disculpe, no le entendí bien…»); cuenta repeticiones. |
| 30 | `regionesExaminadas` | lista | Regiones anatómicas examinadas, distintas, en el orden en que se examinaron, separadas por `|`. Incluye las regiones que el sistema detecta cerca de la mano además de las que se quiso examinar. |
| 31 | `examenesPedidos` | lista | Exámenes complementarios pedidos (nombre), distintos, separados por `|`. |
| 32 | `examenesPertinentes` | entero | De los pedidos, los que la ficha clasifica como Pertinente. |
| 33 | `examenesIndiferentes` | entero | De los pedidos, los clasificados como Indiferente (no suman ni restan). |
| 34 | `examenesNoPertinentes` | entero | De los pedidos, los clasificados como No pertinente más todo examen pedido que la ficha no trae (misma regla que el puntaje). |
| 35 | `erroresDetectados` | lista | Ids de los errores frecuentes detectados (la condición de cada error, p. ej. `omitio:TAC`), separados por `|`. |

Los campos de texto con comas o comillas van entre comillas dobles (comillas duplicadas). La fila nunca contiene saltos de línea.

### 7.4 Extraer e importar
Igual que `resultados_rcp.csv` (secciones 4 y 5), con el nombre `resultados_diagnostico.csv`:

```
/sdcard/Android/data/com.UnityTechnologies.com.unity.template.urpblank/files/resultados_diagnostico.csv
```

Ruta **no verificada en el dispositivo**. Desinstalar la aplicación borra el archivo. Las listas con `|` se separan en Excel con *Texto en columnas* (delimitador `|`) o en pandas con `str.split("|")`.

### 7.5 Limitaciones
- La anamnesis no entiende negaciones («¿no le duele?» coincide como «¿le duele?»).
- El contenido clínico y los pesos del puntaje son un borrador sin validación formal.
- El caso se elige al azar (la decisión de fijarlo está pendiente), por lo que los puntajes entre participantes de casos distintos no son directamente comparables.
- La pose es la de la escena (por defecto `Sentado`); la aplicación no pide elegirla al participante.
