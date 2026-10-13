using AutoPlannerHelpers.Context;
using AutoPlannerHelpers.Logging;
using AutoPlannerOptimizationLoop.Helpers;
using AutoPlannerOptimizationLoop.Settings;
using AutoPlannerOptimizationLoop.ViewModels;
using AutoPlannerOptimizationLoop.Views;
using System;
using System.Threading;
using System.Windows.Threading;

namespace AutoPlannerOptimizationLoop
{
    /// <summary>
    /// Entry point used by the Eclipse binary plugin (VMATAutoPlanner.esapi.dll).
    /// Same threading model as the stand-alone App: the UI runs on its own STA thread while every ESAPI call is
    /// marshalled to the ESAPI thread (here: Eclipse's UI thread, which keeps pumping messages until the UI closes).
    /// </summary>
    public static class PluginEntry
    {
        public static void Run()
        {
            OptimizationLoopSettings.ClearSettings();
            OptimizationLoopSettings.Reminders.Clear();
            Dispatcher esapiDispatcher = Dispatcher.CurrentDispatcher;
            ESAPIThreadContext.Initialize(esapiDispatcher, new DispatcherSynchronizationContext(esapiDispatcher));
            SynchronizationContext previous = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(esapiDispatcher));

            DispatcherFrame frame = new DispatcherFrame();
            Exception uiError = null;
            Thread t = new Thread(() =>
            {
                Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
                {
                    e.Handled = true;
                    try { System.Windows.MessageBox.Show($"Error: {e.Exception.GetBaseException().Message}\n\n{e.Exception.GetBaseException().StackTrace}", "VMAT AutoPlanner"); } catch { }
                };
                try
                {
                    OptimizationLoopMainView mv = new OptimizationLoopMainView { DataContext = new OptimizationLoopMainViewModel(new string[] { }) };
                    ESAPIThreadContext.UIDispatcher = mv.Dispatcher;
                    mv.ShowDialog();
                }
                catch (Exception e) { uiError = e; }
                finally
                {
                    //release the ESAPI (Eclipse) thread
                    esapiDispatcher.BeginInvoke(new Action(() => frame.Continue = false));
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.CurrentCulture = Thread.CurrentThread.CurrentCulture;
            t.CurrentUICulture = Thread.CurrentThread.CurrentUICulture;
            t.Start();
            //keep processing ESAPI requests from the UI thread until the optimization loop window is closed
            Dispatcher.PushFrame(frame);
            SynchronizationContext.SetSynchronizationContext(previous);
            if (uiError != null) Logger.GetInstance().LogError($"Optimization loop error: {uiError.Message}\n\n{uiError.StackTrace}");
            if (!EclipseContext.GetInstance().IsInitialized) return;
        }
    }
}
