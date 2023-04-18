﻿using System;
using HLE.Collections;
using HLE.Memory;
using HLE.Twitch.Models;

namespace HLE.Twitch;

/// <summary>
/// A class that handles incoming IRC messages.
/// </summary>
public sealed class IrcHandler : IEquatable<IrcHandler>
{
    #region Events

    /// <summary>
    /// Is invoked if a JOIN message has been received.
    /// </summary>
    public event EventHandler<JoinedChannelArgs>? OnJoinedChannel;

    /// <summary>
    /// Is invoked if a PART message has been received.
    /// </summary>
    public event EventHandler<LeftChannelArgs>? OnLeftChannel;

    /// <summary>
    /// Is invoked if a ROOMSTATE message has been received.
    /// </summary>
    public event EventHandler<RoomstateArgs>? OnRoomstateReceived;

    /// <summary>
    /// Is invoked if a PRIVMSG message has been received.
    /// </summary>
    public event EventHandler<ChatMessage>? OnChatMessageReceived;

    internal event EventHandler? OnReconnectReceived;

    internal event EventHandler<ReceivedData>? OnPingReceived;

    #endregion Events

    private const string _joinCommand = "JOIN";
    private const string _roomstateCommand = "ROOMSTATE";
    private const string _privmsgCommand = "PRIVMSG";
    private const string _pingCommand = "PING";
    private const string _partCommand = "PART";
    private const string _reconnectCommand = "RECONNECT";

    /// <summary>
    /// Handles the incoming messages.
    /// </summary>
    /// <param name="ircMessage">The IRC message.</param>
    /// <returns>True, if an event has been invoked, otherwise false.</returns>
    public bool Handle(ReadOnlySpan<char> ircMessage)
    {
        Span<int> indicesOfWhitespace = stackalloc int[ircMessage.Length];
        int whitespaceCount = ircMessage.IndicesOf(' ', indicesOfWhitespace);

        switch (whitespaceCount)
        {
            case > 2:
                ReadOnlySpan<char> thirdWord = ircMessage[(indicesOfWhitespace[1] + 1)..indicesOfWhitespace[2]];
                if (thirdWord.Equals(_privmsgCommand, StringComparison.Ordinal))
                {
                    OnChatMessageReceived?.Invoke(this, new(ircMessage, indicesOfWhitespace[..whitespaceCount]));
                    return true;
                }

                if (thirdWord.Equals(_roomstateCommand, StringComparison.Ordinal))
                {
                    OnRoomstateReceived?.Invoke(this, new(ircMessage, indicesOfWhitespace[..whitespaceCount]));
                    return true;
                }

                break;
            case > 1:
                ReadOnlySpan<char> secondWord = ircMessage[(indicesOfWhitespace[0] + 1)..indicesOfWhitespace[1]];
                if (secondWord.Equals(_joinCommand, StringComparison.Ordinal))
                {
                    OnJoinedChannel?.Invoke(this, new(ircMessage, indicesOfWhitespace[..whitespaceCount]));
                    return true;
                }

                if (secondWord.Equals(_partCommand, StringComparison.Ordinal))
                {
                    OnLeftChannel?.Invoke(this, new(ircMessage, indicesOfWhitespace[..whitespaceCount]));
                    return true;
                }

                break;
            case > 0:
                ReadOnlySpan<char> firstWord = ircMessage[..indicesOfWhitespace[0]];
                if (firstWord.Equals(_pingCommand, StringComparison.Ordinal))
                {
                    OnPingReceived?.Invoke(this, ReceivedData.Create(ircMessage[6..]));
                    return true;
                }

                secondWord = ircMessage[(indicesOfWhitespace[0] + 1)..];
                if (secondWord.Equals(_reconnectCommand, StringComparison.Ordinal))
                {
                    OnReconnectReceived?.Invoke(this, EventArgs.Empty);
                    return true;
                }

                break;
        }

        return false;
    }

    public bool Equals(IrcHandler? other)
    {
        return ReferenceEquals(this, other);
    }

    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj);
    }

    public override int GetHashCode()
    {
        return MemoryHelper.GetRawDataPointer(this).GetHashCode();
    }
}
