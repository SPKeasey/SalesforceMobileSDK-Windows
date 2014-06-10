﻿/*
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
using Salesforce.SDK.Adaptation;
using Salesforce.SDK.App;
using Salesforce.SDK.Auth;
using Salesforce.SDK.Source.Settings;
using System;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using System.Linq;
using Salesforce.SDK.Strings;
using System.Collections.ObjectModel;

namespace Salesforce.SDK.Source.Pages
{
    /// <summary>
    /// Phone based page for displaying accounts. 
    /// </summary>
    public partial class AccountPage : Page, IWebAuthenticationContinuable
    {
        public Account[] Accounts
        {
            get
            {
                return AccountManager.GetAccounts().Values.ToArray();
            }
        }

        public Account CurrentAccount
        {
            get
            {
                Account account = AccountManager.GetAccount();
                return account;
            }
        }

        public ObservableCollection<ServerSetting> Servers
        {
            get
            {
                return SalesforceApplication.ServerConfiguration.ServerList;
            }
        }

        public ServerSetting SelectedServer
        {
            get
            {
                return SalesforceApplication.ServerConfiguration.Server;
            }
        }

        private bool AddServerFlyoutShowing;

        public AccountPage()
        {
            this.InitializeComponent();
            ResourceLoader loader = ResourceLoader.GetForCurrentView("Salesforce.SDK.Core/Resources");
            // applicationTitle.Text = loader.GetString("application_title");
            if (Accounts == null || Accounts.Length == 0)
            {
                string no = loader.GetString("no_accounts");
                listTitle.Text = loader.GetString("no_accounts");
            }
            else
            {
                listTitle.Text = loader.GetString("select_account");
            }
            listboxServers.ItemsSource = Servers;
            listboxServers.SelectedItem = SelectedServer;
            accountsList.ItemsSource = Accounts;
            ServerFlyout.Opening += ServerFlyout_Opening;
            ServerFlyout.Closed += ServerFlyout_Closed;
            AddServerFlyout.Closed += AddServerFlyout_Closed;
            accountsList.SelectionChanged += accountsList_SelectionChanged;
            hostName.PlaceholderText = LocalizedStrings.GetString("name");
            hostAddress.PlaceholderText = LocalizedStrings.GetString("address");
            addConnection.Visibility = (SalesforceApplication.ServerConfiguration.AllowNewConnections ? Windows.UI.Xaml.Visibility.Visible : Windows.UI.Xaml.Visibility.Collapsed);
        }

        void accountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AccountManager.SwitchToAccount(accountsList.SelectedItem as Account);
            SalesforceApplication.ResetClientManager();
            if (SalesforceApplication.GlobalClientManager.PeekRestClient() != null)
            {
                Frame.Navigate(SalesforceApplication.RootApplicationPage);
            }
        }

        void AddServerFlyout_Closed(object sender, object e)
        {
            ServerFlyout.ShowAt(applicationTitle);
            AddServerFlyoutShowing = false;
        }

        void ServerFlyout_Closed(object sender, object e)
        {
            if (!AddServerFlyoutShowing)
                loginBar.Visibility = Windows.UI.Xaml.Visibility.Visible;
        }

        void ServerFlyout_Opening(object sender, object e)
        {
            loginBar.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
        }

        void ShowServerFlyout(object sender, RoutedEventArgs e)
        {
            if (Servers.Count <= 1 && !SalesforceApplication.ServerConfiguration.AllowNewConnections)
            {
                addAccount_Click(sender, e);
            }
            else
            {
                ServerFlyout.Placement = FlyoutPlacementMode.Bottom;
                ServerFlyout.ShowAt(applicationTitle);
            }
        }

        public void StartLoginFlow(LoginOptions loginOptions)
        {
            Uri loginUri = new Uri(OAuth2.ComputeAuthorizationUrl(loginOptions));
            Uri callbackUri = new Uri(loginOptions.CallbackUrl);
            OAuth2.ClearCookies(loginOptions);
            WebAuthenticationBroker.AuthenticateAndContinue(loginUri, callbackUri, null, WebAuthenticationOptions.None);
        }


        public void ContinueWebAuthentication(WebAuthenticationBrokerContinuationEventArgs args)
        {
            var webResult = args.WebAuthenticationResult;
            if (webResult.ResponseStatus == WebAuthenticationStatus.Success)
            {
                Uri responseUri = new Uri(webResult.ResponseData.ToString());
                if (!responseUri.Query.Contains("error="))
                {
                    AuthResponse authResponse = OAuth2.ParseFragment(responseUri.Fragment.Substring(1));
                    PlatformAdapter.Resolve<IAuthHelper>().EndLoginFlow(SalesforceConfig.LoginOptions, authResponse);
                }
            }
        }

        private void addConnection_Click(object sender, RoutedEventArgs e)
        {
            AddServerFlyoutShowing = true;
            hostName.Text = "";
            hostAddress.Text = "";
            AddServerFlyout.ShowAt(applicationTitle);
        }

        private void addAccount_Click(object sender, RoutedEventArgs e)
        {
            SalesforceApplication.ServerConfiguration.SetSelectedServer(listboxServers.SelectedIndex);
            SalesforceApplication.ResetClientManager();
            ServerSetting server = listboxServers.SelectedItem as ServerSetting;
            SalesforceConfig config = SalesforceApplication.ServerConfiguration;
            LoginOptions options = new LoginOptions(server.ServerHost, config.ClientId, config.CallbackUrl, config.Scopes);
            StartLoginFlow(options);
        }

        private void addCustomHostBtn_Click(object sender, RoutedEventArgs e)
        {
            string hname = hostName.Text;
            string haddress = hostAddress.Text;
            ServerSetting server = new ServerSetting()
            {
                ServerHost = haddress,
                ServerName = hname
            };
            SalesforceApplication.ServerConfiguration.AddServer(server);

            ServerFlyout.ShowAt(applicationTitle);
        }

        private void cancelCustomHostBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerFlyout.ShowAt(applicationTitle);
        }


    }
}
