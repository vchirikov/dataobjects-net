// Copyright (C) 2009 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexis Kochetov
// Created:    2009.03.05

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Xtensive.Core;
using Xtensive.Core.Collections;
using Xtensive.Core.Internals.DocTemplates;
using Xtensive.Core.Tuples;

namespace Xtensive.Storage.Rse.Providers.Compilable
{
  /// <summary>
  /// Produces join between <see cref="BinaryProvider.Left"/> and 
  /// <see cref="BinaryProvider.Right"/> sources by <see cref="Predicate"/>.
  /// </summary>
  [Serializable]
  public sealed class PredicateJoinProvider : BinaryProvider
  {
    /// <summary>
    /// Indicates whether current join operation should be executed as left join.
    /// </summary>
    public bool Outer { get; private set; }

    /// <summary>
    /// Gets the predicate.
    /// </summary>
    public Expression<Func<Tuple, Tuple, bool>> Predicate { get; private set; }

    /// <inheritdoc/>
    protected override DirectionCollection<int> CreateExpectedColumnsOrdering()
    {
      var result = Left.ExpectedColumnsOrdering;
      if (Right.ExpectedColumnsOrdering.Count > 0) {
        var leftHeaderLength = Left.ExpectedColumnsOrdering.Count;
        result = new DirectionCollection<int>(
          Enumerable.Union(result, Right.ExpectedColumnsOrdering.Select(p =>
            new KeyValuePair<int, Direction>(p.Key + leftHeaderLength, p.Value))));
      }
      return result;
    }


    // Constructors

    /// <summary>
    /// <see cref="ClassDocTemplate.Ctor" copy="true"/>
    /// </summary>  
    public PredicateJoinProvider(CompilableProvider left, CompilableProvider right, Expression<Func<Tuple, Tuple, bool>> predicate, bool outer)
      : base(ProviderType.PredicateJoin, left, right)
    {
      Predicate = predicate;
      Outer = outer;
    }
  }
}