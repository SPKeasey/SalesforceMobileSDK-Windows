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
using SQLitePCL;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace Salesforce.SDK.SmartStore.Store
{
    public class DBHelper
    {
        #region Statics
        private static Dictionary<string, DBHelper> Instances;
        // Some queries
        private static readonly string CountSelect = "SELECT count(*) FROM {0} {1}";
        private static readonly string SeqSelect = "SELECT seq FROM SQLITE_SEQUENCE WHERE name = ?";
        private static readonly string LimitSelect = "SELECT * FROM ({0}) LIMIT {1}";
        private static readonly string QueryStatement = "SELECT {0} FROM {1} {2} {3}{4}{5}";
        private static readonly string InsertStatement = "INSERT INTO {0} ({1}) VALUES ({2});";
        private static readonly string UpdateStatement = "UPDATE {0} SET {1} WHERE {2}";
        private static readonly string DeleteStatement = "DELETE FROM {0} WHERE {1}";
        #endregion

        #region DBHelper properties
        /// <summary>
        ///  Cache of soup name to soup table names
        /// </summary>
        private Dictionary<string, string> SoupNameToTableNamesMap;

        /// <summary>
        /// 
        /// </summary>
        private Dictionary<string, IndexSpec[]> SoupNameToIndexSpecsMap;

        /// <summary>
        /// Cache of table name to get-next-id compiled statements
        /// </summary>
        private Dictionary<string, SQLiteStatement> TableNameToNextIdStatementsMap;
        //private Dictionary<string, InsertHelper> TableNameToInsertHelpersMap;

        /// <summary>
        /// Cache of raw count sql to compiled statements
        /// </summary>
        private Dictionary<string, SQLiteStatement> RawCountSqlToStatementsMap;

        private string DatabasePath;

        private SQLiteConnection SQLConnection;

        #endregion

        private DBHelper(string sqliteDb)
        {
            SoupNameToTableNamesMap = new Dictionary<string, string>();
            SoupNameToIndexSpecsMap = new Dictionary<string, IndexSpec[]>();
            TableNameToNextIdStatementsMap = new Dictionary<string, SQLiteStatement>();
            RawCountSqlToStatementsMap = new Dictionary<string, SQLiteStatement>();
            DatabasePath = sqliteDb;
            SQLConnection = new SQLiteConnection(DatabasePath);
        }

        public static DBHelper GetInstance(string sqliteDBFile)
        {
            if (Instances == null)
            {
                Instances = new Dictionary<string, DBHelper>();
            }
            DBHelper instance;
            if (!Instances.TryGetValue(sqliteDBFile, out instance))
            {
                instance = new DBHelper(sqliteDBFile);
                Instances.Add(sqliteDBFile, instance);
            }
            return instance;
        }

        public void CacheTableName(string soupName, string tableName)
        {
            SoupNameToTableNamesMap.Add(soupName, tableName);
        }

        public string GetCachedTableName(string soupName)
        {
            string value;
            if (!SoupNameToTableNamesMap.TryGetValue(soupName, out value))
            {
                return null;
            }
            return value;
        }

        public void CacheIndexSpecs(string soupName, IndexSpec[] indexSpecs)
        {
            SoupNameToIndexSpecsMap.Add(soupName, indexSpecs);
        }

        public IndexSpec[] GetCachedIndexSpecs(string soupName)
        {
            IndexSpec[] value;
            if (!SoupNameToIndexSpecsMap.TryGetValue(soupName, out value))
            {
                return null;
            }
            return value;
        }

        public void RemoveFromCache(string soupName)
        {
            if (SoupNameToTableNamesMap.ContainsKey(soupName))
            {
                string tableName = SoupNameToTableNamesMap[soupName];
                // InsertHelper ih = TableNameToInsertHelpersMap.remove(tableName);
                // if (ih != null) ih.close();

                SQLiteStatement prog;
                if (TableNameToNextIdStatementsMap.TryGetValue(soupName, out prog))
                {
                    prog.Dispose();
                    TableNameToNextIdStatementsMap.Remove(soupName);
                }
                CleanupRawCountSqlToStatementMaps(tableName);
            }
            SoupNameToTableNamesMap.Remove(soupName);
            SoupNameToIndexSpecsMap.Remove(soupName);
        }

        private void CleanupRawCountSqlToStatementMaps(string tableName)
        {
            List<string> countSqlToRemove = new List<string>();
            foreach (string entry in RawCountSqlToStatementsMap.Keys)
            {
                if (entry.Contains(tableName))
                {
                    countSqlToRemove.Add(entry);
                }
            }
            foreach (string entry in countSqlToRemove)
            {
                RawCountSqlToStatementsMap.Remove(entry);
            }
        }

        public long GetNextId(string tableName)
        {
            SQLiteStatement prog;

            if (!TableNameToNextIdStatementsMap.TryGetValue(tableName, out prog))
            {
                prog = SQLConnection.Prepare(SeqSelect) as SQLiteStatement;
                prog.Bind(1, tableName);
                TableNameToNextIdStatementsMap.Add(tableName, prog);
            }
            SQLiteResult result = prog.Step();
            return SQLConnection.LastInsertRowId() + 1;
        }

        public SQLiteStatement CountQuery(string table, string whereClause, params string[] args)
        {
            string selectionStr = (whereClause == null ? "" : " WHERE " + whereClause);
            string sql = String.Format(CountSelect, selectionStr);
            var stmt = SQLConnection.Prepare(sql) as SQLiteStatement;
            stmt.Step();
            return stmt;
        }

        public SQLiteStatement LimitRawQuery(string sql, string limit, params string[] args)
        {
            string limitSql = String.Format(LimitSelect, sql, limit);
            var stmt = SQLConnection.Prepare(limitSql) as SQLiteStatement;
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    stmt.Bind(i + 1, args[i]);
                }
            }
            stmt.Step();
            return stmt;
        }

        public long CountRawCountQuery(string countSql, params string[] args)
        {
            SQLiteStatement prog;
            if (!RawCountSqlToStatementsMap.TryGetValue(countSql, out prog))
            {
                prog = SQLConnection.Prepare(countSql) as SQLiteStatement;
                RawCountSqlToStatementsMap.Add(countSql, prog);
            }
            prog.Reset();
            prog.ClearBindings();
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    prog.Bind(i + 1, args[i]);
                }
            }
            var result = prog.Step();
            if (result == SQLiteResult.ROW)
            {
                return prog.GetInteger(0);
            }
            return 0;
        }

        public long CountRawQuery(String sql, params string[] args)
        {
            string countSql = String.Format(CountSelect, "", "(" + sql + ")");
            return CountRawCountQuery(countSql, args);
        }

        public SQLiteStatement Query(string table, string[] columns, string orderBy, string limit, string whereClause, params string[] args)
        {
            if (String.IsNullOrWhiteSpace(table) || columns == null || columns.Length == 0)
            {
                throw new InvalidOperationException("Must specify a table and columns to query");
            }
            if (String.IsNullOrWhiteSpace(whereClause))
            {
                whereClause = String.Empty;
            }
            else
            {
                whereClause = "WHERE " + whereClause;
            }
            string sql = String.Format(QueryStatement,
                String.Join(", ", columns),
                table,
                whereClause,
                orderBy,
                limit,
                String.Empty);
            var stmt = SQLConnection.Prepare(sql);
            if (args != null)
            {
                for (int i = 0; i < args.Length; i++)
                {
                    stmt.Bind(i + 1, args[i]);
                }
            }
            var result = stmt.Step();
            return stmt as SQLiteStatement;
        }

        public long Insert(string table, Dictionary<string, object> contentValues)
        {
            if (String.IsNullOrWhiteSpace(table) || contentValues == null || contentValues.Keys.Count == 0)
            {
                throw new InvalidOperationException("Must specify a table and provide content to insert");
            }
            string columns = String.Join(", ", contentValues.Keys);
            string values = "'" + String.Join("', '", contentValues.Values) + "'";
            string sql = String.Format(InsertStatement,
                table,
                columns,
                values);
            using (var stmt = SQLConnection.Prepare(sql))
            {
               stmt.Step();
            }
            return SQLConnection.LastInsertRowId();
        }

        public bool Update(string table, Dictionary<string, object> contentValues, string whereClause, params string[] args)
        {
            if (String.IsNullOrWhiteSpace(table) || contentValues == null || contentValues.Keys.Count == 0)
            {
                throw new InvalidOperationException("Must specify a table and provide content to update");
            }
            if (String.IsNullOrWhiteSpace(whereClause))
            {
                whereClause = String.Empty;
            }
            string entries = String.Join("= ?, ", contentValues.Keys);
            if (contentValues.Keys.Count > 0)
            {
                entries += " = ?";
            }
            string sql = String.Format(InsertStatement,
                table,
                entries,
                whereClause);
            int place = 1;
            using (var stmt = SQLConnection.Prepare(sql))
            {
                foreach (var next in contentValues.Values)
                {
                    stmt.Bind(place, next);
                    place++;
                }
                if (!String.IsNullOrWhiteSpace(whereClause) && args != null && args.Length > 0)
                {
                    foreach (var next in args)
                    {
                        stmt.Bind(place, next);
                        place++;
                    }
                }

                return stmt.Step() == SQLiteResult.DONE;
            }
        }


        public bool Delete(string table, Dictionary<string, object> contentValues)
        {
            if (String.IsNullOrWhiteSpace(table) || contentValues == null || contentValues.Keys.Count == 0)
            {
                throw new InvalidOperationException("Must specify a table and provide content to delete");
            }
            string values = String.Join(" = ?, ", contentValues.Keys);
            if (contentValues.Keys.Count > 0)
            {
                values += " = ?";
            }
            string sql = String.Format(DeleteStatement,
                table,
                values);
            using (var stmt = SQLConnection.Prepare(sql))
            {
                int place = 1;
                foreach (var next in contentValues.Values)
                {
                    stmt.Bind(place, next);
                    place++;
                }
                return stmt.Step() == SQLiteResult.DONE;
            }
        }

        public async void DeleteDatabase(string databaseFile)
        {
            Instances.Remove(DatabasePath);
            SQLConnection.Dispose();
        }

        public string GetSoupTableName(string soupName)
        {
            string soupTableName = GetCachedTableName(soupName);
            if (String.IsNullOrWhiteSpace(soupTableName))
            {
                soupTableName = GetSoupTableNameFromDb(soupName);
                if (!String.IsNullOrWhiteSpace(soupTableName))
                {
                    CacheTableName(soupName, soupTableName);
                }
            }
            return soupTableName;
        }

        protected string GetSoupTableNameFromDb(string soupName)
        {
            SQLiteStatement stmt = Query(SmartStore.SoupNamesTable, new string[] { SmartStore.IdCol }, String.Empty, String.Empty, SmartStore.SoupNamePredicate, soupName);
            if (stmt.DataCount == 0)
            {
                return null;
            }
            return SmartStore.GetSoupTableName(stmt.GetInteger(SmartStore.IdCol));
        }

        public SQLiteResult Execute(string sql)
        {
            SQLiteStatement statement;
            return Execute(sql, out statement);
        }

        public SQLiteResult Execute(string sql, out SQLiteStatement statement)
        {
            using (statement = SQLConnection.Prepare(sql) as SQLiteStatement)
            {
                return statement.Step();
            }
        }

        public string GetColumnNameForPath(String soupName, String path)
        {
            IndexSpec[] indexSpecs = GetIndexSpecs(soupName);
            foreach (IndexSpec indexSpec in indexSpecs)
            {
                if (indexSpec.Path.Equals(path))
                {
                    return indexSpec.ColumnName;
                }
            }
            throw new SmartStoreException(String.Format("{0} does not have an index on {1}", soupName, path));
        }

        public IndexSpec[] GetIndexSpecs(String soupName)
        {
            IndexSpec[] indexSpecs = GetCachedIndexSpecs(soupName);
            if (indexSpecs == null)
            {
                indexSpecs = GetIndexSpecsFromDb(soupName);
                CacheIndexSpecs(soupName, indexSpecs);
            }
            return indexSpecs;
        }

        protected IndexSpec[] GetIndexSpecsFromDb(String soupName)
        {
            var statement = Query(SmartStore.SoupIndexMapTable, new String[] { SmartStore.PathCol, SmartStore.ColumnNameCol, SmartStore.ColumnTypeCol }, null,
                    null, SmartStore.SoupNamePredicate, soupName);

            if (statement.DataCount < 1)
            {
                throw new SmartStoreException(String.Format("{0} does not have any indices", soupName));
            }
            List<IndexSpec> indexSpecs = new List<IndexSpec>();
            do
            {
                String path = statement.GetText(SmartStore.PathCol);
                String columnName = statement.GetText(SmartStore.ColumnNameCol);
                SmartStoreType columnType = new SmartStoreType(statement.GetText(SmartStore.ColumnTypeCol));
                indexSpecs.Add(new IndexSpec(path, columnType, columnName));
            } while (statement.Step() == SQLiteResult.ROW);
            return indexSpecs.ToArray();
        }

        public bool BeginTransaction()
        {
            using (var stmt = SQLConnection.Prepare("Begin Transaction"))
            {
                return SQLiteResult.DONE == stmt.Step();
            }
        }

        public bool CommitTransaction()
        {
            using (var stmt = SQLConnection.Prepare("Commit Transaction"))
            {
                return SQLiteResult.DONE == stmt.Step();
            }
        }

        public bool RollbackTransaction()
        {
            using (var stmt = SQLConnection.Prepare("Rollback Transaction"))
            {
                return SQLiteResult.DONE == stmt.Step();
            }
        }
    }
}
