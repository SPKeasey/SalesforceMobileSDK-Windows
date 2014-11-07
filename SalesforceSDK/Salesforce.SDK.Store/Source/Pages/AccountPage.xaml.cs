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

using System;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel.Resources;
using Windows.Security.Authentication.Web;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Salesforce.SDK.Adaptation;
using Salesforce.SDK.App;
using Salesforce.SDK.Auth;
using Salesforce.SDK.Source.Settings;
using Salesforce.SDK.Strings;

namespace Salesforce.SDK.Source.Pages
{
    /// <summary>
    ///     Phone based page for displaying accounts.
    /// </summary>
    public partial class AccountPage : Page
    {
        private const string SingleUserViewState = "SingleUser";
        private const string MultipleUserViewState = "MultipleUser";
        private const string LoggingUserInViewState = "LoggingUserIn";
        private string _currentState;

        public AccountPage()
        {
            InitializeComponent();
        }

        public Account[] Accounts
        {
            get { return AccountManager.GetAccounts().Values.ToArray(); }
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
            get { return SalesforceApplication.ServerConfiguration.ServerList; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            SetupAccountPage();
        }

        private void SetupAccountPage()
        {
            ResourceLoader loader = ResourceLoader.GetForCurrentView("Salesforce.SDK.Core/Resources");
            SalesforceConfig config = SalesforceApplication.ServerConfiguration;
            bool titleMissing = true;
            if (!String.IsNullOrWhiteSpace(config.ApplicationTitle))
            {
                ApplicationTitle.Visibility = Visibility.Visible;
                ApplicationTitle.Text = config.ApplicationTitle;
                titleMissing = false;
            }
            else
            {
                ApplicationTitle.Visibility = Visibility.Collapsed;
            }

            if (config.LoginBackgroundLogo != null)
            {
                if (ApplicationLogo.Items != null)
                {
                    ApplicationLogo.Items.Clear();
                    ApplicationLogo.Items.Add(config.LoginBackgroundLogo);
                }
                if (titleMissing)
                {
                    var padding = new Thickness(10, 24, 10, 24);
                    ApplicationLogo.Margin = padding;
                }
            }
            var background = new SolidColorBrush(config.LoginBackgroundColor);
            PageRoot.Background = background;
            // ServerFlyoutPanel.Background = background;
            //  AddServerFlyoutPanel.Background = background;
            if (Accounts == null || Accounts.Length == 0)
            {
                _currentState = SingleUserViewState;
                SetLoginBarVisibility(Visibility.Collapsed);
                PincodeManager.WipePincode();
                VisualStateManager.GoToState(this, SingleUserViewState, true);
            }
            else
            {
                _currentState = MultipleUserViewState;
                SetLoginBarVisibility(Visibility.Visible);
                ListTitle.Text = loader.GetString("select_account");
                VisualStateManager.GoToState(this, MultipleUserViewState, true);
            }
            ListboxServers.ItemsSource = Servers;
            AccountsList.ItemsSource = Accounts;
            ServerFlyout.Opening += ServerFlyout_Opening;
            ServerFlyout.Closed += ServerFlyout_Closed;
            AddServerFlyout.Closed += AddServerFlyout_Closed;
            AccountsList.SelectionChanged += accountsList_SelectionChanged;
            ListboxServers.SelectedValue = null;
            HostName.PlaceholderText = LocalizedStrings.GetString("name");
            HostAddress.PlaceholderText = LocalizedStrings.GetString("address");
            AddConnection.Visibility = (SalesforceApplication.ServerConfiguration.AllowNewConnections
                ? Visibility.Visible
                : Visibility.Collapsed);
        }


        private async void accountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await AccountManager.SwitchToAccount(AccountsList.SelectedItem as Account);
            SalesforceApplication.ResetClientManager();
            if (SalesforceApplication.GlobalClientManager.PeekRestClient() != null)
            {
                Frame.Navigate(SalesforceApplication.RootApplicationPage);
                Account account = AccountManager.GetAccount();
                if (account.Policy != null)
                {
                    PincodeManager.LaunchPincodeScreen();
                }
            }
        }

        private void AddServerFlyout_Closed(object sender, object e)
        {
            ServerFlyout.ShowAt(ApplicationLogo);
        }

        private void ServerFlyout_Closed(object sender, object e)
        {
            SetLoginBarVisibility(Visibility.Visible);
        }

        private void ServerFlyout_Opening(object sender, object e)
        {
            SetLoginBarVisibility(Visibility.Collapsed);
        }

        private void ShowServerFlyout(object sender, RoutedEventArgs e)
        {
            if (Servers.Count <= 1 && !SalesforceApplication.ServerConfiguration.AllowNewConnections)
            {
                ListboxServers.SelectedIndex = 0;
                addAccount_Click(sender, e);
            }
            else
            {
                ServerFlyout.Placement = FlyoutPlacementMode.Bottom;
                ServerFlyout.ShowAt(ApplicationLogo);
            }
        }

        private void DisplayErrorDialog(string message)
        {
            MessageContent.Text = message;
            MessageFlyout.ShowAt(ApplicationLogo);
        }

        private async void DoAuthFlow(LoginOptions loginOptions)
        {
            loginOptions.DisplayType = LoginOptions.DefaultStoreDisplayType;
            var loginUri = new Uri(OAuth2.ComputeAuthorizationUrl(loginOptions));
            var callbackUri = new Uri(loginOptions.CallbackUrl);
            OAuth2.ClearCookies(loginOptions);
            WebAuthenticationResult webAuthenticationResult =
                await WebAuthenticationBroker.AuthenticateAsync(WebAuthenticationOptions.None, loginUri, callbackUri);
            if (webAuthenticationResult.ResponseStatus == WebAuthenticationStatus.Success)
            {
                var responseUri = new Uri(webAuthenticationResult.ResponseData);
                if (!String.IsNullOrWhiteSpace(responseUri.Query) &&
                    responseUri.Query.IndexOf("error", StringComparison.CurrentCultureIgnoreCase) >= 0)
                {
                    DisplayErrorDialog(LocalizedStrings.GetString("generic_authentication_error"));
                    SetupAccountPage();
                }
                else
                {
                    AuthResponse authResponse = OAuth2.ParseFragment(responseUri.Fragment.Substring(1));
                    PlatformAdapter.Resolve<IAuthHelper>().EndLoginFlow(loginOptions, authResponse);
                }
            }
            else if (webAuthenticationResult.ResponseStatus == WebAuthenticationStatus.UserCancel)
            {
                SetupAccountPage();
            }
            else
            {
                DisplayErrorDialog(LocalizedStrings.GetString("generic_error"));
                SetupAccountPage();
            }
        }

        private void addConnection_Click(object sender, RoutedEventArgs e)
        {
            HostName.Text = "";
            HostAddress.Text = "";
            AddServerFlyout.ShowAt(ApplicationLogo);
        }

        private void addCustomHostBtn_Click(object sender, RoutedEventArgs e)
        {
            string hname = HostName.Text;
            string haddress = HostAddress.Text;
            var server = new ServerSetting
            {
                ServerHost = haddress,
                ServerName = hname
            };
            SalesforceApplication.ServerConfiguration.AddServer(server);

            ServerFlyout.ShowAt(ApplicationLogo);
        }

        private void cancelCustomHostBtn_Click(object sender, RoutedEventArgs e)
        {
            ServerFlyout.ShowAt(ApplicationLogo);
        }

        private void LoginToSalesforce_OnClick(object sender, RoutedEventArgs e)
        {
            if (ListboxServers.Items != null) StartLoginFlow(ListboxServers.Items[0] as ServerSetting);
        }

        private void ListboxServers_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            StartLoginFlow(ListboxServers.SelectedItem as ServerSetting);
        }

        private void addAccount_Click(object sender, RoutedEventArgs e)
        {
            StartLoginFlow(ListboxServers.SelectedItem as ServerSetting);
        }

        private void StartLoginFlow(ServerSetting server)
        {
            if (server != null)
            {
                VisualStateManager.GoToState(this, LoggingUserInViewState, true);
                SalesforceApplication.ResetClientManager();
                SalesforceConfig config = SalesforceApplication.ServerConfiguration;
                var options = new LoginOptions(server.ServerHost, config.ClientId, config.CallbackUrl, config.Scopes);
                SalesforceConfig.LoginOptions = new LoginOptions(server.ServerHost, config.ClientId, config.CallbackUrl,
                    config.Scopes);
                DoAuthFlow(options);
            }
        }

        private void SetLoginBarVisibility(Visibility state)
        {
            LoginBar.Visibility = MultipleUserViewState.Equals(_currentState) ? state : Visibility.Collapsed;
        }

        private void CloseMessageButton_OnClick(object sender, RoutedEventArgs e)
        {
            MessageFlyout.Hide();
        }
    }
}