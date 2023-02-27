using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using HLE.Memory;

namespace HLE.Emojis;

/// <summary>
/// The generator I used to create the Emoji file containing every Emoji.
/// </summary>
public sealed class EmojiFileGenerator
{
    public string NamespaceName { get; set; }

    public char IndentationChar { get; set; }

    public int IndentationSize { get; set; }

    private readonly FrozenDictionary<string, string> _illegalWords = new Dictionary<string, string>
    {
        { "100", "Hundred" },
        { "+1", "ThumbUp" },
        { "-1", "ThumbDown" },
        { "T-rex", "TRex" },
        { "1st_place_medal", "FirstPlaceMedal" },
        { "2nd_place_medal", "SecondPlaceMedal" },
        { "3rd_place_medal", "ThirdPlaceMedal" },
        { "8ball", "EightBall" },
        { "Non-potable_water", "NonPotableWater" },
        { "1234", "OneTwoThreeFour" }
    }.ToFrozenDictionary();

    private byte[]? _emojiJsonBytes;

    private const string _publicConstString = "public const string";
    private const string _equalSignSpaceQuotation = "= \"";
    private const string _quotationSemicolon = "\";";

    public EmojiFileGenerator(string namespaceName, char indentationChar = ' ', int indentationSize = 4)
    {
        NamespaceName = namespaceName;
        IndentationChar = indentationChar;
        IndentationSize = indentationSize;
    }

    /// <summary>
    /// Generates the Emoji file.
    /// <returns>The source code of the file. Null, if the creation was unsuccessful, due to e.g. not being able to retrieve the emoji data.</returns>
    /// </summary>
    [Pure]
    public string Generate()
    {
        if (_emojiJsonBytes is null)
        {
            using HttpClient httpClient = new();
            Task<byte[]> task = httpClient.GetByteArrayAsync("https://raw.githubusercontent.com/github/gemoji/master/db/emoji.json");
            task.Wait();
            _emojiJsonBytes = task.Result;
        }

        StringBuilder builder = MemoryHelper.UseStackAlloc<char>(_emojiJsonBytes.Length >> 2) ? stackalloc char[_emojiJsonBytes.Length >> 2] : new char[_emojiJsonBytes.Length >> 2];
        AppendFileHeader(ref builder);
        AppendEmojis(ref builder);

        builder.Append('}');
        builder.Append(Environment.NewLine);
        return builder.ToString();
    }

    private void AppendEmojis(ref StringBuilder builder)
    {
        Span<char> indentation = stackalloc char[IndentationSize];
        indentation.Fill(IndentationChar);

        ReadOnlySpan<byte> emojiProperty = "emoji"u8;
        ReadOnlySpan<byte> aliasesProperty = "aliases"u8;

        JsonReaderOptions options = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };
        Utf8JsonReader jsonReader = new(_emojiJsonBytes, options);

        Span<char> emojiBuffer = stackalloc char[100];
        int emojiLength = 0;
        Span<char> nameBuffer = stackalloc char[100];

        while (jsonReader.Read())
        {
            switch (jsonReader.TokenType)
            {
                case JsonTokenType.PropertyName when jsonReader.ValueTextEquals(emojiProperty):
                    jsonReader.Read();
                    ReadOnlySpan<byte> emojiBytes = jsonReader.ValueSpan;
                    emojiLength = Encoding.UTF8.GetChars(emojiBytes, emojiBuffer);
                    break;
                case JsonTokenType.PropertyName when jsonReader.ValueTextEquals(aliasesProperty):
                    jsonReader.Read();
                    jsonReader.Read();
                    ReadOnlySpan<byte> nameBytes = jsonReader.ValueSpan;
                    int nameLength = Encoding.UTF8.GetChars(nameBytes, nameBuffer);
                    nameBuffer[0] = char.ToUpper(nameBuffer[0]);
                    CheckForIllegalName(nameBuffer, ref nameLength);

                    builder.Append(indentation, _publicConstString, StringHelper.Whitespace, nameBuffer[..nameLength], StringHelper.Whitespace);
                    builder.Append(_equalSignSpaceQuotation, emojiBuffer[..emojiLength], _quotationSemicolon, Environment.NewLine);
                    break;
                default:
                    continue;
            }
        }
    }

    private void AppendFileHeader(ref StringBuilder builder)
    {
        builder.Append("#pragma warning disable 1591", Environment.NewLine);
        builder.Append("// ReSharper disable UnusedMember.Global", Environment.NewLine);
        builder.Append("// ReSharper disable InconsistentNaming", Environment.NewLine, Environment.NewLine);
        builder.Append("namespace ", NamespaceName, ";", Environment.NewLine, Environment.NewLine);
        builder.Append("/// <summary>", Environment.NewLine);
        builder.Append("/// A class that contains (almost) every existing emoji. Generated ", DateTime.UtcNow.ToString("R"), " with the <see cref=\"", nameof(EmojiFileGenerator), "\"/>.", Environment.NewLine);
        builder.Append("/// </summary>", Environment.NewLine);
        builder.Append("public static class Emoji", Environment.NewLine, "{", Environment.NewLine);
    }

    private void CheckForIllegalName(Span<char> name, ref int nameLength)
    {
        ReadOnlySpan<char> readOnlyName = name[..nameLength];
        foreach (var illegalWord in _illegalWords)
        {
            if (!readOnlyName.Equals(illegalWord.Key, StringComparison.Ordinal))
            {
                continue;
            }

            illegalWord.Value.CopyTo(name);
            nameLength = illegalWord.Value.Length;
            return;
        }

        for (int i = 0; i < nameLength; i++)
        {
            if (name[i] != '_')
            {
                continue;
            }

            name[(i + 1)..nameLength].CopyTo(name[i..]);
            nameLength -= 1;
            name[i] = char.ToUpper(name[i]);
        }
    }
}
