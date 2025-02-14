﻿namespace Phetch.Core;

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// A query with unknown type arguments.
/// </summary>
public interface IQuery
{
    /// <summary>
    /// An event that fires whenever the state of this query changes.
    /// </summary>
    public event Action StateChanged;

    /// <summary>
    /// The current status of this query.
    /// </summary>
    public QueryStatus Status { get; }

    /// <summary>
    /// The exception thrown the last time the query failed with this arg, or <c>null</c> if the
    /// query has never failed with this arg.
    /// </summary>
    [SuppressMessage("Naming", "CA1716:Identifiers should not match keywords", Justification = "Only conflicts with VB.NET")]
    public Exception? Error { get; }

    /// <summary>
    /// True if the query is currently loading and has not previously succeeded with the same argument.
    /// </summary>
    /// <remarks>
    /// This will return <c>false</c> if the query is currently re-fetching in the background, and
    /// already has data. Use <see cref="IsFetching"/> for these cases (e.g., to show a loading indicator).
    /// </remarks>
    public bool IsLoading { get; }

    /// <summary>
    /// True if the query threw an exception and has not been re-run.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    public bool IsError { get; }

    /// <summary>
    /// True if the query has succeeded.
    /// </summary>
    /// <remarks>
    /// In many cases you should prefer to use <see cref="HasData"/> as it works better with
    /// nullable reference types.
    /// </remarks>
    public bool IsSuccess { get; }

    /// <summary>
    /// True if the query has succeeded and returned a non-null response.
    /// </summary>
    /// <remarks>
    /// This is particularly useful in combination with nullable reference types, as it lets you
    /// safely access <see cref="IQuery{TArg, TResult}.Data"/> without a compiler warning.
    /// </remarks>
    public bool HasData { get; }

    /// <summary>
    /// True if no arguments have been provided to this query yet.
    /// </summary>
    public bool IsUninitialized { get; }

    /// <summary>
    /// True if the query is currently running, either for the initial load or for subsequent
    /// fetches once the data is stale.
    /// </summary>
    /// <remarks>
    /// If you only need to know about the initial load, use <see cref="IsLoading"/> instead.
    /// </remarks>
    public bool IsFetching { get; }

    /// <summary>
    /// Stop listening to changes of the current query.
    /// </summary>
    public void Detach();

    /// <summary>
    /// Cancels the currently running query, along with the <see cref="CancellationToken"/> that was
    /// passed to it.
    /// </summary>
    /// <remarks>
    /// If the query function does not use the CancellationToken, the query state will be reset, but
    /// the underlying operation will continue to run.
    /// You should not rely on this to cancel operations with side effects.
    /// </remarks>
    public void Cancel();
}

/// <summary>
/// An asynchronous query taking one parameter of type <typeparamref name="TArg"/> and returning a
/// result of type <typeparamref name="TResult"/>
/// </summary>
/// <remarks>
/// <para>For queries with no parameters, you can use the <see cref="Query{TResult}"/> class.</para>
/// <para>For queries with multiple parameters, you can use a tuple or record in place of <c>TArg</c>:
/// <code>Query&lt;(int, string), string&gt;</code>
/// </para>
/// </remarks>
public interface IQuery<TArg, TResult> : IQuery
{
    /// <summary>
    /// An event that fires whenever this query succeeds.
    /// </summary>
    public event Action<QuerySuccessEventArgs<TArg, TResult>>? Succeeded;

    /// <summary>
    /// An event that fires whenever this query fails.
    /// </summary>
    public event Action<QueryFailureEventArgs<TArg>>? Failed;

    /// <summary>
    /// Options for this query.
    /// </summary>
    public QueryOptions<TArg, TResult> Options { get; }

    /// <summary>
    /// The current argument passed to this query, or <c>default</c> if the query is uninitialized.
    /// </summary>
    public TArg? Arg { get; }

    /// <summary>
    /// The response data from the current query if it exists.
    /// </summary>
    /// <remarks>
    /// To also keep data from previous args while a new query is loading, use <see
    /// cref="LastData"/> instead.
    /// </remarks>
    public TResult? Data { get; }

    /// <summary>
    /// The response data from the current query if it exists, otherwise the response data from the
    /// last successful query.
    /// </summary>
    /// <remarks>
    /// This is useful for pagination, if you want to keep the data of the previous page visible
    /// while the next page loads. May return data from a different query argument if the argument
    /// has changed.
    /// </remarks>
    public TResult? LastData { get; }

    /// <summary>
    /// The instance of <see cref="FixedQuery{TArg, TResult}"/> that this query is currently
    /// observing. This changes every time the <see cref="Arg"/> for this query changes.
    /// </summary>
    public FixedQuery<TArg, TResult>? CurrentQuery { get; }

    /// <summary>
    /// Runs the original query function once, completely bypassing caching and other extra behavior
    /// </summary>
    /// <param name="arg">The argument passed to the query function</param>
    /// <param name="ct">An optional cancellation token</param>
    /// <returns>The value returned by the query function</returns>
    public Task<TResult> Invoke(TArg arg, CancellationToken ct = default);

    /// <summary>
    /// Re-runs the query using the most recent argument and returns the result asynchronously.
    /// </summary>
    /// <returns>The value returned by the query function</returns>
    /// <exception cref="InvalidOperationException">Thrown if no argument has been provided to the query</exception>
    public Task<TResult> RefetchAsync();

    /// <summary>
    /// Updates the argument for this query, and re-run the query if the argument has changed.
    /// </summary>
    /// <remarks>
    /// If you do not need to <c>await</c> the completion of the query, use <see cref="QueryExtensions.SetArg"/> instead.
    /// </remarks>
    /// <returns>
    /// A <see cref="Task"/> which completes when the query returns, or immediately if there is a
    /// non-stale cached value for this argument.
    /// </returns>
    public Task<TResult> SetArgAsync(TArg arg);

    /// <summary>
    /// Run the query function without sharing state or cache with other queries.
    /// </summary>
    /// <remarks>
    /// This is typically used for queries that have side effects (e.g., POST requests). This has
    /// the following differences from <see cref="SetArgAsync(TArg)"/>:
    /// <list type="bullet">
    /// <item>
    /// This will always run the query function, even if it was previously run with the same query argument.
    /// </item>
    /// <item>
    /// The state of this query (including the cached return value) will not be shared with other
    /// queries that use the same query argument.
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="arg">The argument to pass to the query function</param>
    /// <returns>The value returned by the query function</returns>
    public Task<TResult> TriggerAsync(TArg arg);
}

/// <inheritdoc cref="IQuery{TArg, TResult}"/>
public class Query<TArg, TResult> : IQuery<TArg, TResult>
{
    private readonly QueryCache<TArg, TResult> _cache;
    private readonly QueryOptions<TArg, TResult>? _options;
    private readonly TimeSpan _staleTime;
    private FixedQuery<TArg, TResult>? _lastSuccessfulQuery;
    private FixedQuery<TArg, TResult>? _currentQuery;

    /// <inheritdoc/>
    public event Action StateChanged = delegate { };

    /// <inheritdoc/>
    public event Action<QuerySuccessEventArgs<TArg, TResult>>? Succeeded;

    /// <inheritdoc/>
    public event Action<QueryFailureEventArgs<TArg>>? Failed;

    /// <inheritdoc/>
    public QueryOptions<TArg, TResult> Options => _options ?? QueryOptions<TArg, TResult>.Default;

    internal Query(
        QueryCache<TArg, TResult> cache,
        QueryOptions<TArg, TResult>? options,
        EndpointOptions<TArg, TResult> endpointOptions)
    {
        _cache = cache;
        _options = options;
        _staleTime = options?.StaleTime ?? endpointOptions.DefaultStaleTime;
        Succeeded += options?.OnSuccess;
        Failed += options?.OnFailure;
    }

    /// <inheritdoc/>
    public TArg? Arg => _currentQuery is { } query ? query.Arg : default;

    /// <inheritdoc/>
    public QueryStatus Status => _currentQuery?.Status ?? QueryStatus.Idle;

    /// <inheritdoc/>
    public TResult? Data => _currentQuery is not null
        ? _currentQuery.Data
        : default;

    /// <inheritdoc/>
    public TResult? LastData => IsSuccess
        ? _currentQuery.Data
        : _lastSuccessfulQuery?.Status == QueryStatus.Success
            ? _lastSuccessfulQuery.Data
            : default;

    /// <inheritdoc/>
    public Exception? Error => _currentQuery?.Error;

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(CurrentQuery))]
    public bool IsLoading => _currentQuery?.Status == QueryStatus.Loading;

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(true, nameof(CurrentQuery))]
    public bool IsError => Status == QueryStatus.Error;

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(_currentQuery))]
    [MemberNotNullWhen(true, nameof(CurrentQuery))]
    public bool IsSuccess => _currentQuery?.Status == QueryStatus.Success;

    /// <inheritdoc/>
    [MemberNotNullWhen(true, nameof(Data))]
    public bool HasData => IsSuccess && Data is not null;

    /// <inheritdoc/>
    [MemberNotNullWhen(false, nameof(Arg))]
    [MemberNotNullWhen(false, nameof(CurrentQuery))]
    public bool IsUninitialized => Status == QueryStatus.Idle;

    /// <inheritdoc/>
    public bool IsFetching => _currentQuery?.IsFetching ?? false;

    /// <inheritdoc/>
    public FixedQuery<TArg, TResult>? CurrentQuery => _currentQuery;

    /// <inheritdoc/>
    public void Detach()
    {
        // TODO: Consider redesign
        _currentQuery?.RemoveObserver(this);
        _currentQuery = null;
    }

    /// <inheritdoc/>
    public Task<TResult> Invoke(TArg arg, CancellationToken ct = default)
    {
        return _cache.QueryFn.Invoke(arg, ct);
    }

    /// <inheritdoc/>
    public void Cancel() => _currentQuery?.Cancel();

    /// <inheritdoc/>
    public Task<TResult> RefetchAsync()
    {
        if (_currentQuery is null)
            throw new InvalidOperationException("Cannot refetch an uninitialized query");

        return _currentQuery.RefetchAsync(_options?.RetryHandler);
    }

    /// <inheritdoc/>
    public async Task<TResult> SetArgAsync(TArg arg)
    {
        var newQuery = _cache.GetOrAdd(arg);
        if (newQuery != _currentQuery)
        {
            _currentQuery?.RemoveObserver(this);
            newQuery.AddObserver(this);
            _currentQuery = newQuery;
            // TODO: Is this the best behavior?

            if (!newQuery.IsFetching && newQuery.IsStaleByTime(_staleTime, DateTime.Now))
            {
                return await newQuery.RefetchAsync(_options?.RetryHandler).ConfigureAwait(false);
            }
        }
        if (newQuery.LastInvocation is { } task)
        {
            return await task;
        }
        else
        {
            // Probably not possible to get here, but just in case
            Debug.Fail("newQuery should have been invoked before this point");
            return newQuery.Data!;
        }
    }

    /// <inheritdoc/>
    public async Task<TResult> TriggerAsync(TArg arg)
    {
        // TODO: Re-use when arguments unchanged?
        var query = _cache.AddUncached(arg);
        _currentQuery?.RemoveObserver(this);
        query.AddObserver(this);
        _currentQuery = query;
        return await query.RefetchAsync(_options?.RetryHandler).ConfigureAwait(false);
    }

    internal void OnQuerySuccess(QuerySuccessEventArgs<TArg, TResult> args)
    {
        _lastSuccessfulQuery = _currentQuery;
        Succeeded?.Invoke(args);
        StateChanged?.Invoke();
    }

    internal void OnQueryFailure(QueryFailureEventArgs<TArg> args)
    {
        Failed?.Invoke(args);
        StateChanged?.Invoke();
    }

    internal void OnQueryUpdate()
    {
        StateChanged?.Invoke();
    }
}

/// <summary>
/// An alternate version of <see cref="Query{TArg, TResult}"/> for queries with no return value.
/// </summary>
/// <remarks>Aside from having no return value, this functions identically to a normal Query</remarks>
[Obsolete("Use Query<TArg, Unit> instead, which is equivalent.", true)]
public class Mutation<TArg> : Query<TArg, Unit>
{
    internal Mutation(
        QueryCache<TArg, Unit> cache,
        QueryOptions<TArg, Unit>? options,
        EndpointOptions<TArg, Unit> endpointOptions
    ) : base(cache, options, endpointOptions)
    {
    }
}

/// <summary>
/// An alternate version of <see cref="Query{TArg, TResult}"/> for queries with no parameters.
/// </summary>
/// <remarks>Aside from having no parameters, this functions identically to a normal Query</remarks>
public class Query<TResult> : Query<Unit, TResult>
{
    internal Query(
        QueryCache<Unit, TResult> cache,
        QueryOptions<Unit, TResult>? options,
        EndpointOptions<Unit, TResult> endpointOptions
    ) : base(cache, options, endpointOptions)
    { }
}

/// <summary>
/// A collection of helpers to simplify working with <see cref="IQuery"/> objects.
/// </summary>
public static class QueryExtensions
{
    /// <summary>
    /// Updates the argument for this query, and re-run the query if the argument has changed.
    /// </summary>
    /// <remarks>
    /// If you need to <c>await</c> the completion of the query, use <see cref="Query{TArg, TResult}.SetArgAsync(TArg)"/> instead.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public static void SetArg<TArg, TResult>(this IQuery<TArg, TResult> self, TArg arg)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        _ = self.SetArgAsync(arg);
    }

    /// <summary>
    /// Re-runs the query using the most recent argument, without waiting for the result.
    /// </summary>
    /// <remarks>
    /// To also return the result of the query, use <see cref="Query{TArg, TResult}.RefetchAsync"/>.
    /// </remarks>
    /// <inheritdoc cref="Query{TArg, TResult}.RefetchAsync" path="/exception"/>
    [ExcludeFromCodeCoverage]
    public static void Refetch<TArg, TResult>(this IQuery<TArg, TResult> self)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        _ = self.RefetchAsync();
    }

    /// <inheritdoc cref="Query{TArg, TResult}.TriggerAsync(TArg)"/>
    /// <param name="self">The query to trigger.</param>
    /// <param name="arg">The argument to pass to the query.</param>
    /// <param name="onFailure">
    /// An optional callback which will be fired if the query fails. This is not fired if the query
    /// is cancelled.
    /// </param>
    /// <param name="onSuccess">An optional callback which will be fired if the query succeeds.</param>
    public static async Task<TResult> TriggerAsync<TArg, TResult>(
        this IQuery<TArg, TResult> self,
        TArg arg,
        Action<QuerySuccessEventArgs<TArg, TResult>>? onSuccess = null,
        Action<QueryFailureEventArgs<TArg>>? onFailure = null)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        try
        {
            var result = await self.TriggerAsync(arg);
            onSuccess?.Invoke(new(arg, result));
            return result;
        }
        // OperationCancelledException is generally not considered a failure
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            onFailure?.Invoke(new(arg, ex));
            throw;
        }
    }

    /// <remarks>
    /// <inheritdoc cref="Query{TArg, TResult}.TriggerAsync(TArg)"/>
    /// To also return the result of the query, use <see cref="Query{TArg, TResult}.TriggerAsync(TArg)"/>.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public static void Trigger<TArg, TResult>(
        this IQuery<TArg, TResult> self,
        TArg arg,
        Action<QuerySuccessEventArgs<TArg, TResult>>? onSuccess = null,
        Action<QueryFailureEventArgs<TArg>>? onFailure = null)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        _ = self.TriggerAsync(arg, onSuccess, onFailure);
    }

    /// <summary>
    /// Causes this query to fetch if it has not already.
    /// </summary>
    /// <remarks>
    /// This is equivalent to <see cref="Query{TArg, TResult}.SetArgAsync(TArg)"/>, but for parameterless queries.
    /// </remarks>
    [ExcludeFromCodeCoverage]
    public static void Fetch<TResult>(this Query<Unit, TResult> self)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        _ = self.SetArgAsync(default);
    }

    /// <inheritdoc cref="Fetch"/>
    [ExcludeFromCodeCoverage]
    public static Task<TResult> FetchAsync<TResult>(this Query<Unit, TResult> self)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        return self.SetArgAsync(default);
    }

    /// <inheritdoc cref="Query{TArg, TResult}.TriggerAsync(TArg)"/>
    [ExcludeFromCodeCoverage]
    public static void Trigger<TResult>(
        this Query<Unit, TResult> self,
        Action<QuerySuccessEventArgs<Unit, TResult>>? onSuccess = null,
        Action<QueryFailureEventArgs<Unit>>? onFailure = null)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        _ = self.TriggerAsync(default, onSuccess, onFailure);
    }

    /// <inheritdoc cref="Query{TArg, TResult}.TriggerAsync(TArg)"/>
    [ExcludeFromCodeCoverage]
    public static Task<TResult> TriggerAsync<TResult>(this Query<Unit, TResult> self)
    {
        _ = self ?? throw new ArgumentNullException(nameof(self));
        return self.TriggerAsync(default);
    }
}
