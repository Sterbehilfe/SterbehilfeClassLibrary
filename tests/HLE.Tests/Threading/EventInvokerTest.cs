using System;
using System.Threading;
using System.Threading.Tasks;
using HLE.Test.TestUtilities;
using HLE.Threading;
using Xunit;

namespace HLE.Tests.Threading;

public sealed class EventInvokerTest
{
    public static TheoryData<int> TargetCountParameters { get; } = TheoryDataHelpers.CreateRange(0, 16);

    private int _counter;

    [Theory]
    [MemberData(nameof(TargetCountParameters))]
    public async Task InvokeAsync_Test_Async(int targetCount)
    {
        AsyncEventHandler<EventInvokerTest, string>? eventHandler = null;
        for (int i = 0; i < targetCount; i++)
        {
            eventHandler += OnSomethingAsync;
        }

        await EventInvoker.InvokeAsync(eventHandler, this, "hello");

        int invocationListLength = eventHandler?.GetInvocationList().Length ?? 0;
        Assert.Equal(invocationListLength, _counter);
    }

    [Theory]
    [MemberData(nameof(TargetCountParameters))]
    public async Task QueueOnThreadPool_Test_Async(int targetCount)
    {
        EventHandler<string>? eventHandler = null;
        for (int i = 0; i < targetCount; i++)
        {
            eventHandler += OnSomething;
        }

        EventInvoker.QueueOnThreadPool(eventHandler, this, "hello");
        await Task.Delay(1_024);

        int invocationListLength = eventHandler?.GetInvocationList().Length ?? 0;
        Assert.Equal(invocationListLength, _counter);
    }

    private Task OnSomethingAsync(EventInvokerTest sender, string args)
    {
        Assert.Same(this, sender);
        Assert.Same("hello", args);
        Interlocked.Increment(ref _counter);
        return Task.Delay(Random.Shared.Next(64, 512));
    }

    private void OnSomething(object? sender, string args)
    {
        Assert.Same(this, sender);
        Assert.Same("hello", args);
        Interlocked.Increment(ref _counter);
        Thread.Sleep(Random.Shared.Next(64, 512));
    }
}