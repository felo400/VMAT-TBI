using System;
using System.Threading;
using System.Windows.Threading;

namespace AutoPlannerHelpers.Helpers
{
    public class ESAPIWorker
    {
        public bool isError = false;
        public readonly Dispatcher _dispatcher;

        /// <summary>
        /// Constructor
        /// </summary>
        public ESAPIWorker()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
        }

        /// <summary>
        /// Method to asynchronously execute the supplied task on the MAIN thread
        /// </summary>
        /// <param name="a"></param>
        public void DoWork(Action a)
        {
            _dispatcher.BeginInvoke(a);
        }

        /// <summary>
        /// Method to create a new thread, set the apartment state, set the new thread to be a background thread, 
        /// and execute the action supplied to this method
        /// </summary>
        /// <param name="a"></param>
        public void RunOnNewThread(Action a)
        {
            Thread t = new Thread(() =>
            {
                //an unhandled exception on this (progress window) thread would terminate the host process (Eclipse when running
                //as a plugin): catch it, report it, and keep the application alive
                Dispatcher.CurrentDispatcher.UnhandledException += (s, e) =>
                {
                    e.Handled = true;
                    isError = true;
                    try { System.Windows.MessageBox.Show($"Error: {e.Exception.GetBaseException().Message}\n\n{e.Exception.GetBaseException().StackTrace}", "VMAT AutoPlanner"); } catch { }
                };
                try { a(); }
                catch (Exception e)
                {
                    isError = true;
                    try { System.Windows.MessageBox.Show($"Error: {e.GetBaseException().Message}\n\n{e.GetBaseException().StackTrace}", "VMAT AutoPlanner"); } catch { }
                }
            });
            t.CurrentCulture = Thread.CurrentThread.CurrentCulture;
            t.CurrentUICulture = Thread.CurrentThread.CurrentUICulture;
            t.SetApartmentState(ApartmentState.STA);
            t.IsBackground = true;
            t.Start();
        }
    }
}
