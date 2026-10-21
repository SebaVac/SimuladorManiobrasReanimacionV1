# Validación clínica — marcas de los 7 casos

Generado por `Tools/Razonamiento Clínico/Validación clínica/Generar paquete de validación (docs/)` (borrador pendiente de validación clínica). Solo lee los assets; no modifica nada.

## Qué marcas se infirieron SIN validación clínica

Tres marcas de los casos se asignaron por inferencia, sin que un profesional las revisara. Cada una alimenta un componente del puntaje (100 puntos en Evaluación):

| Marca | Dónde está | Componente del puntaje que afecta | Cómo la usa el motor |
|---|---|---|---|
| `esPreguntaClave` | cada pregunta de anamnesis | **Anamnesis (15 puntos)** | puntaje = 15 × (preguntas clave realizadas ÷ preguntas clave totales) |
| `esHallazgoRelevante` | cada región del examen físico | **Examen físico (10 puntos)** | puntaje = 10 × (regiones relevantes examinadas ÷ regiones relevantes totales) |
| `esExamenPertinente` (hoy clasificación Pertinente / Indiferente / No pertinente) | cada examen, por caso | **Exámenes (20 puntos)** | pedir un pertinente suma; pedir uno no pertinente resta; los indiferentes no cuentan. **Se revisa en `docs/matriz_examenes.md`**, no aquí |

Además hay dos elementos con efecto en el puntaje que también están sin validar: los **conjuntos de palabras clave** (preguntas y diagnóstico, secciones 1 y 5 de cada caso; el Manejo está en `docs/conjuntos_manejo.md`) y los **errores frecuentes** (sección 4: cada uno detectado resta 5 puntos, tope 15).

Convención: `{a,b}` es UN conjunto (AND: todas las palabras deben aparecer en el texto del jugador); los conjuntos de una misma pregunta o acción son alternativas (OR). La comparación es por substring, en minúsculas y sin tildes. Las palabras de «legado» (palabras clave y sinónimos sueltos) se muestran con el prefijo `legado:`.

## Resumen de conteos por caso

| Caso | Nombre | Preguntas | Hallazgos | Acciones críticas | Errores frecuentes | Conjuntos de diagnóstico |
|---|---|---|---|---|---|---|
| CASO-SCA | Síndrome Coronario Agudo (STEMI) | 7 (clave: 7) | 7 (relevantes: 5) | 2 (Manejo: 2) | 4 (sin condición: 0) | 4 |
| CASO-TBC | Tuberculosis pulmonar | 7 (clave: 7) | 7 (relevantes: 5) | 2 (Manejo: 2) | 4 (sin condición: 1) | 3 |
| CASO-EPA | Edema Pulmonar Agudo (cardiogénico) | 7 (clave: 7) | 7 (relevantes: 7) | 2 (Manejo: 2) | 5 (sin condición: 1) | 3 |
| CASO-EPOC | Exacerbación de EPOC | 6 (clave: 6) | 7 (relevantes: 5) | 2 (Manejo: 2) | 3 (sin condición: 1) | 3 |
| CASO-NAC | Neumonía adquirida en la comunidad | 6 (clave: 6) | 7 (relevantes: 4) | 2 (Manejo: 2) | 4 (sin condición: 0) | 3 |
| CASO-SARS | Síndrome Respiratorio Agudo Grave (SARS) | 6 (clave: 6) | 7 (relevantes: 6) | 2 (Manejo: 2) | 3 (sin condición: 1) | 3 |
| CASO-BRONQ | Bronquitis aguda | 6 (clave: 6) | 7 (relevantes: 3) | 2 (Manejo: 2) | 2 (sin condición: 1) | 2 |

## CASO-SCA — Síndrome Coronario Agudo (STEMI)

Motivo de consulta: Tengo un dolor fuerte en el pecho que se me corre al brazo izquierdo, hace como media hora.

Conteos: 7 preguntas (7 clave), 7 regiones (5 relevantes), 2 acciones críticas, 4 errores frecuentes (0 sin condición válida), 4 conjuntos de diagnóstico, 4 exámenes propios (ver `docs/matriz_examenes.md`).

### CASO-SCA · 1. Preguntas de anamnesis

| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|---|:-:|---|---|---|
| `sca_inicio_dolor` | ¿Cuándo comenzó el dolor? | Hace unos 30 minutos, de repente. | Sí | {cuando,dol} {empez,dol} |  |  |
| `sca_caracteristica_dolor` | ¿Cómo describe el dolor? | Como un peso fuerte en el pecho, opresivo. | Sí | {como,dol} {opresivo} {peso,pecho} |  |  |
| `sca_irradiacion` | ¿El dolor se corre a algún lado? | Sí, al brazo izquierdo y al cuello. | Sí | {dol,irradia} {dol,corre} {dol,brazo} |  |  |
| `sca_sintomas_asociados` | ¿Ha tenido náuseas o sudoración? | Sí, estoy sudando frío y con ganas de vomitar. | Sí | {nause} {sud} {vomit} |  |  |
| `sca_antecedente_cardiaco` | ¿Antecedentes de enfermedades del corazón? | Tuve un infarto hace dos años. | Sí | {cardi} {corazon} {infarto} |  |  |
| `sca_factores_riesgo` | ¿Es hipertenso, diabético o fuma? | Sí, soy hipertenso y diabético, y fumo. | Sí | {hipertens} {diabet} {fum} |  |  |
| `sca_relacion_reposo` | ¿El dolor cede con el reposo? | No, sigue igual aunque estoy sentado. | Sí | {dol,repos} {cede,repos} |  |  |

### CASO-SCA · 2. Hallazgos por región (examen físico)

| Región | Hallazgo | esHallazgoRelevante (inferida) | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|
| Cabeza y cuello | Sudoración profusa, palidez facial. | Sí |  |  |
| Tórax anterior | Sin hallazgos auscultatorios pulmonares relevantes. | No |  |  |
| Tórax posterior | Murmullo pulmonar conservado. | No |  |  |
| Precordio | Dolor variable a la palpación, cuarto ruido (S4) audible. | Sí |  |  |
| Abdomen | Refiere náuseas; sin hallazgos físicos relevantes. | Sí |  |  |
| Extremidades | Piel fría; pulsos periféricos conservados (disminuidos si hay compromiso hemodinámico). | Sí |  |  |
| Piel y mucosas | Palidez, sudoración fría. | Sí |  |  |

### CASO-SCA · 3. Acciones críticas

| Descripción | Tipo | Peso | idReferencia | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|---|---|
| ECG de 12 derivaciones dentro de los primeros 10 minutos, monitorización continua y vía venosa permeable | Manejo | 50 | `manejo_ecg_monitor_via` | ver `docs/conjuntos_manejo.md` |  |  |
| Activar el protocolo de reperfusión / traslado oportuno | Manejo | 50 | `manejo_activar_reperfusion` | ver `docs/conjuntos_manejo.md` |  |  |

### CASO-SCA · 4. Errores frecuentes

| Descripción | idReferenciaCondicion | Retroalimentación | Detección | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| No preguntar por la irradiación del dolor | `omitio:sca_irradiacion` | La irradiación a brazo izquierdo, cuello o mandíbula aumenta la probabilidad de origen isquémico. | automática |  |  |
| No preguntar por la duración exacta del dolor | `omitio:sca_inicio_dolor` | El tiempo de evolución define la ventana de reperfusión y la urgencia del traslado. | automática |  |  |
| No tomar el ECG de forma oportuna | `omitio:Electrocardiograma` | El ECG de 12 derivaciones debe realizarse dentro de los primeros 10 minutos ante todo dolor torácico sugerente. | automática |  |  |
| No cuantificar la intensidad del dolor (EVA) | `omitio:Exploración clínica del dolor` | La EVA permite objetivar el dolor, seguir la respuesta al tratamiento y priorizar la atención. | automática |  |  |

### CASO-SCA · 5. Diagnóstico esperado

| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|
| Síndrome Coronario Agudo con supradesnivel del ST (IAM tipo STEMI). | {infarto} {sca} {sindrome,coronario} {stemi} |  |  |

Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).

## CASO-TBC — Tuberculosis pulmonar

Motivo de consulta: Llevo varias semanas con una tos que no se me quita, y he bajado de peso sin querer.

Conteos: 7 preguntas (7 clave), 7 regiones (5 relevantes), 2 acciones críticas, 4 errores frecuentes (1 sin condición válida), 3 conjuntos de diagnóstico, 6 exámenes propios (ver `docs/matriz_examenes.md`).

### CASO-TBC · 1. Preguntas de anamnesis

| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|---|:-:|---|---|---|
| `tbc_hemoptisis` | ¿Ha notado sangre en el esputo? | Sí, un par de veces vi como hilos de sangre. | Sí | {sangre,tos} {sangre,esputo} {sangre,flem} {hemoptisis} |  |  |
| `tbc_duracion_tos` | ¿Cuánto tiempo lleva con tos? | Como tres semanas; antes era seca y ahora sale con flema. | Sí | {cuanto,tos} {hace,tos} {tiempo,tos} |  |  |
| `tbc_baja_peso` | ¿Ha bajado de peso? | Sí, unos cinco kilos sin hacer dieta. | Sí | {baj,peso} {adelgaz} {kilos} |  |  |
| `tbc_sudor_nocturno` | ¿Suda en la noche? | Sí, a veces empapo las sábanas. | Sí | {sud,noch} {empap,saban} |  |  |
| `tbc_contacto` | ¿Ha estado en contacto con alguien con tuberculosis? | Mi vecino tuvo tuberculosis hace unos meses. | Sí | {contact} {tuberculos} {vecino} |  |  |
| `tbc_fiebre` | ¿Tiene fiebre? | En las tardes me siento afiebrado. | Sí | {fiebr} |  |  |
| `tbc_hacinamiento` | ¿Vive con muchas personas en su casa? | Sí, somos siete en una casa chica. | Sí | {personas,casa} {hacinamiento} |  |  |

### CASO-TBC · 2. Hallazgos por región (examen físico)

| Región | Hallazgo | esHallazgoRelevante (inferida) | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|
| Cabeza y cuello | Adenopatías cervicales palpables, no dolorosas. | Sí |  |  |
| Tórax anterior | Murmullo pulmonar disminuido en los vértices. | Sí |  |  |
| Tórax posterior | Crépitos en los vértices, matidez apical. | Sí |  |  |
| Precordio | Sin alteraciones. | No |  |  |
| Abdomen | Sin hallazgos relevantes. | No |  |  |
| Extremidades | Pérdida de masa muscular, aspecto caquéctico. | Sí |  |  |
| Piel y mucosas | Palidez, mucosas secas; refiere sudoración nocturna. | Sí |  |  |

### CASO-TBC · 3. Acciones críticas

| Descripción | Tipo | Peso | idReferencia | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|---|---|
| Instaurar aislamiento respiratorio apenas se sospecha | Manejo | 50 | `manejo_aislamiento_respiratorio` | ver `docs/conjuntos_manejo.md` |  |  |
| Reconocer que es una enfermedad de notificación obligatoria | Manejo | 50 | `reconocer_notificacion_obligatoria` | ver `docs/conjuntos_manejo.md` |  |  |

### CASO-TBC · 4. Errores frecuentes

| Descripción | idReferenciaCondicion | Retroalimentación | Detección | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| No indagar el contacto epidemiológico | `omitio:tbc_contacto` | El antecedente de contacto con un caso confirmado es determinante para la sospecha diagnóstica. | automática |  |  |
| No indagar el hacinamiento | `omitio:tbc_hacinamiento` | El hacinamiento es un factor de riesgo y de transmisión intradomiciliaria que orienta el estudio de contactos. | automática |  |  |
| Omitir preguntar por hemoptisis | `omitio:tbc_hemoptisis` | La hemoptisis, aunque poco frecuente, es un dato de alarma y de alta especificidad en este contexto. | automática |  |  |
| No usar EPP (mascarilla de alta eficiencia) antes de examinar | (vacía) | Ante la sospecha de TBC bacilífera, la protección respiratoria del examinador debe colocarse ANTES del contacto. | **no se detecta automáticamente** |  |  |

### CASO-TBC · 5. Diagnóstico esperado

| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|
| Tuberculosis pulmonar activa. | {tuberculosis} {tbc} {bacilo} |  |  |

Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).

## CASO-EPA — Edema Pulmonar Agudo (cardiogénico)

Motivo de consulta: Me cuesta mucho respirar, siento que me ahogo, sobre todo cuando estoy acostado.

Conteos: 7 preguntas (7 clave), 7 regiones (7 relevantes), 2 acciones críticas, 5 errores frecuentes (1 sin condición válida), 3 conjuntos de diagnóstico, 8 exámenes propios (ver `docs/matriz_examenes.md`).

### CASO-EPA · 1. Preguntas de anamnesis

| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|---|:-:|---|---|---|
| `epa_inicio_disnea` | ¿Desde cuándo comenzó la dificultad para respirar? | Hace unas dos horas, y empeoró rápido. | Sí | {desde,respir} {hace,ahog} {empez,respir} |  |  |
| `epa_ortopnea` | ¿La dificultad empeora al acostarse? | Sí, tengo que dormir sentado con varias almohadas. | Sí | {acost,respir} {acost,ahog} {dorm,almohad} |  |  |
| `epa_dpn` | ¿Se ha despertado en la noche con falta de aire? | Sí, anoche me desperté sintiendo que me ahogaba. | Sí | {noch,ahog} {noch,aire} {despert,ahog} |  |  |
| `epa_tos` | ¿Tiene tos? ¿Cómo es? | Sí, con una flema rosada y espumosa. | Sí | {tos} |  |  |
| `epa_cardiopatia` | ¿Tiene antecedentes de enfermedades del corazón? | Sí, tengo insuficiencia cardíaca. | Sí | {cardi} {corazon} |  |  |
| `epa_adherencia` | ¿Ha tomado sus medicamentos como corresponde? | La verdad, se me olvidó tomar el diurético estos últimos días. | Sí | {medic} {diuretic} {tratamiento} |  |  |
| `epa_edema` | ¿Ha notado hinchazón en las piernas? | Sí, se me hincharon harto los pies. | Sí | {hinch} {edema} |  |  |

### CASO-EPA · 2. Hallazgos por región (examen físico)

| Región | Hallazgo | esHallazgoRelevante (inferida) | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|
| Cabeza y cuello | Ingurgitación yugular presente. | Sí |  |  |
| Tórax anterior | Uso de musculatura accesoria, tiraje intercostal, taquipnea. | Sí |  |  |
| Tórax posterior | Crépitos bibasales bilaterales, matidez en las bases. | Sí |  |  |
| Precordio | Taquicardia, tercer ruido (S3) audible. | Sí |  |  |
| Abdomen | Reflujo hepatoyugular positivo. | Sí |  |  |
| Extremidades | Edema bilateral con signo de fóvea en extremidades inferiores. | Sí |  |  |
| Piel y mucosas | Piel fría y sudorosa, cianosis peribucal leve. | Sí |  |  |

### CASO-EPA · 3. Acciones críticas

| Descripción | Tipo | Peso | idReferencia | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|---|---|
| Oxígeno suplementario inmediato y posición semisentada / Fowler alta | Manejo | 50 | `manejo_oxigeno_fowler` | ver `docs/conjuntos_manejo.md` |  |  |
| Reconocer signos de sobrecarga de volumen (ingurgitación yugular, edema) como causa cardiogénica | Manejo | 50 | `reconocer_causa_cardiogenica` | ver `docs/conjuntos_manejo.md` |  |  |

### CASO-EPA · 4. Errores frecuentes

| Descripción | idReferenciaCondicion | Retroalimentación | Detección | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| Omitir preguntar por ortopnea | `omitio:epa_ortopnea` | La ortopnea es un dato clave de congestión pulmonar cardiogénica; siempre indagar la relación de la disnea con el decúbito. | automática |  |  |
| Omitir preguntar por disnea paroxística nocturna | `omitio:epa_dpn` | Despertar de noche con falta de aire orienta fuertemente a insuficiencia cardíaca izquierda descompensada. | automática |  |  |
| No auscultar el tórax posterior | `omitio:Tórax posterior` | Los crépitos bibasales bilaterales son un hallazgo central; auscultar ambos campos en su cara posterior. | automática |  |  |
| No valorar el edema de extremidades | `omitio:Extremidades` | El edema con fóvea confirma sobrecarga de volumen y apoya el origen cardiogénico. | automática |  |  |
| Confundir el cuadro con una infección respiratoria pura | (vacía) | La ausencia de fiebre, la ortopnea, las yugulares ingurgitadas y el edema orientan a causa cardiogénica, no infecciosa. | **no se detecta automáticamente** |  |  |

### CASO-EPA · 5. Diagnóstico esperado

| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|
| Edema pulmonar agudo de origen cardiogénico (descompensación de insuficiencia cardíaca). | {edema,pulmonar} {edema,cardiogenic} {insuficiencia,cardiac} |  |  |

Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).

## CASO-EPOC — Exacerbación de EPOC

Motivo de consulta: Llevo años con tos, pero ahora me cuesta respirar mucho más que antes, se me agravó.

Conteos: 6 preguntas (6 clave), 7 regiones (5 relevantes), 2 acciones críticas, 3 errores frecuentes (1 sin condición válida), 3 conjuntos de diagnóstico, 4 exámenes propios (ver `docs/matriz_examenes.md`).

### CASO-EPOC · 1. Preguntas de anamnesis

| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|---|:-:|---|---|---|
| `epoc_duracion_tos` | ¿Hace cuánto tiempo tiene tos? | Hace años; tos de fumador le dicen. | Sí | {hace,tos} {años,tos} |  |  |
| `epoc_tabaquismo` | ¿Fuma o ha fumado? ¿Cuánto? | Fumé como 30 años, un paquete al día. | Sí | {fum} |  |  |
| `epoc_expectoracion` | ¿La tos tiene flema? | Sí, en las mañanas boto harta flema. | Sí | {tos,flem} {flem} |  |  |
| `epoc_disnea_esfuerzo` | ¿Ha notado que se cansa más al hacer esfuerzo? | Sí, ya no puedo subir escaleras como antes. | Sí | {cansa,esfuerzo} {fatiga} |  |  |
| `epoc_baja_peso` | ¿Ha bajado de peso? | Un poco, sí. | Sí | {baj,peso} {adelgaz} |  |  |
| `epoc_exacerbaciones` | ¿Ha tenido episodios similares antes? | Sí, esta es como la tercera vez que me pasa esto. | Sí | {episodio,antes} {recaida} |  |  |

### CASO-EPOC · 2. Hallazgos por región (examen físico)

| Región | Hallazgo | esHallazgoRelevante (inferida) | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|
| Cabeza y cuello | Respiración con labios fruncidos. | Sí |  |  |
| Tórax anterior | Tórax en tonel, murmullo disminuido, sibilancias espiratorias. | Sí |  |  |
| Tórax posterior | Hipersonoridad a la percusión, roncus. | Sí |  |  |
| Precordio | Ruidos cardíacos alejados por hiperinsuflación. | Sí |  |  |
| Abdomen | Sin hallazgos relevantes. | No |  |  |
| Extremidades | Sin edema (salvo cor pulmonale asociado). | No |  |  |
| Piel y mucosas | Coloración rosada-grisácea, sin cianosis franca. | Sí |  |  |

### CASO-EPOC · 3. Acciones críticas

| Descripción | Tipo | Peso | idReferencia | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|---|---|
| Valorar patrón respiratorio y titular O2 con cautela (evitar hiperoxia en retenedores de CO2) | Manejo | 50 | `manejo_o2_titulado` | ver `docs/conjuntos_manejo.md` |  |  |
| Reconocer necesidad de broncodilatadores y valorar fatiga respiratoria | Manejo | 50 | `manejo_broncodilatadores` | ver `docs/conjuntos_manejo.md` |  |  |

### CASO-EPOC · 4. Errores frecuentes

| Descripción | idReferenciaCondicion | Retroalimentación | Detección | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| No indagar la historia tabáquica | `omitio:epoc_tabaquismo` | Cuantificar el tabaquismo (paquetes/año) es esencial para el diagnóstico y el pronóstico de la EPOC. | automática |  |  |
| No indagar exacerbaciones previas | `omitio:epoc_exacerbaciones` | La frecuencia de exacerbaciones define el fenotipo y la gravedad del paciente EPOC. | automática |  |  |
| Administrar oxígeno a alto flujo sin control | (vacía) | En el retenedor de CO2 la hiperoxia puede deprimir el estímulo respiratorio; titular O2 a una SatO2 objetivo de 88-92%. | **no se detecta automáticamente** |  |  |

### CASO-EPOC · 5. Diagnóstico esperado

| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|
| Exacerbación de Enfermedad Pulmonar Obstructiva Crónica (EPOC). | {epoc} {exacerbacion,epoc} {obstructiva,cronica} |  |  |

Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).

## CASO-NAC — Neumonía adquirida en la comunidad

Motivo de consulta: Tengo fiebre alta, me cuesta respirar y tengo un dolor fuerte en el pecho al respirar hondo.

Conteos: 6 preguntas (6 clave), 7 regiones (4 relevantes), 2 acciones críticas, 4 errores frecuentes (0 sin condición válida), 3 conjuntos de diagnóstico, 6 exámenes propios (ver `docs/matriz_examenes.md`).

### CASO-NAC · 1. Preguntas de anamnesis

| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|---|:-:|---|---|---|
| `nac_inicio_fiebre` | ¿Hace cuánto tiene fiebre? | Desde ayer, muy alta, con calofríos. | Sí | {hace,fiebr} {desde,fiebr} |  |  |
| `nac_tos_productiva` | ¿Tiene tos? ¿Con flema? | Sí, con flema amarillo-verdosa. | Sí | {tos,flem} {flem} |  |  |
| `nac_dolor_pleuritico` | ¿Le duele el pecho al respirar o toser? | Sí, un dolor punzante de este lado. | Sí | {dol,pecho} {punzante} |  |  |
| `nac_disnea` | ¿Le cuesta respirar? | Sí, respiro rápido y corto. | Sí | {cuesta,respir} {falta,aire} |  |  |
| `nac_contacto` | ¿Ha tenido contacto con alguien enfermo? | No que yo sepa. | Sí | {contact,enferm} |  |  |
| `nac_comorbilidad` | ¿Tiene alguna enfermedad crónica? | Tengo EPOC. | Sí | {enfermedad,cronic} {epoc} |  |  |

### CASO-NAC · 2. Hallazgos por región (examen físico)

| Región | Hallazgo | esHallazgoRelevante (inferida) | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|
| Cabeza y cuello | Sin adenopatías relevantes. | No |  |  |
| Tórax anterior | Crépitos localizados, murmullo disminuido en la zona afectada. | Sí |  |  |
| Tórax posterior | Matidez y broncofonía/egofonía en la zona de consolidación. | Sí |  |  |
| Precordio | Taquicardia. | Sí |  |  |
| Abdomen | Sin hallazgos relevantes (posible dolor referido si la base derecha está comprometida). | No |  |  |
| Extremidades | Sin edema; posible cianosis distal si hay hipoxemia severa. | No |  |  |
| Piel y mucosas | Piel caliente y sudorosa; cianosis peribucal si el cuadro es grave. | Sí |  |  |

### CASO-NAC · 3. Acciones críticas

| Descripción | Tipo | Peso | idReferencia | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|---|---|
| Posicionar en semifowler e iniciar oxigenoterapia si hay hipoxemia | Manejo | 50 | `manejo_semifowler_o2` | ver `docs/conjuntos_manejo.md` |  |  |
| Reconocer signos de gravedad (confusión, hipotensión, taquipnea severa) | Manejo | 50 | `manejo_reconocer_gravedad` | ver `docs/conjuntos_manejo.md` |  |  |

### CASO-NAC · 4. Errores frecuentes

| Descripción | idReferenciaCondicion | Retroalimentación | Detección | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| No auscultar comparativamente ambos campos pulmonares | `omitio:Tórax anterior` | La comparación entre ambos hemitórax hace evidente la asimetría del murmullo y localiza la consolidación. | automática |  |  |
| Omitir preguntar por dolor pleurítico | `omitio:nac_dolor_pleuritico` | El dolor que aumenta con la inspiración profunda orienta a compromiso pleural por la consolidación. | automática |  |  |
| No solicitar la radiografía de tórax | `omitio:Radiografía de tórax` | La radiografía confirma la consolidación y es el examen de referencia para el diagnóstico. | automática |  |  |
| No solicitar hemograma | `omitio:Hemograma` | La leucocitosis con desviación a izquierda apoya el origen bacteriano y ayuda a la estratificación. | automática |  |  |

### CASO-NAC · 5. Diagnóstico esperado

| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|
| Neumonía adquirida en la comunidad. | {neumonia} {neumonia,comunidad} {pulmonia} |  |  |

Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).

## CASO-SARS — Síndrome Respiratorio Agudo Grave (SARS)

Motivo de consulta: Tengo fiebre, me duele todo el cuerpo y hace unos días que me cuesta respirar.

Conteos: 6 preguntas (6 clave), 7 regiones (6 relevantes), 2 acciones críticas, 3 errores frecuentes (1 sin condición válida), 3 conjuntos de diagnóstico, 6 exámenes propios (ver `docs/matriz_examenes.md`).

### CASO-SARS · 1. Preguntas de anamnesis

| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|---|:-:|---|---|---|
| `sars_nexo` | ¿Ha viajado recientemente o tenido contacto con alguien enfermo? | Volví hace una semana de un viaje donde había un brote. | Sí | {viaj} {contact,enferm} {brote} |  |  |
| `sars_fiebre` | ¿Tiene fiebre? ¿De cuánto? | Sí, como 38 grados, con calofríos. | Sí | {fiebr} |  |  |
| `sars_mialgias` | ¿Le duele el cuerpo? | Sí, todo el cuerpo, sobre todo las piernas. | Sí | {dol,cuerpo} {mialgia} |  |  |
| `sars_cefalea` | ¿Le duele la cabeza? | Sí, harto. | Sí | {dol,cabeza} {cefalea} |  |  |
| `sars_disnea` | ¿Ha notado que le cuesta respirar? | Sí, sobre todo cuando camino. | Sí | {cuesta,respir} {falta,aire} {disnea} |  |  |
| `sars_tos` | ¿Ha tenido tos? | Un poco, seca. | Sí | {tos} |  |  |

### CASO-SARS · 2. Hallazgos por región (examen físico)

| Región | Hallazgo | esHallazgoRelevante (inferida) | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|
| Cabeza y cuello | Refiere cefalea; sin adenopatías. | Sí |  |  |
| Tórax anterior | Taquipnea, leve uso de musculatura accesoria. | Sí |  |  |
| Tórax posterior | Crépitos difusos bilaterales. | Sí |  |  |
| Precordio | Taquicardia sinusal. | Sí |  |  |
| Abdomen | Sin hallazgos relevantes. | No |  |  |
| Extremidades | Refiere mialgias; sin edema. | Sí |  |  |
| Piel y mucosas | Cianosis peribucal leve; piel con calofríos. | Sí |  |  |

### CASO-SARS · 3. Acciones críticas

| Descripción | Tipo | Peso | idReferencia | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|---|---|
| Aislamiento / precauciones apenas se sospecha, con monitorización continua de SatO2 | Manejo | 50 | `manejo_aislamiento_monitor_sato2` | ver `docs/conjuntos_manejo.md` |  |  |
| Preparar manejo escalonado (oxigenoterapia y eventual intubación) | Manejo | 50 | `manejo_escalonado_via_aerea` | ver `docs/conjuntos_manejo.md` |  |  |

### CASO-SARS · 4. Errores frecuentes

| Descripción | idReferenciaCondicion | Retroalimentación | Detección | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| No indagar el nexo epidemiológico | `omitio:sars_nexo` | El antecedente de viaje o contacto en contexto de brote es el dato que dispara las precauciones y el estudio dirigido. | automática |  |  |
| No valorar la cianosis ni la disnea progresiva | `omitio:Piel y mucosas` | La cianosis peribucal y la disnea de esfuerzo indican hipoxemia significativa y necesidad de oxígeno. | automática |  |  |
| Retrasar el inicio de la oxigenoterapia | (vacía) | Con SatO2 90% y disnea progresiva, la oxigenoterapia no debe esperar a la confirmación diagnóstica. | **no se detecta automáticamente** |  |  |

### CASO-SARS · 5. Diagnóstico esperado

| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|
| Síndrome Respiratorio Agudo Grave (SARS). | {sars} {respiratorio,agudo,grave} {sindrome,respiratorio} |  |  |

Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).

## CASO-BRONQ — Bronquitis aguda

Motivo de consulta: Tengo tos hace unos días, al principio seca y ahora sale con flema, y me duele el pecho al toser.

Conteos: 6 preguntas (6 clave), 7 regiones (3 relevantes), 2 acciones críticas, 2 errores frecuentes (1 sin condición válida), 2 conjuntos de diagnóstico, 2 exámenes propios (ver `docs/matriz_examenes.md`).

### CASO-BRONQ · 1. Preguntas de anamnesis

| idPregunta | Pregunta sugerida | Respuesta del paciente | esPreguntaClave (inferida) | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|---|:-:|---|---|---|
| `bronq_inicio_tos` | ¿Cuándo comenzó la tos? | Hace unos cinco días. | Sí | {cuando,tos} {empez,tos} |  |  |
| `bronq_tos_seca_a_productiva` | ¿La tos era seca al principio? | Sí, y ahora boto flema clara. | Sí | {tos,seca} {seca,flem} |  |  |
| `bronq_dolor_toracico` | ¿Le duele el pecho al toser? | Sí, como que se me tensa el pecho. | Sí | {dol,pecho} {tensa,pecho} |  |  |
| `bronq_fiebre` | ¿Tiene fiebre? | Un poco, me he sentido afiebrado. | Sí | {fiebr} |  |  |
| `bronq_exposicion` | ¿Ha estado expuesto al frío o resfriado recientemente? | Sí, hace una semana andaba resfriado. | Sí | {frio} {resfri} |  |  |
| `bronq_tabaquismo` | ¿Fuma? | Sí, fumo desde hace varios años. | Sí | {fum} |  |  |

### CASO-BRONQ · 2. Hallazgos por región (examen físico)

| Región | Hallazgo | esHallazgoRelevante (inferida) | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|
| Cabeza y cuello | Congestión nasal leve, faringe levemente eritematosa. | Sí |  |  |
| Tórax anterior | Roncus difusos, sin signos de consolidación. | Sí |  |  |
| Tórax posterior | Murmullo conservado con roncus dispersos. | Sí |  |  |
| Precordio | Sin alteraciones. | No |  |  |
| Abdomen | Sin hallazgos relevantes. | No |  |  |
| Extremidades | Mialgias leves, sin edema. | No |  |  |
| Piel y mucosas | Piel tibia, sin cianosis. | No |  |  |

### CASO-BRONQ · 3. Acciones críticas

| Descripción | Tipo | Peso | idReferencia | Conjuntos | Validación (OK / Cambiar) | Comentario |
|---|---|:-:|---|---|---|---|
| Diferenciar de neumonía descartando consolidación o signos de gravedad | Manejo | 50 | `manejo_descartar_neumonia` | ver `docs/conjuntos_manejo.md` |  |  |
| Entregar indicaciones de automanejo y señales de alarma para reconsultar | Manejo | 50 | `manejo_educacion_alarma` | ver `docs/conjuntos_manejo.md` |  |  |

### CASO-BRONQ · 4. Errores frecuentes

| Descripción | idReferenciaCondicion | Retroalimentación | Detección | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| Indicar antibióticos sin justificación | (vacía) | La bronquitis aguda es habitualmente viral; el antibiótico no acorta el curso y expone a efectos adversos y resistencia. | **no se detecta automáticamente** |  |  |
| No indagar el consumo de tabaco ni educar sobre su abandono | `omitio:bronq_tabaquismo` | El tabaquismo perpetúa la inflamación bronquial; la consulta aguda es una oportunidad para la intervención breve. | automática |  |  |

### CASO-BRONQ · 5. Diagnóstico esperado

| Diagnóstico esperado | Conjuntos (AND dentro de `{}`, OR entre conjuntos) | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|
| Bronquitis aguda. | {bronquitis} {bronquitis,aguda} |  |  |

Exámenes complementarios de este caso: ver `docs/matriz_examenes.md` (no se duplican aquí).

