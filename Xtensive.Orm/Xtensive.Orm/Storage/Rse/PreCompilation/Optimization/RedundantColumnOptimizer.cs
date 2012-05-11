// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alexey Gamzov
// Created:    2009.10.12

using System;
using Xtensive.Storage.Rse.Compilation;
using Xtensive.Storage.Rse.Providers;
using Xtensive.Storage.Rse.Providers.Compilable;

namespace Xtensive.Storage.Rse.PreCompilation.Optimization
{
  /// <summary>
  /// <see cref="IPreCompiler"/> implementation that removes redundant columns in tree.
  /// </summary>
  [Serializable]
  public class RedundantColumnOptimizer: IPreCompiler
  {

    /// <inheritdoc/>
    CompilableProvider IPreCompiler.Process(CompilableProvider rootProvider)
    {
      return new RedundantColumnRemover(rootProvider).RemoveRedundantColumns();
    }
  }
}