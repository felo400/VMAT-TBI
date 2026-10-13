# Cambios realizados respecto al código original (Stanford, ESAPI 15.6)

## Errores corregidos

### 1. Separador decimal (CRÍTICO en Windows en español)
El programa leía todos los números de los `.ini`, de las plantillas y de los registros con
`double.Parse(...)` sin indicar cultura. Con Windows en **Español (Colombia)** el punto es separador de miles,
así que los valores se leían mal:

| Valor en el `.ini` | Leído en es-CO | Valor correcto |
|---|---|---|
| `collimator rotations={3.0,357.0,…}` | 30°, 3570° | 3°, 357° |
| `max Y-jaw field extent=390.0` | 3900 mm | 390 mm |
| `contour field overlap margin=1.0` | 10 cm | 1 cm |
| `default flash margin=0.5` | 5 cm | 0.5 cm |

El mismo problema rompía los registros que el planificador escribe y que el ciclo de optimización
lee después (por ejemplo, `3600,0` dentro de listas separadas por comas).
**Solución:** todos los ejecutables fuerzan la cultura invariante al arrancar.
Archivos: `AutoPlannerHelpers/Helpers/CultureHelper.cs` y los `App.xaml.cs` de cada programa, más
`ImportListener.cs` y `AutoPlannerLauncher/App.xaml.cs`.

### 2. Argumentos con espacios entre Eclipse y los ejecutables
Ni `LaunchAutoPlanner.cs` ni `AutoPlannerLauncher` ponían comillas a los argumentos. Por eso un curso
llamado `VMAT CSI` (el ID por defecto) llegaba partido como `VMAT` + `CSI`, y lo mismo pasaba con MRN o
rutas de red con espacios.
**Solución:** se añadió `QuoteArgument` (reglas de Windows) en el script de Eclipse, en el lanzador y en el
lanzamiento de `ImportListener`.

### 3. ImportListener: validación de argumentos
El programa comprobaba que hubiera al menos 7 argumentos, pero leía 9. Se corrigió la comprobación.

### 4. Modelos de cálculo y optimización
- Los valores por defecto eran `AAA_15605` y `PO_15605`, que no existen en un Eclipse 16.1.
- Un modelo de optimización vacío o inválido producía una excepción al crear el plan.

**Solución:** el programa comprueba los nombres contra los modelos instalados en Eclipse. Si el nombre
está vacío o no existe, pide escoger uno de la lista. Esto aplica al modelo de cálculo y al de
optimización VMAT. Archivo: `AutoPlannerHelpers/BaseCore/GeneratePlansAndPlaceBeamsBase.cs`.

### 5. Botones de ayuda
Los botones de ayuda solo mostraban un mensaje `"test"`. Ahora abren las guías PDF de `documentation\`
(`AutoPlannerHelpers/Helpers/DocumentationHelper.cs`).

### 6. Pestaña de auto-contorneo
La pestaña Export CT / Import Structure Set (auto-contorneo externo de Stanford) no hacía nada cuando faltaba la configuración. Ahora:
- está oculta en CSI y TMLI, y el programa abre directamente en *Specify Targets*;
- si se reactiva, explica con un mensaje por qué no puede exportar (`CTExportCheckHelper.cs`).

## Adaptación a Eclipse 16.1
- Las referencias a ESAPI ya no apuntan a una ruta fija 15.6: usan `$(ESAPIDir)`, definido en
  `VMATTBICSITMLIAutoPlanner/Directory.Build.props`.
  - Por defecto busca 16.1 en la instalación de Eclipse o en `Resources\16.1\`.
  - Si no encuentra las DLL, la compilación se detiene con un mensaje claro.
  - Las referencias usan `Private=False`, como recomienda Varian: Eclipse aporta las DLL en tiempo de ejecución.
- Los flujos de GitHub (CI) siguen compilando con 15.6 mediante `/p:ESAPIVersion=15.6`.

## Configuración
- Se quitaron de los `.ini` los servidores, IPs y aceleradores de Stanford (`LA16`, `LA17`, `LA20`).
  En su lugar hay marcadores (`CAMBIAR_ID_LINAC`) e instrucciones en español.
- Los modelos de cálculo y optimización quedan vacíos: el programa los pide la primera vez.
- La exportación e importación DICOM queda comentada porque es opcional.

## Lo que NO se modificó (requiere decisión clínica local)
- Las plantillas de plan: dosis, restricciones, nombres de estructuras y márgenes.
- La geometría de la técnica de Stanford: campos por isocentro, colimadores, mandíbulas y solapamientos.
- La tasa de dosis de 600 UM/min y la energía 6X fija de los campos AP/PA de piernas (TBI/TMLI).

El archivo `cambios.patch`, entregado junto con el paquete, contiene el diff completo.
