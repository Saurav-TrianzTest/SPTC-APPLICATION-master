using System.Windows;
using SPTC_APPLICATION.Objects;
using System;

namespace SPTC_APPLICATION
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        // Replaced static singleton with instance-based approach
        // In cloud environments, each instance should manage its own state
        private int openWindowCount = 0;
        
        // Application instance is now managed per-process, not globally
        // This ensures proper isolation in multi-instance cloud deployments
        private static App _currentInstance;

        public static App CurrentInstance
        {
            get
            {
                if (_currentInstance == null)
                {
                    _currentInstance = (App)Application.Current;
                }
                return _currentInstance;
            }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            _currentInstance = this;
            IncrementOpenWindowCount();
            
            // Load configuration from cloud storage on startup
            try
            {
                AppState.LoadFromJson();
            }
            catch (Exception ex)
            {
                EventLogger.Post($"WARN :: Failed to load AppState from cloud: {ex.Message}");
            }
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            openWindowCount--;

            if (openWindowCount <= 0)
            {
                EventLogger.Post("Main :: Application Closed");
                try
                {
                    AppState.SaveToJson();
                }
                catch (Exception ex)
                {
                    EventLogger.Post($"WARN :: Failed to save AppState to cloud: {ex.Message}");
                }
            }
        }
        
        public void IncrementOpenWindowCount()
        {
            openWindowCount++;
        }
    }
}
