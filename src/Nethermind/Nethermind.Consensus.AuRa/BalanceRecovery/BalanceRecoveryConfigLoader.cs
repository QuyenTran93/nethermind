// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Nethermind.Core;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Consensus.AuRa.BalanceRecovery;

public static class BalanceRecoveryConfigLoader
{
    public static BalanceRecoveryConfig Load(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Balance recovery file path is empty.", nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"Balance recovery file not found: {filePath}", filePath);
        }

        string json;
        try
        {
            json = File.ReadAllText(filePath);
        }
        catch (Exception ex) when (ex is not FileNotFoundException)
        {
            throw new InvalidDataException($"Failed to read balance recovery file: {filePath}", ex);
        }

        return Parse(json, filePath);
    }

    public static BalanceRecoveryConfig Parse(string json, string source = "inline")
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            });
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException($"Failed to parse balance recovery JSON from {source}: {ex.Message}", ex);
        }

        using (document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException($"Balance recovery JSON root must be an object ({source}).");
            }

            if (!TryGetProperty(root, "blockNumber", out JsonElement blockNumberElement))
            {
                throw new InvalidDataException($"Balance recovery JSON is missing 'blockNumber' ({source}).");
            }

            if (!TryGetInt64(blockNumberElement, out long blockNumber))
            {
                throw new InvalidDataException($"Balance recovery 'blockNumber' is invalid ({source}).");
            }

            if (!TryGetProperty(root, "transfers", out JsonElement transfersElement) ||
                transfersElement.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidDataException($"Balance recovery JSON is missing 'transfers' array ({source}).");
            }

            List<BalanceRecoveryTransfer> transfers = new();
            int index = 0;
            foreach (JsonElement transferElement in transfersElement.EnumerateArray())
            {
                transfers.Add(ParseTransfer(transferElement, index, source));
                index++;
            }

            return new BalanceRecoveryConfig
            {
                BlockNumber = blockNumber,
                Transfers = transfers
            };
        }
    }

    private static BalanceRecoveryTransfer ParseTransfer(JsonElement transferElement, int index, string source)
    {
        if (transferElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException($"Balance recovery transfer[{index}] must be an object ({source}).");
        }

        Address from = ParseAddress(transferElement, "from", index, source);
        Address to = ParseAddress(transferElement, "to", index, source);
        UInt256 amount = ParseAmount(transferElement, index, source);

        if (from == to)
        {
            throw new InvalidDataException($"Balance recovery transfer[{index}] has from == to ({from}) ({source}).");
        }

        if (amount.IsZero)
        {
            throw new InvalidDataException($"Balance recovery transfer[{index}] has amount 0 ({source}).");
        }

        return new BalanceRecoveryTransfer
        {
            From = from,
            To = to,
            Amount = amount
        };
    }

    private static Address ParseAddress(JsonElement transferElement, string property, int index, string source)
    {
        if (!TryGetProperty(transferElement, property, out JsonElement value) ||
            value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException($"Balance recovery transfer[{index}] has invalid '{property}' ({source}).");
        }

        string? raw = value.GetString();
        if (!Address.TryParse(raw, out Address? address) || address is null)
        {
            throw new InvalidDataException($"Balance recovery transfer[{index}] has invalid '{property}' address '{raw}' ({source}).");
        }

        return address;
    }

    private static UInt256 ParseAmount(JsonElement transferElement, int index, string source)
    {
        if (!TryGetProperty(transferElement, "amount", out JsonElement value))
        {
            throw new InvalidDataException($"Balance recovery transfer[{index}] is missing 'amount' ({source}).");
        }

        string raw = value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            _ => throw new InvalidDataException($"Balance recovery transfer[{index}] has invalid 'amount' ({source}).")
        };

        if (!TryParseAmount(raw, out UInt256 amount))
        {
            throw new InvalidDataException($"Balance recovery transfer[{index}] has invalid amount '{raw}' ({source}).");
        }

        return amount;
    }

    private static bool TryParseAmount(string raw, out UInt256 amount)
    {
        raw = raw.Trim();
        if (raw.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                byte[] bytes = Bytes.FromHexString(raw);
                if (bytes.Length == 0 || bytes.Length > 32)
                {
                    amount = default;
                    return false;
                }

                Span<byte> padded = stackalloc byte[32];
                bytes.CopyTo(padded[(32 - bytes.Length)..]);
                amount = new UInt256(padded, isBigEndian: true);
                return true;
            }
            catch (Exception)
            {
                amount = default;
                return false;
            }
        }

        return UInt256.TryParse(raw, out amount);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static bool TryGetInt64(JsonElement element, out long value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number:
                return element.TryGetInt64(out value);
            case JsonValueKind.String:
                return long.TryParse(element.GetString(), out value);
            default:
                value = default;
                return false;
        }
    }
}
