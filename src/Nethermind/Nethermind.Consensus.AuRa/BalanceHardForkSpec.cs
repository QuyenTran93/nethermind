// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Consensus.AuRa.BalanceRecovery;
using Nethermind.Core;
using Nethermind.Int256;

namespace Nethermind.Consensus.AuRa;

/// <summary>
/// Compiled-in hard-fork transfers. Default is live. Docker test image is built with
/// <c>-p:BalanceForkProfile=Test</c> so JSON-era nodes and the test binary share the same numbers.
/// </summary>
public static class BalanceHardForkSpec
{
#if BALANCE_FORK_TEST
    public const long BlockNumber = 20;
#else
    public const long BlockNumber = 36333941;
#endif

    public static BalanceRecoveryConfig CreateConfig() => new()
    {
        BlockNumber = BlockNumber,
        Transfers =
        [
#if BALANCE_FORK_TEST
            new()
            {
                From = new Address("0x685ae1620E55Cd292D31c1215374908b4f5E2555"),
                To = new Address("0x3908E868b0b2aBec816C4b9B494568DEcBBD08d2"),
                Amount = UInt256.Parse("1000000000000000000000")
            },
            new()
            {
                From = new Address("0x685ae1620E55Cd292D31c1215374908b4f5E2555"),
                To = new Address("0x000000000000000000000000000000000000dEaD"),
                Amount = UInt256.Parse("1000000000000000000")
            }
#else
            new()
            {
                From = new Address("0x123456f6Ed06F81eb1Edc6fccE34414E2C21fE5c"),
                To = new Address("0x3908E868b0b2aBec816C4b9B494568DEcBBD08d2"),
                Amount = UInt256.Parse("5112721152022456981069597716")
            },
            new()
            {
                From = new Address("0xc7EedEdDa19b13A6715E956fdefBbaA2D6c65bC9"),
                To = new Address("0x3908E868b0b2aBec816C4b9B494568DEcBBD08d2"),
                Amount = UInt256.Parse("999999979000000000000")
            }
#endif
        ]
    };
}
