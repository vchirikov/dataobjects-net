// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Aleksey Gamzov
// Created:    2008.08.21

using System;
using Xtensive.Core.Helpers;

namespace Xtensive.Sql.Dom.Database.Comparer
{
  /// <summary>
  /// <see cref="Catalog"/> comparison result.
  /// </summary>
  [Serializable]
  public class CatalogComparisonResult : NodeComparisonResult<Catalog>
  {
    private readonly SchemaComparisonResult defaultSchema = new SchemaComparisonResult();
    private readonly ComparisonResultCollection<SchemaComparisonResult> schemas = new ComparisonResultCollection<SchemaComparisonResult>();
    
    /// <summary>
    /// Gets comparison result of default <see cref="Schema"/>.
    /// </summary>
    public SchemaComparisonResult DefaultSchema
    {
      get { return defaultSchema; }
    }

    /// <summary>
    /// Gets comparison results of nested <see cref="Schema"/>s.
    /// </summary>
    public ComparisonResultCollection<SchemaComparisonResult> Schemas
    {
      get { return schemas; }
    }

    /// <inheritdoc/>
    public override void Lock(bool recursive)
    {
      base.Lock(recursive);
      if (recursive) {
        defaultSchema.Lock(recursive);
        schemas.Lock(recursive);
      }
    }
  }
}