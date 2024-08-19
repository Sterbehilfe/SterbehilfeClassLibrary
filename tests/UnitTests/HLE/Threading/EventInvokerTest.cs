using System;
using System.Threading;
using System.Threading.Tasks;
using HLE.TestUtilities;
using HLE.Threading;
using Xunit;

namespace HLE.UnitTests.Threading;

public sealed class EventInvokerTest
{
    public static TheoryData<int> TargetCountParameters { get; } = TheoryDataHelpers.CreateRange(0, Environment.ProcessorCount * 2);

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
        Assert.Equal(targetCount, invocationListLength);
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
        await Task.Delay(targetCount * 2, TestContext.Current.CancellationToken);

        int invocationListLength = eventHandler?.GetInvocationList().Length ?? 0;
        Assert.Equal(targetCount, invocationListLength);
        Assert.Equal(invocationListLength, _counter);
    }

    private Task OnSomethingAsync(EventInvokerTest sender, string args)
    {
        Assert.Same(this, sender);
        Assert.Same("hello", args);
        Interlocked.Increment(ref _counter);
        return Task.CompletedTask;
    }

    private void OnSomething(object? sender, string args)
    {
        Assert.Same(this, sender);
        Assert.Same("hello", args);
        Interlocked.Increment(ref _counter);
    }
}