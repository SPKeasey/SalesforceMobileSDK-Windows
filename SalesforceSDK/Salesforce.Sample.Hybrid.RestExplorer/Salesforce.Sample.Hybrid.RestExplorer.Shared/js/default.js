﻿// For an introduction to the Blank template, see the following documentation:
// http://go.microsoft.com/fwlink/?LinkID=392286
(function () {
    "use strict";

    var app = WinJS.Application;
    var activation = Windows.ApplicationModel.Activation;
    var authzInProgress = false; 

    function test() {
        var object = window.Salesforce;
        var accounts = Salesforce.SDK.Hybrid.Auth.HybridAccountManager.getAccounts();

        for (i = 0; i < accounts.length; ++i) {
            console.log(accounts[i]);
            console.log(accounts[i].UserId);
        }
    }

    var logout = function(mouseEvent) {
        var rest = Salesforce.SDK.Hybrid.Rest;
        var cm = new rest.ClientManager();
        var client = cm.peekRestClient();
        if (client != null) {
            cm.logout().done(function() {
                location.reload();
            });
        }
    }

    var fetchRecords = function () {
        var soql = 'SELECT Id, Name FROM Contact LIMIT 10';
        var rest = Salesforce.SDK.Hybrid.Rest;
        var cm = new rest.ClientManager();
        var client = cm.peekRestClient();

        var request = rest.RestRequest.getRequestForQuery("v31.0", soql);

        var response = client.sendAsync(request).then(function (data) {
            var users = JSON.parse(data.asString).records;

            var listItemsHtml = document.querySelector('#users');
            for (var i = 0; i < users.length; i++) {
                var li = document.createElement("li");
                var div = document.createElement("div");

                li.setAttribute("class", "table-view-cell");
                div.setAttribute("class", "media-body");
                div.innerHTML = users[i].Name;
                li.appendChild(div);
                listItemsHtml.appendChild(li);
            }
            var buttonli = document.createElement("li");
            var buttondiv = document.createElement("div");
            var logoutButton = document.createElement("button");
            buttonli.setAttribute("class", "table-view-cell");
            buttonli.setAttribute("align", "center");
            buttondiv.setAttribute("class", "media-body");
            logoutButton.addEventListener("click", logout, false);
            logoutButton.innerText = "Logout";
            buttondiv.appendChild(logoutButton);
            buttonli.appendChild(buttondiv);
            listItemsHtml.appendChild(buttonli);
        });
    };

    function sfdcLogin() {
        WinJS.xhr({ url: "data/bootconfig.json" }).done(function complete(response) {
            var auth = Salesforce.SDK.Hybrid.Auth;
            auth.HybridAccountManager.initEncryption();
            var account = auth.HybridAccountManager.getAccount();
            if (account == null) {
                auth.HybridAccountManager.initEncryption();
                var jsonResult = JSON.parse(response.responseText);
                var endUri = new Windows.Foundation.Uri(jsonResult.oauthRedirectURI);
                var options = new auth.LoginOptions("https://test.salesforce.com/", jsonResult.remoteAccessConsumerKey, jsonResult.oauthRedirectURI, jsonResult.oauthScopes);
                var startUriStr = auth.OAuth2.computeAuthorizationUrl(options);
                var startUri = new Windows.Foundation.Uri(startUriStr);
                authzInProgress = true;
                Windows.Security.Authentication.Web.WebAuthenticationBroker.authenticateAsync(
                        Windows.Security.Authentication.Web.WebAuthenticationOptions.none, startUri, endUri)
                    .done(function(result) {
                        var responseResult = new Windows.Foundation.Uri(result.responseData);
                        var authResponse = responseResult.fragment.substring(1);
                        auth.HybridAccountManager.createNewAccount(options, authResponse).then(function(newAccount) {
                            authzInProgress = false;
                            if (newAccount != null) {
                                fetchRecords();
                            } else {
                                sfdcLogin();
                            }
                           
                        });
                    }, function(err) {
                        WinJS.log("Error returned by WebAuth broker: " + err, "Web Authentication SDK Sample", "error");
                        document.getElementById("AnyServiceDebugArea").value += " Error Message: " + err.message + "\r\n";
                        authzInProgress = false;
                    });
            } else {
                auth.OAuth2.refreshAuthToken(account);
                fetchRecords();
            }
        });
    }

    app.onactivated = function (args) {
        if (args.detail.kind === activation.ActivationKind.launch) {
            if (args.detail.previousExecutionState !== activation.ApplicationExecutionState.terminated) {
                // TODO: This application has been newly launched. Initialize
                var p = WinJS.UI.processAll().done(function() {
                    sfdcLogin();
                });
            } else {
                sfdcLogin();
            }
            args.setPromise(WinJS.UI.processAll());
        }
    };

    app.oncheckpoint = function (args) {
        // TODO: This application is about to be suspended. Save any state
        // that needs to persist across suspensions here. You might use the
        // WinJS.Application.sessionState object, which is automatically
        // saved and restored across suspension. If you need to complete an
        // asynchronous operation before your application is suspended, call
        // args.setPromise().
    };

    app.start();
})();