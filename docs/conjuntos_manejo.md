# Conjuntos de palabras clave — acciones de Manejo

> **Borrador pendiente de validación clínica.** Generado desde `Assets/Editor/ConfigurarConjuntosManejo.cs`; este archivo es el registro porque Unity pierde los comentarios YAML al re-serializar los .asset.

Reglas: `{a, b}` es UN conjunto (AND: todas las palabras deben aparecer). Los conjuntos de una acción son alternativas (OR). Coincidencia por substring, tras normalizar (minúsculas y sin tildes) el texto del jugador y las palabras de la ficha.

| Caso | idReferencia | Descripción | Conjuntos | Frase de prueba |
|---|---|---|---|---|
| CASO-SCA | `manejo_ecg_monitor_via` | ECG de 12 derivaciones dentro de los primeros 10 minutos, monitorización continua y vía venosa permeable | {ecg, monitor} {ecg, venos} {ecg, derivaciones} {electrocardiograma, monitor} {electrocardiograma, venos} {electrocardiograma, derivaciones} | «Tomar ECG de 12 derivaciones y monitorizar» |
| CASO-SCA | `manejo_activar_reperfusion` | Activar el protocolo de reperfusión / traslado oportuno | {reperfusion} {trombolis} {trombolit} {fibrinolis} {angioplast} {hemodinamia} {cateteris} | «Activar reperfusión y trasladar a hemodinamia» |
| CASO-TBC | `manejo_aislamiento_respiratorio` | Instaurar aislamiento respiratorio apenas se sospecha | {aisl} {n95} {presion, negativa} | «Aislamiento respiratorio y mascarilla N95» |
| CASO-TBC | `reconocer_notificacion_obligatoria` | Reconocer que es una enfermedad de notificación obligatoria | {notific, obligator} {notific, autoridad} {notific, seremi} {notific, epidemiolog} {declaracion, obligatoria} | «Notificación obligatoria a la SEREMI» |
| CASO-EPA | `manejo_oxigeno_fowler` | Oxígeno suplementario inmediato y posición semisentada / Fowler alta | {oxigen, fowler} {oxigen, semisent} {oxigen, sentad} {oxigen, sentar} {oxigen, incorpor} {oxigen, cabecera} {o2, fowler} {o2, semisent} {o2, sentad} {o2, sentar} {o2, incorpor} {o2, cabecera} | «Oxígeno y posición semisentada» |
| CASO-EPA | `reconocer_causa_cardiogenica` | Reconocer signos de sobrecarga de volumen (ingurgitación yugular, edema) como causa cardiogénica | {cardiogenic} {insuficiencia, cardiac} {sobrecarga, volumen} {diuretic} {furosemida} {nitrato} {nitroglicerina} {restriccion, liquidos} | «Diurético (furosemida) por sobrecarga de volumen» |
| CASO-EPOC | `manejo_o2_titulado` | Valorar patrón respiratorio y titular O2 con cautela (evitar hiperoxia en retenedores de CO2) | {oxigen, titul} {oxigen, cautela} {oxigen, controlad} {oxigen, venturi} {o2, titul} {o2, cautela} {o2, controlad} {o2, venturi} {venturi} {88, 92} | «Oxígeno titulado, saturación 88-92» |
| CASO-EPOC | `manejo_broncodilatadores` | Reconocer necesidad de broncodilatadores y valorar fatiga respiratoria | {broncodilat} {salbutamol} {ipratropio} {nebuliz} | «Broncodilatadores nebulizados» |
| CASO-NAC | `manejo_semifowler_o2` | Posicionar en semifowler e iniciar oxigenoterapia si hay hipoxemia | {oxigen, fowler} {oxigen, semisent} {oxigen, sentad} {oxigen, sentar} {oxigen, incorpor} {oxigen, cabecera} {o2, fowler} {o2, semisent} {o2, sentad} {o2, sentar} {o2, incorpor} {o2, cabecera} | «Oxígeno y semifowler» |
| CASO-NAC | `manejo_reconocer_gravedad` | Reconocer signos de gravedad (confusión, hipotensión, taquipnea severa) | {gravedad} {curb} {hospitaliz} {cuidados, intensiv} {unidad, critic} | «Evaluar gravedad con CURB-65 y hospitalizar» |
| CASO-SARS | `manejo_aislamiento_monitor_sato2` | Aislamiento / precauciones apenas se sospecha, con monitorización continua de SatO2 | {aisl, saturacion} {aisl, sato2} {aisl, spo2} {aisl, saturometr} {aisl, oximetr} {aisl, monitor} | «Aislamiento y monitorización continua de SatO2» |
| CASO-SARS | `manejo_escalonado_via_aerea` | Preparar manejo escalonado (oxigenoterapia y eventual intubación) | {intub} {ventilacion, mecanic} {vmni} {via, aerea} {escalonad} {alto, flujo} | «Oxígeno y preparar intubación si empeora» |
| CASO-BRONQ | `manejo_descartar_neumonia` | Diferenciar de neumonía descartando consolidación o signos de gravedad | {neumon} {radiografia} {consolidacion} | «Descartar neumonía con radiografía de tórax» |
| CASO-BRONQ | `manejo_educacion_alarma` | Entregar indicaciones de automanejo y señales de alarma para reconsultar | {alarma} {reconsult} {automanejo} {educ, paciente} {volver, consultar} | «Educar al paciente sobre signos de alarma y reconsultar» |

Falsos positivos conocidos: `o2` aparece dentro de «co2» y `gravedad` dentro de «sin gravedad» (el matching es por substring, sin negación).
