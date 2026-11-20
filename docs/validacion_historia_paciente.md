# Validación — historia del paciente, catálogo de intenciones y mapeo

Generado por `Tools/Razonamiento Clínico/Anamnesis/3. Generar documentos de validación (docs/)` (borrador pendiente de validación clínica). Solo lee los assets. Las columnas **Validación (OK / Cambiar)** y **Comentario** son para quien revise; el `.csv` trae las mismas filas.

Qué cambia con esto: cuando el estudiante escribe una pregunta que no coincide con ninguna pregunta clave del caso, el sistema busca la **intención** más específica del catálogo y el paciente responde con el campo correspondiente de su **historia**. **El puntaje no cambia**: solo puntúan las preguntas clave, y una intención puntúa únicamente si está vinculada a una pregunta clave (sección 3). El matching sigue siendo determinista (sin IA ni red).

## 1. Historia del paciente (respuestas en primera persona)

### CASO-BRONQ — Bronquitis aguda (Femenino, 41 años)

| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| inicio | Empecé con tos hace unos cinco días. | respuesta bronq_inicio_tos; motivoConsulta | | |
| evolucion | Al principio la tos era seca y ahora sale con flema clara. | respuesta bronq_tos_seca_a_productiva | | |
| empeora | Cuando toso se me tensa el pecho. | motivoConsulta; respuesta bronq_dolor_toracico | | |
| alivia | No refiere | sin fuente en la ficha (pendiente) | | |
| remedios | No refiere | sin fuente en la ficha (pendiente) | | |
| alergias | No refiere | sin fuente en la ficha (pendiente) | | |
| antecedentes | Fumo y hace una semana andaba con un resfrío. | antecedentes (tabaquismo; resfrío reciente); respuesta bronq_exposicion | | |
| habitos | Fumo desde hace varios años. | respuesta bronq_tabaquismo | | |
| intensidad | No refiere | sin fuente en la ficha (pendiente) | | |
| localizacion | Me duele el pecho. | motivoConsulta | | |
| sintomasAsociados | Un poco de fiebre. | respuesta bronq_fiebre | | |
| perfil | Soy Patricia González, tengo 41 años. | datosAdministrativos | | |

### CASO-EPA — Edema Pulmonar Agudo (cardiogénico) (Femenino, 68 años)

| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| inicio | Hace unas dos horas, y empeoró rápido. | respuesta epa_inicio_disnea | | |
| evolucion | Fue empeorando rápido desde que empezó. | respuesta epa_inicio_disnea | | |
| empeora | Me ahogo más cuando me acuesto; tengo que dormir sentada con varias almohadas. | motivoConsulta; respuesta epa_ortopnea | | |
| alivia | No refiere | sin fuente en la ficha (pendiente) | | |
| remedios | Tengo un diurético, pero estos últimos días se me olvidó tomarlo. | respuesta epa_adherencia; antecedentes (mal apego al diurético) | | |
| alergias | No refiere | sin fuente en la ficha (pendiente) | | |
| antecedentes | Tengo problemas del corazón y la presión alta. | antecedentes (insuficiencia cardíaca, hipertensión) dicho sin nombrar el diagnóstico | | |
| habitos | No refiere | sin fuente en la ficha (pendiente) | | |
| intensidad | Me cuesta mucho respirar, siento que me ahogo. | motivoConsulta | | |
| localizacion | No refiere | sin fuente en la ficha (pendiente) | | |
| sintomasAsociados | Tengo tos con una flema rosada y espumosa, y se me hincharon harto los pies. | respuesta epa_tos; respuesta epa_edema | | |
| perfil | Soy Elena Sepúlveda, tengo 68 años. | datosAdministrativos | | |

### CASO-EPOC — Exacerbación de EPOC (Masculino, 64 años)

| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| inicio | Llevo años con tos; ahora me cuesta respirar mucho más que antes. | motivoConsulta | | |
| evolucion | Se me agravó; ya no puedo subir escaleras como antes. | motivoConsulta; respuesta epoc_disnea_esfuerzo | | |
| empeora | Me canso más cuando hago esfuerzo; ya no puedo subir escaleras como antes. | respuesta epoc_disnea_esfuerzo | | |
| alivia | No refiere | sin fuente en la ficha (pendiente) | | |
| remedios | No refiere | sin fuente en la ficha (pendiente) | | |
| alergias | No refiere | sin fuente en la ficha (pendiente) | | |
| antecedentes | Esta es como la tercera vez que me pasa esto. | antecedentes (exacerbaciones previas); respuesta epoc_exacerbaciones | | |
| habitos | Fumé como 30 años, un paquete al día. | respuesta epoc_tabaquismo | | |
| intensidad | Me cuesta respirar mucho más que antes. | motivoConsulta | | |
| localizacion | No refiere | sin fuente en la ficha (pendiente) | | |
| sintomasAsociados | Boto harta flema en las mañanas y he bajado un poco de peso. | respuesta epoc_expectoracion; respuesta epoc_baja_peso | | |
| perfil | Soy Manuel Rojas, tengo 64 años. | datosAdministrativos | | |

### CASO-NAC — Neumonía adquirida en la comunidad (Masculino, 72 años)

| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| inicio | Desde ayer, con fiebre muy alta y calofríos. | respuesta nac_inicio_fiebre | | |
| evolucion | No refiere | sin fuente en la ficha (pendiente) | | |
| empeora | Me duele más cuando respiro hondo. | motivoConsulta | | |
| alivia | No refiere | sin fuente en la ficha (pendiente) | | |
| remedios | No refiere | sin fuente en la ficha (pendiente) | | |
| alergias | No refiere | sin fuente en la ficha (pendiente) | | |
| antecedentes | Tengo EPOC y no he estado hospitalizado hace poco. | respuesta nac_comorbilidad; antecedentes (sin hospitalización reciente) | | |
| habitos | No refiere | sin fuente en la ficha (pendiente) | | |
| intensidad | Tengo un dolor fuerte en el pecho y la fiebre es muy alta. | motivoConsulta; respuesta nac_inicio_fiebre | | |
| localizacion | Un dolor punzante en el pecho, de este lado. | respuesta nac_dolor_pleuritico | | |
| sintomasAsociados | Tengo tos con flema amarillo-verdosa, calofríos y respiro rápido y corto. | respuesta nac_tos_productiva; nac_inicio_fiebre; nac_disnea | | |
| perfil | Soy Eduardo Fernández, tengo 72 años. | datosAdministrativos | | |

### CASO-SARS — Síndrome Respiratorio Agudo Grave (SARS) (Femenino, 29 años)

| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| inicio | Hace unos días que me cuesta respirar. | motivoConsulta | | |
| evolucion | No refiere | sin fuente en la ficha (pendiente) | | |
| empeora | Me cuesta más respirar cuando camino. | respuesta sars_disnea | | |
| alivia | No refiere | sin fuente en la ficha (pendiente) | | |
| remedios | No refiere | sin fuente en la ficha (pendiente) | | |
| alergias | No refiere | sin fuente en la ficha (pendiente) | | |
| antecedentes | No tengo otras enfermedades. Volví hace una semana de un viaje donde había un brote. | antecedentes (sin comorbilidades); respuesta sars_nexo | | |
| habitos | No refiere | sin fuente en la ficha (pendiente) | | |
| intensidad | La cabeza me duele harto. | respuesta sars_cefalea | | |
| localizacion | Me duele todo el cuerpo, sobre todo las piernas, y también la cabeza. | respuesta sars_mialgias; sars_cefalea | | |
| sintomasAsociados | Tengo fiebre como de 38 grados con calofríos, dolor de cabeza y un poco de tos seca. | respuesta sars_fiebre; sars_cefalea; sars_tos | | |
| perfil | Soy Valentina Torres, tengo 29 años. | datosAdministrativos | | |

### CASO-SCA — Síndrome Coronario Agudo (STEMI) (Masculino, 59 años)

| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| inicio | Hace unos 30 minutos, de repente. | respuesta sca_inicio_dolor | | |
| evolucion | No refiere | sin fuente en la ficha (pendiente) | | |
| empeora | No refiere | sin fuente en la ficha (pendiente) | | |
| alivia | No se me pasa; sigue igual aunque estoy sentado. | respuesta sca_relacion_reposo | | |
| remedios | No refiere | sin fuente en la ficha (pendiente) | | |
| alergias | No refiere | sin fuente en la ficha (pendiente) | | |
| antecedentes | Soy hipertenso y diabético, tengo alterados los lípidos y tuve un problema del corazón hace dos años. | respuesta sca_factores_riesgo; antecedentes (dislipidemia; infarto previo dicho sin nombrar el diagnóstico) | | |
| habitos | Fumo. | respuesta sca_factores_riesgo (fuma) | | |
| intensidad | Es un dolor fuerte. | motivoConsulta (dolor fuerte) | | |
| localizacion | En el pecho, y se me corre al brazo izquierdo y al cuello. | motivoConsulta; respuesta sca_irradiacion | | |
| sintomasAsociados | Estoy sudando frío y con ganas de vomitar. | respuesta sca_sintomas_asociados | | |
| perfil | Soy Jorge Alarcón, tengo 59 años. | datosAdministrativos | | |

### CASO-TBC — Tuberculosis pulmonar (Masculino, 34 años)

| Campo | Respuesta del paciente | Fuente en la ficha | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| inicio | Como tres semanas con la tos. | respuesta tbc_duracion_tos | | |
| evolucion | Antes era seca y ahora sale con flema. | respuesta tbc_duracion_tos | | |
| empeora | No refiere | sin fuente en la ficha (pendiente) | | |
| alivia | No refiere | sin fuente en la ficha (pendiente) | | |
| remedios | No refiere | sin fuente en la ficha (pendiente) | | |
| alergias | No refiere | sin fuente en la ficha (pendiente) | | |
| antecedentes | Estuve en contacto con un vecino que estuvo enfermo hace unos meses, y somos siete en una casa chica. | antecedentes (contacto reciente; hacinamiento) sin nombrar el diagnóstico; respuesta tbc_contacto; tbc_hacinamiento | | |
| habitos | No refiere | sin fuente en la ficha (pendiente) | | |
| intensidad | No refiere | sin fuente en la ficha (pendiente) | | |
| localizacion | No refiere | sin fuente en la ficha (pendiente) | | |
| sintomasAsociados | He bajado unos cinco kilos sin hacer dieta, a veces empapo las sábanas en la noche, me siento afiebrado en las tardes y un par de veces vi hilos de sangre. | respuesta tbc_baja_peso; tbc_sudor_nocturno; tbc_fiebre; tbc_hemoptisis | | |
| perfil | Soy Carlos Muñoz, tengo 34 años. | datosAdministrativos | | |

## 2. Catálogo de intenciones (compartido por los 7 casos)

Los conjuntos son raíces sin tildes: dentro de un conjunto TODAS las raíces deben aparecer; basta que coincida UN conjunto. Las raíces de 5 o más letras toleran una letra de diferencia (errores de tipeo). Si el texto contiene alguna raíz **excluyente**, la intención no se aplica. Cuando coinciden varias intenciones gana la de conjunto más largo.

| Id | Descripción | Responde con | Conjuntos | Excluyentes | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|---|
| `tiempo_evolucion` | Cuánto tiempo hace / desde cuándo empezó el problema. | Inicio | {hace,cuant} {desde,cuand} {cuand,empez} {cuand,comenz} {cuand,inici} {cuand,parti} {cuand,aparec} {cuand,cuenta} {cuant,tiempo} {cuant,llev} {tiempo,llev} {cuant,dias} {cuant,horas} {cuant,semanas} {momento,empez} {momento,comenz} {hace,tiempo} {hace,mucho,que} {desde,hace} | {fum} {cigarr} {tabac} {alcohol} {trago} {copete} {remedi} {medicament} {pastill} {trabaj} {vacun} {operad} {alerg} {ejercic} {deport} {vive} {hospital} {esper} | | |
| `intensidad` | Qué tan fuerte es el dolor o la molestia. | Intensidad | {cuant,duel} {cuant,dol} {cuant,molest} {fuert} {intens} {escala} {nota,dol} {soportab} {duel,mucho} {dol,mucho} {molest,mucho} {tan,mal} {grave} {hart,duel} {hart,dol} {hart,molest} | {hace} {desde} {tiempo} {llev} {dias} {horas} {semanas} {meses} {anos} | | |
| `localizacion` | Dónde le duele o dónde siente la molestia. | Localizacion | {donde,duel} {donde,dol} {donde,molest} {donde,siente} {que,parte} {lado,duel} {lado,dol} {parte,duel} {parte,dol} {parte,molest} {parte,pecho} {zona} {ubic} {localiz} {mostr,donde} {donde,exact} {corre} {irradia} {extiende} {lugar,dol} {lugar,duel} {lugar,molest} | {aparte} {ademas} | | |
| `empeora` | Qué hace que empeore. | Empeora | {empeor} {agrav} {peor} {aument,dol} {aument,molest} {provo} {desencaden} {gatill} {fuert,cuando} {intensific} | — | | |
| `alivia` | Qué hace que mejore o se calme. | Alivia | {mejor} {alivi} {calma} {cede,repos} {cede,dol} {cede,algo} {cede,descans} {quita} {disminu} {baja,dol} {baja,molest} {baje,dol} {baje,molest} {repos} {posicion} {se,le,pase} | — | | |
| `remedios` | Qué remedios o medicamentos toma o ha tomado. | Remedios | {remedi} {medicament} {medicin} {medicac} {pastill} {tomando} {farmac} {analgesic} {paracetamol} {ibuprofen} {automedic} {tratamiento} {toma,algo,para} {tomo,algo,para} {tomad,algo} | {alerg} {reaccion} {cae,mal} | | |
| `alergias` | Si es alérgico a algo. | Alergias | {alerg} {intoleranc} {reaccion,medicament} {reaccion,alimento} {cae,mal,remedi} {cae,mal,medicament} {cae,mal,alimento} | — | | |
| `antecedentes` | Enfermedades, operaciones u hospitalizaciones previas. | Antecedentes | {antecedent} {enfermedad} {cronic} {operaci} {operad} {cirugi} {hospital} {padece} {sufre,de,algo} {patolog} {diabet} {hipertens} {presion,alta} {colesterol} {problema,salud} {historial} {historia,medica} {historia,clinica} {parecido,antes} {diagnostic,previ} | — | | |
| `habitos` | Hábitos: tabaco, alcohol, drogas, ejercicio, alimentación. | Habitos | {fum} {cigarr} {tabac} {alcohol} {trago} {copete} {bebid} {drog} {ejercic} {deport} {habito} {estilo,vida} {dieta} {alimentac} {consume} {vicio} | — | | |
| `sintomas_asociados` | Otros síntomas que acompañan al problema. | SintomasAsociados | {sintoma} {algo,mas} {ademas} {acompan,algo} {acompan,otr} {tambien,siente} {tambien,tiene} {otra,molest} {otra,cosa} {otro,problema} {otro,malestar} {que,mas,siente} | — | | |
| `motivo_consulta` | Qué le pasa / qué lo trae (responde con el motivo de consulta de la ficha). | MotivoConsulta | {que,le,pasa} {molest,algo} {que,molest} {motivo} {trae,aqui} {como,siente} {que,siente} {ayud} {consulta} {que,duel} {cual,problema} {que,problema} {que,trae} {sucede} {como,encuentra} {que,le,paso} {que,vino} {ocurre} | {cuesta} {cobr} {precio} {valor} | | |

Respuesta cuando el campo es "No refiere": **Mmm, no sabría decirle.**

Respuesta cuando nada coincide (por caso, `respuestaGenericaSinMatch`): **Disculpe, no le entendí bien. ¿Me lo puede preguntar de otra forma?**

## 3. Mapeo intención → pregunta clave

**Aplicado** (equivalencia evidente por el texto de la pregunta): si el estudiante pregunta por la intención y el texto no coincide con las palabras de la pregunta clave, se registra esa pregunta (puntúa) y el paciente responde con su `respuestaPaciente` de siempre.

| Caso | Pregunta clave | Texto de la pregunta | Intención | Respuesta del paciente | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|---|
| CASO-SCA | `sca_inicio_dolor` | ¿Cuándo comenzó el dolor? | `tiempo_evolucion` | Hace unos 30 minutos, de repente. | | |
| CASO-BRONQ | `bronq_inicio_tos` | ¿Cuándo comenzó la tos? | `tiempo_evolucion` | Hace unos cinco días. | | |
| CASO-EPA | `epa_inicio_disnea` | ¿Desde cuándo comenzó la dificultad para respirar? | `tiempo_evolucion` | Hace unas dos horas, y empeoró rápido. | | |
| CASO-NAC | `nac_inicio_fiebre` | ¿Hace cuánto tiene fiebre? | `tiempo_evolucion` | Desde ayer, muy alta, con calofríos. | | |
| CASO-EPOC | `epoc_duracion_tos` | ¿Hace cuánto tiempo tiene tos? | `tiempo_evolucion` | Hace años; tos de fumador le dicen. | | |
| CASO-TBC | `tbc_duracion_tos` | ¿Cuánto tiempo lleva con tos? | `tiempo_evolucion` | Como tres semanas; antes era seca y ahora sale con flema. | | |
| CASO-SCA | `sca_sintomas_asociados` | ¿Ha tenido náuseas o sudoración? | `sintomas_asociados` | Sí, estoy sudando frío y con ganas de vomitar. | | |

**Propuestos y NO aplicados** (hay duda razonable; si el revisor los aprueba se agregan a `intencionesQueSatisface` de la pregunta):

| Caso | Pregunta clave | Intención | Por qué no se aplicó | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| CASO-SCA | `sca_relacion_reposo` | `alivia` | La pregunta es solo por el reposo; "qué lo alivia" es más amplia. | | |
| CASO-EPA | `epa_ortopnea` | `empeora` | La pregunta es solo por acostarse; "qué lo empeora" es más amplia. | | |
| CASO-BRONQ | `bronq_tabaquismo` | `habitos` | "Hábitos" incluye alcohol, ejercicio, etc.; la pregunta es solo por fumar. | | |
| CASO-EPOC | `epoc_tabaquismo` | `habitos` | Igual que arriba. | | |
| CASO-SCA | `sca_antecedente_cardiaco` | `antecedentes` | La pregunta es solo por el corazón; "antecedentes" abarca más. | | |
| CASO-EPA | `epa_cardiopatia` | `antecedentes` | Igual que arriba. | | |
| CASO-NAC | `nac_comorbilidad` | `antecedentes` | "Enfermedad crónica" es un subconjunto de antecedentes. | | |
| CASO-EPOC | `epoc_exacerbaciones` | `antecedentes` | "Episodios similares" es un subconjunto de antecedentes. | | |
| CASO-SCA | `sca_irradiacion` | `localizacion` | La pregunta es por la irradiación, no por dónde duele. | | |

## 4. Conjuntos agregados a las preguntas clave (cobertura de vocabulario)

"dol" no es subcadena de "duele/duelen", así que ¿"Le duele el pecho al toser?" no coincidía con su propia pregunta clave. A cada conjunto con "dol" se le agregó un gemelo con "duel" (se mantiene el original), más dos conjuntos para la pregunta de ortopnea del caso EPA. Solo AGREGAN formas de decir lo mismo: no cambia ninguna pregunta clave, su respuesta ni el puntaje máximo.

| Caso | Pregunta clave | Conjunto agregado | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|
| CASO-BRONQ | `bronq_dolor_toracico` | {duel,pecho} | | |
| CASO-EPA | `epa_ortopnea` | {acost,dificult} | | |
| CASO-EPA | `epa_ortopnea` | {acost,empeor} | | |
| CASO-NAC | `nac_dolor_pleuritico` | {duel,pecho} | | |
| CASO-SARS | `sars_mialgias` | {duel,cuerpo} | | |
| CASO-SARS | `sars_cefalea` | {duel,cabeza} | | |
| CASO-SCA | `sca_inicio_dolor` | {cuando,duel} | | |
| CASO-SCA | `sca_inicio_dolor` | {empez,duel} | | |
| CASO-SCA | `sca_caracteristica_dolor` | {como,duel} | | |
| CASO-SCA | `sca_irradiacion` | {duel,irradia} | | |
| CASO-SCA | `sca_irradiacion` | {duel,corre} | | |
| CASO-SCA | `sca_irradiacion` | {duel,brazo} | | |
| CASO-SCA | `sca_relacion_reposo` | {duel,repos} | | |

## 5. Respuestas ya existentes que se modificaron (control anti-fuga)

El control anti-fuga encontró estas respuestas de preguntas clave que nombraban el diagnóstico del caso. Se reescribieron sin la palabra, conservando lo que el paciente sabe. **Requieren revisión clínica**: la versión nueva es menos específica.

| Caso | Pregunta clave | Antes | Ahora | Validación (OK / Cambiar) | Comentario |
|---|---|---|---|---|---|
| CASO-EPA | `epa_cardiopatia` | Sí, tengo insuficiencia cardíaca. | Sí, ya me habían dicho que tengo problemas del corazón. | | |
| CASO-SCA | `sca_antecedente_cardiaco` | Tuve un infarto hace dos años. | Tuve un problema del corazón hace dos años. | | |
| CASO-TBC | `tbc_contacto` | Mi vecino tuvo tuberculosis hace unos meses. | Mi vecino estuvo enfermo hace unos meses. | | |

## 6. Observaciones para el revisor

- **Control anti-fuga** (palabras del propio diagnóstico en historia, respuestas y catálogo): 0 coincidencia(s). Detalle en `docs/antifuga_historia_paciente.txt`.
- Las únicas respuestas existentes que se modificaron son las de la sección 5 (las que nombraban el diagnóstico).
- Concordancia de género en respuestas ya existentes: varias respuestas usan el masculino con pacientes de sexo femenino (p. ej. BQ "afiebrado"/"resfriado" con una paciente, EPA "sentado"/"acostado" con una paciente). La historia nueva evita la marca de género o usa el género de la ficha.
- Preguntas sin reconocer: con `registrarSinReconocer` activo, el GestorCasoClinico anota cada pregunta no reconocida en `anamnesis_sin_reconocer.csv` (Application.persistentDataPath) para ampliar el vocabulario.
