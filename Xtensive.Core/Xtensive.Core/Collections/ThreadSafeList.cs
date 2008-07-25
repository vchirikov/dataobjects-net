// Copyright (C) 2008 Xtensive LLC.
// All rights reserved.
// For conditions of distribution and use, see license.
// Created by: Alex Yakunin
// Created:    2008.02.06

using System;
using System.Threading;

namespace Xtensive.Core.Collections
{
  /// <summary>
  /// Thread-safe list. Any operation on it is atomic.
  /// Note: it recreates its internal array (makes it twice larger) when it should grow up.
  /// </summary>
  /// <typeparam name="TItem">Value type.</typeparam>
  [Serializable]
  public struct ThreadSafeList<TItem>
  {
    private const int InitialSize = 16;
    private readonly static TItem defaultItem = default(TItem);
    private TItem[] implementation;

    #region GetValue methods with generator

    /// <summary>
    /// Gets the value or generates it using specified <paramref name="generator"/> and 
    /// adds it to the list.
    /// </summary>
    /// <param name="syncRoot">The object to synchronize on (<see cref="Monitor"/> class is used).</param>
    /// <param name="index">The index of the value to get.</param>
    /// <param name="generator">The value generator.</param>
    /// <returns>Found or generated value.</returns>
    public TItem GetValue(object syncRoot, int index, Func<int, TItem> generator)
    {
      TItem item = GetValue(index);
      if (!ReferenceEquals(item, defaultItem))
        return item;
      else lock (syncRoot) {
        item = GetValue(index);
        if (!ReferenceEquals(item, defaultItem))
          return item;
        item = generator.Invoke(index);
        SetValue(index, item);
        return item;
      }
    }

    /// <summary>
    /// Gets the value or generates it using specified <paramref name="generator"/> and 
    /// adds it to the list.
    /// </summary>
    /// <typeparam name="T">The type of the <paramref name="argument"/> to pass to the <paramref name="generator"/>.</typeparam>
    /// <param name="syncRoot">The object to synchronize on (<see cref="Monitor"/> class is used).</param>
    /// <param name="index">The index of the value to get.</param>
    /// <param name="generator">The value generator.</param>
    /// <param name="argument">The argument to pass to the <paramref name="generator"/>.</param>
    /// <returns>Found or generated value.</returns>
    public TItem GetValue<T>(object syncRoot, int index, Func<int, T, TItem> generator, T argument)
    {
      TItem item = GetValue(index);
      if (!ReferenceEquals(item, defaultItem))
        return item;
      else lock (syncRoot) {
        item = GetValue(index);
        if (!ReferenceEquals(item, defaultItem))
          return item;
        item = generator.Invoke(index, argument);
        SetValue(index, item);
        return item;
      }
    }

    /// <summary>
    /// Gets the value or generates it using specified <paramref name="generator"/> and 
    /// adds it to the list.
    /// </summary>
    /// <typeparam name="T1">The type of the <paramref name="argument1"/> to pass to the <paramref name="generator"/>.</typeparam>
    /// <typeparam name="T2">The type of the <paramref name="argument2"/> to pass to the <paramref name="generator"/>.</typeparam>
    /// <param name="syncRoot">The object to synchronize on (<see cref="Monitor"/> class is used).</param>
    /// <param name="index">The index of the value to get.</param>
    /// <param name="generator">The value generator.</param>
    /// <param name="argument1">The first argument to pass to the <paramref name="generator"/>.</param>
    /// <param name="argument2">The second argument to pass to the <paramref name="generator"/>.</param>
    /// <returns>Found or generated value.</returns>
    public TItem GetValue<T1, T2>(object syncRoot, int index, Func<int, T1, T2, TItem> generator, T1 argument1, T2 argument2)
    {
      TItem item = GetValue(index);
      if (!ReferenceEquals(item, defaultItem))
        return item;
      else lock (syncRoot) {
        item = GetValue(index);
        if (!ReferenceEquals(item, defaultItem))
          return item;
        item = generator.Invoke(index, argument1, argument2);
        SetValue(index, item);
        return item;
      }
    }

    #endregion

    #region Base methods: GetValue, SetValue, Clear

    /// <summary>
    /// Gets the value by its index.
    /// </summary>
    /// <param name="index">The index to get value for.</param>
    /// <returns>Found value, or <see langword="default(TItem)"/>.</returns>
    public TItem GetValue(int index)
    {
      try {
        return implementation[index];
      }
      catch (IndexOutOfRangeException) {
        return defaultItem;
      }
    }

    /// <summary>
    /// Sets the value associated with specified index.
    /// </summary>
    /// <param name="index">The index to set value for.</param>
    /// <param name="item">The value to set.</param>
    public void  SetValue(int index, TItem item)
    {
      lock (implementation) {
        if (index<0)
          throw new ArgumentOutOfRangeException("index");
        int length = implementation.Length;
        if (index<length) {
          implementation[index] = item;
          return;
        }
        while (index>=length) checked {
          length *= 2;
        }
        TItem[] newImplementation = new TItem[length];
        implementation.Copy(newImplementation, 0);
        newImplementation[index] = item;
        implementation = newImplementation;
      }
    }

    /// <summary>
    /// Clears the list.
    /// </summary>
    public void Clear()
    {
      lock (implementation) {
        implementation = new TItem[InitialSize];
      }
    }

    #endregion

    /// <summary>
    /// Initializes the list. 
    /// This method should be invoked just once - before
    /// the first operation on this list.
    /// </summary>
    public void Initialize()
    {
      if (implementation!=null)
        throw Exceptions.AlreadyInitialized(null);
      implementation = new TItem[InitialSize];
    }


    // Static constructor replacement
    
    /// <summary>
    /// Creates and initializes a new <see cref="ThreadSafeList{TItem}"/>.
    /// </summary>
    /// <returns>New initialized <see cref="ThreadSafeList{TItem}"/>.</returns>
    public static ThreadSafeList<TItem> Create()
    {
      ThreadSafeList<TItem> result = new ThreadSafeList<TItem>();
      result.Initialize();
      return result;
    }
  }
}