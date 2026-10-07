// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using FluentAssertions;
using Nethermind.Consensus.AuRa;
using Nethermind.Consensus.AuRa.BalanceRecovery;
using Nethermind.Core;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using Nethermind.Int256;
using Nethermind.Logging;
using Nethermind.Specs.Forks;
using Nethermind.State;
using Nethermind.Trie.Pruning;
using NUnit.Framework;

namespace Nethermind.AuRa.Test;

public class BalanceRewriterTests
{
    private const long ForkBlock = 10;

    [Test]
    public void Apply_at_fork_block_transfers_full_amount_when_balance_is_sufficient()
    {
        IWorldState state = CreateState();
        state.CreateAccount(TestItem.AddressA, 100);
        Commit(state);

        BalanceRewriter rewriter = CreateRewriter(FromTo(TestItem.AddressA, TestItem.AddressB, 40));
        rewriter.Apply(ForkBlock, state, London.Instance, LimboLogs.Instance.GetClassLogger());

        state.GetBalance(TestItem.AddressA).Should().Be((UInt256)60);
        state.GetBalance(TestItem.AddressB).Should().Be((UInt256)40);
    }

    [Test]
    public void Apply_caps_to_available_balance_and_warns()
    {
        IWorldState state = CreateState();
        state.CreateAccount(TestItem.AddressA, 80);
        Commit(state);

        CollectingLogger collecting = new();
        ILogger logger = new(collecting);
        BalanceRewriter rewriter = CreateRewriter(FromTo(TestItem.AddressA, TestItem.AddressB, 100));
        rewriter.Apply(ForkBlock, state, London.Instance, logger);

        state.GetBalance(TestItem.AddressA).Should().Be(UInt256.Zero);
        state.GetBalance(TestItem.AddressB).Should().Be((UInt256)80);
        collecting.Warns.Should().Contain(w => w.Contains("capped", StringComparison.OrdinalIgnoreCase));
    }

    [Test]
    public void Apply_is_noop_on_wrong_block()
    {
        IWorldState state = CreateState();
        state.CreateAccount(TestItem.AddressA, 100);
        Commit(state);

        BalanceRewriter rewriter = CreateRewriter(FromTo(TestItem.AddressA, TestItem.AddressB, 40));
        rewriter.Apply(ForkBlock + 1, state, London.Instance, LimboLogs.Instance.GetClassLogger());

        state.GetBalance(TestItem.AddressA).Should().Be((UInt256)100);
        state.GetBalance(TestItem.AddressB).Should().Be(UInt256.Zero);
    }

    [Test]
    public void Apply_creates_recipient_account_when_missing()
    {
        IWorldState state = CreateState();
        state.CreateAccount(TestItem.AddressA, 50);
        Commit(state);

        state.AccountExists(TestItem.AddressB).Should().BeFalse();

        BalanceRewriter rewriter = CreateRewriter(FromTo(TestItem.AddressA, TestItem.AddressB, 50));
        rewriter.Apply(ForkBlock, state, London.Instance, LimboLogs.Instance.GetClassLogger());

        state.AccountExists(TestItem.AddressB).Should().BeTrue();
        state.GetBalance(TestItem.AddressB).Should().Be((UInt256)50);
        state.GetBalance(TestItem.AddressA).Should().Be(UInt256.Zero);
        state.GetNonce(TestItem.AddressA).Should().Be(UInt256.Zero);
        state.GetNonce(TestItem.AddressB).Should().Be(UInt256.Zero);
    }

    [Test]
    public void Apply_executes_transfers_in_list_order()
    {
        IWorldState state = CreateState();
        state.CreateAccount(TestItem.AddressA, 100);
        state.CreateAccount(TestItem.AddressB, 10);
        Commit(state);

        BalanceRewriter rewriter = CreateRewriter(
            FromTo(TestItem.AddressA, TestItem.AddressB, 40),
            FromTo(TestItem.AddressB, TestItem.AddressC, 45));
        rewriter.Apply(ForkBlock, state, London.Instance, LimboLogs.Instance.GetClassLogger());

        // A -> B first: A=60, B=50; then B -> C: B=5, C=45
        state.GetBalance(TestItem.AddressA).Should().Be((UInt256)60);
        state.GetBalance(TestItem.AddressB).Should().Be((UInt256)5);
        state.GetBalance(TestItem.AddressC).Should().Be((UInt256)45);
    }

    [Test]
    public void Hard_fork_applies_baked_in_transfers_at_fork_block()
    {
        Address from1 = new("0x123456f6Ed06F81eb1Edc6fccE34414E2C21fE5c");
        Address from2 = new("0xc7EedEdDa19b13A6715E956fdefBbaA2D6c65bC9");
        Address to = new("0x3908E868b0b2aBec816C4b9B494568DEcBBD08d2");
        UInt256 amount1 = UInt256.Parse("5112721152022456981069597716");
        UInt256 amount2 = UInt256.Parse("999999979000000000000");

        IWorldState state = CreateState();
        state.CreateAccount(from1, amount1);
        state.CreateAccount(from2, amount2);
        Commit(state);

        BalanceRewriter.Create().Apply(BalanceRewriter.ForkBlockNumber, state, London.Instance, LimboLogs.Instance.GetClassLogger());

        state.GetBalance(from1).Should().Be(UInt256.Zero);
        state.GetBalance(from2).Should().Be(UInt256.Zero);
        state.GetBalance(to).Should().Be(amount1 + amount2);
    }

    [Test]
    public void Hard_fork_is_noop_on_blocks_after_live_fork()
    {
        Address from1 = new("0x123456f6Ed06F81eb1Edc6fccE34414E2C21fE5c");
        Address to = new("0x3908E868b0b2aBec816C4b9B494568DEcBBD08d2");
        UInt256 remaining = 123;

        IWorldState state = CreateState();
        state.CreateAccount(from1, remaining);
        state.CreateAccount(to, 1);
        Commit(state);

        BalanceRewriter.Create().Apply(BalanceRewriter.ForkBlockNumber + 1, state, London.Instance, LimboLogs.Instance.GetClassLogger());

        state.GetBalance(from1).Should().Be(remaining);
        state.GetBalance(to).Should().Be((UInt256)1);
    }

    private static BalanceRewriter CreateRewriter(params BalanceRecoveryTransfer[] transfers) =>
        new(new BalanceRecoveryConfig { BlockNumber = ForkBlock, Transfers = transfers });

    private static BalanceRecoveryTransfer FromTo(Address from, Address to, int amount) =>
        new() { From = from, To = to, Amount = (UInt256)amount };

    private static IWorldState CreateState()
    {
        IDb stateDb = new MemDb();
        IDb codeDb = new MemDb();
        return new WorldState(new TrieStore(stateDb, LimboLogs.Instance), codeDb, LimboLogs.Instance);
    }

    private static void Commit(IWorldState state)
    {
        state.Commit(London.Instance);
        state.CommitTree(0);
    }

    private sealed class CollectingLogger : InterfaceLogger
    {
        public List<string> Warns { get; } = new();
        public void Info(string text) { }
        public void Warn(string text) => Warns.Add(text);
        public void Debug(string text) { }
        public void Trace(string text) { }
        public void Error(string text, Exception ex = null) { }
        public bool IsInfo => true;
        public bool IsWarn => true;
        public bool IsDebug => true;
        public bool IsTrace => true;
        public bool IsError => true;
    }
}
