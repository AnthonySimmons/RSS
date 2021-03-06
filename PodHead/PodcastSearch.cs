﻿using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PodHead
{
    public delegate void SearchResultsReceivedEventHandler(List<Subscription> subscriptions);
        

    public class PodcastSearch
    {
        public event SearchResultsReceivedEventHandler SearchResultReceived;

        public event ErrorEventHandler ErrorEncountered;

        //https://itunes.apple.com/search?&term=bill+burr&media=podcast&entity=podcast&limit=10

        private const string iTunesSearchUrlFormat = @"https://itunes.apple.com/search?&term={0}&media=podcast&entity=podcast&limit={1}";

        public static int Limit = 10;

        private static PodcastSearch _instance;

        private static object _instanceLock = new object();

        private readonly IConfig _config;

        private readonly Parser _parser;

        private readonly ErrorLogger _errorLogger;

        public ConcurrentList<Subscription> Results = new ConcurrentList<Subscription>();

        public static PodcastSearch Get(IConfig config, Parser parser)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    _instance = new PodcastSearch(config, parser);
                }
            }
            return _instance;
        }


        private PodcastSearch(IConfig config, Parser parser)
        {
            _config = config;
            _parser = parser;
            _errorLogger = ErrorLogger.Get(_config);
        }

        public void SearchAsync(string searchTerm)
        {
            try
            {
                var searchUrl = GetSearchUrl(searchTerm);
                using (var client = new WebClient())
                {
                    client.OpenReadCompleted += Client_OpenReadCompleted;
                    client.OpenReadAsync(new Uri(searchUrl));
                }
            }
            catch (Exception ex)
            {
                _errorLogger.Log(ex);
                OnErrorEncountered(ex.Message);
            }
        }
        
		private void OnErrorEncountered(string message)
		{
            ErrorEncountered?.Invoke(message);
        }

        private void Client_OpenReadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            var json = string.Empty;
            using (var reader = new StreamReader(e.Result))
            {
                json = reader.ReadToEnd();
            }

            var subscriptions = PodcastCharts.DeserializeSubscriptions(json, _config, _parser);
            Results.Clear();
            Results.AddRange(subscriptions);
     
			OnSearchResultsReceived(subscriptions);
        }

		private void OnSearchResultsReceived(List<Subscription> subscriptions)
		{
            SearchResultReceived?.Invoke(subscriptions);
        }

        private static string GetSearchUrl(string searchTerm)
        {
            return string.Format(iTunesSearchUrlFormat, searchTerm, Limit);
        }
    }
}
