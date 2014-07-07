﻿using Salesforce.SDK.App;
using Salesforce.SDK.Auth;
using Salesforce.SDK.Native;
using Salesforce.SDK.Rest;
/*
 * Copyright (c) 2014, salesforce.com, inc.
 * All rights reserved.
 * Redistribution and use of this software in source and binary forms, with or
 * without modification, are permitted provided that the following conditions
 * are met:
 * - Redistributions of source code must retain the above copyright notice, this
 * list of conditions and the following disclaimer.
 * - Redistributions in binary form must reproduce the above copyright notice,
 * this list of conditions and the following disclaimer in the documentation
 * and/or other materials provided with the distribution.
 * - Neither the name of salesforce.com, inc. nor the names of its contributors
 * may be used to endorse or promote products derived from this software without
 * specific prior written permission of salesforce.com, inc.
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
 * ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
 * LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
 * CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
 * SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
 * CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
 * ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
 * POSSIBILITY OF SUCH DAMAGE.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace Salesforce1.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : NativeMainPage
    {

        public const string CurrentPage = "currentPage{0}";
        public const string defaultPage = "/one/one.app";

        public MainPage()
        {
            this.InitializeComponent();
            oneView.FrameContentLoading += oneView_FrameContentLoading;
        }

        void oneView_FrameContentLoading(WebView sender, WebViewContentLoadingEventArgs args)
        {
            SavePage(AccountManager.GetAccount(), args.Uri.ToString());
        }

        /// <summary>
        /// When navigated to, we try to get a RestClient
        /// If we are not already authenticated, this will kick off the login flow
        /// </summary>
        /// <param name="e"></param>
        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {

            Account account = AccountManager.GetAccount();
            if (account != null)
            {
                if (!oneView.CanGoBack)
                {
                    account = await OAuth2.RefreshAuthToken(account);
                    String startPage = OAuth2.ComputeFrontDoorUrl(account.InstanceUrl, account.AccessToken, GetPage(account));
                    oneView.Navigate(new Uri(startPage));
                }
            } else
            {
               base.OnNavigatedTo(e);
            }
        }

        private void SwitchAccount(object sender, RoutedEventArgs e)
        {
            AccountManager.SwitchAccount();
        }

        private async void Logout(object sender, RoutedEventArgs e)
        {
            await SalesforceApplication.GlobalClientManager.Logout();
        }
        
        private void SavePage(Account account, string page)
        {
            if (account != null && page != null && page.Contains(defaultPage))
            {
                var settings = ApplicationData.Current.LocalSettings;
                var key = string.Format(CurrentPage, account.UserId);
                settings.Values[key] = page;
            }
        }

        private string GetPage(Account account)
        {
            string value = "";
            if (account != null)
            {
                value = account.InstanceUrl + defaultPage;
                var settings = ApplicationData.Current.LocalSettings;
                var key = string.Format(CurrentPage, account.UserId);
                if (settings.Values.ContainsKey(key))
                {
                    value = settings.Values[key] as string;
                }
                if (!value.Contains(defaultPage))
                {
                    value = account.InstanceUrl + defaultPage;
                }
            }
            return value;
        }
    }
}
