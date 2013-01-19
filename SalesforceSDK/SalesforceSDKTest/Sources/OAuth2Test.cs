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
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using Salesforce.WinSDK.Net;
using System;
using System.Collections.Generic;
using System.Net;

namespace Salesforce.WinSDK.Auth
{
    [TestClass]
    public class OAuth2Test
    {
        [TestMethod]
        public void testGetAuthorizationUrl()
        {
            String loginServer = "https://login.salesforce.com";
            String clientId = "TEST_CLIENT_ID";
            String callbackUrl = "test://sfdc";
            String[] scopes = { "web", "api" };

            String expectedUrl = "https://login.salesforce.com/services/oauth2/authorize?display=mobile&response_type=token&client_id=TEST_CLIENT_ID&redirect_uri=test://sfdc&scope=web%20api%20refresh_token";
            String actualUrl = OAuth2.getAuthorizationUrl(loginServer, clientId, callbackUrl, scopes);
            Assert.AreEqual(expectedUrl, actualUrl, "Wrong authorization url");
        }

        [TestMethod]
        public void testRefreshAuthToken()
        {
            // Try describe without being authenticated, expect 401
            Assert.AreEqual(HttpStatusCode.Unauthorized, doDescribe(null));

            // Get auth token (through refresh)
            String loginServer = "https://test.salesforce.com";
            String clientId = "3MVG92.uWdyphVj4bnolD7yuIpCQsNgddWtqRND3faxrv9uKnbj47H4RkwheHA2lKY4cBusvDVp0M6gdGE8hp";
            String refreshToken = "5Aep861_OKMvio5gy9sGt9Z9mdt62xXK.9ugif6nZJYknXeANTICBf4ityN9j6YDgHjFvbzu6FTUQ==";
            RefreshResponse refreshResponse = OAuth2.refreshAuthToken(loginServer, clientId, refreshToken).Result;

            // Try describe again, expect 200
            Assert.AreEqual(HttpStatusCode.OK, doDescribe(refreshResponse.AccessToken));
        }


        private HttpStatusCode doDescribe(String authToken)
        {
            String instanceServer = "https://tapp0.salesforce.com";
            String describeAccountPath = "/services/data/v26.0/sobjects/Account/describe";
            Dictionary<String, String> headers = (authToken == null ? null : new Dictionary<string, string> { { "Authorization", "Bearer " + authToken }});
            return HttpCall.createGet(headers, instanceServer + describeAccountPath).execute().Result.StatusCode;
        }
    
    }
}
