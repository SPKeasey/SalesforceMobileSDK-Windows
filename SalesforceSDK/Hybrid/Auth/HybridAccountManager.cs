﻿using System.Collections.Generic;
using System.Threading.Tasks;
using Windows.Foundation;
using System;
using Windows.Networking.Sockets;
using Salesforce.SDK.Auth;
using Salesforce.SDK.Source.Security;
using Salesforce.SDK.Source.Settings;

namespace Salesforce.SDK.Hybrid.Auth
{
    public sealed class HybridAccountManager
    {
        private static HybridAccountManager _instance = new HybridAccountManager();

        public static HybridAccountManager GetInstance()
        {
            return _instance;
        }

        public static void DeleteAccount()
        {
            SDK.Auth.AccountManager.DeleteAccount();
        }

        public static void InitEncryption()
        {
            if (Encryptor.Settings == null)
            {
                Encryptor.init(new EncryptionSettings(new HmacSHA256KeyGenerator()));
                SDKManager.ResetClientManager();
            }
        }

        public static IDictionary<string, Account> GetAccounts()
        {
            InitEncryption();
            Dictionary<string, Account> accounts = new Dictionary<string, Account>();
            var acMgrAccounts = SDK.Auth.AccountManager.GetAccounts();
            foreach (var key in acMgrAccounts.Keys)
            {
                accounts[key] = Account.FromJson(SDK.Auth.Account.ToJson(acMgrAccounts[key]));
            }
            return accounts;
        }

        public static Account GetAccount()
        {
            InitEncryption();
            var account = SDK.Auth.AccountManager.GetAccount();
            return Account.FromJson(SDK.Auth.Account.ToJson(account));
        }

        public static void WipeAccounts()
        {
            InitEncryption();
            SDK.Auth.AccountManager.WipeAccounts();
        }

        public static IAsyncOperation<Account> CreateNewAccount(LoginOptions loginOptions, string response)
        {
            InitEncryption();
            SDK.Auth.AuthResponse authResponse = SDK.Auth.OAuth2.ParseFragment(response);
            return Task.Run(async () =>
            {
                var account = await SDK.Auth.AccountManager.CreateNewAccount(loginOptions.ConvertToSDKLoginOptions(), authResponse);
                return Account.FromJson(SDK.Auth.Account.ToJson(account));
            }).AsAsyncOperation();
        }

        public static IAsyncOperation<bool> SwitchToAccount(Account account)
        {
            InitEncryption();
            var sdkAccount = (account == null ? null : account.ConvertToSDKAccount());
            return Task.Run(async () => await SDK.Auth.AccountManager.SwitchToAccount(sdkAccount)).AsAsyncOperation();
        }
    }
}
