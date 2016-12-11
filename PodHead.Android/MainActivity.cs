﻿using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using PodHead;
using PodHead;

namespace PodHead.Android
{
    [Activity(Label = "PodHead", MainLauncher = true, Icon = "@drawable/icon")]
	public class MainActivity : global::Xamarin.Forms.Platform.Android.FormsApplicationActivity
    {
        public static Activity ActivityContext;

        protected override void OnCreate(Bundle bundle)
        {
            base.OnCreate(bundle);
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            AndroidEnvironment.UnhandledExceptionRaiser += AndroidEnvironment_UnhandledExceptionRaiser;
            ActivityContext = this;

			global::Xamarin.Forms.Forms.Init (this, bundle);
            
			LoadApplication (new App());
        }

        private void AndroidEnvironment_UnhandledExceptionRaiser(object sender, RaiseThrowableEventArgs e)
        {
            ErrorLogger.Log(e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            ErrorLogger.Log(e.ExceptionObject as Exception);
        }

        protected override void OnStop ()
		{
			base.OnStop ();
			Feeds.Instance.Save(RSSConfig.ConfigFileName);
		}

        protected override void OnSaveInstanceState(Bundle outState)
        {
            base.OnSaveInstanceState(outState);
            Feeds.Instance.Save(RSSConfig.ConfigFileName);
        }
    }
}

