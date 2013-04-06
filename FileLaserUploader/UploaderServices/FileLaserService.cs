using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Web.Script.Serialization;
using System.Threading;
using System.Diagnostics;


namespace UploaderServices
{

    public class FileLaserUploaderService
    {

        public FileLaserUploaderService()
        {
            Uploads = new List<Upload>();

            ServicePointManager.DefaultConnectionLimit = Int32.MaxValue;
        }

        public void Authenticate(string username, string password)
        {

            Username = username;
            Password = password;

            if (Status == UploaderStatus.Authenticated)
            {

                ID = 0;
                Alias = null;
                FTPpassword = null;
                IsPremium = false;
                Points = 0;
                PremiumExpiry = new DateTime();
                RefererPoints = 0;
                SpaceUsed = 0;

                Status = UploaderStatus.NotAuthenticated;
                OnStatusChanged(new UploaderStatusEventArgs(Status));
            }

            if (Username != null)
                ThreadPool.QueueUserWorkItem(AuthenticateInner);
        }

        public void AuthenticateInner(object state)
        {

            Status = UploaderStatus.Authenticating;
            OnStatusChanged(new UploaderStatusEventArgs(Status));

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://www.filelaser.com/api/getuserinfo/");
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.Proxy = Proxy;

            Stream rs = null;

            try
            {
                rs = req.GetRequestStream();

                using (rs)
                    using (StreamWriter sw = new StreamWriter(rs))
                        sw.Write("username={0}&password={1}", Username, Password);

                rs = null;

                Stream stream = null;

                try
                {
                    HttpWebResponse resp = req.GetResponse() as HttpWebResponse;
                    stream = resp.GetResponseStream(); 
                }
                catch (WebException ex)
                {
                    stream = ex.Response.GetResponseStream();

                    if (stream == null)
                        throw;

                }

                string body;
                InfoResponse response;

                JavaScriptSerializer js = new JavaScriptSerializer();
                
                using (StreamReader sr = new StreamReader(stream))
                {
                    body = sr.ReadToEnd();
                    response = js.Deserialize<InfoResponse>(body);
                }

                if (response.result == false)
                {
                    LastError = "Username/password rejected";
                    Status = UploaderStatus.FailedAuthentication;
                    OnStatusChanged(new UploaderStatusEventArgs(Status));
                    return;
                }

                lock (this)
                {

                    Status = UploaderStatus.Authenticated;

                    ID = response.userdetails.id;
                    Alias = response.userdetails.alias;
                    FTPpassword = response.userdetails.ftppassword;
                    IsPremium = response.userdetails.ispremium;
                    Points = response.userdetails.points;
                    PremiumExpiry = response.userdetails.premiumexpire;
                    RefererPoints = response.userdetails.refererpoints;
                    SpaceUsed = response.userdetails.spaceused;

                }

                OnStatusChanged(new UploaderStatusEventArgs(Status));


            }
            catch (WebException ex)
            {

                if (rs != null)
                    req.Abort();

                LastError = ex.Message;
                Status = UploaderStatus.FailedAuthentication;

                OnStatusChanged(new UploaderStatusEventArgs(Status));
            }


        }


        public Upload CreateUpload(string file)
        {

            Upload upload = new Upload(this, file);

            Uploads.Add(upload);

            OnUploadCreated(new UploadEventArgs(upload));

            return upload;

        }


        public string Username { get; internal set; }

        public string Password { get; internal set; }

        public string LastError { get; internal set; }

        public UploaderStatus Status { get; internal set; }

        public IWebProxy Proxy { get; internal set; }

        public List<Upload> Uploads { get; internal set; }

        public int ID { get; internal set; }

        public string Alias { get; internal set; }

        public int Points { get; internal set; }

        public int RefererPoints { get; internal set; }

        public int SpaceUsed { get; internal set; }

        public bool IsPremium { get; internal set; }

        public DateTime PremiumExpiry { get; internal set; }

        public string FTPpassword { get; internal set; }

        public event EventHandler<UploaderStatusEventArgs> StatusChanged;

        public event EventHandler<UploadEventArgs> UploadCreated;

        internal void OnStatusChanged(UploaderStatusEventArgs e)
        {
            var evt = StatusChanged;
            if (evt != null) evt(this, e);
        }

        internal void OnUploadCreated(UploadEventArgs e)
        {
            var evt = UploadCreated;
            if (evt != null) evt(this, e);
        }

    }

    public enum UploaderStatus
    {
        NotAuthenticated,
        FailedAuthentication,
        Authenticating,
        Authenticated,
    }

    public class UploadEventArgs : EventArgs
    {
        public Upload Upload { get; internal set; }

        internal UploadEventArgs(Upload upload)
        {
            Upload = upload;
        }

    }

    public class UploaderStatusEventArgs : EventArgs
    {

        public UploaderStatus Status { get; internal set; }

        public UploaderStatusEventArgs(UploaderStatus status)
        {
            Status = status;
        }

    }

    internal class InfoResponse
    {
        public bool result { get; set; }
        public UserDetails userdetails { get; set; }
    }

    internal class UserDetails
    {
        public int id { get; set; }
        public string alias { get; set; }
        public int points { get; set; }
        public int refererpoints { get; set; }
        public int spaceused { get; set; }
        public bool ispremium { get; set; }
        public DateTime premiumexpire { get; set; }
        public string ftppassword { get; set; }
    }
}
