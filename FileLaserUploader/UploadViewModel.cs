using System;
using System.ComponentModel;
using UploaderServices;

namespace FileLaserUploader
{

    public class UploadViewModel : INotifyPropertyChanged
    {

        internal UploaderViewModel _main;
        internal bool _active;


        public UploadViewModel(UploaderViewModel main, Upload upload)
        {

            _main = main;
            Upload = upload;
            _active = true;
            Size = UploaderViewModel.SizeFormatter(Upload.Size);
            SetStatusMessage();
            SetProgress(0);

            Upload.StatusChanged += new EventHandler<UploadStatusEventArgs>(UploadStatusChanged);
            Upload.ProgressChanged += new EventHandler<UploadProgressEventArgs>(UploadProgressChanged);
            Upload.SpeedChanged += new EventHandler<UploadSpeedEventArgs>(UploadSpeedChanged);
            Upload.ETAChanged += new EventHandler<UploadETAEventArgs>(UploadETAChanged);

        }

        void SetStatusMessage()
        {


            if (Upload.Status == UploadStatus.Idle)
                StatusMessage = "Idle";

            else if (Upload.Status == UploadStatus.Uploading)
                StatusMessage = "Uploading";

            else if (Upload.Status == UploadStatus.Success)
                StatusMessage = "Complete";

            else if (Upload.Status == UploadStatus.Cancelled)
                StatusMessage = "Cancelled";

            else if (Upload.Status == UploadStatus.Error)
                StatusMessage = Upload.LastError;

        }

        void LogStatusChange()
        {

            string log = null;

            if (Upload.Status == UploadStatus.Uploading)
                log = String.Format("Now uploading '{0}'", FileName);

            else if (Upload.Status == UploadStatus.Success)
                log = String.Format("'{0}' has been uploaded to '{1}'", FileName, DownloadURL);

            else if (Upload.Status == UploadStatus.Cancelled)
                log = String.Format("'{0}' cancelled", FileName);

            else if (Upload.Status == UploadStatus.Error)
                log = String.Format("'{0} failed': {1}", FileName, Upload.LastError);

            _main.Dispatcher.Invoke(new Action<LogEventArgs>(_main.OnLogUpdate), new LogEventArgs(DateTime.Now, log));

        }


        void UploadStatusChanged(object sender, UploadStatusEventArgs e)
        {

            if (e.Status < UploadStatus.Success && !_active)
            {
                _active = true;
                _main.Dispatcher.Invoke(new Func<UploadViewModel, bool>(_main.Complete.Remove), this);
                _main.Dispatcher.Invoke(new Action<UploadViewModel>(_main.Active.Add), this);
            }

            else if (e.Status >= UploadStatus.Success && _active)
            {
                _active = false;
                _main.Dispatcher.Invoke(new Func<UploadViewModel, bool>(_main.Active.Remove), this);
                _main.Dispatcher.Invoke(new Action<UploadViewModel>(_main.Complete.Add), this);
            }

            SetStatusMessage();

            OnPropertyChanged(new PropertyChangedEventArgs("StatusMessage"));
            OnPropertyChanged(new PropertyChangedEventArgs("Status"));

            LogStatusChange();

        }

        void UploadSpeedChanged(object sender, UploadSpeedEventArgs e)
        {
            StatusMessage = String.Format("Uploading ({0}ps)", UploaderViewModel.SizeFormatter(e.Speed));
            OnPropertyChanged(new PropertyChangedEventArgs("StatusMessage"));
        }

        void UploadProgressChanged(object sender, UploadProgressEventArgs e)
        {
            SetProgress(e.Progress);
        }

        void SetProgress(long progress)
        {
            PercentComplete = progress > 0 ? (double)progress / (double)Upload.Size : 0;
            Progress = String.Format("{0} ({1:P1})", UploaderViewModel.SizeFormatter(progress), PercentComplete);

            OnPropertyChanged(new PropertyChangedEventArgs("PercentComplete"));
            OnPropertyChanged(new PropertyChangedEventArgs("Progress"));
        }


        void UploadETAChanged(object sender, UploadETAEventArgs e)
        {
            ETA = UploaderViewModel.TimeFormatter(e.ETA);
            OnPropertyChanged(new PropertyChangedEventArgs("ETA"));
        }

        public void Close()
        {
            _main.Complete.Remove(this);
        }

        public void Restart()
        {
            _main.Complete.Remove(this);
            _main.CreateUpload(Upload.FullPath);
        }

        public void Cancel()
        {
            Upload.Cancel();
        }

        public Upload Upload { get; internal set; }

        public string DownloadURL
        {
            get
            {
                return Upload.DownloadURL;
            }
        }

        public UploadStatus Status
        {
            get
            {
                return Upload.Status;
            }
        }

        public string FileName
        {
            get
            {
                return Upload.FileName;
            }
        }

        public string ETA { get; internal set; }

        public string Size { get; internal set; }

        public double PercentComplete { get; internal set; }

        public string Progress { get; internal set; }

        public string Speed { get; internal set; }

        public string StatusMessage { get; internal set; }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            PropertyChangedEventHandler ev = PropertyChanged;
            if (ev != null)
                PropertyChanged(this, e);

        }


    }

}
