# Proyecto ER simple

Aplicación ASP.NET Core que restaura un `.bak` de SQL Server para generar diagramas ER (Chen) y relacional con Mermaid.

## Requisitos
- SQL Server accesible.
- Permisos de lectura para `NT SERVICE\MSSQLSERVER` sobre la carpeta indicada en `Upload:Carpeta`.

## Configuración
Ejemplo de `appsettings.json`:
```json
{
  "Upload": { "Carpeta": "App_Data/Uploads" },
  "ConnectionStrings": {
    "SqlMaestra": "Server=localhost;Database=master;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

## Cómo correr
1. Restaurar dependencias y compilar:
   ```bash
   dotnet run
   ```
2. Abrir `https://localhost:5001`.

## Flujo de uso
1. Ir a `/DiagramaEr/Subir`.
2. Subir el archivo `.bak`.
3. Ver diagrama ER (Chen) y, si aparece, resolver ambigüedades EER.
4. Ir a `Modelo Relacional` para ver `erDiagram`.
5. Eliminar la BD restaurada desde la interfaz.

## Estructura de carpetas
```
Controllers/
  DiagramaErController.cs
  RelacionalController.cs
Servicios/
  ServicioRestauracionSql.cs
  LectorEsquemaSql.cs
  EERChoicesService.cs
Modelos/
  InfoTabla.cs
  InfoColumna.cs
  InfoLlaveForanea.cs
  InstantaneaEsquema.cs
Renderizadores/
  ConstructorDiagramaChen.cs
  ConstructorDiagramaRelacional.cs
Utilidades/
  TextoMermaid.cs
Views/
  DiagramaER/
    Subir.cshtml
    Resultado.cshtml
  Relacional/
    Index.cshtml
```
