using AutoPlannerHelpers.Models;
using System.Collections.Generic;
using System.Windows;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using AutoPlannerHelpers.Messengers;

namespace AutoPlannerHelpers.ViewModels
{
    public class CTExportViewModel : ObservableObject
    {
        public ObservableCollectionPropertyNotify<ExportCTModel> CTImageList { get; set; }

        #region properties
        public ExportCTModel SelectedCTImage { get; private set; } = null;
        #endregion

        #region commands
        public ICommand ExportCTCommand { get; set; }
        public ICommand ShowExportCTInfoCommand { get; set; }
        public ICommand KeyboardTestCommand { get; set; }
        public RelayCommand<ExportCTModel> CTImageSelectionChangedCommand { get; set; }
        #endregion

        public CTExportViewModel()
        {
            CTImageList = new ObservableCollectionPropertyNotify<ExportCTModel> { };
            ShowExportCTInfoCommand = new RelayCommand(ShowExportCTInfo);
            CTImageSelectionChangedCommand = new RelayCommand<ExportCTModel>(CTImageSelectionChanged);
            ExportCTCommand = new RelayCommand(ExportCT);
            InitializeMessengers();
        }

        private void InitializeMessengers()
        {
            WeakReferenceMessenger.Default.Register<RequestUpdateCTList>(this, (r,m) =>
            {
                UpdateCTListForExport(m.Images);
            });
        }

        public void UpdateCTListForExport(IEnumerable<ExportCTModel> images)
        {
            foreach (ExportCTModel itr in images) CTImageList.Add(itr);
        }

        public void ShowExportCTInfo()
        {
            MessageBox.Show("Select a CT image to export to the deep learning model for autocontouring");
        }

        public void CTImageSelectionChanged(ExportCTModel model)
        {
            if(CTImageList.Any(x => x != model && x.SelectedForExport))
            {
                foreach (var ctImage in CTImageList.Where(x => x != model && x.SelectedForExport))
                {
                    ctImage.SelectedForExport = false;
                }
                CTImageList.Refresh();
            }
        }

        public void ExportCT()
        {
            if (!CTImageList.Any(x => x.SelectedForExport))
            {
                //previously nothing happened when no CT was ticked
                MessageBox.Show("Marque la casilla 'Select' del CT que desea exportar.\nTick the CT you want to export.\n\nNOTA: esta pestaña es OPCIONAL (auto-contorno externo). Si el CT y las estructuras ya están en Eclipse, empiece en 'Set Targets'.");
                return;
            }
            SelectedCTImage = CTImageList.FirstOrDefault(x => x.SelectedForExport);
            WeakReferenceMessenger.Default.Send(new RequestExportCT(SelectedCTImage));
        }
    }
}
