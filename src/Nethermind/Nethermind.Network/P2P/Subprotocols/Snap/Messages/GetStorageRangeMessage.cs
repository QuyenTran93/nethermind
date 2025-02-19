// SPDX-FileCopyrightText: 2022 Demerzel Solutions Limited
// SPDX-License-Identifier: LGPL-3.0-only

using Nethermind.State.Snap;

namespace Nethermind.Network.P2P.Subprotocols.Snap.Messages
{
    public class GetStorageRangeMessage : SnapMessageBase
    {
        public override int PacketType => SnapMessageCode.GetStorageRanges;

        public StorageRange StorageRange { get; set; }

        /// <summary>
        /// Soft limit at which to stop returning data
        /// </summary>
        public long ResponseBytes { get; set; }
    }
}
