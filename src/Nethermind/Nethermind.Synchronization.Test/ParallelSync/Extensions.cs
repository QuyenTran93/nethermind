// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.Synchronization.ParallelSync;
using NSubstitute;
using static Nethermind.Synchronization.Test.ParallelSync.MultiSyncModeSelectorTestsBase;

namespace Nethermind.Synchronization.Test.ParallelSync
{
    internal static class Extensions
    {
        public static SyncMode GetSyncMode(this FastBlocksState state, bool isFullSync = false)
        {
            return state switch
            {
                FastBlocksState.None => SyncMode.FastHeaders,
                FastBlocksState.FinishedHeaders => isFullSync ? SyncMode.FastBodies : SyncMode.None,
                FastBlocksState.FinishedBodies => isFullSync ? SyncMode.FastReceipts : SyncMode.None,
                _ => SyncMode.None,
            };
        }

        public static FastBlocksFinishedState IsFastBlocksFinished(this ISyncProgressResolver syncProgressResolver)
        {
            return new FastBlocksFinishedState(syncProgressResolver);
        }

        internal class FastBlocksFinishedState
        {
            private readonly ISyncProgressResolver _syncProgressResolver;

            public FastBlocksFinishedState(ISyncProgressResolver syncProgressResolver)
            {
                _syncProgressResolver = syncProgressResolver;
            }

            public void Returns(FastBlocksState returns)
            {
                _syncProgressResolver.IsFastBlocksHeadersFinished().Returns(returns >= FastBlocksState.FinishedHeaders);
                _syncProgressResolver.IsFastBlocksBodiesFinished().Returns(returns >= FastBlocksState.FinishedBodies);
                _syncProgressResolver.IsFastBlocksReceiptsFinished().Returns(returns >= FastBlocksState.FinishedReceipts);
            }
        }
    }
}
