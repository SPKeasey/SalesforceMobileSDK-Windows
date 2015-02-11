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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.Web.Http;
using Newtonsoft.Json.Linq;
using Salesforce.SDK.Auth;
using Salesforce.SDK.Rest;
using Salesforce.SDK.SmartStore.Store;
using Salesforce.SDK.SmartSync.Model;
using Salesforce.SDK.SmartSync.Util;

namespace Salesforce.SDK.SmartSync.Manager
{
    public class SyncManager
    {
        public const int PageSize = 2000;
        private const int Unchanged = -1;
        public const string Local = "__local__";
        public const string LocallyCreated = "__locally_created__";
        public const string LocallyUpdated = "__locally_updated__";
        public const string LocallyDeleted = "__locally_deleted__";

        private static volatile Dictionary<string, SyncManager> _instances;
        private static readonly object Synclock = new Object();
        private readonly string _apiVersion;
        private readonly RestClient _restClient;
        private readonly SmartStore.Store.SmartStore _smartStore;

        private SyncManager(Account account, string communityId)
        {
            _smartStore = new SmartStore.Store.SmartStore();
            _restClient = new RestClient(account.InstanceUrl, account.AccessToken,
                async () =>
                {
                    account = AccountManager.GetAccount();
                    AuthResponse authResponse =
                        await OAuth2.RefreshAuthTokenRequest(account.GetLoginOptions(), account.RefreshToken);
                    account.AccessToken = authResponse.AccessToken;
                    return account.AccessToken;
                }
                );
            _apiVersion = ApiVersionStrings.VersionNumber;
            SyncState.SetupSyncsSoupIfNeeded(_smartStore);
        }

        /// <summary>
        ///     Returns the instance of this class associated with this user and community.
        /// </summary>
        /// <param name="account"></param>
        /// <param name="communityId"></param>
        /// <returns></returns>
        public static SyncManager GetInstance(Account account, string communityId = null)
        {
            if (account == null)
            {
                account = AccountManager.GetAccount();
            }
            if (account == null)
            {
                return null;
            }
            string uniqueId = Constants.GenerateAccountCommunityId(account, communityId);
            lock (Synclock)
            {
                SyncManager instance = null;
                if (_instances != null)
                {
                    if (_instances.TryGetValue(uniqueId, out instance))
                    {
                        SyncState.SetupSyncsSoupIfNeeded(instance._smartStore);
                        return instance;
                    }
                    instance = new SyncManager(account, communityId);
                    _instances.Add(uniqueId, instance);
                }
                else
                {
                    _instances = new Dictionary<string, SyncManager>();
                    instance = new SyncManager(account, communityId);
                    _instances.Add(uniqueId, instance);
                }
                SyncState.SetupSyncsSoupIfNeeded(instance._smartStore);
                return instance;
            }
        }

        /// <summary>
        ///     Resets the Sync manager associated with this user and community.
        /// </summary>
        /// <param name="account"></param>
        /// <param name="communityId"></param>
        public static void Reset(Account account, string communityId = null)
        {
            if (account == null)
            {
                account = AccountManager.GetAccount();
            }
            if (account != null)
            {
                lock (Synclock)
                {
                    SyncManager instance = GetInstance(account, communityId);
                    if (instance == null) return;
                    _instances.Remove(Constants.GenerateAccountCommunityId(account, communityId));
                }
            }
        }

        /// <summary>
        ///     Get details of a sync state.
        /// </summary>
        /// <param name="syncId"></param>
        /// <returns></returns>
        public SyncState GetSyncStatus(long syncId)
        {
            return SyncState.ById(_smartStore, syncId);
        }

        /// <summary>
        ///     Create and run a sync down.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="soupName"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public SyncState SyncDown(SyncTarget target, string soupName, Action<SyncState> callback)
        {
            SyncState sync = SyncState.CreateSyncDown(_smartStore, target, soupName);
            RunSync(sync, callback);
            return sync;
        }

        /// <summary>
        ///     Create and run a sync up.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="soupName"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public SyncState SyncUp(SyncOptions options, string soupName, Action<SyncState> callback)
        {
            SyncState sync = SyncState.CreateSyncUp(_smartStore, options, soupName);
            RunSync(sync, callback);
            return sync;
        }

        public SyncState ReSync(long syncId, Action<SyncState> callback)
        {
            SyncState sync = SyncState.ById(_smartStore, syncId);
            if (sync == null)
            {
                throw new SmartStoreException("Cannot run ReSync:" + syncId + ": no sync found");
            }
            if (sync.SyncType != SyncState.SyncTypes.SyncDown)
            {
                throw new SmartStoreException("Cannot run ReSync:" + syncId + ": wrong type: " + sync.SyncType);
            }
            if (sync.Target.QueryType != SyncTarget.QueryTypes.Soql)
            {
                throw new SmartStoreException("Cannot run ReSync:" + syncId + ": wrong query type: " +
                                              sync.Target.QueryType);
            }
            if (sync.Status != SyncState.SyncStatusTypes.Done)
            {
                throw new SmartStoreException("Cannot run ReSync:" + syncId + ": not done: " + sync.Status);
            }
            RunSync(sync, callback);
            return sync;
        }

        public async void RunSync(SyncState sync, Action<SyncState> callback)
        {
            UpdateSync(sync, SyncState.SyncStatusTypes.Running, 0, callback);
            try
            {
                switch (sync.SyncType)
                {
                    case SyncState.SyncTypes.SyncDown:
                        await SyncDown(sync, callback);
                        break;
                    case SyncState.SyncTypes.SyncUp:
                        await SyncUp(sync, callback);
                        break;
                }
                UpdateSync(sync, SyncState.SyncStatusTypes.Done, 100, callback);
            }
            catch (Exception)
            {
                Debug.WriteLine("SmartSyncManager:runSync, Error during sync: " + sync.Id);
                UpdateSync(sync, SyncState.SyncStatusTypes.Failed, Unchanged, callback);
            }
        }

        private async Task SyncUp(SyncState sync, Action<SyncState> callback)
        {
            if (sync == null)
                throw new SmartStoreException("SyncState sync was null");
            HashSet<string> dirtyRecordIds = GetDirtyRecordIds(sync.SoupName, SmartStore.Store.SmartStore.SoupEntryId);
            int totalSize = dirtyRecordIds.Count;
            sync.TotalSize = totalSize;
            const int i = 0;
            foreach (
                JObject record in
                    dirtyRecordIds.Select(
                        id => _smartStore.Retrieve(sync.SoupName, long.Parse(id))[0].ToObject<JObject>()))
            {
                await SyncUpOneRecord(sync.SoupName, sync.Options.FieldList, record, sync.MergeMode);

                int progress = (i + 1)*100/totalSize;
                if (progress < 100)
                {
                    UpdateSync(sync, SyncState.SyncStatusTypes.Running, progress, callback);
                }
            }
        }

        private async Task<bool> IsNewerThanServer(string objectType, string objectId, long lastModifiedDate)
        {
            long serverLastModified;
            
            // build query
            var builder = SOQLBuilder.GetInstanceWithFields(Constants.LastModifiedDate);
            builder.From(objectType);
            builder.Where(Constants.Id + " = '" + objectId + "'");
            var query = builder.Build();

            // make async call
            var lastModResponse = await _restClient.SendAsync(RestRequest.GetRequestForQuery(_apiVersion, query));

            // validation of response
            if (lastModResponse == null || !lastModResponse.Success) return false;
            var responseJson = lastModResponse.AsJObject;
            if (responseJson == null) return false;
            
            // obtain records list
            var records = responseJson.ExtractValue<JArray>("records");
            if (records == null || records.Count <= 0) return false;
            var obj = records[0].ToObject<JObject>();
            if (obj == null) return false;
            
            // check if LastModifiedDate exists
            DateTime lastModified = obj.ExtractValue<DateTime>(Constants.LastModifiedDate);
            serverLastModified = lastModified.Ticks;
            return serverLastModified <= lastModifiedDate;
        }

        private async Task<bool> SyncUpOneRecord(string soupName, List<string> fieldList, JObject record, SyncState.MergeModeOptions mergeMode)
        {
            var action = SyncAction.None;
            if (record.ExtractValue<bool>(LocallyDeleted))
                action = SyncAction.Delete;
            else if (record.ExtractValue<bool>(LocallyCreated))
                action = SyncAction.Create;
            else if (record.ExtractValue<bool>(LocallyUpdated))
                action = SyncAction.Update;
            if (SyncAction.None == action)
            {
                // nothing to do for this record
                return true;
            }

            // getting type and id

            string objectType = SmartStore.Store.SmartStore.Project(record, Constants.SobjectType).ToString();
            var objectId = record.ExtractValue<string>(Constants.Id);
            long lastModifiedDate = record.ExtractValue<long>(SmartStore.Store.SmartStore.SoupLastModifiedDate);

            /*
             * Check if we're attempting to update a record that has been updated on the server after the client update.
             * If merge mode passed in tells us to leave the record alone, we will do nothing and return here.
             */
            if (SyncState.MergeModeOptions.LeaveIfChanged == mergeMode &&
                (action == SyncAction.Update || action == SyncAction.Delete))
            {
                var isNewer = await IsNewerThanServer(objectType, objectId, lastModifiedDate);
                if (isNewer) return true;
            }

            var fields = new Dictionary<string, object>();
            if (SyncAction.Create == action || SyncAction.Update == action)
            {
                foreach (
                    string fieldName in
                        fieldList.Where(fieldName => !Constants.Id.Equals(fieldName, StringComparison.CurrentCulture)))
                {
                    fields.Add(fieldName, record[fieldName]);
                }
            }

            RestRequest request = null;

            switch (action)
            {
                case SyncAction.Create:
                    request = RestRequest.GetRequestForCreate(_apiVersion, objectType, fields);
                    break;
                case SyncAction.Delete:
                    request = RestRequest.GetRequestForDelete(_apiVersion, objectType, objectId);
                    break;
                case SyncAction.Update:
                    request = RestRequest.GetRequestForUpdate(_apiVersion, objectType, objectId, fields);
                    break;
            }

            RestResponse response = await _restClient.SendAsync(request);

            // don't continue if not successful
            if (!response.Success) return false;

            // delete or update the record
            if (SyncAction.Create == action)
            {
                record[Constants.Id] = response.AsJObject.ExtractValue<string>(Constants.Lid);
            }

            record[Local] = false;
            record[LocallyCreated] = false;
            record[LocallyUpdated] = false;
            record[LocallyUpdated] = false;

            if (SyncAction.Delete == action)
            {
                _smartStore.Delete(soupName,
                    new[] {record.ExtractValue<long>(SmartStore.Store.SmartStore.SoupEntryId)}, false);
            }
            else
            {
                _smartStore.Update(soupName, record,
                    record.ExtractValue<long>(SmartStore.Store.SmartStore.SoupEntryId), false);
            }
            return false;
        }

        private async Task<bool> SyncDown(SyncState sync, Action<SyncState> callback)
        {
            switch (sync.Target.QueryType)
            {
                case SyncTarget.QueryTypes.Mru:
                    await SyncDownMru(sync, callback);
                    break;
                case SyncTarget.QueryTypes.Soql:
                    await SyncDownSoql(sync, callback);
                    break;
                case SyncTarget.QueryTypes.Sosl:
                    await SyncDownSosl(sync, callback);
                    break;
            }
            return true;
        }

        private async Task<int> SyncDownMru(SyncState sync, Action<SyncState> callback)
        {
            SyncTarget target = sync.Target;
            // Get recent items ids from server
            SyncState.MergeModeOptions mergeMode = sync.MergeMode;
            RestRequest request = RestRequest.GetRequestForMetadata(_apiVersion, target.ObjectType);
            RestResponse response = await _restClient.SendAsync(request);
            List<string> recentItems = Pluck<string>(response.AsJObject.ExtractValue<JArray>(Constants.RecentItems),
                Constants.Id);

            // Building SOQL query to get requested at
            string soql =
                SOQLBuilder.GetInstanceWithFields(target.FieldList.ToArray())
                    .From(target.ObjectType)
                    .Where("Id IN ('" + String.Join("', '", recentItems) + "')")
                    .Build();

            // Get recent items attributes from server
            request = RestRequest.GetRequestForQuery(_apiVersion, soql);
            response = await _restClient.SendAsync(request);
            JObject responseJson = response.AsJObject;
            var records = responseJson.ExtractValue<JArray>(Constants.Records);
            int totalSize = records.Count;
            sync.TotalSize = totalSize;
            // Save to smartstore
            UpdateSync(sync, SyncState.SyncStatusTypes.Running, 0, callback);
            if (totalSize > 0)
            {
                SaveRecordsToSmartStore(sync.SoupName, records, sync.MergeMode);
            }
            return totalSize;
        }

        private async Task<bool> SyncDownSoql(SyncState sync, Action<SyncState> callback)
        {
            string soupName = sync.SoupName;
            SyncTarget target = sync.Target;
            string query = target.Query;
            long maxTimeStamp = sync.MaxTimeStamp;
            query = AddFilterForReSync(query, maxTimeStamp);
            RestRequest request = RestRequest.GetRequestForQuery(_apiVersion, query);

            // Call server
            RestResponse response;
            try
            {
                response = await _restClient.SendAsync(request);
            }
            catch (Exception)
            {
                UpdateSync(sync, SyncState.SyncStatusTypes.Failed, Unchanged, callback);
                return false;
            }
            JObject responseJson = response.AsJObject;

            int countSaved = 0;
            var totalSize = responseJson.ExtractValue<int>(Constants.TotalSize);
            sync.TotalSize = totalSize;
            UpdateSync(sync, SyncState.SyncStatusTypes.Running, 0, callback);

            do
            {
                var records = responseJson.ExtractValue<JArray>(Constants.Records);
                // Save to smartstore
                SaveRecordsToSmartStore(soupName, records, sync.MergeMode);
                countSaved += records.Count;
                maxTimeStamp = Math.Max(maxTimeStamp, GetMaxTimeStamp(records));
                // Update sync status
                sync.MaxTimeStamp = maxTimeStamp;
                if (countSaved < totalSize)
                {
                    UpdateSync(sync, SyncState.SyncStatusTypes.Running, countSaved*100/totalSize, callback);
                }


                // Fetch next records if any
                var nextRecordsUrl = responseJson.ExtractValue<string>(Constants.NextRecordsUrl);
                responseJson = null;
                if (!String.IsNullOrWhiteSpace(nextRecordsUrl))
                {
                    RestResponse result = await _restClient.SendAsync(HttpMethod.Get, nextRecordsUrl);
                    if (result != null)
                    {
                        responseJson = result.AsJObject;
                    }
                }
            } while (responseJson != null);
            return true;
        }

        private async Task<int> SyncDownSosl(SyncState sync, Action<SyncState> callback)
        {
            SyncTarget target = sync.Target;
            RestRequest request = RestRequest.GetRequestForSearch(_apiVersion, target.Query);

            // Call server
            RestResponse response = await _restClient.SendAsync(request);

            // Parse response
            JArray records = response.AsJArray;
            int totalSize = records.Count;
            sync.TotalSize = totalSize;
            // Save to smartstore
            UpdateSync(sync, SyncState.SyncStatusTypes.Running, 0, callback);
            if (totalSize > 0)
            {
                SaveRecordsToSmartStore(sync.SoupName, records, sync.MergeMode);
            }
            return totalSize;
        }

        private HashSet<string> GetDirtyRecordIds(string soupName, string idField)
        {
            var idsToSkip = new HashSet<string>();
            string dirtyRecordsSql = string.Format("SELECT {{{0}:{1}}} FROM {{{2}}} WHERE {{{3}:{4}}} = 'True'",
                soupName,
                idField, soupName, soupName, Local);
            QuerySpec smartQuerySpec = QuerySpec.BuildSmartQuerySpec(dirtyRecordsSql, PageSize);
            bool hasMore = true;
            for (int pageIndex = 0; hasMore; pageIndex++)
            {
                JArray results = _smartStore.Query(smartQuerySpec, pageIndex);
                hasMore = (results.Count == PageSize);
                idsToSkip.UnionWith(ToSet(results));
            }
            return idsToSkip;
        }

        private static List<T> Pluck<T>(IEnumerable<JToken> jArray, string key)
        {
            return jArray.Select(t => t.ToObject<JObject>().Value<T>(key)).ToList();
        }

        private HashSet<string> ToSet(JArray jsonArray)
        {
            var set = new HashSet<String>();
            List<string> list = jsonArray.Select(t => t.ToObject<JArray>()[0].Value<string>()).ToList();
            set.UnionWith(list);
            return set;
        }

        private void SaveRecordsToSmartStore(string soupName, IEnumerable<JToken> records,
            SyncState.MergeModeOptions mergeMode)
        {
            _smartStore.Database.BeginTransaction();
            HashSet<string> idsToSkip = null;

            if (SyncState.MergeModeOptions.LeaveIfChanged == mergeMode)
            {
                idsToSkip = GetDirtyRecordIds(soupName, Constants.Id);
            }

            foreach (JObject record in records.Select(t => t.ToObject<JObject>()))
            {
                // Skip if LeaveIfChanged and id is in dirty list
                if (idsToSkip != null && SyncState.MergeModeOptions.LeaveIfChanged == mergeMode)
                {
                    var id = record.ExtractValue<string>(Constants.Id);
                    if (!String.IsNullOrWhiteSpace(id) && idsToSkip.Contains(id))
                    {
                        continue; // don't write over dirty record
                    }
                }

                // Save
                record[Local] = false;
                record[LocallyCreated] = false;
                record[LocallyUpdated] = false;
                record[LocallyUpdated] = false;
                _smartStore.Upsert(soupName, record, Constants.Id, false);
            }
            _smartStore.Database.CommitTransaction();
        }

        private void UpdateSync(SyncState sync, SyncState.SyncStatusTypes status, int progress,
            Action<SyncState> callback)
        {
            if (sync == null)
                return;
            sync.Status = status;
            if (progress != Unchanged)
            {
                sync.Progress = progress;
            }
            sync.Save(_smartStore);
            if (callback != null)
            {
                callback(sync);
            }
        }

        private string AddFilterForReSync(string query, long maxTimeStamp)
        {
            if (maxTimeStamp != Unchanged)
            {
                string extraPredicate = Constants.SystemModstamp + " > " +
                                        new DateTime(maxTimeStamp, DateTimeKind.Utc).ToString("o");
                if (query.Contains(" where "))
                {
                    var reg = new Regex("( where )");
                    query = reg.Replace(query, "$1 where " + extraPredicate + " and ", 1);
                }
                else
                {
                    string pred = "$1 where " + extraPredicate;
                    var reg = new Regex("( from[ ]+[^ ]*)");
                    query = reg.Replace(query, pred, 1);
                }
            }
            return query;
        }

        private long GetMaxTimeStamp(JArray jArray)
        {
            long maxTimeStamp = Unchanged;
            foreach (JToken t in jArray)
            {
                var jObj = t.ToObject<JObject>();
                if (jObj != null)
                {
                    var timeStampStr = jObj.ExtractValue<string>(Constants.SystemModstamp);
                    if (String.IsNullOrWhiteSpace(timeStampStr))
                    {
                        maxTimeStamp = Unchanged;
                        break;
                    }
                    try
                    {
                        long timeStamp = DateTime.Parse(timeStampStr).Ticks / TimeSpan.TicksPerMillisecond;
                        maxTimeStamp = Math.Max(timeStamp, maxTimeStamp);
                    }
                    catch (Exception)
                    {
                        Debug.WriteLine("SmartSync.GetMaxTimeStamp could not parse systemModstamp");
                        maxTimeStamp = Unchanged;
                        break;
                    }
                }
            }
            return maxTimeStamp;
        }

        private string ReplaceFirst(string text, string search, string replace)
        {
            if (String.IsNullOrWhiteSpace(text) ||
                String.IsNullOrWhiteSpace(search) ||
                String.IsNullOrWhiteSpace(replace))
            {
                return text;
            }
            int pos = text.IndexOf(search, StringComparison.CurrentCulture);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        private enum SyncAction
        {
            Create,
            Update,
            Delete,
            None
        }
    }
}