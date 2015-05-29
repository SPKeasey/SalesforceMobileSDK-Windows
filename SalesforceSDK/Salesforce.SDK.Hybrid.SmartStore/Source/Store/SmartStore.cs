﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Salesforce.SDK.Hybrid.Auth;
using Salesforce.SDK.Hybrid.SmartStore.Source.Store;
using Account = Salesforce.SDK.Hybrid.Auth.Account;

namespace Salesforce.SDK.Hybrid.SmartStore
{
    public sealed class SmartStore : ISmartStore
    {
        private SDK.SmartStore.Store.SmartStore NativeSmartStore
        {
            get { return SDK.SmartStore.Store.SmartStore.GetSmartStore(); }
        }

        public static SmartStore GetSmartStore()
        {
            var store = SDK.SmartStore.Store.SmartStore.GetSmartStore();
            store.CreateMetaTables();
            var hybridStore = JsonConvert.SerializeObject(store);
            return JsonConvert.DeserializeObject<SmartStore>(hybridStore);
        }

        public static SmartStore GetSmartStore(Account account)
        {
            var store = SDK.SmartStore.Store.SmartStore.GetSmartStore();
            store.CreateMetaTables();
            var hybridStore = JsonConvert.SerializeObject(store);
            return JsonConvert.DeserializeObject<SmartStore>(hybridStore);
        }

        public static SmartStore GetGlobalSmartStore()
        {
            // generate a "global" smartstore
            var store = SDK.SmartStore.Store.SmartStore.GetGlobalSmartStore(); 
            store.CreateMetaTables();
            var hybridStore = JsonConvert.SerializeObject(store);
            return JsonConvert.DeserializeObject<SmartStore>(hybridStore);
        }

        public static string GenerateDatabasePath(Account account)
        {
            DBOpenHelper open = DBOpenHelper.GetOpenHelper(HybridAccountManager.GetAccount());
            return Path.Combine(ApplicationData.Current.LocalFolder.Path, open.DatabaseFile);
        }

        public static IAsyncOperation<bool> HasGlobalSmartStore()
        {
            return Task.Run(async () => await HasSmartStore(null)).AsAsyncOperation<bool>();
        }

        public static IAsyncOperation<bool> HasSmartStore(Account account)
        {
            var accountJson = JsonConvert.SerializeObject(account);
            var sdkAccount = JsonConvert.DeserializeObject<SDK.Auth.Account>(accountJson);
            return SDK.SmartStore.Store.SmartStore.HasSmartStore(sdkAccount).AsAsyncOperation();
        }

        public void ResetDatabase()
        {
            NativeSmartStore.ResetDatabase();
        }

        public void RegisterSoup(String soupName, [ReadOnlyArray()]IndexSpec[] indexSpecs)
        {
            NativeSmartStore.RegisterSoup(soupName, IndexSpec.ConvertToSdkIndexSpecs(indexSpecs));
        }

        public static void DeleteAllDatabases(bool includeGlobal)
        {
            SDK.SmartStore.Store.SmartStore.DeleteAllDatabases(true);
        }

        public void CreateMetaTables()
        {
            NativeSmartStore.CreateMetaTables();
        }

        
        public void ReIndexSoup(string soupName, [ReadOnlyArray()]string[] indexPaths, bool handleTx)
        {
            NativeSmartStore.ReIndexSoup(soupName, indexPaths, handleTx);
        }

        public IndexSpec[] GetSoupIndexSpecs(string soupName)
        {
            return IndexSpec.ConvertToHybridIndexSpecs(NativeSmartStore.GetSoupIndexSpecs(soupName));
        }

        public void ClearSoup(string soupName)
        {
            NativeSmartStore.ClearSoup(soupName);
        }

        public bool HasSoup(string soupName)
        {
            return NativeSmartStore.HasSoup(soupName);
        }

        public void DropSoup(string soupName)
        {
            NativeSmartStore.DropSoup(soupName);
        }

        public void DropAllSoups()
        {
            NativeSmartStore.DropAllSoups();
        }

        public void DropAllSoups(string databasePath)
        {
            SDK.SmartStore.Store.SmartStore.DropAllSoups(databasePath);
        }

        public IList<string> GetAllSoupNames()
        {
            return NativeSmartStore.GetAllSoupNames();
        }

        public string Query(QuerySpec querySpec, int pageIndex)
        {
            return NativeSmartStore.Query(querySpec.SdkQuerySpec, pageIndex).ToString();
        }

        public long CountQuery(QuerySpec querySpec)
        {
            return NativeSmartStore.CountQuery(querySpec.SdkQuerySpec);
        }

        public bool Delete(string soupName, [ReadOnlyArray()]long[] soupEntryIds, Boolean handleTx)
        {
            return NativeSmartStore.Delete(soupName, soupEntryIds, handleTx);
        }

        public object Create(string soupName, string soupElt)
        {
            return NativeSmartStore.Create(soupName, JObject.Parse(soupElt));
        }

        public object Create(string soupName, string soupElt, bool handleTx)
        {
            return NativeSmartStore.Create(soupName, JObject.Parse(soupElt), handleTx);
        }

        public object Upsert(string soupName, string soupElt, string externalIdPath)
        {
            return NativeSmartStore.Upsert(soupName, JObject.Parse(soupElt), externalIdPath);
        }

        public object Upsert(string soupName, string soupElt)
        {
            return NativeSmartStore.Upsert(soupName, JObject.Parse(soupElt));
        }

        public object Upsert(string soupName, string soupElt, string externalIdPath, bool handleTx)
        {
            return NativeSmartStore.Upsert(soupName, JObject.Parse(soupElt), externalIdPath, handleTx);
        }

        public bool BeginDatabaseTransaction()
        {
            return NativeSmartStore.Database.BeginTransaction();
        }

        public bool CommitDatabaseTransaction()
        {
            return NativeSmartStore.Database.CommitTransaction();
        }

        public long LookupSoupEntryId(string soupName, string fieldPath, string fieldValue)
        {
            return NativeSmartStore.LookupSoupEntryId(soupName, fieldPath, fieldValue);
        }

        public object Update(String soupName, string soupElt, long soupEntryId, bool handleTx)
        {
            return NativeSmartStore.Update(soupName, JObject.Parse(soupElt), soupEntryId, handleTx);
        }

        public string Retrieve(string soupName, params long[] soupEntryIds)
        {
            return NativeSmartStore.Retrieve(soupName, soupEntryIds).ToString();
        }

        public static object Project(object soup, string path)
        {
            return SDK.SmartStore.Store.SmartStore.Project((JObject) soup, path);
        }

        public string ConvertSmartSql(string smartSql)
        {
            return NativeSmartStore.ConvertSmartSql(smartSql);
        }

        public static string GetSoupTableName(long soupId)
        {
            return SDK.SmartStore.Store.SmartStore.GetSoupTableName(soupId);
        }

    }
}
