// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using System;
using System.Collections.Generic;
using System.IO;
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
    public void Loader_parses_decimal_and_hex_amounts()
    {
        string json = """
            {
              "blockNumber": 7,
              "transfers": [
                { "from": "0x1111111111111111111111111111111111111111", "to": "0x2222222222222222222222222222222222222222", "amount": "1000" },
                { "from": "0x3333333333333333333333333333333333333333", "to": "0x4444444444444444444444444444444444444444", "amount": "0x10" }
              ]
            }
            """;

        BalanceRecoveryConfig config = BalanceRecoveryConfigLoader.Parse(json);
        config.BlockNumber.Should().Be(7);
        config.Transfers.Should().HaveCount(2);
        config.Transfers[0].Amount.Should().Be((UInt256)1000);
        config.Transfers[1].Amount.Should().Be((UInt256)16);
    }

    [Test]
    public void Loader_rejects_from_equals_to_and_zero_amount()
    {
        string sameAddress = """
            { "blockNumber": 1, "transfers": [
              { "from": "0x1111111111111111111111111111111111111111", "to": "0x1111111111111111111111111111111111111111", "amount": "1" }
            ]}
            """;
        Assert.Throws<InvalidDataException>(() => BalanceRecoveryConfigLoader.Parse(sameAddress));

        string zeroAmount = """
            { "blockNumber": 1, "transfers": [
              { "from": "0x1111111111111111111111111111111111111111", "to": "0x2222222222222222222222222222222222222222", "amount": "0" }
            ]}
            """;
        Assert.Throws<InvalidDataException>(() => BalanceRecoveryConfigLoader.Parse(zeroAmount));
    }

    [Test]
    public void Loader_fails_closed_when_file_is_missing()
    {
        Assert.Throws<FileNotFoundException>(() =>
            BalanceRecoveryConfigLoader.Load(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json")));
    }

    [Test]
    public void Loader_loads_file_and_ignores_unknown_fields()
    {
        string path = Path.Combine(Path.GetTempPath(), "balance-recovery-a7-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllText(path, """
                {
                  "blockNumber": 0,
                  "snapshotBlock": 1,
                  "transfers": [
                    {
                      "from": "0x1111111111111111111111111111111111111111",
                      "to": "0x2222222222222222222222222222222222222222",
                      "amount": "5112721152022456981069597716",
                      "amountHex": "0x108524d1044ad69bb7618c14"
                    }
                  ]
                }
                """);

            BalanceRecoveryConfig config = BalanceRecoveryConfigLoader.Load(path);
            config.BlockNumber.Should().Be(0);
            config.Transfers.Should().HaveCount(1);
            config.Transfers[0].Amount.Should().Be(UInt256.Parse("5112721152022456981069597716"));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
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
