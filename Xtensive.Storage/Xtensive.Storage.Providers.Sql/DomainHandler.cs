// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2008.05.20

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Xtensive.Core.Tuples;
using Xtensive.Sql.Common;
using Xtensive.Sql.Dom;
using Xtensive.Sql.Dom.Database;
using Xtensive.Sql.Dom.Database.Providers;
using Xtensive.Sql.Dom.Ddl;
using Xtensive.Sql.Dom.Dml;
using Xtensive.Storage.Model;
using Xtensive.Storage.Rse.Compilation;
using Xtensive.Storage.Providers.Sql.Resources;
using ColumnInfo = Xtensive.Storage.Model.ColumnInfo;
using IndexInfo  = Xtensive.Storage.Model.IndexInfo;
using SqlFactory = Xtensive.Sql.Dom.Sql;
using SqlModel = Xtensive.Sql.Dom.Database.Model;
using Xtensive.Core.Helpers;

namespace Xtensive.Storage.Providers.Sql
{
  public abstract class DomainHandler : Providers.DomainHandler
  {
    private DbTransaction transaction;
    private Catalog catalog;
    private readonly Dictionary<IndexInfo, Table> realIndexes = new Dictionary<IndexInfo, Table>();
    private SqlConnection connection;
    // private SqlModel model;

    protected override CompilationContext GetCompilationContext()
    {
      return new CompilationContext(new Compilers.Compiler(Handlers));
    }

    /// <inheritdoc/>
    public override void Build()
    {
      var provider = new SqlConnectionProvider();
      using (connection = provider.CreateConnection(Handlers.Domain.Configuration.ConnectionInfo.ToString()) as SqlConnection) {
        if (connection==null)
          throw new InvalidOperationException(Strings.ExUnableToCreateConnection);
        connection.Open();
        var modelProvider = new SqlModelProvider(connection);
        SqlModel existingModel = SqlModel.Build(modelProvider);
        string serverName = existingModel.DefaultServer.Name;
        string catalogName = Handlers.Domain.Configuration.ConnectionInfo.Resource;
        string schemaName = existingModel.DefaultServer.Catalogs[catalogName].DefaultSchema.Name;
        SqlModel newModel = BuildNewModel(serverName, catalogName, schemaName);
        ISqlCompileUnit syncroniztionScript = BuildSyncronizationScript(Handlers.Domain.Model, existingModel.DefaultServer.Catalogs[catalogName], newModel.DefaultServer.Catalogs[catalogName]);
        ExecuteNonQuery(syncroniztionScript);
        catalog = SqlModel.Build(modelProvider).DefaultServer.Catalogs[catalogName];
      }
    }

    protected ISqlCompileUnit BuildSyncronizationScript(DomainModel domainModel, Catalog existingCatalog, Catalog newCatalog)
    {
      SqlBatch batch = SqlFactory.Batch();
      batch.Add(ClearCatalogScript(existingCatalog));
      batch.Add(BuildCatalogScript(newCatalog));
      return batch;
    }

    private SqlBatch BuildCatalogScript(Catalog catalog)
    {
      SqlBatch batch = SqlFactory.Batch();
      foreach (Table table in catalog.DefaultSchema.Tables) {
        batch.Add(SqlFactory.Create(table));
        foreach (Index index in table.Indexes) {
          batch.Add(SqlFactory.Create(index));
        }
      }
      return batch;
    }

    private SqlModel BuildNewModel(string serverName, string catalogName, string schemaName)
    {
      SqlModel model = new SqlModel();
      Server server = model.CreateServer(serverName);
      Catalog catalog = server.CreateCatalog(catalogName);
      Schema schema = catalog.CreateSchema(schemaName);
      foreach (TypeInfo type in Handlers.Domain.Model.Types) {
        IndexInfo primaryIndex = type.Indexes.FindFirst(IndexAttributes.Real | IndexAttributes.Primary);
        if (primaryIndex != null && !realIndexes.ContainsKey(primaryIndex)) {
          Table table = schema.CreateTable(primaryIndex.ReflectedType.Name);
          realIndexes.Add(primaryIndex, table);
          var keyColumns = new List<TableColumn>();
          foreach (ColumnInfo column in primaryIndex.Columns) {
            TableColumn tableColumn = table.CreateColumn(column.Name, GetSqlType(column.ValueType, column.Length));
            tableColumn.IsNullable = column.IsNullable;
            if (column.IsPrimaryKey)
              keyColumns.Add(tableColumn);
          }
          table.CreatePrimaryKey(primaryIndex.Name, keyColumns.ToArray());
          // Primary key included columns
          if (primaryIndex.IncludedColumns.Count > 0) {
            Index index = table.CreateIndex(primaryIndex.Name + "_IncludedColumns");
            index.IsUnique = false;
            index.Filegroup = "\"default\"";
            foreach (ColumnInfo includedColumn in primaryIndex.IncludedColumns) {
              ColumnInfo includedColumn1 = includedColumn;
              index.CreateIndexColumn(table.TableColumns.First(tableColumn => tableColumn.Name == includedColumn1.Name));
            }
          }
          // Secondary indexes
          foreach (IndexInfo secondaryIndex in type.Indexes.Find(IndexAttributes.Real).Where(indexInfo => !indexInfo.IsPrimary)) {
            Index index = table.CreateIndex(secondaryIndex.Name);
            index.IsUnique = secondaryIndex.IsUnique;
            index.FillFactor = (byte)(secondaryIndex.FillFactor * 10);
            index.Filegroup = "\"default\"";
            foreach (ColumnInfo secondaryIndexColumn in secondaryIndex.Columns.Where(columnInfo => !columnInfo.IsPrimaryKey && !columnInfo.IsSystem)) {
              string primaryIndexColumnName = GetPrimaryIndexColumnName(primaryIndex, secondaryIndexColumn, secondaryIndex);
              index.CreateIndexColumn(table.TableColumns.First(tableColumn => tableColumn.Name == primaryIndexColumnName));
            }
            foreach (var nonKeyColumn in secondaryIndex.IncludedColumns) {
              string primaryIndexColumnName = GetPrimaryIndexColumnName(primaryIndex, nonKeyColumn, secondaryIndex);
              index.NonkeyColumns.Add(table.TableColumns.First(tableColumn => tableColumn.Name == primaryIndexColumnName));
            }
          }
        }
      }
      return model;
    }

    private SqlBatch ClearCatalogScript(Catalog catalog)
    {
      SqlBatch batch = SqlFactory.Batch();
      Schema schema = catalog.DefaultSchema;
      foreach (View view in schema.Views)
        batch.Add(SqlFactory.Drop(view));
      schema.Views.Clear();
      foreach (Table table in schema.Tables)
        batch.Add(SqlFactory.Drop(table));
      schema.Tables.Clear();
      return batch;
    }

    private static string GetPrimaryIndexColumnName(IndexInfo primaryIndex, ColumnInfo secondaryIndexColumn, IndexInfo secondaryIndex)
    {
      string primaryIndexColumnName = null;
      foreach (ColumnInfo primaryColumn in primaryIndex.Columns) {
        if (primaryColumn.Field.Equals(secondaryIndexColumn.Field)) {
          primaryIndexColumnName = primaryColumn.Name;
          break;
        }
      }
      if (primaryIndexColumnName.IsNullOrEmpty()) {
        throw new InvalidOperationException(String.Format(Strings.UnableToFindColumnInPrimaryIndex, secondaryIndexColumn.Name, secondaryIndex.Name));
      }
      return primaryIndexColumnName;
    }

    private void ExecuteNonQuery(ISqlCompileUnit statement)
    {
      using (transaction = connection.BeginTransaction()) {
        using (var command = new SqlCommand(connection)) {
          command.Statement = statement;
          command.Prepare();
          command.Transaction = transaction;
          command.ExecuteNonQuery();
        }
        transaction.Commit();
      }
    }

    /// <summary>
    /// Gets <see cref="SqlDataType"/> by .NET <see cref="Type"/> and length.
    /// </summary>
    /// <param name="type"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    protected abstract SqlDataType GetSqlDataType(Type type, int? length);

    /// <summary>
    /// Builds "Where" expression for key for specified table.
    /// </summary>
    /// <param name="table">Table to build statement for.</param>
    /// <param name="mapping">Table-to-tuple mapping</param>
    /// <param name="key">Key data</param>
    /// <returns>Expression that can be used in "where" statements</returns>
    public virtual SqlExpression GetWhereStatement(SqlTable table, IEnumerable<StatementMapping> mapping, Key key)
    {
      if (key==null)
        return null;
      SqlExpression expression = null;
      foreach (StatementMapping statementMapping in mapping) {
        SqlBinary binary = (table[statementMapping.TablePosition] == GetSqlExpression(key.Tuple, statementMapping.TuplePosition));
        if (expression == null)
          expression = binary;
        else
          expression &= binary;
      }
      return expression;
    }

    public virtual SqlExpression GetSqlExpression(Tuple tuple, int index)
    {
      if (!tuple.IsAvailable(index) || tuple.IsNull(index) || tuple.GetValueOrDefault(index) == null)
        return null;
      Type type = tuple.Descriptor[index];
      if (type == typeof(Boolean))
        return tuple.GetValue<bool>(index);
      if (type == typeof(Char))
        return tuple.GetValue<char>(index);
      if (type == typeof(SByte))
        return tuple.GetValue<SByte>(index);
      if (type == typeof(Byte))
        return tuple.GetValue<Byte>(index);
      if (type == typeof(Int16))
        return tuple.GetValue<Int16>(index);
      if (type == typeof(UInt16))
        return tuple.GetValue<UInt16>(index);
      if (type == typeof(Int32))
        return tuple.GetValue<Int32>(index);
      if (type == typeof(UInt32))
        return tuple.GetValue<UInt32>(index);
      if (type == typeof(Int64))
        return tuple.GetValue<Int64>(index);
      if (type == typeof(UInt64))
        return tuple.GetValue<UInt64>(index);
      if (type == typeof(Decimal))
        return tuple.GetValue<Decimal>(index);
      if (type == typeof(float))
        return tuple.GetValue<float>(index);
      if (type == typeof(double))
        return tuple.GetValue<double>(index);
      if (type == typeof(DateTime))
        return tuple.GetValue<DateTime>(index);
      if (type == typeof(TimeSpan))
        return tuple.GetValue<TimeSpan>(index);
      if (type == typeof(String))
        return tuple.GetValue<String>(index);
      if (type == typeof(byte[]))
        return tuple.GetValue<byte[]>(index);
      if (type == typeof(Guid))
        return tuple.GetValue<Guid>(index);
      throw new InvalidOperationException(); //Should never be
    }

    #region Internal

    internal Dictionary<IndexInfo, Table> RealIndexes
    {
      get { return realIndexes; }
    }

    public Catalog Catalog
    {
      get { return catalog; }
    }

    #endregion

    private SqlValueType GetSqlType(Type type, int? length)
    {
      // TODO: Get this data from Connection.Driver.ServerInfo.DataTypes
      var result = (length==null)
        ? new SqlValueType(GetSqlDataType(type, null))
        : new SqlValueType(GetSqlDataType(type, length.Value), length.Value);
      return result;
    }
  }
}