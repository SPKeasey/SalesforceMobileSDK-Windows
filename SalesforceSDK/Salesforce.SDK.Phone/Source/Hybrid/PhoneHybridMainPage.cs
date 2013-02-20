﻿/*
 * Copyright (c) 2013, salesforce.com, inc.
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
using Microsoft.Phone.Controls;
using Salesforce.SDK.Auth;
using Salesforce.SDK.Rest;
using System;
using System.Windows.Navigation;

namespace Salesforce.SDK.Hybrid
{
    /// <summary>
    /// Super class for Windows Phone hybrid application main page
    /// Note: some methods have empty implementations so it can't be used directly
    /// </summary>
    public class PhoneHybridMainPage : PhoneApplicationPage
    {
        private BootConfig _bootConfig;
        private LoginOptions _loginOptions;
        private ClientManager _clientManager;
        private RestClient _client;

        /// <summary>
        /// Concrete hybrid main page page class should override this method to load uri in web view
        /// </summary>
        /// <param name="uri"></param>
        protected virtual void LoadUri(Uri uri) { }

        /// <summary>
        /// Constructor
        /// </summary>
        public PhoneHybridMainPage()
        {
            _bootConfig = BootConfig.GetBootConfig();
            _loginOptions = new LoginOptions("https://test.salesforce.com" /* FIXME once we have a server picker */,
                _bootConfig.ClientId, _bootConfig.CallbackURL, _bootConfig.Scopes);
            _clientManager = new ClientManager(_loginOptions);
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            _client = _clientManager.GetRestClient();
            if (_client != null)
            {
                Uri startPageUri = null;
                if (_bootConfig.IsLocal)
                {
                    startPageUri = new Uri("www/" + _bootConfig.StartPage, UriKind.Relative);
                }
                else
                {
                    startPageUri = new Uri(OAuth2.ComputeFrontDoorUrl(_client.InstanceUrl, _client.AccessToken, _bootConfig.StartPage), UriKind.Absolute);
                }
                LoadUri(startPageUri);
            }
        }
    }

}