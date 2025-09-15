# Documentación funcional del proyecto

Este proyecto ASP.NET Core automatiza la restauración temporal de bases de datos SQL Server y genera artefactos educativos a partir de su esquema: diagramas ER/EER, el modelo relacional textual y un traductor entre álgebra relacional y SQL. A continuación se documenta el funcionamiento de cada componente relevante.

## Restauración de respaldos

* **Clase:** `RestauracionRepositorio` (`Repositories/RestauracionRepositorio.cs`).
* **Responsabilidad:** restaurar archivos `.bak` en una instancia SQL Server usando SQL Server Management Objects (SMO) y eliminarlos cuando dejan de ser necesarios.
* **Pasos clave:**
  1. Se genera un nombre único para la base temporal con un prefijo identificable (`ER_`, `REL_`, etc.).
  2. Se abre la conexión a la base maestra y se crea un `Server` de SMO.
  3. Se leen los nombres lógicos de los archivos de datos y log para reubicarlos en las rutas por defecto del servidor.
  4. Se ejecuta `SqlRestore` dentro de un `Task.Run` debido a que SMO solo ofrece API sincrónica.
  5. Si la restauración falla, se notifica mediante una `InvalidOperationException` para que los controladores muestren el mensaje al usuario.
  6. La eliminación posterior usa `KillDatabase` de SMO para forzar el cierre de conexiones y limpiar la base temporal.

Los controladores `DiagramaErController` y `RelacionalController` guardan primero el archivo subido en disco y delegan en este repositorio la restauración. Tras completar la operación, eliminan el archivo físico temporal.

## Lectura del esquema restaurado

* **Clase:** `EsquemaRepositorio` (`Repositories/EsquemaRepositorio.cs`).
* **Responsabilidad:** construir una instantánea del esquema con tablas, columnas, llaves foráneas y tablas puente (`InstantaneaEsquema`).
* **Proceso:**
  * Recorre todas las tablas de usuario y registra sus columnas, marcando claves primarias, índices únicos y nulabilidad.
  * Analiza cada llave foránea para determinar cardinalidades (si la tabla hija es única o permite nulos) y para identificar tablas puente de relaciones muchos-a-muchos.
  * Los datos recopilados alimentan tanto al generador de diagramas ER como al modelo relacional textual y a la inferencia EER.

## Diagramas ER (Chen)

* **Clase:** `DiagramaChenRepositorio` (`Repositories/DiagramaChenRepositorio.cs`).
* **Responsabilidad:** convertir `InstantaneaEsquema` en instrucciones Mermaid que representan entidades, atributos y relaciones.
* **Detalles relevantes:**
  * Se definen estilos Mermaid para entidades, relaciones y atributos.
  * Cada tabla de usuario se renderiza como nodo entidad con atributos conectados; las PK se subrayan y los atributos únicos se muestran punteados.
  * Las llaves foráneas generan relaciones binarias con multiplicidades calculadas según la unicidad y nulabilidad de la tabla hija.
  * Las tablas puente se dibujan como relaciones muchos-a-muchos con sus atributos propios.

## Inferencia de jerarquías EER

* **Archivo:** `Utils/InferenciaEER.cs` + `Services/EspecializacionEerService.cs` y `Repositories/EspecializacionEerRepositorio.cs`.
* **Objetivo:** detectar jerarquías de especialización (supertipo/subtipos) y determinar si son totales/disjuntas o parciales/solapadas.
* **Mecánica:**
  * `InferenciaEER.DetectarJerarquias` busca claves foráneas únicas que reutilizan exactamente la PK de la tabla hija, indicador de especialización.
  * Se buscan columnas discriminadoras comunes (`Tipo`, `Clase`, etc.) para ajustar la disyunción.
  * `EspecializacionEerService` consulta directamente la base restaurada para verificar si todos los padres tienen hijo (totalidad) y si los subtipos se solapan (intersección).
  * `InferenciaEER.RenderMermaidEER` arma un diagrama Mermaid adicional que muestra cada jerarquía con sus etiquetas.

## Modelo relacional en texto

* **Clase:** `ModeloRelacionalTextoRepositorio` (`Repositories/ModeloRelacionalTextoRepositorio.cs`).
* **Responsabilidad:** generar una lista ordenada de relaciones en notación `Tabla(atributos)` o en HTML enriquecido.
* **Funcionamiento:**
  * Determina qué atributos actúan como claves foráneas y resalta primero las claves primarias y candidatos únicos.
  * Cada atributo recibe etiquetas `[PK]`, `[FK]`, `[UK]` en modo texto o marcado HTML (subrayado/cursiva) en modo visual.
  * El controlador `RelacionalController` muestra esta salida en la vista `ModeloR` y permite eliminar la base temporal al finalizar.

## Traductor Álgebra Relacional ↔ SQL

* **Clase:** `TraductorRepositorio` (`Repositories/TraductorRepositorio.cs`).
* **Responsabilidad:** interpretar un subconjunto común de expresiones en álgebra relacional (selección, proyección, unión, diferencia, join y división) y traducirlas a SQL, y viceversa.
* **Estrategia:**
  * Se definen expresiones regulares específicas para cada operador o patrón SQL.
  * En la dirección AR→SQL se combinan las coincidencias para construir sentencias `SELECT`, `JOIN`, `UNION`, `EXCEPT` y el patrón clásico de división.
  * En la dirección SQL→AR se mapea cada cláusula `SELECT`, `JOIN`, `WHERE`, `UNION` o `EXCEPT` a los operadores σ, π, ⋈, ∪ y −.
  * El `TraductorController` recibe el modo solicitado, invoca al repositorio y retorna el resultado en la misma vista.

## Flujo completo para diagramas ER/EER

1. El usuario accede a `/DiagramaEr/Subir` y sube un archivo `.bak`.
2. `DiagramaErController.Subir` guarda el respaldo temporalmente, lo restaura (`RestauracionRepositorio`), lee el esquema (`EsquemaRepositorio`) y genera los diagramas (`DiagramaChenRepositorio` y `InferenciaEER`).
3. Se calculan las etiquetas de especialización (`EspecializacionEerService`) y se representa la jerarquía con Mermaid.
4. La vista `Resultado` muestra el nombre de la base, el diagrama ER y el EER; también ofrece un botón para eliminar la base restaurada.

## Flujo para modelo relacional textual

1. El usuario visita `/Relacional/Subir` y carga el respaldo.
2. Tras restaurar la base, el controlador redirige a `ModeloR`, que invoca a `ModeloRelacionalTextoRepositorio` para obtener la notación textual.
3. La vista muestra el modelo y un botón para eliminar la base temporal.

## Flujo del traductor

1. Se ingresa a `/Traductor` y se selecciona el modo de traducción (AR→SQL o SQL→AR).
2. `TraductorController.Traducir` procesa el texto con el repositorio correspondiente.
3. El resultado y el modo elegido se devuelven a la vista para permitir refinamientos sucesivos.

## Limpieza y seguridad

* Todos los controladores eliminan el archivo `.bak` temporal al terminar (bloque `finally`).
* Se ofrece una acción explícita para eliminar la base restaurada (`EliminarBase`) a fin de liberar recursos del servidor SQL.
* Los nombres de las bases restauradas usan GUIDs con prefijos para evitar colisiones.

Esta documentación debe servir como guía de referencia rápida para comprender cómo el proyecto restaura respaldos, reconstruye el conocimiento del modelo de datos y ofrece utilidades de apoyo académico.
