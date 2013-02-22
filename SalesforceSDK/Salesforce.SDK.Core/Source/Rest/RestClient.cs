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
using Salesforce.SDK.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Salesforce.SDK.Rest
{
    public delegate void AsyncRequestCallback(RestResponse response);
    public delegate Task<string> AccessTokenProvider();

    public class RestClient
    {
        private AccessTokenProvider _accessTokenProvider;

        private string _instanceUrl;
        public string InstanceUrl
        {
            get
            {
                return _instanceUrl;
            }
        }


        private string _accessToken;
        public string AccessToken
        {
            get
            {
                return _accessToken;
            }
        }

        public RestClient(string instanceUrl, string accessToken, AccessTokenProvider accessTokenProvider)
        {
            _instanceUrl = instanceUrl;
            _accessToken = accessToken;
            _accessTokenProvider = accessTokenProvider;
        }

        public void SendAsync(RestRequest request, AsyncRequestCallback callback)
        {
            SendAsync(request).ContinueWith((t) => callback(t.Result));
        }

        public async Task<RestResponse> SendAsync(RestRequest request)
        {
            return await Task.Factory.StartNew(() => SendSync(request));
        }

        public RestResponse SendSync(RestRequest request)
        {
            return new RestResponse(SendSync(request, true));
        }

        private HttpCall SendSync(RestRequest request, bool retryInvalidToken)
        {
            string url = _instanceUrl + request.Path;
            Dictionary<string, string> headers = new Dictionary<string, string>() {};
            if (_accessToken != null) 
            {
                headers["Authorization"] = "Bearer " + _accessToken;
            }
            if (request.AdditionalHeaders != null)
            {
                headers.Concat(request.AdditionalHeaders);
            }

            HttpCall call = new HttpCall(request.Method, headers, url, request.Body, request.ContentType).Execute().Result;

            if (!call.HasResponse)
            {
                throw call.Error;
            }

            if (call.StatusCode == HttpStatusCode.Unauthorized)
            {
                if (retryInvalidToken && _accessTokenProvider != null)
                {
                    string newAccessToken = _accessTokenProvider().Result;
                    if (newAccessToken != null)
                    {
                        _accessToken = newAccessToken;
                        call = SendSync(request, false);
                    }
                }
            }

            // Done
            return call;
        }

    }
}
