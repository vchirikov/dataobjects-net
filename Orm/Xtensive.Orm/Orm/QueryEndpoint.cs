﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Xtensive.Core;
using Xtensive.Diagnostics;
using Xtensive.Orm.Internals;
using Xtensive.Orm.Internals.Prefetch;
using Xtensive.Orm.Linq;
using Xtensive.Orm.Linq.Expressions.Visitors;
using Xtensive.Parameters;
using Xtensive.Reflection;
using Activator = System.Activator;
using Tuple = Xtensive.Tuples.Tuple;

namespace Xtensive.Orm
{
  /// <summary>
  /// Provides methods allowing to run LINQ queries,
  /// create future (delayed) and compiled queries,
  /// and finally, resolve <see cref="Key"/>s to <see cref="Entity">entities</see>.
  /// </summary>
  public sealed class QueryEndpoint
  {
    private readonly Session session;

    /// <summary>
    /// Gets outer <see cref="QueryEndpoint"/>.
    /// For root <see cref="QueryEndpoint"/> returns <see langword="null"/>.
    /// </summary>
    public QueryEndpoint Outer { get; private set; }

    /// <summary>
    /// Gets <see cref="IQueryProvider"/> implementation
    /// for this session.
    /// </summary>
    public QueryProvider Provider { get; private set; }

    /// <summary>
    /// Gets <see cref="IQueryRootBuilder"/> associated with this instance.
    /// If <see cref="IQueryRootBuilder"/> is not set for this instance
    /// returns <see langword="null"/>.
    /// </summary>
    public IQueryRootBuilder RootBuilder { get; private set; }

    /// <summary>
    /// The "starting point" for any LINQ query -
    /// a <see cref="IQueryable{T}"/> enumerating all the instances
    /// of type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">Type of the sequence element.</typeparam>
    /// <returns>
    /// An <see cref="IQueryable{T}"/> enumerating all the instances
    /// of type <typeparamref name="T"/>.
    /// </returns>
    public IQueryable<T> All<T>()
      where T : class, IEntity
    {
      return Provider.CreateQuery<T>(BuildRootExpression(typeof (T)));
    }

    /// <summary>
    /// The "starting point" for dynamic LINQ query -
    /// a <see cref="IQueryable"/> enumerating all the instances
    /// of type <paramref name="elementType"/>.
    /// </summary>
    /// <param name="elementType">Type of the sequence element.</param>
    /// <returns>
    /// An <see cref="IQueryable"/> enumerating all the instances
    /// of type <paramref name="elementType"/>.
    /// </returns>
    public IQueryable All(Type elementType)
    {
      var provider = (IQueryProvider) Provider;
      return provider.CreateQuery(BuildRootExpression(elementType));
    }

    /// <summary>
    /// Performs full-text query for the text specified in free text form.
    /// </summary>
    /// <typeparam name="T">Type of the entity to query full-text index of.</typeparam>
    /// <param name="searchCriteria">The search criteria in free text form.</param>
    /// <returns>
    /// An <see cref="IQueryable{T}"/> of <see cref="FullTextMatch{T}"/>
    /// allowing to continue building the query.
    /// </returns>
    public IQueryable<FullTextMatch<T>> FreeText<T>(string searchCriteria)
      where T : Entity
    {
      ArgumentValidator.EnsureArgumentNotNull(searchCriteria, "searchCriteria");
      var method = WellKnownMembers.Query.FreeTextString.MakeGenericMethod(typeof(T));
      var expression = Expression.Call(method, Expression.Constant(searchCriteria));
      return Provider.CreateQuery<FullTextMatch<T>>(expression);
    }

    /// <summary>
    /// Performs full-text query for the text specified in free text form.
    /// </summary>
    /// <typeparam name="T">Type of the entity to query full-text index of.</typeparam>
    /// <param name="searchCriteria">The search criteria in free text form.</param>
    /// <returns>
    /// An <see cref="IQueryable{T}"/> of <see cref="FullTextMatch{T}"/>
    /// allowing to continue building the query.
    /// </returns>
    public IQueryable<FullTextMatch<T>> FreeText<T>(Expression<Func<string>> searchCriteria)
      where T : Entity
    {
      ArgumentValidator.EnsureArgumentNotNull(searchCriteria, "searchCriteria");
      var method = WellKnownMembers.Query.FreeTextExpression.MakeGenericMethod(typeof(T));
      var expression = Expression.Call(null, method, new[] { searchCriteria });
      return Provider.CreateQuery<FullTextMatch<T>>(expression);
    }


    /// <summary>
    /// Resolves (gets) the <see cref="Entity"/> by the specified <paramref name="key"/>
    /// in the current <see cref="session"/>.
    /// </summary>
    /// <param name="key">The key to resolve.</param>
    /// <returns>
    /// The <see cref="Entity"/> specified <paramref name="key"/> identifies.
    /// </returns>
    /// <exception cref="KeyNotFoundException">Entity with the specified key is not found.</exception>
    public Entity Single(Key key)
    {
      if (key == null)
        return null;
      var result = SingleOrDefault(key);
      if (result == null)
        throw new KeyNotFoundException(String.Format(
          Strings.EntityWithKeyXDoesNotExist, key));
      return result;
    }

    /// <summary>
    /// Resolves (gets) the <see cref="Entity"/> by the specified <paramref name="key"/>
    /// in the current <see cref="session"/>.
    /// </summary>
    /// <param name="key">The key to resolve.</param>
    /// <returns>
    /// The <see cref="Entity"/> specified <paramref name="key"/> identifies.
    /// <see langword="null"/>, if there is no such entity.
    /// </returns>
    public Entity SingleOrDefault(Key key)
    {
      if (key==null)
        return null;
      Entity result;
      using (var tx = session.OpenAutoTransaction()) {
        EntityState state;
        if (!session.LookupStateInCache(key, out state)) {
          OrmLog.Debug(Strings.LogSessionXResolvingKeyYExactTypeIsZ, session, key, key.HasExactType ? Strings.Known : Strings.Unknown);
          state = session.Handler.FetchEntityState(key);
        }
        if (state==null || state.IsNotAvailableOrMarkedAsRemoved
          || !key.TypeReference.Type.UnderlyingType.IsAssignableFrom(state.Type.UnderlyingType))
          // No state or Tuple = null or incorrect query type => no data in storage
          result = null;
        else
          result = state.Entity;

        tx.Complete();
      }
      return result;
    }

    /// <summary>
    /// Resolves (gets) the <see cref="Entity"/> by the specified <paramref name="key"/>
    /// in the current <see cref="session"/>.
    /// </summary>
    /// <typeparam name="T">Type of the entity.</typeparam>
    /// <param name="key">The key to resolve.</param>
    /// <returns>
    /// The <see cref="Entity"/> specified <paramref name="key"/> identifies.
    /// <see langword="null"/>, if there is no such entity.
    /// </returns>
    public T Single<T>(Key key)
      where T : class, IEntity
    {
      return (T)(object)Single(key);
    }

    /// <summary>
    /// Resolves (gets) the <see cref="Entity"/> by the specified <paramref name="keyValues"/>
    /// in the current <see cref="session"/>.
    /// </summary>
    /// <typeparam name="T">Type of the entity.</typeparam>
    /// <param name="keyValues">Key values.</param>
    /// <returns>
    /// The <see cref="Entity"/> specified <paramref name="keyValues"/> identify.
    /// <see langword="null"/>, if there is no such entity.
    /// </returns>
    public T Single<T>(params object[] keyValues)
      where T : class, IEntity
    {
      return (T)(object)Single(GetKeyByValues<T>(keyValues));
    }

    /// <summary>
    /// Resolves (gets) the <see cref="Entity"/> by the specified <paramref name="key"/>
    /// in the current <see cref="session"/>.
    /// </summary>
    /// <typeparam name="T">Type of the entity.</typeparam>
    /// <param name="key">The key to resolve.</param>
    /// <returns>
    /// The <see cref="Entity"/> specified <paramref name="key"/> identifies.
    /// </returns>
    public T SingleOrDefault<T>(Key key)
      where T : class, IEntity
    {
      return (T)(object)SingleOrDefault(key);
    }

    /// <summary>
    /// Resolves (gets) the <see cref="Entity"/> by the specified <paramref name="keyValues"/>
    /// in the current <see cref="session"/>.
    /// </summary>
    /// <typeparam name="T">Type of the entity.</typeparam>
    /// <param name="keyValues">Key values.</param>
    /// <returns>
    /// The <see cref="Entity"/> specified <paramref name="keyValues"/> identify.
    /// </returns>
    public T SingleOrDefault<T>(params object[] keyValues)
      where T : class, IEntity
    {
      return (T)(object)SingleOrDefault(GetKeyByValues<T>(keyValues));
    }

    /// <summary>
    /// Fetches multiple instances of specified type  by provided <paramref name="keys"/>.
    /// </summary>
    /// <param name="keys">The source sequence.</param>
    /// <returns>The sequence of entities of type <typeparam name="T"/> matching provided <paramref name="keys"/>.</returns>
    public IEnumerable<T> Many<T>(IEnumerable<Key> keys)
      where T : class, IEntity
    {
      return new PrefetchFacade<T>(session, keys);
    }

    /// <summary>
    /// Fetches multiple instances of specified type  by provided <paramref name="keys"/>.
    /// </summary>
    /// <param name="keys">The source sequence.</param>
    /// <returns>The sequence of entities of type <typeparam name="T"/> matching provided <paramref name="keys"/>.</returns>
    public IEnumerable<T> Many<T,TElement>(IEnumerable<TElement> keys)
      where T : class, IEntity
    {
      var elementType = typeof (TElement);
      Func<TElement, Key> selector;
      if (elementType == typeof(object[]))
        selector = e => Key.Create<T>(session.Domain, (object[]) (object) e);
      else if (typeof (Tuple).IsAssignableFrom(elementType))
        selector = e => Key.Create<T>(session.Domain, (Tuple) (object) e);
      else
        selector = e => Key.Create<T>(session.Domain, new object[] {e} );

      return new PrefetchFacade<T>(session, keys.Select(selector));
    }

    /// <summary>
    /// Finds compiled query in cache by provided <paramref name="query"/> delegate
    /// (in fact, by its <see cref="MethodInfo"/> instance)
    /// and executes them if it's already cached;
    /// otherwise executes the <paramref name="query"/> delegate
    /// and caches the result.
    /// </summary>
    /// <typeparam name="TElement">The type of the resulting sequence element.</typeparam>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>Query result.</returns>
    public IEnumerable<TElement> Execute<TElement>(Func<QueryEndpoint, IQueryable<TElement>> query)
    {
      return new CompiledQueryRunner(this, query.Method, query.Target).ExecuteCompiled(query);
    }

    /// <summary>
    /// Finds compiled query in cache by provided <paramref name="key"/>
    /// and executes them if it's already cached;
    /// otherwise executes the <paramref name="query"/> delegate
    /// and caches the result.
    /// </summary>
    /// <typeparam name="TElement">The type of the resulting sequence element.</typeparam>
    /// <param name="key">An object identifying this query in cache.</param>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>Query result.</returns>
    public IEnumerable<TElement> Execute<TElement>(object key, Func<QueryEndpoint, IQueryable<TElement>> query)
    {
      return new CompiledQueryRunner(this, key, query.Target).ExecuteCompiled(query);
    }

    /// <summary>
    /// Finds compiled query in cache by provided <paramref name="query"/> delegate
    /// (in fact, by its <see cref="MethodInfo"/> instance)
    /// and executes them if it's already cached;
    /// otherwise executes the <paramref name="query"/> delegate
    /// and caches the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>Query result.</returns>
    public TResult Execute<TResult>(Func<QueryEndpoint,TResult> query)
    {
      return new CompiledQueryRunner(this, query.Method, query.Target).ExecuteCompiled(query);
    }

    /// <summary>
    /// Finds compiled query in cache by provided <paramref name="key"/>
    /// and executes them if it's already cached;
    /// otherwise executes the <paramref name="query"/> delegate
    /// and caches the result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="key">An object identifying this query in cache.</param>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>Query result.</returns>
    public TResult Execute<TResult>(object key, Func<QueryEndpoint,TResult> query)
    {
      return new CompiledQueryRunner(this, key, query.Target).ExecuteCompiled(query);
    }

    /// <summary>
    /// Creates future scalar query and registers it for the later execution.
    /// The query associated with the future scalar will be cached.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="key">An object identifying this query in cache.</param>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>
    /// The future that will be executed when its result is requested.
    /// </returns>
    public Delayed<TResult> ExecuteDelayed<TResult>(object key, Func<QueryEndpoint,TResult> query)
    {
      return new CompiledQueryRunner(this, key, query.Target).ExecuteDelayed(query);
    }

    /// <summary>
    /// Creates future scalar query and registers it for the later execution.
    /// The query associated with the future scalar will not be cached.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>
    /// The future that will be executed when its result is requested.
    /// </returns>
    public Delayed<TResult> ExecuteDelayed<TResult>(Func<QueryEndpoint,TResult> query)
    {
      return new CompiledQueryRunner(this, query.Method, query.Target).ExecuteDelayed(query);
    }

    /// <summary>
    /// Creates future query and registers it for the later execution.
    /// The associated query will be cached.
    /// </summary>
    /// <typeparam name="TElement">The type of the resulting sequence element.</typeparam>
    /// <param name="key">An object identifying this query in cache.</param>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>
    /// The future that will be executed when its result is requested.
    /// </returns>
    public IEnumerable<TElement> ExecuteDelayed<TElement>(object key, Func<QueryEndpoint,IQueryable<TElement>> query)
    {
      return new CompiledQueryRunner(this, key, query.Target).ExecuteDelayed(query);
    }

    /// <summary>
    /// Creates future query and registers it for the later execution.
    /// The associated query will be cached.
    /// </summary>
    /// <typeparam name="TElement">The type of the resulting sequence element.</typeparam>
    /// <param name="query">A delegate performing the query to cache.</param>
    /// <returns>
    /// The future that will be executed when its result is requested.
    /// </returns>
    public IEnumerable<TElement> ExecuteDelayed<TElement>(Func<QueryEndpoint,IQueryable<TElement>> query)
    {
      return new CompiledQueryRunner(this, query.Method, query.Target).ExecuteDelayed(query);
    }

    public IQueryable<TElement> Store<TElement>(IEnumerable<TElement> source)
    {
      var method = WellKnownMembers.Queryable.AsQueryable.MakeGenericMethod(typeof(TElement));
      var expression = Expression.Call(method, Expression.Constant(source));
      return Provider.CreateQuery<TElement>(expression);
    }

    #region Private / internal methods

    /// <exception cref="ArgumentException"><paramref name="keyValues"/> array is empty.</exception>
    private Key GetKeyByValues<T>(object[] keyValues)
      where T : class, IEntity
    {
      ArgumentValidator.EnsureArgumentNotNull(keyValues, "keyValues");
      if (keyValues.Length == 0)
        throw new ArgumentException(Strings.ExKeyValuesArrayIsEmpty, "keyValues");
      if (keyValues.Length == 1) {
        var keyValue = keyValues[0];
        if (keyValue is Key)
          return keyValue as Key;
        if (keyValue is Entity)
          return (keyValue as Entity).Key;
      }
      return Key.Create(session.Domain, typeof(T), keyValues);
    }

    private Expression BuildRootExpression(Type elementType)
    {
      return RootBuilder!=null
        ? RootBuilder.BuildRootExpression(elementType)
        : Expression.Call(null, WellKnownMembers.Query.All.MakeGenericMethod(elementType));
    }

    #endregion


    // Constructors

    internal QueryEndpoint(QueryProvider provider)
    {
      ArgumentValidator.EnsureArgumentNotNull(provider, "provider");
      Provider = provider;
      session = provider.Session;
    }

    internal QueryEndpoint(QueryEndpoint outerEndpoint, IQueryRootBuilder queryRootBuilder)
    {
      ArgumentValidator.EnsureArgumentNotNull(outerEndpoint, "outerEndpoint");
      ArgumentValidator.EnsureArgumentNotNull(queryRootBuilder, "queryRootBuilder");
      Provider = outerEndpoint.Provider;
      session = outerEndpoint.session;
      RootBuilder = queryRootBuilder;
    }
  }
}