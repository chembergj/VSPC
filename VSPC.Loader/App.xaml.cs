using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Windows;
using System.Reflection;
using VSPC.Core;
using VSPC.Loader.Config;
using System.Windows.Interop;
using System.Windows.Threading;

namespace VSPC.Loader
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        MessageBroker messageBroker = new MessageBroker();
        static List<IVSPCLogConsumer> logConsumers = new List<IVSPCLogConsumer>();

        public static void LogMethod(string level, string message)
        {
            foreach (var lc in logConsumers)
                lc.Log(level, message);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            string assemblyName;
            string className;

            try
            {
                var config = (ModuleConfiguration)ConfigurationManager.GetSection("vspcmodules");
                foreach (ModuleElement moduleElement in config.Modules)
                {
                    if (!moduleElement.TypeName.Contains(','))
                        throw new ArgumentException(string.Format("Illegal type name '{0}' in configuration file, must be on the form <class name>,<assembly name>", moduleElement.TypeName));

                    assemblyName = moduleElement.TypeName.Split(',')[1];
                    className = moduleElement.TypeName.Split(',')[0];

                    var asm = Assembly.Load(assemblyName);
                    if (asm == null)
                        throw new ArgumentException(string.Format("Illegal assembly name '{0}' in configuration file, not found", assemblyName));

                    var type = asm.GetType(className);
                    if (type == null)
                        throw new ArgumentException(string.Format("Illegal class name '{0}' in configuration file, not found", className));

                    if (typeof(IVSPCModule).IsAssignableFrom(type))
                    {
                        IVSPCModule module = (IVSPCModule)Activator.CreateInstance(type);
                        
                        module.OnModuleLoad(messageBroker);

                        if (typeof(IVSPCLogConsumer).IsAssignableFrom(type))
                        {
                            logConsumers.Add((IVSPCLogConsumer)module);
                        }
                    }
                    else
                    {
                        throw new ArgumentException(string.Format("Illegal type name '{0}' in configuration file, type must implement IVSPCModule", moduleElement.TypeName));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error occurred during startup: " + ex.Message, "VSPC Startup error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }
    }
}
