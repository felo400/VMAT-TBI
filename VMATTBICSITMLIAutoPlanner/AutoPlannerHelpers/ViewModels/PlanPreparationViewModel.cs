using AutoPlannerHelpers.Context;
using AutoPlannerHelpers.Enums;
using AutoPlannerHelpers.Logging;
using AutoPlannerHelpers.Messengers;
using AutoPlannerHelpers.Prompts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using VMS.TPS.Common.Model.API;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;

namespace AutoPlannerHelpers.ViewModels
{
    public class PlanPreparationViewModel : ObservableObject
    {
        #region properties
        private string _generateShiftText;

        public string GenerateShiftText
        {
            get { return _generateShiftText; }
            set { SetProperty(ref _generateShiftText, value); }
        }

        private string _separatePlanText;

        public string SeparatePlanText
        {
            get { return _separatePlanText; }
            set { SetProperty(ref _separatePlanText, value); }
        }

        private string  _planId;

        public string  PlanId
        {
            get { return _planId; }
            set { SetProperty(ref _planId, value); }
        }

        private Visibility _doseCalculationVisibility;

        public Visibility DoseCalculationVisibility
        {
            get { return _doseCalculationVisibility; }
            set { SetProperty(ref _doseCalculationVisibility, value); }
        }

        private string _calculateDoseText;

        public string CalculateDoseText
        {
            get { return _calculateDoseText; }
            set { SetProperty(ref _calculateDoseText, value); }
        }

        #endregion

        #region fields
        private bool _canSeparatePlans = false;
        #endregion

        #region commands
        public ICommand GenerateShiftNoteCommand { get; set; }
        public ICommand SeparatePlansCommand { get; set; }
        public ICommand CalculateDoseCommand { get; set; }
        #endregion

        public PlanPreparationViewModel()
        {
            GenerateShiftText = "NO";
            SeparatePlanText = "NO";
            if (EclipseContext.GetInstance().IsInitialized && EclipseContext.GetInstance().VMATPlans.Any())
            {
                PlanId = EclipseContext.GetInstance().VMATPlans.First().Id;
            }
            GenerateShiftNoteCommand = new RelayCommand(GenerateShiftNote);
            SeparatePlansCommand = new RelayCommand(SeparatePlans);
            CalculateDoseCommand = new RelayCommand(CalculateDose);
            DoseCalculationVisibility = Visibility.Collapsed;
        }

        /// <summary>
        /// Asegura que el plan de trabajo sea el plan VMAT (con arcos) creado por el autoplanificador y no, por ejemplo,
        /// el plan que estaba abierto en Eclipse al iniciar el script
        /// </summary>
        private bool EnsureVMATPlan()
        {
            if (!EclipseContext.GetInstance().IsInitialized) return false;
            ExternalPlanSetup current = EclipseContext.GetInstance().VMATPlans.FirstOrDefault();
            if (Helpers.PlanPrepHelper.HasVMATArcs(current))
            {
                PlanId = current.Id;
                return true;
            }
            List<ExternalPlanSetup> candidates = Helpers.PlanPrepHelper.GetCandidateVMATPlans();
            if (!candidates.Any())
            {
                MessageBox.Show("No encontré en este paciente ningún plan con arcos VMAT.\n\n" +
                    "La Preparación del plan trabaja sobre el plan que crea el autoplanificador en la pestaña 'Campos'.\n" +
                    "Posibles causas:\n" +
                    " 1) La pestaña 'Campos' no terminó (revise si salió un error en la ventana de progreso).\n" +
                    " 2) Los cambios no se GUARDARON en Eclipse después de cerrar el script (un plugin no guarda solo).\n\n" +
                    "Solución: ejecute 'Campos' (y la optimización), cierre el script, pulse GUARDAR en Eclipse y vuelva a abrir el script.",
                    "No hay plan VMAT");
                return false;
            }
            ExternalPlanSetup chosen = candidates.First();
            if (candidates.Count > 1)
            {
                string list = string.Join("\n", candidates.Take(8).Select(p => $" - {p.Id} (curso {p.Course.Id})"));
                MessageBoxResult r = MessageBox.Show($"Planes con arcos VMAT encontrados:\n{list}\n\nSe usará el más reciente: {chosen.Id} (curso {chosen.Course.Id}).\n¿Es correcto?\n\n(Si no, pulse No, abra en Eclipse el plan correcto y vuelva a ejecutar el script.)",
                    "Plan para preparar", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r != MessageBoxResult.Yes) return false;
            }
            EclipseContext.GetInstance().VMATPlans = new List<ExternalPlanSetup> { chosen };
            Logger.GetInstance().PlanUIDs = new List<string> { chosen.UID };
            PlanId = chosen.Id;
            return true;
        }

        private void GenerateShiftNote()
        {
            if (!EclipseContext.GetInstance().IsInitialized)
            {
                Logger.GetInstance().LogError("Error! Script is not connected to aria! Cannot perform preparation for treatment!");
                return;
            }
            if (!EnsureVMATPlan()) return;
            Logger.GetInstance().OpType = ScriptOperationType.PlanPrep;

            //logic needs to be handled by specific plan type classes
            var result = WeakReferenceMessenger.Default.Send(new RequestGenerateShiftNoteMessage());
            if (!result)
            {
                

                GenerateShiftText = "SÍ";
                _canSeparatePlans = true;
            }
        }

        private void SeparatePlans()
        {
            if (!_canSeparatePlans)
            {
                MessageBox.Show("Primero genere la nota de desplazamientos y después separe los planes.", "Orden de pasos");
                return;
            }
            if (!EnsureVMATPlan()) return;
            if (!EclipseContext.GetInstance().VMATPlans.FirstOrDefault().Beams.Any(x => x.IsSetupField))
            {
                ConfirmPrompt CUI = new ConfirmPrompt($"El plan {EclipseContext.GetInstance().VMATPlans.FirstOrDefault().Id} no tiene campos de setup (CBCT/kV)." + Environment.NewLine + Environment.NewLine + "¿Desea continuar de todos modos?");
                CUI.ShowDialog();
                if (!CUI.GetSelection()) return;
            }

            var result = WeakReferenceMessenger.Default.Send(new RequestSeparatePlanMessage());
            if (!result)
            {
                //inform the user it's done
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("¡Planes separados por isocentro!");
                sb.AppendLine("Revise en cada plan el volumen blanco y el punto de referencia primario.");
                if (EclipseContext.GetInstance().VMATPlans.FirstOrDefault().Beams.Any(x => x.IsSetupField))
                {
                    sb.AppendLine("Revise también el isocentro de los campos de setup.");
                }
                MessageBox.Show(sb.ToString());

                SeparatePlanText = "SÍ";
                bool doseRecalcNeeded = WeakReferenceMessenger.Default.Send(new RequestDoSeparatedPlansRequireDoseRecalculation());
                if(WeakReferenceMessenger.Default.Send(new RequestAreSeparatedPlansAutomaticallyRecalculated()) && WeakReferenceMessenger.Default.Send(new RequestDoSeparatedPlansRequireDoseRecalculation()))
                {
                    DoseCalculationVisibility = Visibility.Visible;
                }
            }
        }

        private void CalculateDose()
        {
            //ask the user if they are sure they want to do this. Each plan will calculate dose sequentially, which will take time
            ConfirmPrompt CUI = new ConfirmPrompt("Atención: cada plan se calcula uno tras otro y puede tardar." + Environment.NewLine + "¿Continuar?");
            CUI.ShowDialog();
            if (!CUI.GetSelection()) return;

            bool recalculationFailed = WeakReferenceMessenger.Default.Send(new RequestRecalculateDoseForSeparatedPlans());
            if(!recalculationFailed)
            {
                //let the user know this step has been completed
                CalculateDoseText = "SÍ";
            }
        }
    }
}
