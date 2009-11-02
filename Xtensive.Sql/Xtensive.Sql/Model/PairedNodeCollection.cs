// Copyright (C) 2007 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.

using System;
using System.Collections;
using System.Collections.Generic;
using Xtensive.Core;
using Xtensive.Core.Helpers;

namespace Xtensive.Sql.Model
{
  /// <summary>
  /// Represents paired collection of <see cref="Node"/>s.
  /// </summary>
  /// <typeparam name="TOwner">Owner node type</typeparam>
  /// <typeparam name="TNode">Item node type</typeparam>
  [Serializable]
  public class PairedNodeCollection<TOwner, TNode>: NodeCollection<TNode>
    where TOwner : Node
    where TNode : Node, IPairedNode<TOwner>
  {
    private readonly TOwner owner;
    private readonly string property;

    /// <summary>
    /// Gets a value indicating whether the <see cref="IList"/> is read-only.
    /// </summary>
    /// <value></value>
    /// <returns><see langword="True"/> if the <see cref="IList"/> is read-only; otherwise, <see langword="false"/>.</returns>
    public override bool IsReadOnly
    {
      get { return IsLocked ? true : base.IsReadOnly; }
    }

    /// <summary>
    /// Performs additional custom processes when changing the contents of the
    /// collection instance.
    /// </summary>
    protected override void OnChanging()
    {
      this.EnsureNotLocked();
    }

    /// <summary>
    /// Performs additional custom processes after inserting a new element into the
    /// collection instance.
    /// </summary>
    /// <param name="index">The zero-based <paramref name="index"/> at which to insert value.</param>
    /// <param name="value">The new value of the element at <paramref name="index"/>.</param>
    protected override void OnInserted(TNode value, int index)
    {
      base.OnInserted(value, index);
      value.UpdatePairedProperty(property, owner);
    }

    /// <summary>
    /// Performs additional custom processes after removing an element from the
    /// collection instance.
    /// </summary>
    /// <param name="index">The zero-based <paramref name="index"/> at which to insert value.</param>
    /// <param name="value">The value of the element to remove from <paramref name="index"/>.</param>
    protected override void OnRemoved(TNode value, int index)
    {
      base.OnRemoved(value, index);
      value.UpdatePairedProperty(property, null);
    }

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="PairedNodeCollection{TOwner,TNode}"/> class.
    /// </summary>
    /// <param name="owner">The collectionowner.</param>
    /// <param name="property">Owner collection property.</param>
    public PairedNodeCollection(TOwner owner, string property)
      : this(owner, property, 0)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PairedNodeCollection{TOwner,TNode}"/> class.
    /// </summary>
    /// <param name="owner">The collection owner.</param>
    /// <param name="property">Owner collection property.</param>
    /// <param name="capacity">The initial collection capacity.</param>
    public PairedNodeCollection(TOwner owner, string property, int capacity)
      : base(capacity)
    {
      ArgumentValidator.EnsureArgumentNotNull(owner, "owner");
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(property, "property");
      this.owner = owner;
      this.property = property;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PairedNodeCollection{TOwner,TNode}"/> class.
    /// </summary>
    /// <param name="owner">The collection owner.</param>
    /// <param name="property">Owner collection property.</param>
    /// <param name="collection">The collection whose elements are copied to the new list.</param>
    public PairedNodeCollection(TOwner owner, string property, IEnumerable<TNode> collection)
      : base(collection)
    {
      ArgumentValidator.EnsureArgumentNotNull(owner, "owner");
      ArgumentValidator.EnsureArgumentNotNullOrEmpty(property, "property");
      this.owner = owner;
      this.property = property;
    }

    #endregion
  }
}