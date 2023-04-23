﻿using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using HLE.Twitch.Api.Models;

namespace HLE.Twitch.Api.JsonConverters;

public sealed class TimeOfExpirationJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        int expiresInSeconds = NumberHelper.ParsePositiveInt32(reader.ValueSpan);
        TimeSpan expiresIn = TimeSpan.FromMilliseconds(expiresInSeconds);
        return DateTime.UtcNow + expiresIn;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        throw new NotSupportedException($"The property {nameof(AccessToken)}.{nameof(AccessToken.TimeOfExpiration)} is not available for serialization.");
    }
}