﻿using System;
using System.Collections.Generic;
using System.Linq;
using Orient.Client.API;
using Orient.Client.API.Query;
using Orient.Client.Protocol;
using Orient.Client.Protocol.Operations;
using Orient.Client.Protocol.Operations.Command;

namespace Orient.Client
{
    public class ODatabase : IDisposable
    {
        private bool _containsConnection;
        private Connection _connection;

        public IDictionary<ORID, ODocument> ClientCache { get; private set; }

        public OSqlCreate Create { get { return new OSqlCreate(_connection); } }
        public OSqlDelete Delete { get { return new OSqlDelete(_connection); } }
        public OLoadRecord Load { get { return new OLoadRecord(_connection); } }
        public ORecordMetadata Metadata { get { return new ORecordMetadata(_connection); } }
        public OSqlSchema Schema { get { return new OSqlSchema(_connection); } }

        public OTransaction Transaction { get; private set; }

        public ODatabase(string alias)
        {
            _connection = OClient.ReleaseConnection(alias);
            _connection.Database = this;
            _containsConnection = true;
            ClientCache = new Dictionary<ORID, ODocument>();
            Transaction = new OTransaction(_connection);
        }

        public int ProtocolVersion
        {
            get { return _connection.ProtocolVersion; }
        }

        public List<OCluster> GetClusters()
        {
            return _connection.Document.GetField<List<OCluster>>("Clusters");
        }

        public short GetClusterIdFor(string className)
        {
            var clusterName = CorrectClassName(className).ToLower();
            OCluster oCluster = GetClusters().FirstOrDefault(x => x.Name == clusterName);
            if (oCluster == null)
            {
                _connection.Reload();
                oCluster = GetClusters().First(x => x.Name == clusterName);
            }
            return oCluster.Id;
        }

        private string CorrectClassName(string className)
        {
            if (className == "OVertex")
                return "V";
            if (className == "OEdge")
                return "E";
            return className;
        }

        public void AddCluster(string className, short clusterId)
        {
            var clusters = _connection.Document.GetField<List<OCluster>>("Clusters");
            clusters.Add(new OCluster() { Id = clusterId, Name = className.ToLower() });
            _connection.Document.SetField("Clusters", clusters);

        }

        public OSqlSelect Select(params string[] projections)
        {
            return new OSqlSelect(_connection).Select(projections);
        }

        #region Insert

        public OSqlInsert Insert()
        {
            return new OSqlInsert(_connection);
        }

        public OSqlInsert Insert<T>(T obj)
        {
            return new OSqlInsert(_connection)
                .Insert(obj);
        }

        #endregion

        #region Update

        public OSqlUpdate Update()
        {
            return new OSqlUpdate(_connection);
        }

        public OSqlUpdate Update(ORID orid)
        {
            return new OSqlUpdate(_connection)
                .Update(orid);
        }

        public OSqlUpdate Update<T>(T obj)
        {
            return new OSqlUpdate(_connection)
                .Update(obj);
        }

        #endregion

        #region Query

        public List<ODocument> Query(string sql)
        {
            return Query(sql, "*:0");
        }

        public List<ODocument> Query(string sql, string fetchPlan)
        {
            CommandPayloadQuery payload = new CommandPayloadQuery();
            payload.Text = sql;
            payload.NonTextLimit = -1;
            payload.FetchPlan = fetchPlan;
            //payload.SerializedParams = new byte[] { 0 };

            Command operation = new Command();
            operation.OperationMode = OperationMode.Asynchronous;
            operation.CommandPayload = payload;

            ODocument document = _connection.ExecuteOperation(operation);

            return document.GetField<List<ODocument>>("Content");
        }

        #endregion

        public List<ODocument> Gremlin(string query)
        {
            CommandPayloadScript payload = new CommandPayloadScript();
            payload.Language = "gremlin";
            payload.Text = query;

            Command operation = new Command();
            operation.OperationMode = OperationMode.Synchronous;
            operation.CommandPayload = payload;

            ODocument document = _connection.ExecuteOperation(operation);

            return document.GetField<List<ODocument>>("Content");
        }
        public List<ODocument> JavaScript(string query)
        {
            CommandPayloadScript payload = new CommandPayloadScript();
            payload.Language = "javascript";
            payload.Text = query;

            Command operation = new Command();
            operation.OperationMode = OperationMode.Synchronous;
            operation.CommandPayload = payload;

            ODocument document = _connection.ExecuteOperation(operation);

            return document.GetField<List<ODocument>>("Content");
        }
        public OCommandResult Command(string sql)
        {
            CommandPayloadCommand payload = new CommandPayloadCommand();
            payload.Text = sql;

            Command operation = new Command();
            operation.OperationMode = OperationMode.Synchronous;
            operation.CommandPayload = payload;

            ODocument document = _connection.ExecuteOperation(operation);

            return new OCommandResult(document);
        }
        
        public long Size
        {
            get
            {
                var operation = new DBSize();
                var document = _connection.ExecuteOperation(operation);
                return document.GetField<long>("size");
            }
        }
        
        public long CountRecords
        {
            get
            {
                var operation = new DBCountRecords();
                var document = _connection.ExecuteOperation(operation);
                return document.GetField<long>("count");
            }
        }

        public void Close()
        {
            if (_containsConnection)
            {
                _connection.Database = null;

                if (_connection.IsReusable)
                {
                    OClient.ReturnConnection(_connection);
                }
                else
                {
                    _connection.Dispose();
                }

                _containsConnection = false;
            }
        }

        public void Dispose()
        {
            Close();
        }
    }
}
