﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Net;
using System.Data;

namespace PodHead
{
    public enum FeedType
    {
        Atom = 0,
        Rss1 = 1,
        Rss2 = 2,
    }


    public delegate void SubscriptionParsedCompleteEventHandler(Subscription subscription);

    public static class Parser
    {
        public static event SubscriptionParsedCompleteEventHandler SubscriptionParsedComplete;
        

        private static FeedType GetFeedType(string rssString)
        {
            FeedType type = FeedType.Atom;

            var doc = new XmlDocument();
            doc.LoadXml(rssString);
            var rssTags = doc.GetElementsByTagName("rss");
            if (rssTags != null && rssTags.Count > 0)
            {
                string rssVersion = rssTags[0].Attributes["version"].Value;
                if (rssVersion == "1.0")
                {
                    type = FeedType.Rss1;
                }
                if(rssVersion == "2.0")
                {
                    type = FeedType.Rss2;
                }
            }
            
            return type;
        }


        private static string processDescription(string description)
        {
            string newDescrip = "";
            bool tag = false;
            for (int i = 0; i < description.Length; i++)
            {
                if (description[i] == '<')
                {
                    tag = true;
                }
                else if (description[i] == '>')
                {
                    tag = false;
                }
                else if (!tag)
                {
                    newDescrip += description[i];
                }
            }
            return newDescrip;
        }
        

        public static bool LoadSubscriptionAsync(Subscription sub)
        {
            sub.Items.Clear();
            string url = sub.RssLink;
            bool success = true;
            try
            {
                using (var client = new RssWebClient(sub))
                {
                    client.OpenReadCompleted += LoadSubscriptionOpenReadCompleted;
                    client.OpenReadAsync(new Uri(url));
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
                success = false;
            }
            return success;
        }

        public static bool LoadSubscription(Subscription sub, int maxItems)
        {
            sub.Items.Clear();
            string url = sub.RssLink;
            bool success = true;
            try
            {
                Stream rssStream;
                using (var client = new RssWebClient(sub))
                {
                    client.OpenReadCompleted += LoadSubscriptionOpenReadCompleted;
                    rssStream = client.OpenRead(url);
                }
                using (var reader = new StreamReader(rssStream))
                {
                    string rss = reader.ReadToEnd();
                    LoadSubscription(sub, rss, maxItems);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
                success = false;
            }
            return success;
        }

        private static bool LoadSubscription(Subscription sub, string rss, int maxItems)
        {
            bool success = false;
            try
            {
                if (!string.IsNullOrEmpty(rss))
                {
                    FeedType feedType = GetFeedType(rss);
                    switch (feedType)
                    {
                        case FeedType.Atom:
                            success = LoadXMLAtom(sub, rss, maxItems);
                            break;
                        case FeedType.Rss1:
                            success = LoadXMLRSS1_0(sub, rss, maxItems);
                            break;
                        case FeedType.Rss2:
                            success = LoadXMLRSS2_0(sub, rss, maxItems);
                            break;
                    }
                }
                DownloadImage(sub);
                sub.IsLoaded = success;
            }
            catch (Exception e)
            {
                sub.HasErrors = true;
                ErrorLogger.Log(e);
            }
            return success;
        }

        private static void DownloadImage(Subscription sub)
        {
            if (!sub.ImageLoaded)
            {
                DownloadImage(sub.ImageUrl, sub.ImageFilePath);
                sub.ImageLoaded = true;
            }
        }

        private static void DownloadImage(string imageUrl, string imageFilePath)
        {
            
            using (var client = new WebClient())
            {
                client.DownloadFile(new Uri(imageUrl), imageFilePath);
            }
        }

        private static void LoadSubscriptionOpenReadCompleted(object sender, OpenReadCompletedEventArgs e)
        {
            try
            {
                var rss = string.Empty;
                using (StreamReader sr = new StreamReader(e.Result))
                {
                    rss = sr.ReadToEnd();
                }
                var rssWebClient = sender as RssWebClient;
                if (rssWebClient != null)
                {
                    LoadSubscription(rssWebClient.Subscription, rss, rssWebClient.MaxItems);
                }
                OnSubscriptionParsedComplete(rssWebClient.Subscription);
            }
            catch(Exception ex)
            {
                ErrorLogger.Log(ex);
            }
        }

        private static void OnSubscriptionParsedComplete(Subscription subscription)
        {
            var copy = SubscriptionParsedComplete;
            if(copy != null)
            {
                copy.Invoke(subscription);
            }
        }

        private static string GetXmlElementValue(XmlNode parentNode, string elementName)
        {
            string value = string.Empty;

            if (parentNode[elementName] != null)
            {
                value = parentNode[elementName].InnerText;
            }

            return value;
        }

        private static string GetXmlAttribute(XmlNode xmlNode, string attributeName)
        {
            string attribute = string.Empty;
			if (xmlNode != null && xmlNode.Attributes != null && xmlNode.Attributes[attributeName] != null)
            {
                string value = xmlNode.Attributes[attributeName].Value;
                if (!string.IsNullOrEmpty(value))
                {
                    attribute = value;
                }
            }

            return attribute;
        }

        private static bool LoadXMLRSS2_0(Subscription sub, string rss, int maxItems)
        {
            bool success = true;
            try
            {
                var xmlDocument = new XmlDocument();
                var bytes = Encoding.UTF8.GetBytes(rss);
                using (var stream = new MemoryStream(bytes))
                {
                    xmlDocument.Load(stream);
                }
                var channels = xmlDocument.GetElementsByTagName("channel");
                foreach (XmlElement channel in channels)
                {
                    int counter = 0;
                    sub.Title = GetXmlElementValue(channel, "title");
                    sub.SiteLink = GetXmlElementValue(channel, "link");
                    sub.Description = GetXmlElementValue(channel, "description");
                    sub.PubDate = GetXmlElementValue(channel, "pubDate");
                    sub.Ttl = GetXmlElementValue(channel, "ttl");
                    sub.LastBuildDate = GetXmlElementValue(channel, "lastBuildDate");

                    if (string.IsNullOrEmpty(sub.ImageUrl))
                    {
                        var imageNodes = channel.GetElementsByTagName("image");
                        if (imageNodes.Count > 0)
                        {
                            var imageNode = imageNodes[0];
                            sub.ImageUrl = GetXmlElementValue(imageNode, "url");
                        }
                    }

                    var items = channel.GetElementsByTagName("item");
                    foreach(XmlNode item in items)
                    {
                        if (counter++ >= maxItems) { break; }
                        var it = new Item();
                        it.Title = GetXmlElementValue(item, "title");
                        it.Link = GetXmlElementValue(item, "link");

                        if (item["enclosure"] != null)
                        {
                            it.Link = GetXmlAttribute(item["enclosure"], "url");
                        }

                        it.Description = processDescription(GetXmlElementValue(item, "description"));
                        it.Guid = GetXmlElementValue(item, "guid");
                        it.PubDate = GetXmlElementValue(item, "pubDate");
                        it.RowNum = counter;
                        it.ParentSubscription = sub;
                        it.IsLoaded = true;
                        sub.ItemsLoaded = true;
                        sub.Items.Add(it);
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
                success = false;
            }
            sub.HasErrors = !success;
            return success;
        }

        private static bool LoadXMLRSS1_0(Subscription sub, string rss, int maxItems)
        {
            bool success = true;
            try
            {
                var xmlDocument = new XmlDocument();
                var bytes = Encoding.UTF8.GetBytes(rss);
                using (var stream = new MemoryStream(bytes))
                {
                    xmlDocument.Load(stream);
                }
                var channels = xmlDocument.GetElementsByTagName("channel");
                foreach (XmlElement channel in channels)
                {
                    int count = 0;
                    sub.Title = GetXmlElementValue(channel, "title");
                    sub.Description = GetXmlElementValue(channel, "description");
                    sub.SiteLink = GetXmlElementValue(channel, "link");

                    var items = channel.GetElementsByTagName("item");
                        
                    foreach(XmlNode item in items)
                    {
                        if (count++ >= maxItems) { break; }
                        var it = new Item();
                        it.Title = GetXmlElementValue(item, "title");
                        it.Description = GetXmlElementValue(item, "description");
                        it.Link = GetXmlElementValue(item, "link");
                        it.Guid = GetXmlElementValue(item, "guid");
                        it.PubDate = GetXmlElementValue(item, "pubDate");
                        it.RowNum = count;
                        it.ParentSubscription = sub;
                        sub.ItemsLoaded = true;
                        it.IsLoaded = true;
                        sub.Items.Add(it);
                    }

                }
            }
            catch(Exception ex)
            {
                ErrorLogger.Log(ex);
                success = false;                
            }
            sub.HasErrors = !success;
            return success;
        }
        


        private static bool LoadXMLAtom(Subscription sub, string rss, int maxItems)
        {
            bool success = true;
            try
            {
                var doc = new XmlDocument();
                doc.LoadXml(rss);
                var feedNode = doc["feed"];
                sub.Title = GetXmlElementValue(feedNode, "title");
                if (string.IsNullOrEmpty(sub.ImageUrl))
                {
                    sub.ImageUrl = GetXmlElementValue(feedNode, "icon");
                }

                var entries = feedNode.GetElementsByTagName("entry");

                int count = 0;
                foreach (XmlNode entry in entries)
                {
                    if (count++ >= maxItems) { break; }
                    var it = new Item();
                    it.Title = GetXmlElementValue(entry, "title");
                    it.Description = GetXmlElementValue(entry, "summary");
                    it.PubDate = GetXmlElementValue(entry, "updated");
                    
                    it.Link = GetXmlAttribute(entry["link"], "href");
                    if (entry["author"] != null)
                    {
                        foreach (XmlNode authorNode in entry["author"])
                        {
                            var auth = new author();
                            auth.name = GetXmlElementValue(authorNode, "name");
                            auth.email = GetXmlElementValue(authorNode, "email");
                            it.Authors.Add(auth);
                        }
                    }
                    it.RowNum = count;
                    it.ParentSubscription = sub;
                    sub.ItemsLoaded = true;
                    it.IsLoaded = true;
                    sub.Items.Add(it);
                }
            }
            catch (Exception ex)
            {
                ErrorLogger.Log(ex);
                success = false;
            }
            sub.HasErrors = !success;
            return success;
        }
    }
}