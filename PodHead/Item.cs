﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace PodHead
{

    public delegate void DownloadProgressEvent(Item item, double percent);

    public delegate void DownloadCompleteEvent(Item item);

    public delegate void DownloadRemovedEvent(Item item);

    public delegate void PercentPlayedChangeEvent(Item item);

    public delegate void IsPlayingChangedEvent(Item item);

    public class Item
    {
        private readonly IConfig _config;

        public string Title { get; set; }

        public string Description { get; set; }

        public string Link { get; set; }

        public string Guid { get; set; }

        private string _pubDate;
        public string PubDate
        {
            get
            {
                return _pubDate;
            }
            set
            {
                if (_pubDate != value)
                {
                    _pubDate = value;
                    DateTime pubDateTime;
                    if (DateTime.TryParse(_pubDate, out pubDateTime))
                    {
                        PubDateTime = pubDateTime;
                    }
                }
            }
        }

        public DateTime PubDateTime { get; private set; }

        public DateTime DownloadDate { get; set; }
     
        public ConcurrentList<author> Authors = new ConcurrentList<author>();
        
        public bool Read { get; set; }

        public Subscription ParentSubscription { get; set; }
        
        public int RowNum;

        private int _position;
        public int Position
        {
            get
            {
                return _position;
            }
            set
            {
                if(value != _position)
                {
                    _position = value;
                    OnPercentPlayedChanged();
                }
            }
        }

        public string GetFormattedDurationString()
        {
            return TimeSpan.FromMilliseconds(Duration).ToString(@"hh\:mm\:ss");
        }

        public string GetFormattedPositionString()
        {
            return TimeSpan.FromMilliseconds(Position).ToString(@"hh\:mm\:ss");
        }
        
        /// <summary>
        /// Media duration in Milliseconds.
        /// </summary>
        private int _duration;
        public int Duration
        {
            get
            {
                return _duration;
            }
            set
            {
                if(value != _duration)
                {
                    _duration = value;
                    OnPercentPlayedChanged();
                }
            }
        }

        public double PercentPlayed
        {
            get
            {
                if(Duration == 0)
                {
                    return 0;
                }
                return 100.0 * (double)Position / (double)Duration;
            }
            set
            {
                Position = (int)(value * Duration);
                OnPercentPlayedChanged();
            }
        }

        public static event DownloadCompleteEvent AnyDownloadComplete;

        public event DownloadProgressEvent DownloadProgress;

        public static event DownloadRemovedEvent AnyDownloadRemoved;

        public event PercentPlayedChangeEvent PercentPlayedChanged;

        public event IsPlayingChangedEvent IsPlayingChanged;

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
                return Path.Combine(_config.DownloadFolder, GetCleanFileName() + GetFileType());
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
                
        public bool IsNowPlaying { get; set; }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get { return _isPlaying; }
            set
            {
                if (_isPlaying != value)
                {
                    _isPlaying = value;
                    OnIsPlayingChanged();
                }
            }
        }

		public Item(IConfig config)
		{
			Title = string.Empty;
			Description = string.Empty;
			Link = string.Empty;
			Guid = string.Empty;
			PubDate = string.Empty;
            _config = config;
		}

        public void DownloadFile()
        {
            if (!String.IsNullOrEmpty(Link))
            {
                if (!Directory.Exists(_config.DownloadFolder))
                {
                    Directory.CreateDirectory(_config.DownloadFolder);
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

        protected virtual void OnPercentPlayedChanged()
        {
            var copy = PercentPlayedChanged;
            if (copy != null)
            {
                copy.Invoke(this);
            }
        }

        protected virtual void OnIsPlayingChanged()
        {
            var copy = IsPlayingChanged;
            if(copy != null)
            {
                copy.Invoke(this);
            }
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            MbSize = (int)(e.TotalBytesToReceive / (1024 * 1024));
            string MbString = (e.BytesReceived / (1024 * 1024)).ToString() + "/" + (e.TotalBytesToReceive / (1024 * 1024)).ToString() + "Mb";

            OnDownloadProgress(e.ProgressPercentage);
        }

        private static List<string> fileTypes = new List<string>()
        {
            ".mp3",
            ".wav",
        };

        private const string DefaultFileType = ".mp3";

        private string GetFileType()
        {
            foreach(string fileType in fileTypes)
            {
                if(Link.Contains(fileType))
                {
                    return fileType;
                }
            }
            return DefaultFileType;
        }

        private string GetCleanFileName()
        {
            string filename = Title;
            string cleanFileName = "";
            if (!string.IsNullOrEmpty(filename))
            {
                cleanFileName = filename.Replace(":", "").Replace("\\", "").Replace("/", "");
                cleanFileName = RemoveChars(cleanFileName, Path.GetInvalidFileNameChars());
                cleanFileName = RemoveChars(cleanFileName, Path.GetInvalidPathChars());
            }
            return cleanFileName;
        }

        private string RemoveChars(string input, IEnumerable<char> charsToRemove)
        {
            foreach(char c in charsToRemove)
            {
                input = input.Replace(c.ToString(), string.Empty);
            }
            return input;
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

