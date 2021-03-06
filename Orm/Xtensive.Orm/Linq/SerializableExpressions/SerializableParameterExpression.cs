// Copyright (C) 2003-2010 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Denis Krjuchkov
// Created:    2009.05.12

using System;
using System.Linq.Expressions;
using System.Runtime.Serialization;

namespace Xtensive.Linq.SerializableExpressions
{
  /// <summary>
  /// A serializable representation of <see cref="ParameterExpression"/>.
  /// </summary>
  [Serializable]
  public sealed class SerializableParameterExpression : SerializableExpression
  {
    /// <summary>
    /// <see cref="ParameterExpression.Name"/>.
    /// </summary>
    public string Name;

    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
      base.GetObjectData(info, context);
      info.AddValue("Name", Name);
    }

    public SerializableParameterExpression()
    {
    }

    public SerializableParameterExpression(SerializationInfo info, StreamingContext context)
      : base(info, context)
    {
      Name = info.GetString("Name");
    }
  }
}