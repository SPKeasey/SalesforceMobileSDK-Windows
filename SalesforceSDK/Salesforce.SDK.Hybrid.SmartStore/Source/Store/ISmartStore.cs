﻿using System.Runtime.InteropServices.WindowsRuntime;

namespace Salesforce.SDK.Hybrid.SmartStore.Source.Store
{
    public interface ISmartStore
    {
        long CountQuery(QuerySpec querySpec);
        string Query(QuerySpec querySpec, int pageIndex);
        bool Delete(string soupName, [ReadOnlyArray()]long[] soupEntryIds, bool handleTx);
        string Retrieve(string soupName, [ReadOnlyArray()]long[] soupEntryIds);
        bool HasSoup(string soupName);
        void RegisterSoup(string soupName, [ReadOnlyArray()]IndexSpec[] indexSpecs);
        void DropSoup(string soupName);
        void ResetDatabase();
        object Upsert(string soupName, string soupElt, string externalIdPath, bool handleTx);
        bool BeginDatabaseTransaction();
        bool CommitDatabaseTransaction();
    }
}
