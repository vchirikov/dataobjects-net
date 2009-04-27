// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Elena Vakhtina
// Created:    2008.09.18

using System;
using System.Collections.Generic;
using Xtensive.Core.Collections;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Tuples;
using Xtensive.Core.Tuples.Transform;
using System.Linq;

namespace Xtensive.Storage.Rse.Providers.Compilable
{
  /// <summary>
  /// Compilable provider that applies aggregate functions to grouped columns from <see cref="UnaryProvider.Source"/>.
  /// </summary>
  [Serializable]
  public sealed class AggregateProvider : UnaryProvider
  {
    private const string ToStringFormat = "{0}, Group by ({1})";

    /// <summary>
    /// Gets the aggregate columns.
    /// </summary>
    public AggregateColumn[] AggregateColumns { get; private set; }

    /// <summary>
    /// Gets column indexes to group by.
    /// </summary>
    public int[] GroupColumnIndexes { get; private set; }

    /// <summary>
    /// Gets header resize transform.
    /// </summary>
    public MapTransform Transform { get; private set; }

    /// <inheritdoc/>
    protected override RecordSetHeader BuildHeader()
    {
      return Source.Header
        .Select(GroupColumnIndexes)
        .Add(AggregateColumns);
    }

    /// <inheritdoc/>
    public override string ParametersToString()
    {
      if (GroupColumnIndexes.Length==0)
        return AggregateColumns.ToCommaDelimitedString();
      else
        return string.Format(ToStringFormat,
          AggregateColumns.ToCommaDelimitedString(), 
          GroupColumnIndexes.ToCommaDelimitedString());
    }

    /// <inheritdoc/>
    protected override void Initialize()
    {
      base.Initialize();
      var types = new List<Type>();
      int i = 0;
      var columnIndexes = new int[GroupColumnIndexes.Length];
      foreach (var index in GroupColumnIndexes) {
        types.Add(Source.Header.Columns[index].Type);
        columnIndexes[i++] = index;
      }
      Transform = new MapTransform(false, TupleDescriptor.Create(types), columnIndexes);
    }

    /// <inheritdoc/>
    protected override DirectionCollection<int> CreateExpectedColumnsOrdering()
    {
      return new DirectionCollection<int>(
          Source.ExpectedColumnsOrdering.Where(p => GroupColumnIndexes.Contains(p.Key)));
    }


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>
    /// <param name="source">The <see cref="UnaryProvider.Source"/> property value.</param>
    /// <param name="columnDescriptors">The descriptors of <see cref="AggregateColumns"/>.</param>
    /// <param name="groupIndexes">The column indexes to group by.</param>
    public AggregateProvider(CompilableProvider source, int[] groupIndexes, params AggregateColumnDescriptor[] columnDescriptors)
      : base(ProviderType.Aggregate, source)
    {
      groupIndexes = groupIndexes ?? ArrayUtils<int>.EmptyArray;
      var columns = new AggregateColumn[columnDescriptors.Length];
      for (int i = 0; i < columnDescriptors.Length; i++) {
        AggregateColumnDescriptor descriptor = columnDescriptors[i];
        Type type = descriptor.AggregateType == AggregateType.Count ? typeof(long) : Source.Header.Columns[descriptor.SourceIndex].Type;
        var col = new AggregateColumn(descriptor, groupIndexes.Length + i, type);
        columns.SetValue(col, i);
      }
      AggregateColumns = columns;
      GroupColumnIndexes = groupIndexes;
    }
  }
}