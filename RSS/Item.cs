﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace RSS
{

    public delegate void DownloadProgressEvent(Item item, double percent);

    public delegate void DownloadCompleteEvent(Item item);

    public delegate void DownloadRemovedEvent(Item item);

    public class Item
    {
        public string Title { get; set; }

        public string Description { get; set; }

        public string Link { get; set; }

        public string Guid { get; set; }

        public string PubDate { get; set; }

        public DateTime DownloadDate { get; set; }
     
        public List<author> Authors = new List<author>();
        
        public bool Read { get; set; }

        public Subscription ParentSubscription { get; set; }
        
        public int RowNum;

        public static event DownloadCompleteEvent AnyDownloadComplete;

        public event DownloadProgressEvent DownloadProgress;

        public static event DownloadRemovedEvent AnyDownloadRemoved;

        public bool IsDownloaded
        {
            get
            {
                return File.Exists(FilePath);
            }
        }
        
        public bool CanBeDownloaded 
		{
			get
			{
				return !String.IsNullOrEmpty (Link) && !String.IsNullOrEmpty (Path.GetExtension (Link));
			}
		}
        
        public int MbSize;

        public string FilePath
        {
            get
            {
                return RSSConfig.DownloadFolder + GetCleanFileName() + GetFileType();
            }
        }

        private bool _isLoaded;
        public bool IsLoaded
        {
            get
            {
                return _isLoaded;
            }
            set
            {
                _isLoaded = value;
                if(_isLoaded)
                {
                    if (IsDownloaded)
                    {
                        OnAnyDownloadComplete();
                    }
                }
            }
        }


		public Item()
		{
			Title = string.Empty;
			Description = string.Empty;
			Link = string.Empty;
			Guid = string.Empty;
			PubDate = string.Empty;
		}

        public void DownloadFile()
        {
            if (!String.IsNullOrEmpty(Link))
            {
                if (!Directory.Exists(RSSConfig.DownloadFolder))
                {
                    Directory.CreateDirectory(RSSConfig.DownloadFolder);
                }

                using (WebClient client = new WebClient())
                {
                    client.DownloadProgressChanged += client_DownloadProgressChanged;
                    client.DownloadFileCompleted += client_DownloadFileCompleted;
                    client.DownloadFileAsync(new Uri(Link), FilePath);
                }
            }
        }


        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            ((WebClient)sender).DownloadProgressChanged -= client_DownloadProgressChanged;
            ((WebClient)sender).DownloadFileCompleted -= client_DownloadFileCompleted;
            
            OnAnyDownloadComplete();
        }

        private void OnAnyDownloadComplete()
        {
            var copy = AnyDownloadComplete;
            if(copy != null)
            {
                copy.Invoke(this);
            }
        }
        

        private void OnAnyDownloadRemoved()
        {
            var copy = AnyDownloadRemoved;
            if(copy != null)
            {
                copy.Invoke(this);
            }
        }

        private void OnDownloadProgress(double percent)
        {
            var copy = DownloadProgress;
            if(copy != null)
            {
                copy.Invoke(this, percent);
            }
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            MbSize = (int)(e.TotalBytesToReceive / (1024 * 1024));
            string MbString = (e.BytesReceived / (1024 * 1024)).ToString() + "/" + (e.TotalBytesToReceive / (1024 * 1024)).ToString() + "Mb";

            OnDownloadProgress(e.ProgressPercentage);
        }

        private string GetFileType()
        {
            string type = "";
            string[] strArr = Link.Split('.');
            if (strArr.Length > 0)
            {
                type = "." + strArr[strArr.Length - 1];
            }
            return type;
        }

        private string GetCleanFileName()
        {
            string filename = Title;
            string cleanFileName = "";
            if (!string.IsNullOrEmpty(filename))
            {
                cleanFileName = filename.Replace(":", "").Replace("\\", "").Replace("/", "");
            }
            return cleanFileName;
        }
        

        public int GetFileSizeMb()
        {
            int size = 0;
            if (File.Exists(FilePath))
            {
                FileInfo info = new FileInfo(FilePath);
                size = (int)(info.Length / (1024 * 1024));
            }
            MbSize = size;
            return MbSize;
        }


        public bool DeleteFile()
        {
            bool success = false;
            string url = Link;
            try
            {
                if (IsDownloaded)
                {
                    if (File.Exists(FilePath))
                    {
                        File.SetAttributes(FilePath, FileAttributes.Normal);
                        File.Delete(FilePath);
                        OnAnyDownloadRemoved();
                    }

                    success = !File.Exists(FilePath);
                    if (success)
                    {
                        MbSize = 0;
                    }
                }
            }
            catch
            {
                success = false;
            }
            return success;
        }
        
    }

    public class author
    {
        public string name;
     
        public string email;
    }
}

