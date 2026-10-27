# Plataforma de Urgencias en Realidad Virtual

Plataforma de entrenamiento en medicina de urgencias para **Meta Quest 3**, desarrollada en Unity como proyecto de Seminario de Título (Escuela de Ingeniería Informática, PUCV).

> ⚠️ **Proyecto académico, no es un dispositivo médico.** El contenido clínico es un borrador pendiente de validación formal por un profesional de la salud. No debe usarse para formación clínica real ni para tomar decisiones médicas.

## Módulos

| Módulo | Escena | Descripción |
|---|---|---|
| **RCP** | `SceneSimulador` | Entrenamiento de reanimación cardiopulmonar con *hand tracking* nativo. Evalúa profundidad (5,0–6,0 cm), frecuencia (100–120/min) y postura de manos según estándares AHA. |
| **Razonamiento Clínico Basado en Casos** | `SceneDiagnostico` | El usuario revisa un caso (anamnesis, examen físico, exámenes complementarios), registra diagnóstico y manejo, y recibe un debriefing con puntaje. 7 casos: SCA, EPA, Neumonía, Bronquitis aguda, EPOC, TBC y SARS. |

La escena de inicio es `SceneInicio` (menú de navegación).

## Requisitos

- **Unity 2022.3.62f3** (2022.3 LTS) con módulo *Android Build Support* (incluye OpenJDK y SDK/NDK)
- **Meta XR All-in-One SDK 81.0.0** (se instala solo desde `Packages/manifest.json`)
- Render pipeline: URP 14
- Dispositivo: **Meta Quest 3** con seguimiento de manos habilitado

## Cómo abrir el proyecto

1. Clona el repositorio:
   ```bash
   git clone <URL-DEL-REPOSITORIO>
   ```
2. Abre la carpeta con **Unity Hub** usando la versión 2022.3.62f3. La primera vez Unity regenera `Library/` y descarga los paquetes (puede tardar varios minutos).
3. Abre `Assets/Scenes/SceneInicio.unity`.
4. Para probar en el visor: *File → Build Settings → Android*, selecciona **Meta Quest** como dispositivo de ejecución y usa *Build And Run* con el Quest 3 conectado por USB (modo desarrollador activado).

## Estructura

```
Assets/
  Scenes/        SceneInicio, SceneSimulador, SceneDiagnostico
  Scripts/       Lógica de RCP y del módulo de razonamiento clínico
  Editor/        Herramientas de editor (generadores y validación)
  Enviroment/    Ambientación hospitalaria (assets de terceros)
  Materiales/    Materiales y texturas propias
docs/            Documentos de validación clínica y capturas
Packages/        Dependencias (manifest.json)
ProjectSettings/ Configuración del proyecto Unity
```

## Documentación de validación clínica

En `docs/` hay documentos generados desde los datos de los casos (preguntas, hallazgos, errores frecuentes, matriz de exámenes, conjuntos de palabras clave de manejo), pensados para que un profesional los revise. Empieza por [`docs/README_validacion.md`](docs/README_validacion.md).

## Créditos y assets de terceros

- **Hospital Assets** por [Atomic Realm](https://atomicrealm.itch.io/hospital-assets) (modelos low-poly de ambientación).
- **Banana Man** por Banana Yellow Games (modelo base del maniquí).
- Meta XR All-in-One SDK, Unity URP y TextMesh Pro, bajo sus respectivas licencias.

> Los assets de terceros conservan su licencia original. Revisa las condiciones de cada uno antes de reutilizarlos o redistribuirlos.

## Autores

_Completar: nombres de los autores y, si corresponde, profesores guía._

## Licencia

_Pendiente de definir._ Mientras no se agregue un archivo `LICENSE`, todos los derechos están reservados por los autores.
