// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.AuRa.BalanceRecovery;
using Nethermind.Consensus.AuRa.Config;
using Nethermind.Core.Specs;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.State;

namespace Nethermind.Consensus.AuRa;

/// <summary>
/// One-shot native balance remap at a configured AuRa block, mirroring <see cref="ContractRewriter"/>.
/// Policy: transfer min(amount, actual balance); warn and continue if capped. Does not touch nonce/code/storage.
/// </summary>
public class BalanceRewriter
{
    private readonly BalanceRecoveryConfig _config;

    public BalanceRewriter(BalanceRecoveryConfig config)
    {
        _config = config;
    }

    public static BalanceRewriter? CreateIfConfigured(IAuraConfig auraConfig)
    {
        if (string.IsNullOrWhiteSpace(auraConfig.BalanceRecoveryFilePath))
        {
            return null;
        }

        return new BalanceRewriter(BalanceRecoveryConfigLoader.Load(auraConfig.BalanceRecoveryFilePath));
    }

    public void Apply(long blockNumber, IWorldState state, IReleaseSpec spec, ILogger logger)
    {
        if (blockNumber != _config.BlockNumber)
        {
            return;
        }

        if (logger.IsInfo) logger.Info($"Applying balance recovery at block {blockNumber}: {_config.Transfers.Count} transfer(s).");

        foreach (BalanceRecoveryTransfer transfer in _config.Transfers)
        {
            UInt256 balance = state.GetBalance(transfer.From);
            UInt256 actual = UInt256.Min(transfer.Amount, balance);

            if (actual < transfer.Amount)
            {
                if (logger.IsWarn)
                {
                    logger.Warn(
                        $"Balance recovery: transfer from {transfer.From} to {transfer.To} capped from {transfer.Amount} to {actual} at block {blockNumber} (available {balance}).");
                }
            }

            if (actual.IsZero)
            {
                continue;
            }

            state.SubtractFromBalance(transfer.From, actual, spec);
            state.AddToBalanceAndCreateIfNotExists(transfer.To, actual, spec);
        }

        if (logger.IsInfo) logger.Info("Balance recovery applied.");
    }
}
