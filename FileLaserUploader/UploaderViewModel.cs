using System;
using System.Collections.ObjectModel;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Windows.Threading;
using Microsoft.Win32;
using UploaderServices;
using System.Diagnostics;

namespace FileLaserUploader
{

    public partial class UploaderViewModel
    {

        public UploaderViewModel()
        {

            Dispatcher = Dispatcher.CurrentDispatcher;

            Active = new ObservableCollection<UploadViewModel>();
            Complete = new ObservableCollection<UploadViewModel>();

            RegistryKey registry = Registry.CurrentUser.CreateSubKey(@"Software\Kwerty FileLaser Uploader");
            string username;
            byte[] passwordEnc;
            string proxy;

            using (registry)
            {
                FirstTime = (int)registry.GetValue("notfirsttime", 0) != 1 ? true : false;

                username = (string)registry.GetValue("username");
                passwordEnc = (byte[])registry.GetValue("password");
                proxy = (string)registry.GetValue("proxy");
            }

            string password = null;

            if (username != null && passwordEnc != null)
                password = UTF8Encoding.UTF8.GetString(ProtectedData.Unprotect(passwordEnc, null, DataProtectionScope.CurrentUser));

            UploaderService = new FileLaserUploaderService();
            UploaderService.StatusChanged += new EventHandler<UploaderStatusEventArgs>(UploaderStatusUpdated);
            UploaderService.UploadCreated += new EventHandler<UploadEventArgs>(UploadCreated);

            if (username != null)
            {
                SetProxy(proxy);
                SetUser(username, password);
                Authenticate();
            }

        }

        public void SaveSettings()
        {

            RegistryKey registry = Registry.CurrentUser.CreateSubKey(@"Software\Kwerty FileLaser Uploader");

            using (registry)
            {
                if (Username != null)
                {
                    byte[] password = ProtectedData.Protect(UTF8Encoding.UTF8.GetBytes(Password), null, DataProtectionScope.CurrentUser);

                    registry.SetValue("username", Username);
                    registry.SetValue("password", password);
                    registry.SetValue("notfirsttime", 1);
                }
                else
                {
                    registry.DeleteValue("username", false);
                    registry.DeleteValue("password", false);
                }

                if (Proxy != null)
                    registry.SetValue("proxy", Proxy);
                else
                    registry.DeleteValue("proxy", false);

            }

        }

        public void CreateUpload(string file)
        {
            UploaderService.CreateUpload(file);
        }

        void UploadCreated(object sender, UploadEventArgs e)
        {
            Active.Add(new UploadViewModel(this, e.Upload));
            OnLogUpdate(new LogEventArgs(DateTime.Now, String.Format("'{0}' ({1}) added to queue", e.Upload.FileName, UploaderViewModel.SizeFormatter(e.Upload.Size))));
        }

        void UploaderStatusUpdated(object sender, UploaderStatusEventArgs e)
        {
            Dispatcher.Invoke(new Action<UploaderStatusEventArgs>(OnStatusChanged), e);

            string status = null;

            if (e.Status == UploaderStatus.Authenticated)
                status = String.Format("Authenticated as {0}", UploaderService.Username);
            else if (e.Status == UploaderStatus.Authenticating)
                status = "Authenticating";
            else if (e.Status == UploaderStatus.FailedAuthentication)
                status = String.Format("Authentication error: {0}", UploaderService.LastError);
            else if (e.Status == UploaderStatus.NotAuthenticated)
                status = "No longer authenticated";

            Dispatcher.Invoke(new Action<LogEventArgs>(OnLogUpdate), new LogEventArgs(DateTime.Now, status));
        }

        public void SetProxy(string proxy) {

            if (proxy == null)
            {
                Proxy = proxy;
                UploaderService.Proxy = WebRequest.DefaultWebProxy;
            }
            else if (proxy == ProxyDisabled)
            {
                Proxy = proxy;
                UploaderService.Proxy = null;
            }
            else
            {

                Uri uri;

                if (Uri.TryCreate(String.Format("http://{0}", proxy), UriKind.Absolute, out uri))
                {
                    Proxy = proxy;
                    UploaderService.Proxy = new WebProxy(uri);
                }
                else
                    throw new ArgumentException();
            }

        }

        public void SetUser(string username, string password)
        {
            Username = username;
            Password = password;
        }

        public void Authenticate() {
            UploaderService.Authenticate(Username, Password);
        }


        internal Dispatcher Dispatcher { get; set; }

        internal FileLaserUploaderService UploaderService { get; set; }

        public ObservableCollection<UploadViewModel> Active { get; internal set; }

        public ObservableCollection<UploadViewModel> Complete { get; internal set; }

        public bool FirstTime { get; internal set; }

        public string Username { get; set; }

        public string Password { get; set; }

        public string Proxy { get; set; }


        public string LastError
        {
            get
            {
                return UploaderService.LastError;
            }
        }

        public string Alias
        {
            get
            {
                return UploaderService.Alias;
            }
        }

        public string Points
        {
            get
            {
                return UploaderService.Points > 0 ? UploaderService.Points.ToString() : "n/a";
            }
        }

        public string RefererPoints
        {
            get
            {
                return UploaderService.RefererPoints > 0 ? UploaderService.RefererPoints.ToString() : "n/a";
            }
        }

        public string SpaceUsed
        {
            get
            {
                return UploaderViewModel.SizeFormatter(UploaderService.SpaceUsed);
            }
        }

        public string AccountType
        {
            get
            {
                return UploaderService.IsPremium ? "Premium account" : "Free account";
            }
        }

        public DateTime PremiumExpiry
        {
            get
            {
                return UploaderService.PremiumExpiry;
            }
        }

        public string FTPpassword
        {
            get
            {
                return UploaderService.FTPpassword != String.Empty ? UploaderService.FTPpassword : "n/a";
            }
        }

        public static string ProxyDisabled = "disabled";

        public event EventHandler<UploaderStatusEventArgs> StatusChanged;

        public event EventHandler<LogEventArgs> LogUpdate;

        internal void OnLogUpdate(LogEventArgs e)
        {
            var evt = LogUpdate;
            if (evt != null) evt(this, e);
        }

        internal void OnStatusChanged(UploaderStatusEventArgs e)
        {
            var evt = StatusChanged;
            if (evt != null) evt(this, e);
        }


        public static string TimeFormatter(double seconds)
        {

            TimeSpan span = TimeSpan.FromSeconds(seconds);

            if (span.Days > 0 && span.Hours > 0)
                return String.Format("{0}d {1}h", span.Days, span.Hours);

            else if (span.Days > 0)
                return String.Format("{0}d", span.Days);

            else if (span.Hours > 0 && span.Minutes > 0)
                return String.Format("{0}h {1}m", span.Hours, span.Minutes);

            else if (span.Hours > 0)
                return String.Format("{0}h", span.Hours);

            else if (span.Minutes > 0 && span.Seconds > 0)
                return String.Format("{0}m {1}s", span.Minutes, span.Seconds);

            else if (span.Minutes > 0)
                return String.Format("{0}m", span.Minutes);

            else
                return String.Format("{0}s", span.Seconds);
        }

        public static string SizeFormatter(double size)
        {

            string[] units = { "kb", "mb", "gb" };
            int unit = 0;

            size = size / 1024;

            while (size >= 1024)
            {
                size = size / 1024;
                unit++;
            }

            return String.Format("{0:f1}{1}", size, units[unit]);

        }

    }

    public class LogEventArgs : EventArgs
    {

        public DateTime Timestamp { get; internal set; }
        public string Message { get; internal set; }

        internal LogEventArgs(DateTime timestamp, string message)
        {
            Timestamp = timestamp;
            Message = message;
        }

    }

}
