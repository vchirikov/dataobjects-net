// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Dmitri Maximov
// Created:    2007.08.21

using System;

namespace Xtensive.Storage.Configuration.TypeRegistry
{
  /// <summary>
  /// <see cref="TypeRegistration"/> processor for processing <see cref="Persistent"/> 
  /// and <see cref="IEntity"/> descendants registration in 
  /// <see cref="DomainConfiguration.Types"/> registry.
  /// </summary>
  /// <remarks>This implementation provides topologically sorted list 
  /// of <see cref="Type"/>s.</remarks>
  [Serializable]
  internal sealed class TypeProcessor: ActionProcessor
  {
    private readonly Type baseInterface = typeof (IEntity);
    private readonly Type baseType = typeof (Persistent);

    /// <inheritdoc/>
    public override Type BaseInterface
    {
      get { return baseInterface; }
    }

    /// <inheritdoc/>
    public override Type BaseType
    {
      get { return baseType; }
    }

    protected override void ProcessType(Context context, Type type)
    {
      if (context.Contains(type))
        return;
      if (type.IsClass && type.BaseType != BaseType)
        ProcessType(context, type.BaseType);
      Type[] interfaces = type.FindInterfaces(
        (typeObj, filterCriteria) => BaseInterface.IsAssignableFrom(typeObj), type);
      for (int index = 0; index < interfaces.Length; index++) {
        ProcessType(context, interfaces[index]);
      }
      context.Register(type);
    }
  }
}