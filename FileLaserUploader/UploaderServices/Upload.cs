using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Threading;
using System.IO;
using System.Diagnostics;
using System.Web.Script.Serialization;

namespace UploaderServices
{

    public class Upload
    {

        FileLaserUploaderService _service;

        string _uploadId;

        int _userID;
        IWebProxy _proxy;

        Dictionary<string, string> _errors;

        HttpWebRequest _req;
        Stream _fs;
        Stream _rs;

        byte[] _buffer;
        byte[] _leading;
        byte[] _trailing;

        bool _cancel;
        long _filePos;
        int _readLength;
        int _checkCounter;
        Stopwatch _windowClock;
        Stopwatch _stopwatch;
        Queue<double> _speeds;


        internal Upload(FileLaserUploaderService main, string filePath)
        {

            _errors = new Dictionary<string, string>()
            {
                {"disklimit", "Over storage limit"},
                {"oversized", "File is too big"},
                {"saveerror", "Couldn't save file"},
            };

            _uploadId = Path.GetFileNameWithoutExtension(Path.GetRandomFileName());

            InitialWait = 3;
            MinBufferSize = 1024 * 5; //5kb
            MaxBufferSize = 1024 * 1024 * 20; //20mb
            BufferSize = 1024 * 25; //25kb
            DesiredInterval = 2;
            IntervalMargin = 0.5; //%
            AverageOver = 5;
            ResizeBufferAfter = 5; //must be equal to or greater than AverageOver

            _service = main;

            FullPath = filePath;
            FileName = Path.GetFileName(FullPath);

            _fs = File.OpenRead(FullPath);

            Debug.WriteLine("{0}: Uploading {1} ({2:f2} kb)", _uploadId, FileName, Size / 1024);
            Debug.WriteLine("{0}: Initial buffer size = {1} bytes", _uploadId, BufferSize);
            Debug.WriteLine("{0}: Initial wait before speed is tracked is {1} seconds", _uploadId, InitialWait);
            Debug.WriteLine("{0}: After initial wait, buffer size will be reevaluated every {1} writes", _uploadId, ResizeBufferAfter);
            Debug.WriteLine("{0}: If writes are not occuring approx every {1} (margin of {2:P0}) seconds, the buffer will be resized to suit", _uploadId, DesiredInterval, IntervalMargin);


            lock (_service)
            {
                if (_service.Status != UploaderStatus.Authenticated)
                {
                    _service.StatusChanged += new EventHandler<UploaderStatusEventArgs>(UploaderStatusChanged);
                    return;
                }

            }

            StartUpload();

        }



        void StartUpload()
        {

            _userID = _service.ID;
            _proxy = _service.Proxy;

            Status = UploadStatus.Uploading;
            OnUploadStatusChanged(new UploadStatusEventArgs(Status));

            ThreadPool.QueueUserWorkItem(StartInner);

        }

        void UploaderStatusChanged(object sender, UploaderStatusEventArgs e)
        {

            if (e.Status != UploaderStatus.Authenticated)
                return;

            _service.StatusChanged -= UploaderStatusChanged;

            StartUpload();

        }

        private void StartInner(object state)
        {

            string b = new String('-', 28) + DateTime.Now.Ticks.ToString("x");

            Dictionary<string, string> pairs = new Dictionary<string, string>();
            pairs.Add("uid", _userID.ToString());
            pairs.Add("description", "kwerty.com");

            using (MemoryStream ms = new MemoryStream())
            {

                using (StreamWriter sw = new StreamWriter(ms))
                {

                    foreach (var pair in pairs)
                    {
                        sw.WriteLine("--" + b);
                        sw.WriteLine("Content-Disposition: form-data; name=\"{0}\"", pair.Key);
                        sw.WriteLine();
                        sw.Write(pair.Value);
                        sw.WriteLine();
                    }

                    sw.WriteLine("--" + b);
                    sw.WriteLine("Content-Disposition: form-data; name=\"file\"; filename=\"{0}\"", FileName);
                    sw.WriteLine();

                }

                _leading = ms.GetBuffer();

            }

            _trailing = UTF8Encoding.UTF8.GetBytes("\r\n" + "--" + b + "--");

            _req = (HttpWebRequest)WebRequest.Create("http://upload.filelaser.com/upload");
            _req.Method = "POST";
            _req.Accept = "application/json";
            _req.ContentType = "multipart/form-data; boundary=" + b;
            _req.AllowWriteStreamBuffering = false;
            _req.ContentLength = _leading.Length + Size + _trailing.Length;
            _req.Proxy = _proxy;

            Debug.WriteLine("{0}: Total web request content length is {1} bytes", _uploadId, _req.ContentLength);

            Debug.WriteLine("{0}: Getting request stream", (object)_uploadId); //it confuses the upload id for debug category

            try
            {
                _req.BeginGetRequestStream(GetRequestStreamCallback, null);
            }
            catch (WebException ex)
            {
                HandleException(ex);
            }
            

        }

        void HandleException(WebException ex)
        {

            Debug.WriteLine("{0}: Exception encountered during web request operation {1}", _uploadId, ex);

            Cleanup();

            Status = UploadStatus.Error;
            LastError = ex.Message;

            OnUploadStatusChanged(new UploadStatusEventArgs(Status));

        }

        void Cleanup()
        {

            _buffer = null;

            if (_fs != null)
                _fs.Close();

            if (_rs != null)
                _req.Abort();

            _service.Uploads.Remove(this);

        }



        void GetRequestStreamCallback(IAsyncResult result)
        {

            try
            {
                _rs = _req.EndGetRequestStream(result);
            }
            catch (WebException ex)
            {
                HandleException(ex);
                return;
            }

            Debug.WriteLine("{0}: Got request stream, writing {1} leading bytes", _uploadId, _leading.Length);

            try
            {
                _rs.BeginWrite(_leading, 0, _leading.Length, WriteLeadingCallback, null);
            }
            catch (WebException ex)
            {
                HandleException(ex);
            }


        }

        void WriteLeadingCallback(IAsyncResult result)
        {

            try
            {
                _rs.EndWrite(result);
            }
            catch (WebException ex)
            {
                HandleException(ex);
                return;
            }

            Debug.WriteLine("{0}: Wrote leading {1} bytes, beginning file read/write", _uploadId, _leading.Length);


            _windowClock = new Stopwatch();
            _stopwatch = new Stopwatch();
            _speeds = new Queue<double>(AverageOver);
            _buffer = new byte[BufferSize];

            ReadData();

        }

        void ReadData()
        {

            if (_cancel)
            {

                Debug.WriteLine("{0}: Cancelled after sending {1}/{2} bytes", _uploadId, Progress, Size);

                Cleanup();

                Status = UploadStatus.Cancelled;
                OnUploadStatusChanged(new UploadStatusEventArgs(Status));

                return;
            }

            Debug.WriteLine("{0}: Read start {1} bytes", _uploadId, BufferSize);

            _stopwatch.Restart();

            try
            {
                _fs.BeginRead(_buffer, 0, BufferSize, ReadCallback, null);
            }
            catch (WebException ex)
            {
                HandleException(ex);
            }

        }

        void ReadCallback(IAsyncResult result)
        {

            _readLength = _fs.EndRead(result);

            Debug.WriteLine("{0}: Read end {1} bytes", _uploadId, _readLength);

            _filePos += _readLength;

            WriteData();

        }

        void WriteData()
        {

            Debug.WriteLine("{0}: Write start {1} bytes", _uploadId, _readLength);

            if (_windowClock != null)
                _windowClock.Start();

            try
            {
                _rs.BeginWrite(_buffer, 0, _readLength, WriteCallback, null);
            }
            catch (WebException ex)
            {
                HandleException(ex);
            }

        }

        void WriteCallback(IAsyncResult result)
        {

            try
            {
                _rs.EndWrite(result);
            }
            catch (WebException ex)
            {
                HandleException(ex);
                return;
            }

            if (_windowClock != null)
                _windowClock.Stop();

            _stopwatch.Stop();

            if (_windowClock != null && _windowClock.Elapsed.Seconds >= InitialWait)
            {
                _windowClock = null;
                Debug.WriteLine("{0}: The initial window of {1} seconds has now passed, buffer size reevaulated every {2} read/writes", _uploadId, InitialWait, ResizeBufferAfter);
            }

            Progress += _readLength;
            
            OnUploadProgressChanged(new UploadProgressEventArgs(Progress));

            double speed = (double)_readLength / _stopwatch.Elapsed.TotalSeconds;

            if (_windowClock == null)
            {

                if (_speeds.Count == AverageOver)
                    _speeds.Dequeue();

                _speeds.Enqueue(speed);

            }

            Debug.WriteLine("{0}: Write end {1} bytes (read/write clocked at {2:f2} kbps completed in {3:f2} seconds)", _uploadId, _readLength, speed / 1024, _stopwatch.Elapsed.TotalSeconds);

            if (_windowClock == null && _speeds.Count == AverageOver)
            {

                double avgSpeed = _speeds.Average();
                double eta = (Size - Progress) / avgSpeed;

                if (avgSpeed != Speed)
                {
                    Speed = avgSpeed;
                    OnUploadSpeedChanged(new UploadSpeedEventArgs(Speed));
                }

                if (eta != ETA)
                {
                    ETA = eta;
                    OnUploadETAChanged(new UploadETAEventArgs(ETA));
                }

            }

            if (_filePos == _fs.Length)
            {
                SendTrailing();
                return;
            }

            if (_windowClock == null)
            {

                _checkCounter++;

                if (_checkCounter == ResizeBufferAfter)
                {

                    double estTime = BufferSize / Speed;

                    Debug.WriteLine("{0}: {1} read/writes have occured (avg speed {2:f2} kbps), reevaluating buffer size", _uploadId, ResizeBufferAfter, Speed / 1024);
                    Debug.WriteLine("{0}: Estimated read/write time with current buffer size {1} is {2:f2} seconds", _uploadId, BufferSize, estTime);

                    if (estTime < DesiredInterval * IntervalMargin ||
                        estTime > DesiredInterval * (1 + IntervalMargin))
                    {

                        int idealSize = ((int)Speed * DesiredInterval / 1024) * 1024;
                        idealSize = Math.Max(idealSize, MinBufferSize);
                        idealSize = Math.Min(idealSize, MaxBufferSize);

                        Debug.WriteLine("{0}: Estimated read/write time {1:f2} with current buffer size is too far from {2} seconds ({3:P0} margin)", _uploadId, estTime, DesiredInterval, IntervalMargin);

                        if (BufferSize != idealSize)
                        {

                            Debug.WriteLine("{0}: Changing buffer size, aiming for {1} second read/writes, using size of {2} bytes", _uploadId, DesiredInterval, idealSize);

                            BufferSize = idealSize;
                            _buffer = new byte[BufferSize];

                        }

                    }
                    else
                        Debug.WriteLine("{0}: Estimated read/write time is within {1:P0} margin of {2} seconds, buffer will not be changed", _uploadId, IntervalMargin, DesiredInterval);

                    _checkCounter = 0;

                }

            }

            ReadData();

        }



        void SendTrailing()
        {

            Debug.WriteLine("{0}: File transferred, now writing {1} trailing bytes", _uploadId, _trailing.Length);

            try
            {
                _rs.BeginWrite(_trailing, 0, _trailing.Length, WriteTrailingCallback, null);
            }
            catch (WebException ex)
            {
                HandleException(ex);
            }

        }

        void WriteTrailingCallback(IAsyncResult result)
        {

            try
            {
                _rs.EndWrite(result);
            }
            catch (WebException ex)
            {
                HandleException(ex);
                return;
            }

            _rs.Close();
            _rs = null;

            Debug.WriteLine("{0}: Wrote {1} trailing bytes, now getting response", _uploadId, _trailing.Length);

            try
            {
                _req.BeginGetResponse(ResponseCallback, null);
            }
            catch (WebException ex)
            {
                HandleException(ex);
            }

        }


        void ResponseCallback(IAsyncResult result)
        {

            Stream stream = null;

            try
            {
                HttpWebResponse resp = _req.EndGetResponse(result) as HttpWebResponse;
                stream = resp.GetResponseStream();
            }

            catch (WebException ex)
            {
                stream = ex.Response.GetResponseStream();

                if (stream == null)
                {
                    HandleException(ex);
                    return;
                }

            }

            Debug.WriteLine("{0}: Got response", (object)_uploadId); //confuses uploadId for category

            //we've made it this far, this means the server sent us a response

            string body;
            UploadResponse response;

            JavaScriptSerializer js = new JavaScriptSerializer();

            using (StreamReader sr = new StreamReader(stream))
            {
                body = sr.ReadToEnd();
                response = js.Deserialize<UploadResponse>(body);
            }

            if (response.error != null)
            {

                string message = String.Format("Unknown error '{0}'", response.error);

                if (_errors.ContainsKey(response.error))
                    message = _errors[response.error];

                Status = UploadStatus.Error;
                LastError = String.Format("Server error: {0}", message);
            }

            else
            {
                InfoURL = response.infourl;
                DeleteCode = response.deletecode;
                DownloadURL = response.downloadurl;
                DeleteURL = response.deleteurl;
                FileID = response.fileid;

                Status = UploadStatus.Success;
            }

            Cleanup();

            OnUploadStatusChanged(new UploadStatusEventArgs(Status));
 
        }

        public void Cancel()
        {
            _cancel = true;
        }

        public string LastError { get; internal set; }

        public UploadStatus Status { get; internal set; }

        internal int DesiredInterval { get; set; }

        internal double IntervalMargin { get; set; }

        internal int MinBufferSize { get; set; }

        internal int MaxBufferSize { get; set; }

        internal int BufferSize { get; set; }

        internal int InitialWait { get; set; }

        internal int AverageOver { get; set; }

        internal int ResizeBufferAfter { get; set; }




        public long Size
        {
            get
            {
                return _fs.Length;
            }
        }

        public string FileName { get; internal set; }

        public string FullPath { get; internal set; }

        public double ETA { get; internal set; }

        public double Speed { get; internal set; }

        public long Progress { get; internal set; }

        public string InfoURL { get; internal set; }

        public string DeleteCode { get; internal set; }

        public string DownloadURL { get; internal set; }

        public string DeleteURL { get; internal set; }

        public string FileID { get; internal set; }

        public event EventHandler<UploadStatusEventArgs> StatusChanged;

        public event EventHandler<UploadProgressEventArgs> ProgressChanged;

        public event EventHandler<UploadSpeedEventArgs>SpeedChanged;

        public event EventHandler<UploadETAEventArgs> ETAChanged;

        internal void OnUploadStatusChanged(UploadStatusEventArgs e)
        {
            var evt = StatusChanged;
            if (evt != null) evt(this, e);
        }

        internal void OnUploadProgressChanged(UploadProgressEventArgs e)
        {
            var evt = ProgressChanged;
            if (evt != null) evt(this, e);
        }

        internal void OnUploadSpeedChanged(UploadSpeedEventArgs e)
        {
            var evt = SpeedChanged;
            if (evt != null) evt(this, e);
        }

        internal void OnUploadETAChanged(UploadETAEventArgs e)
        {
            var evt = ETAChanged;
            if (evt != null) evt(this, e);
        }


    }

    internal class UploadResponse
    {
        public string infourl { get; set; }
        public string deletecode { get; set; }
        public string downloadurl { get; set; }
        public bool result { get; set; }
        public string deleteurl { get; set; }
        public string fileid { get; set; }
        public string error { get; set; }
    }

    public enum UploadStatus
    {
        Idle,
        Uploading,
        Success,
        Cancelled,
        Error,
    }

    public class UploadStatusEventArgs : EventArgs
    {

        public UploadStatus Status { get; internal set; }

        public UploadStatusEventArgs(UploadStatus status)
        {
            Status = status;
        }

    }

    public class UploadProgressEventArgs : EventArgs
    {

        public long Progress { get; internal set; }

        public UploadProgressEventArgs(long progress)
        {
            Progress = progress;
        }

    }

    public class UploadETAEventArgs : EventArgs
    {

        public double ETA { get; internal set; }

        public UploadETAEventArgs(double eta)
        {
            ETA = eta;
        }

    }

    public class UploadSpeedEventArgs : EventArgs
    {

        public double Speed { get; internal set; }

        public UploadSpeedEventArgs(double speed)
        {
            Speed = speed;
        }

    }


}
