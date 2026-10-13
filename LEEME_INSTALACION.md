# VMAT TBI / CSI / TMLI Auto-Planner: guía de instalación para Eclipse 16.1

Versión adaptada del auto-planificador de Stanford (E. Simiele et al.) para usarse en un servicio con
**Eclipse/ESAPI 16.1** y con Windows configurado en **español (Colombia)**.
El detalle de lo que se cambió en el código está en `CAMBIOS_REALIZADOS.md`.

> ⚠️ **Uso clínico.** Este software crea cursos, planes, campos y estructuras en ARIA/Eclipse. Antes de
> aprobarlo en el sistema clínico debe probarse en un **sistema no clínico (t-box / base de datos de
> pruebas)** y comisionarse según los procedimientos de su servicio. Las plantillas de dosis y
> restricciones son las de Stanford: un físico médico y el radioterapeuta deben revisarlas y adaptarlas.

---

## 1. Requisitos

| Elemento | Detalle |
|---|---|
| Visual Studio | 2019 o 2022 con la carga de trabajo **“Desarrollo de escritorio de .NET”** |
| .NET Framework | Paquete de destino (targeting pack) **4.7.2** |
| Internet / NuGet | Para restaurar los paquetes: CommunityToolkit.Mvvm, Microsoft.Xaml.Behaviors.Wpf y EvilDICOM |
| DLL de ESAPI 16.1 | `VMS.TPS.Common.Model.API.dll` y `VMS.TPS.Common.Model.Types.dll` |

Las DLL de ESAPI están en cualquier estación de Eclipse 16.1, en:
`C:\Program Files (x86)\Varian\RTM\16.1\esapi\API\`

- **Si compila en una estación con Eclipse 16.1**, la solución encuentra las DLL automáticamente.
- **Si compila en otro PC**, copie las dos DLL en
  `VMATTBICSITMLIAutoPlanner\Resources\16.1\`.

(La lógica de búsqueda está en `VMATTBICSITMLIAutoPlanner\Directory.Build.props`. Si las DLL no
aparecen, la compilación se detiene y muestra un mensaje que explica qué hacer.)

## 2. Compilar

1. Abra **`VMATTBICSITMLIAutoPlanner\VMATTBICSITMLIAutoPlanner.ci.slnf`** en Visual Studio.
   Este archivo excluye los proyectos de pruebas, que requieren una licencia comercial de JustMock.
   También puede abrir el `.sln` e ignorar los errores de los proyectos `*Tests`.
2. Seleccione la configuración **Release** y la plataforma **x64**.
3. Ejecute **Compilar → Recompilar solución**. Si NuGet no restaura los paquetes, use clic derecho
   sobre la solución → *Restaurar paquetes NuGet*.
4. El resultado queda en la carpeta `bin\` de la raíz del repositorio:

```
bin\
 ├─ LaunchAutoPlanner.cs          ← script que se ejecuta desde Eclipse
 └─ VMAT-TBI-CSI-TMLI\
     ├─ AutoPlannerLauncher.exe, CSIAutoPlanner.exe, TBIAutoPlanner.exe, TMLIAutoPlanner.exe,
     │  AutoPlannerOptimizationLoop.exe, ImportListener.exe, AutoPlannerHelpers.dll …
     ├─ configuration\             ← archivos .ini de configuración
     ├─ templates\CSI | TBI | TMLI ← plantillas de plan
     ├─ documentation\             ← guías PDF (botones de ayuda)
     └─ logs\                      ← registros. La carpeta necesita PERMISO DE ESCRITURA
```

## 3. Configurar el servicio (OBLIGATORIO)

Edite los archivos de `bin\VMAT-TBI-CSI-TMLI\configuration\`. Si los edita en las carpetas
`Configuration` del código fuente, los cambios se copian solos en cada compilación.

- `VMAT_CSI_config.ini`
- `VMAT_TBI_config.ini`
- `VMAT_TMLI_config.ini`

Reglas de estos archivos:

- Las líneas que empiezan con `%` son comentarios.
- No deje líneas vacías.
- Use **punto** como separador decimal: `390.0`, no `390,0`.

| Parámetro | Qué poner |
|---|---|
| `add linac{CAMBIAR_ID_LINAC}` | El **Machine ID exacto** del acelerador en Eclipse. Use una línea por equipo. |
| `add beam energy{6X}` | Las energías disponibles en ese equipo (6X, 10X, 6FFF…), tal como se llaman en Eclipse |
| `calculation model=` | Nombre exacto del modelo de cálculo (AAA/Acuros). **Si lo deja vacío**, el programa muestra la lista de modelos de su Eclipse para escoger uno. |
| `optimization model=` | Nombre exacto del optimizador VMAT (PO). Si lo deja vacío, también muestra la lista. |
| `course Id=` | ID del curso que se creará (por ejemplo, `VMAT CSI`) |
| `MR level restart=` | Nivel de resolución de reinicio del optimizador (por defecto `MR3`) |
| `beams per iso`, `collimator rotations`, `max Y-jaw field extent`, `minimum field overlap`, posiciones de mandíbulas (TBI/TMLI) | Valores de la técnica de Stanford. Revíselos con su física. |
| `img export location`, `RTStruct import location` y los daemons | **Opcional.** Solo sirven para exportar el CT o importar el RTSTRUCT de forma automática (auto-segmentación externa). Están comentados; déjelos así si no los usa. |

En `AutoPlannerOptimizationLoop` se usan además `CSI_optimization_config.ini`, `TBI_optimization_config.ini`
y `TMLI_optimization_config.ini`. Ahí se configura el número de optimizaciones, la normalización
(PTV V100% = 95%), el umbral de decisión y los recordatorios.

### Nombres de estructuras en las plantillas

Las plantillas (`templates\CSI|TBI|TMLI\*.ini`) usan los nombres de estructura de Stanford: `Brain`,
`SpinalCord`, `BrainStem`, `OpticChiasm`, `OpticNrvs`, `Cochleas`, `Lenses`, `Eyes`, `Kidneys`, `Lungs`,
`Body`, etc. Hay dos opciones:

- contornear con **esos mismos nombres**, o
- cambiar los nombres en las plantillas para que coincidan con la nomenclatura de su servicio.

Las dosis de las plantillas están en **cGy**. Revise las prescripciones (por ejemplo, CSI 3600 cGy + boost
1800 cGy) y las restricciones de optimización antes de usarlas en clínica.

## 4. Instalar en Eclipse

1. Copie toda la carpeta `bin\` a una ubicación de red que vean Citrix y las estaciones de Eclipse,
   o al escritorio de una estación (*thick client*).
2. Dé **permiso de escritura** a los usuarios sobre `bin\VMAT-TBI-CSI-TMLI\logs\`. El ciclo de
   optimización lee los registros que deja la preparación del plan.
3. **Aprobación de scripts** (RT Administration → Script Approvals). En el sistema clínico Eclipse 16.1 solo
   permite ejecutar scripts con escritura que estén aprobados. Apruebe:
   `CSIAutoPlanner.exe`, `TBIAutoPlanner.exe`, `TMLIAutoPlanner.exe`, `AutoPlannerOptimizationLoop.exe`,
   `AutoPlannerHelpers.dll` e `ImportListener.exe` (este último solo si usa la importación DICOM).
   En un sistema no clínico (t-box) puede probarlos sin aprobación.

## 5. Uso

1. En Eclipse (External Beam Planning), abra el paciente y el **structure set**.
2. Vaya a **Tools → Scripts**, use **Change Folder…** para elegir la carpeta `bin\`, seleccione
   **`LaunchAutoPlanner.cs`** y pulse **Run**.
3. Escoja **VMAT CSI**, **VMAT TBI** o **VMAT TMLI**. El flujo sigue las pestañas en orden:
   *Set Targets / Derive Targets → Structure Tuning (estructuras de optimización, anillos, recortes) →
   Beam Placement (escoja el acelerador y la energía) → Optimization Setup*.
4. Para optimizar, abra el plan creado y vuelva a ejecutar `LaunchAutoPlanner.cs`. Con un plan abierto
   aparece el botón **Launch Optimization Loop**.
5. Al final use **Plan Preparation** en el planificador (separar planes por isocentro, nota de
   desplazamientos y recálculo).

Los botones de ayuda abren las guías PDF originales (en inglés), que están en `documentation\`.

**Auto-contorneo (Export CT / Import Structure Set):** esta pestaña de Stanford enviaba el CT a un servidor externo de
auto-contorneo. En esta versión está **oculta** porque no se usa: el CT se importa a Eclipse como siempre y las
estructuras se contornean en Eclipse. Para reactivarla, quite `Visibility="Collapsed"` en `CSIMainView.xaml` / `TMLIMainView.xaml`.

## 6. Validación mínima recomendada antes del uso clínico

- [ ] Compilar contra las DLL de **16.1** (no contra las de `Resources\15.6`).
- [ ] Ejecutar el flujo completo en un paciente de prueba en la t-box, para cada técnica que vaya a usar.
- [ ] Revisar los isocentros, ángulos de colimador, mandíbulas, arcos, máquina, energía y tasa de dosis de los campos creados.
  - Los arcos VMAT se crean con **600 UM/min**.
  - En TBI y TMLI los campos AP/PA de piernas usan siempre **6X a 600 UM/min** (valor fijo en el código).
- [ ] Revisar la dosis calculada, la normalización y la verificación independiente de UM (segundo cálculo).
- [ ] Confirmar que los registros se escriben en `logs\` y que el ciclo de optimización los encuentra.
- [ ] Documentar el comisionamiento según el programa de garantía de calidad del servicio.

## 7. Problemas frecuentes

| Síntoma | Causa / solución |
|---|---|
| Error de compilación “No se encontraron las DLL de ESAPI 16.1” | Copie las DLL en `Resources\16.1\` (ver la sección 1). |
| Errores de NuGet al compilar | El PC no tiene acceso a nuget.org. Restaure los paquetes desde una red con Internet. |
| “Machine … not found” o un error al crear los campos | El ID de `add linac{…}` no coincide con el Machine ID de Eclipse. |
| Ventana “No valid calculation model set!” | Es normal si el modelo quedó vacío o mal escrito: escoja el modelo en la lista. Para no volver a ver la ventana, escriba ese nombre en el `.ini`. |
| El script no encuentra `AutoPlannerLauncher.exe` | El `.cs` debe estar en `bin\` y los ejecutables en `bin\VMAT-TBI-CSI-TMLI\`. |
| “Can't add a treatment course” o el plan ya existe | ESAPI no borra planes en un sistema clínico: elimine o renombre manualmente el plan o curso anterior. |
