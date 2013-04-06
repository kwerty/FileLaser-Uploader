using System;
using System.Windows;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Diagnostics;

namespace FileLaserUploader
{

    public partial class App : Application
    {


        private void Application_Startup(object sender, StartupEventArgs e)
        {

            if (DateTime.Today >= DateTime.Parse("2013/02/01"))
            {
                MessageBox.Show("Please download a newer version of Kwerty FileLaser Uploader at http://kwerty.com", "Update required", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
                return;
            }
            
            if (e.Args.Length > 0 && e.Args[0] == "-debug")
            {
                Debug.AutoFlush = true;
                Debug.Listeners.Add(new DebugLogWriter());
            }

            Debug.WriteLine(Assembly.GetExecutingAssembly().GetName().FullName);
            
            UploaderViewModel vm = new UploaderViewModel();
            MainWindow window = new MainWindow(vm);
            window.Show();

        }


    }

    public class DebugLogWriter : TextWriterTraceListener
    {

        

        public DebugLogWriter()
            : base(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "debug.txt"), "DebugLog")
        {
        }

        public override void Write(string message)
        {
            base.Write(String.Format("{0}: {1}", DateTime.Now, message));
        }

        public override void WriteLine(string message)
        {
            base.WriteLine(String.Format("{0}: {1}", DateTime.Now, message));
        }


    }
}
