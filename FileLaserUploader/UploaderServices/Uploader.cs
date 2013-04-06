using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Diagnostics;
using System.Web.Script.Serialization;
using System.Threading;

namespace UploadServices
{

    public partial class FileLaserUploader
    {

        internal string _username;
        string _password;
        internal bool _loggedIn;
        HttpWebRequest _req;


        public FileLaserUploader(String username, String password)
        {
            _username = username;
            _password = password;

            Uploads = new List<Upload>();

            ThreadPool.QueueUserWorkItem(StartInner);
        }

        public void StartInner(object state)
        {

            _req = (HttpWebRequest)WebRequest.Create("http://www.filelaser.com/api/getuserinfo/");
            _req.Method = "POST";
            _req.ContentType = "application/x-www-form-urlencoded";

            _req.BeginGetRequestStream(RequestStreamCallback, null);

        }

        void RequestStreamCallback(IAsyncResult result)
        {

            Stream stream = _req.EndGetRequestStream(result);

            using (StreamWriter sw = new StreamWriter(stream))
                sw.Write("username={0}&password={1}", _username, _password);

            _req.BeginGetResponse(ResponseCallback, null);

        }

        void ResponseCallback(IAsyncResult result) {

            HttpWebResponse resp = (HttpWebResponse)_req.EndGetResponse(result);

            Stream stream = resp.GetResponseStream();

            InfoResponse response;

            JavaScriptSerializer js = new JavaScriptSerializer();

            using (StreamReader sr = new StreamReader(stream))
                response = js.Deserialize<InfoResponse>(sr.ReadToEnd());

            ID = response.userdetails.id;
            Alias = response.userdetails.alias;
            FTPpassword = response.userdetails.ftppassword;
            IsPremium = response.userdetails.ispremium;
            Points = response.userdetails.points;
            PremiumExpiry = response.userdetails.premiumexpire;
            RefererPoints = response.userdetails.refererpoints;
            SpaceUsed = response.userdetails.spaceused;

            // important lock
            lock (this)
                _loggedIn = true;

            OnStatusUpdated(null);


        }

        public Upload CreateUpload(string file)
        {

            Upload upload = new Upload(this, file);

            Uploads.Add(upload);

            OnUploadCreated(new UploadEventArgs(upload));

            return upload;

        }

        public List<Upload> Uploads { get; internal set; }


        public int ID { get; internal set; }

        public string Alias { get; internal set; }

        public int Points { get; internal set; }

        public int RefererPoints { get; internal set; }

        public int SpaceUsed { get; internal set; }

        public bool IsPremium { get; internal set; }

        public DateTime PremiumExpiry { get; internal set; }

        public string FTPpassword { get; internal set; }

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
