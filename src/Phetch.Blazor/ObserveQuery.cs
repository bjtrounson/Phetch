﻿namespace Phetch.Blazor;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Phetch.Core;

public sealed class ObserveQuery<TArg, TResult> : ComponentBase, IDisposable
{
    private IQuery<TArg, TResult>? _target;
    [Parameter, EditorRequired]
    public IQuery<TArg, TResult>? Target
    {
        get => _target;
        set
        {
            if (_target == value) return;
            TryUnsubscribe(_target);
            if (value is not null)
            {
                value.StateChanged += OnStateChanged;
                value.Succeeded += SuccessCallback;
                value.Failed += FailureCallback;
            }
            _target = value;
        }
    }

    /// <summary>
    /// Callback function to call whenever the query state changes. This is usually used to trigger
    /// component re-renders using <c>StateHasChanged</c>.
    /// </summary>
    [Parameter]
    public Action OnChanged { get; set; } = null!;

    /// <summary>
    /// Callback function to call when the query succeeds. This will only be called if this
    /// component is still mounted when the query succeeds. If you make multiple requests at the
    /// same time, this will only be called for the last request.
    /// </summary>
    [Parameter]
    public Action<QuerySuccessEventArgs<TArg, TResult>>? OnSuccess { get; set; }

    /// <summary>
    /// Callback function to call when the query fails. This will only be called if this component
    /// is still mounted when the query fails. If you make multiple requests at the
    /// same time, this will only be called for the last request.
    /// </summary>
    [Parameter]
    public Action<QueryFailureEventArgs<TArg>>? OnFailure { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public void Dispose() => TryUnsubscribe(_target);

    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        if (ChildContent is not null)
        {
            builder?.AddContent(0, ChildContent);
        }
    }

    private void TryUnsubscribe(IQuery<TArg, TResult>? query)
    {
        if (query is not null)
        {
            query.StateChanged -= StateHasChanged;
            query.Detach();
        }
    }

    private void OnStateChanged()
    {
        OnChanged?.Invoke();
        StateHasChanged();
    }

    private void SuccessCallback(QuerySuccessEventArgs<TArg, TResult> context) { OnSuccess?.Invoke(context); }

    private void FailureCallback(QueryFailureEventArgs<TArg> context) { OnFailure?.Invoke(context); }
}
