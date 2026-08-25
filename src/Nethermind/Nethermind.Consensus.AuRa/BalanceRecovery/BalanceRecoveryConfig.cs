// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System.Collections.Generic;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Consensus.AuRa.BalanceRecovery;

public class BalanceRecoveryConfig
{
    public long BlockNumber { get; init; }

    public IReadOnlyList<BalanceRecoveryTransfer> Transfers { get; init; } = [];
}

public class BalanceRecoveryTransfer
{
    public Address From { get; init; } = Address.Zero;
    public Address To { get; init; } = Address.Zero;
    public UInt256 Amount { get; init; }
}
