using AutoPlannerHelpers.Context;
using AutoPlannerHelpers.Enums;
using AutoPlannerHelpers.Models;
using System.Linq;
using System.Windows;

namespace AutoPlannerHelpers.Helpers
{
    /// <summary>
    /// The original code silently returned (nothing happened) when the CT export could not run.
    /// This helper explains to the user WHY the export can't proceed.
    /// NOTE: the "Export CT" tab is OPTIONAL. It sends the CT to an external auto-contouring server and waits for the RTSTRUCT.
    /// It does NOT import a CT into Eclipse and is NOT needed to start planning.
    /// </summary>
    public static class CTExportCheckHelper
    {
        private const string optionalNote = "\n\nNOTA: la pestaña 'Export CT / Import Structure Set' es OPCIONAL (envía el CT a un servidor externo de auto-contorno). " +
                                            "No es necesaria para planificar: si el CT y las estructuras ya están en Eclipse, empiece directamente en 'Set Targets'.\n" +
                                            "NOTE: this tab is OPTIONAL (external auto-contouring). Start directly at 'Set Targets'.";

        /// <summary>
        /// Returns true if the export can proceed. Otherwise shows a message to the user and returns false
        /// </summary>
        public static bool CanExport(ExportCTModel selectedImage, ImportExportDataModel data)
        {
            if (!EclipseContext.GetInstance().IsInitialized || ReferenceEquals(EclipseContext.GetInstance().Patient, null))
            {
                MessageBox.Show("No hay paciente abierto / no patient open.\n\nEjecute el programa desde Eclipse (Tools > Scripts > LaunchAutoPlanner.cs) con el paciente y el CT abiertos." + optionalNote);
                return false;
            }
            if (!EclipseContext.GetInstance().CTImages.Any())
            {
                MessageBox.Show("El paciente no tiene imágenes CT en Eclipse / patient has no CT images.\n\nImporte primero el CT a Eclipse/ARIA con la herramienta normal de importación DICOM de Eclipse." + optionalNote);
                return false;
            }
            if (ReferenceEquals(selectedImage, null))
            {
                MessageBox.Show("Marque la casilla del CT que desea exportar / tick the CT to export." + optionalNote);
                return false;
            }
            if (!EclipseContext.GetInstance().CTImages.Any(x => string.Equals(x.Id, selectedImage.CTId)))
            {
                MessageBox.Show($"No se encontró el CT '{selectedImage.CTId}' / CT not found." + optionalNote);
                return false;
            }
            if (string.IsNullOrEmpty(data.WriteLocation))
            {
                MessageBox.Show("La exportación de CT no está configurada / CT export not configured.\n\nDescomente y configure 'img export location' (y para DICOM: 'img export format=dcm', los daemons y 'RTStruct import location') en el archivo configuration\\VMAT_xxx_config.ini." + optionalNote);
                return false;
            }
            if (data.ExportFormat == ImgExportFormat.DICOM && (!data.AriaDBDaemon.IsInitialized || !data.LocalDaemon.IsInitialized))
            {
                MessageBox.Show("Exportación DICOM seleccionada pero los daemons de ARIA no están configurados / DICOM daemons not configured.\n\nConfigure 'Aria DB daemon', 'VMS File daemon' y 'Local daemon' en el archivo .ini." + optionalNote);
                return false;
            }
            return true;
        }
    }
}
