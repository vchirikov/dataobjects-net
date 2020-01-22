// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2009.12.28

using System.Linq.Expressions;
using Xtensive.IoC;

namespace Xtensive.Orm.Localization
{
  [Service(typeof(IQueryPreprocessor))]
  internal class QueryPreprocessor : DomainBound, IQueryPreprocessor
  {
    private LocalizationExpressionVisitor visitor;

    /// <inheritdoc/>
    public Expression Apply(Expression query)
    {
      if (!IsInitialized)
        TryInitialize();

      if (!IsInitialized)
        return query;

      return visitor.VisitExpression(query);
    }

    private void TryInitialize()
    {
      var map = Domain.Extensions.Get<TypeLocalizationMap>();
      if (map != null)
        visitor = new LocalizationExpressionVisitor(map);
    }

    private bool IsInitialized
    {
      get { return visitor != null; }
    }

    /// <inheritdoc/>
    public bool IsDependentOn(IQueryPreprocessor other)
    {
      return false;
    }

    [ServiceConstructor]
    public QueryPreprocessor(Domain domain)
      : base(domain)
    {
    }
  }
}